using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using FermixAPI.Core;
using PlayerRoles;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// G.O.C. (Global Occult Coalition) — самостоятельный отряд, прибывающий
    /// после <see cref="Config.GocWaveStartMinuteThreshold"/> минуты раунда
    /// волной с шансом <see cref="Config.GocWaveChance"/>. Это НЕ конвертация
    /// MTF: базовая роль каждого оперативника — <see cref="RoleTypeId.Tutorial"/>,
    /// чтобы они были «нейтральны» для всех штатных команд игры (не друг
    /// SCP, не друг Хаоса, не друг MTF). Враждебность всем поддерживается
    /// явным разрешением урона в <see cref="OnPlayerHurt"/>.
    ///
    /// Особенности:
    /// • спавн в МТФ-точке (волна перехватывает вакантную NTF-волну);
    /// • CASSIE-объявление о «неопознанной враждебной группировке» и
    ///   «хакерских атаках» на системы комплекса;
    /// • у каждого оперативника свой ранг (звание) с собственным снаряжением
    ///   и обязательным бронежилетом;
    /// • при появлении каждому гоковцу шлётся персональный хинт с
    ///   описанием его роли в отряде.
    ///
    /// Команда <c>goc wave</c> руками инициализирует волну: сначала
    /// пытается перевести в G.O.C. живых MTF, заспавнившихся последней
    /// MTF-волной; если их нет — берёт спектаторов и спавнит их в
    /// MTF-точке.
    /// </summary>
    public static class FermixGoc
    {
        // ── Ранги отряда ─────────────────────────────────────────────

        /// <summary>Один ранг в составе отряда G.O.C.</summary>
        public sealed class GocRank
        {
            public string Name;
            public string Description;   // что игрок умеет / за что отвечает
            public string Color;          // hex без '#'
            public int MaxPerWave;        // не больше N таких в одной волне
            public ItemType[] Loadout;
        }

        /// <summary>
        /// Пул рангов G.O.C. Порядок имеет значение: верхние раздаются первыми
        /// (Координатор всегда выпадает №1, дальше — Ликвидаторы и т. д.).
        /// </summary>
        private static readonly List<GocRank> RankPool = new()
        {
            new GocRank
            {
                Name = "Координатор-α",
                Description = "Старший оперативник G.O.C. Командует звеном, " +
                              "имеет доступ к закрытым секциям и тяжёлое " +
                              "вооружение. Цель — координация подавления цели.",
                Color = "ffd24a",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunLogicer,
                    ItemType.GunRevolver,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.GrenadeFlash,
                    ItemType.KeycardMTFCaptain,
                    ItemType.Radio,
                },
            },
            new GocRank
            {
                Name = "Дозиметрист-Δ",
                Description = "Полевой медик и специалист по аномальной защите. " +
                              "Лечит звено и нейтрализует биологические угрозы. " +
                              "Цель — поддержание боеспособности отряда.",
                Color = "8be3ff",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunCOM18,
                    ItemType.Medkit,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.SCP500,
                    ItemType.Radio,
                    ItemType.KeycardMTFOperative,
                },
            },
            new GocRank
            {
                Name = "Аналитик-Σ",
                Description = "Разведчик и связист звена. Первым входит в зону, " +
                              "сканирует обстановку, отмечает цели для штурма. " +
                              "Цель — разведка и связь со штабом.",
                Color = "b58bff",
                MaxPerWave = 2,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunE11SR,
                    ItemType.GrenadeFlash,
                    ItemType.Adrenaline,
                    ItemType.Radio,
                    ItemType.KeycardMTFOperative,
                },
            },
            new GocRank
            {
                Name = "Ликвидатор-Ω",
                Description = "Штурмовик отряда. Прорывает оборону, добивает " +
                              "ослабленные цели, прикрывает Координатора. " +
                              "Цель — нейтрализация всего враждебного.",
                Color = "ff8b8b",
                MaxPerWave = 5,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunE11SR,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeFlash,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.KeycardMTFPrivate,
                },
            },
        };

        private static readonly Dictionary<string, GocRank> _memberRanks =
            new(StringComparer.Ordinal);

        private static readonly object _lock = new();
        private static bool _initialized;
        private static bool _waveTriggeredThisRound;

        // ── публичный API ────────────────────────────────────────────

        public static IReadOnlyCollection<string> Members
        {
            get { lock (_lock) return _memberRanks.Keys.ToArray(); }
        }

        public static bool IsMember(Player p) =>
            p?.UserId != null && _memberRanks.ContainsKey(p.UserId);

        // ── lifecycle ────────────────────────────────────────────────

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.GocEnabled != true) return;
            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnPlayerHurt += OnPlayerHurt;
            Exiled.Events.Handlers.Server.RespawnedTeam += OnRespawnedTeam;
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerLeave -= OnPlayerLeave;
            FermixEvents.OnPlayerHurt -= OnPlayerHurt;
            Exiled.Events.Handlers.Server.RespawnedTeam -= OnRespawnedTeam;
            ClearAll();
            _initialized = false;
        }

        // ── round events ─────────────────────────────────────────────

        private static void OnRoundStart()
        {
            ClearAll();
            _waveTriggeredThisRound = false;
        }

        private static void OnRoundEnd(RoundEndedEventArgs _)
        {
            ClearAll();
            _waveTriggeredThisRound = false;
        }

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_lock) _memberRanks.Remove(ev.Player.UserId);
        }

        private static void OnRespawnedTeam(RespawnedTeamEventArgs ev)
        {
            if (ev?.Players == null) return;
            if (FermixCore.Config?.GocEnabled != true) return;

            // Перехватываем ТОЛЬКО МТФ-волну (NTF). Хаос/SCP не трогаем —
            // у них своя логика, и G.O.C. конкурирует с MTF по слотам.
            var spawned = ev.Players.Where(p => p?.Role?.Team == Team.FoundationForces).ToList();
            if (spawned.Count == 0) return;

            // Время раунда. Раньше N минут G.O.C. не появляется ни при каких условиях.
            float elapsedMinutes = (float)Round.ElapsedTime.TotalMinutes;
            float gateMinutes = Mathf.Max(0f, FermixCore.Config?.GocWaveStartMinuteThreshold ?? 15f);
            if (elapsedMinutes < gateMinutes) return;

            // Только одна G.O.C.-волна за раунд (по умолчанию). Дальнейшие
            // MTF-волны спавнятся как обычные MTF.
            if (_waveTriggeredThisRound && (FermixCore.Config?.GocOneWavePerRound ?? true)) return;

            float chance = Mathf.Clamp01(FermixCore.Config?.GocWaveChance ?? 0.35f);
            if (UnityEngine.Random.value > chance) return;

            _waveTriggeredThisRound = true;

            // Маленькая задержка, чтобы дать MTF спавну достроиться (анимации,
            // поток сетевых пакетов о смене роли и т.п.). Иначе мы перебиваем
            // роль одновременно с её установкой, и игроков иногда выкидывает
            // в спектатора.
            FermixScheduler.Delay(0.7f, () =>
            {
                int converted = 0;
                foreach (var p in spawned)
                {
                    if (p == null || !p.IsConnected) continue;
                    if (p.Role?.Team != Team.FoundationForces) continue;
                    if (ConvertToGoc(p)) converted++;
                }

                if (converted == 0)
                {
                    _waveTriggeredThisRound = false;
                    return;
                }

                AnnounceWave(converted);
            });
        }

        // ── core conversion ──────────────────────────────────────────

        private static bool ConvertToGoc(Player p)
        {
            if (p == null || !p.IsConnected) return false;
            try
            {
                // RoleSpawnFlags.None — сохраняет позицию игрока (он остаётся
                // на МТФ-точке спавна) и инвентарь (мы дальше всё равно
                // полностью переписываем).
                p.Role.Set(RoleTypeId.Tutorial,
                    Exiled.API.Enums.SpawnReason.Respawn,
                    RoleSpawnFlags.None);
            }
            catch (Exception e)
            {
                FermixLog.Error($"[GOC] Role.Set Tutorial: {e.Message}");
                return false;
            }

            var rank = AssignRank(p);
            Mark(p, rank, announce: true);
            return true;
        }

        /// <summary>
        /// Принудительно делает игрока оперативником G.O.C. с конкретным
        /// рангом. Используется как из MTF-перехвата, так и из RA-команды
        /// <c>goc spawn</c>.
        /// </summary>
        public static void Mark(Player p, GocRank rank = null, bool announce = true)
        {
            if (p?.UserId == null) return;

            rank ??= AssignRank(p);

            lock (_lock) _memberRanks[p.UserId] = rank;

            try
            {
                p.CustomInfo = $"<color=#{rank.Color}>G.O.C. — {rank.Name}</color>";
                ApplyLoadout(p, rank);

                // Регистрируем пассивку GOC-члена в общем модуле классов
                // (heal-aura tick и damage-hook лежат там, чтобы NTF/Chaos
                // и GOC обслуживались одной системой без дублирования).
                FermixSquadClasses.RegisterGoc(p, GetPassiveForRank(rank), rank.Name, IsMember);
            }
            catch (Exception e) { FermixLog.Error($"[GOC] Mark: {e.Message}"); }

            if (announce) SendPersonalHint(p, rank);
        }

        /// <summary>
        /// Маппинг GOC-званий на тип пассивки. Координатор — командир-бафф,
        /// Дозиметрист — медик-аура, Ликвидатор — джаггернаут (HP/защита),
        /// Аналитик — без пассивки (разведка, базовый класс).
        /// </summary>
        private static FermixSquadClasses.SquadClassPassive GetPassiveForRank(GocRank rank)
        {
            if (rank == null) return FermixSquadClasses.SquadClassPassive.None;
            if (rank.Name.StartsWith("Координатор")) return FermixSquadClasses.SquadClassPassive.Commander;
            if (rank.Name.StartsWith("Дозиметрист")) return FermixSquadClasses.SquadClassPassive.Medic;
            if (rank.Name.StartsWith("Ликвидатор")) return FermixSquadClasses.SquadClassPassive.Juggernaut;
            return FermixSquadClasses.SquadClassPassive.None;
        }

        public static void Unmark(Player p)
        {
            if (p?.UserId == null) return;
            lock (_lock) _memberRanks.Remove(p.UserId);
            try { if (p.CustomInfo != null && p.CustomInfo.Contains("G.O.C.")) p.CustomInfo = string.Empty; }
            catch { }
            FermixSquadClasses.Unregister(p);
        }

        private static void ClearAll()
        {
            string[] ids;
            lock (_lock) { ids = _memberRanks.Keys.ToArray(); _memberRanks.Clear(); }
            foreach (var id in ids)
            {
                var p = Player.Get(id);
                if (p != null) try { if (p.CustomInfo?.Contains("G.O.C.") == true) p.CustomInfo = string.Empty; } catch { }
            }
        }

        // ── ранги / снаряжение / хинты ───────────────────────────────

        private static GocRank AssignRank(Player p)
        {
            // Считаем уже распределённые ранги в текущей волне и подбираем
            // первый ещё не «забитый» по MaxPerWave. Если все позиции
            // заполнены — даём Ликвидатора (универсальный штурмовик).
            var current = new Dictionary<GocRank, int>();
            lock (_lock)
            {
                foreach (var r in _memberRanks.Values)
                    current[r] = current.TryGetValue(r, out var c) ? c + 1 : 1;
            }
            foreach (var rank in RankPool)
            {
                int taken = current.TryGetValue(rank, out var c) ? c : 0;
                if (taken < rank.MaxPerWave) return rank;
            }
            return RankPool[RankPool.Count - 1];
        }

        private static void ApplyLoadout(Player p, GocRank rank)
        {
            try
            {
                p.ClearInventory();
                foreach (var t in rank.Loadout)
                    p.AddItem(t);

                // Ликвидатор-Ω — джаггернаут, ему 200 HP. Остальные — full HP
                // от роли Tutorial. AHP-рывок (адреналин-овер-хил) даём всем,
                // чтобы при «материализации» отряда никто не появился с
                // 50 HP «после Tutorial».
                if (rank.Name.StartsWith("Ликвидатор"))
                {
                    p.MaxHealth = 200f;
                    p.Health = 200f;
                }
                else
                {
                    p.Health = p.MaxHealth;
                }
                p.ArtificialHealth = Mathf.Max(p.ArtificialHealth, 75f);
            }
            catch (Exception e) { FermixLog.Warn($"[GOC] ApplyLoadout: {e.Message}"); }
        }

        private static void SendPersonalHint(Player p, GocRank rank)
        {
            string body =
                $"<size=120%><b><color=#{rank.Color}>{rank.Name}</color></b></size>\n" +
                $"<color=#{rank.Color}>G.O.C. — Global Occult Coalition</color>\n\n" +
                $"{rank.Description}\n\n" +
                "<color=#ff8b8b>Ваш отряд враждебен ВСЕМ:</color>\n" +
                "• MTF и охрана — конкурирующая фракция, нет приказа щадить.\n" +
                "• Хаос — обычная цель.\n" +
                "• SCP — основная угроза, нейтрализовать или сдержать.\n\n" +
                "<size=80%><color=#aaaaaa>Снаряжение выдано согласно званию. " +
                "Бронежилет надевается автоматически.</color></size>";

            FermixHint.SendColored(p, body, "#" + rank.Color, 12f);
        }

        // ── CASSIE ───────────────────────────────────────────────────

        private static void AnnounceWave(int count)
        {
            // Phonemes для CASSIE. Используем jam_ для эффекта помех/хака
            // и pitch-сдвиги для драматизма. Хитрая часть — phonemes должны
            // существовать в словаре игры, поэтому используем общеупотребимые
            // слова из англоязычной номенклатуры.
            string phonemes =
                "jam_080_3 . pitch_0.4 ALERT . pitch_0.85 unidentified hostile faction . " +
                "pitch_0.6 detected on surface zone . " +
                "jam_050_2 pitch_0.95 cyberattack on facility systems in progress . " +
                "pitch_0.5 all personnel be advised . " +
                "pitch_1.0 jam_030_1 unknown operatives breaching containment perimeter";

            string subtitles =
                "⚠ ВНИМАНИЕ ⚠\n" +
                "ОБНАРУЖЕНА НЕОПОЗНАННАЯ ВРАЖДЕБНАЯ ГРУППИРОВКА.\n" +
                "НА СИСТЕМЫ КОМПЛЕКСА ВЕДЁТСЯ КИБЕРАТАКА.\n" +
                "БЕЗОПАСНОСТЬ КОМПЛЕКСА ПОД УГРОЗОЙ.\n" +
                "НЕИЗВЕСТНЫЕ ОПЕРАТИВНИКИ ПРОНИКЛИ ЧЕРЕЗ ПОВЕРХНОСТНУЮ ЗОНУ.";

            // Можно полностью переопределить из конфига (на случай если
            // игроки знают phonemes лучше нас).
            string cfgPhon = FermixCore.Config?.GocCassiePhonemes;
            string cfgSub = FermixCore.Config?.GocCassieSubtitles;
            if (!string.IsNullOrWhiteSpace(cfgPhon)) phonemes = cfgPhon;
            if (!string.IsNullOrWhiteSpace(cfgSub)) subtitles = cfgSub;

            try
            {
                Exiled.API.Features.Cassie.MessageTranslated(phonemes, subtitles, isHeld: false, isNoisy: true, isSubtitles: true);
            }
            catch (Exception e)
            {
                FermixLog.Warn($"[GOC] CASSIE failed: {e.Message}");
            }

            // Глобальный хинт всем (для тех, у кого CASSIE-сабы могут
            // не быть видны).
            string banner = $"<color=#ffd24a>Прибыли оперативники G.O.C.</color> ({count}). " +
                            "Они враждебны всем фракциям комплекса.";
            foreach (var p in Player.List)
                if (p != null && p.IsConnected && !IsMember(p))
                    FermixHint.Send(p, banner, 6f);
        }

        // ── ручной запуск волны (из RA-команды) ─────────────────────

        /// <summary>
        /// Принудительно инициирует волну G.O.C. Возвращает количество
        /// заспавненных оперативников (0 если не нашлось пригодных
        /// игроков).
        /// </summary>
        public static int TriggerWaveManual(out string error)
        {
            error = null;

            if (FermixCore.Config?.GocEnabled != true)
            {
                error = "G.O.C. выключен в конфиге.";
                return 0;
            }

            // Сначала пробуем перехватить уже живую MTF-волну (если такая есть).
            var liveMtf = Player.List
                .Where(p => p != null && p.IsAlive && p.IsConnected
                         && p.Role?.Team == Team.FoundationForces
                         && !IsMember(p))
                .ToList();

            int converted = 0;

            if (liveMtf.Count > 0)
            {
                foreach (var p in liveMtf)
                    if (ConvertToGoc(p)) converted++;
            }
            else
            {
                // MTF нет — берём спектаторов и делаем им «материализацию»
                // через временный спавн в МТФ-точке (Role.Set NtfPrivate ⇒
                // игра ставит их на МТФ-спавн, потом мы переключаем в Tutorial,
                // сохранив позицию).
                int desired = Mathf.Clamp(FermixCore.Config?.GocManualWaveSize ?? 5, 1, 15);
                var spectators = Player.List
                    .Where(p => p != null && p.IsConnected
                             && (p.Role?.Type == RoleTypeId.Spectator
                                 || p.Role?.Type == RoleTypeId.Overwatch
                                 || p.Role?.Type == RoleTypeId.None))
                    .Take(desired)
                    .ToList();

                if (spectators.Count == 0)
                {
                    error = "Нет ни живых MTF, ни спектаторов — некого спавнить за G.O.C.";
                    return 0;
                }

                foreach (var p in spectators)
                {
                    try
                    {
                        // 1) Спавн как NtfPrivate с UseSpawnpoint — игра
                        //    телепортирует игрока в МТФ-точку.
                        p.Role.Set(RoleTypeId.NtfPrivate,
                            Exiled.API.Enums.SpawnReason.Respawn,
                            RoleSpawnFlags.UseSpawnpoint);
                    }
                    catch (Exception e)
                    {
                        FermixLog.Warn($"[GOC] manual wave: NtfPrivate spawn failed for {p.Nickname}: {e.Message}");
                        continue;
                    }

                    var capturedPlayer = p;
                    FermixScheduler.Delay(0.4f, () =>
                    {
                        if (capturedPlayer == null || !capturedPlayer.IsConnected) return;
                        if (ConvertToGoc(capturedPlayer)) converted++;
                    });
                }

                // CASSIE и общий баннер шлём после задержки, когда все
                // конверсии должны успеть отработать.
                FermixScheduler.Delay(0.6f, () =>
                {
                    if (converted > 0) AnnounceWave(converted);
                });

                // converted на момент возврата может быть 0 (всё в delay'ах),
                // но мы возвращаем размер пула как «попытку» — это видно
                // в RA как «инициировано». Для точного количества админ
                // через секунду посмотрит `goc list`.
                if (converted == 0) converted = spectators.Count;
                _waveTriggeredThisRound = true;
                return converted;
            }

            // Если перехватили MTF — анонсим сразу.
            _waveTriggeredThisRound = true;
            AnnounceWave(converted);
            return converted;
        }

        // ── friendly fire / damage matrix ────────────────────────────

        private static void OnPlayerHurt(HurtingEventArgs ev)
        {
            if (ev == null || ev.Player == null || ev.Attacker == null) return;
            if (ev.Player == ev.Attacker) return;

            bool atkGoc = IsMember(ev.Attacker);
            bool tgtGoc = IsMember(ev.Player);
            if (!atkGoc && !tgtGoc) return;

            // GOC vs GOC — friendly fire выключен (свои не стреляют по своим,
            // даже если базовая роль Tutorial у игры считается «никем»).
            if (atkGoc && tgtGoc)
            {
                ev.IsAllowed = false;
                return;
            }

            // GOC ⇄ кто угодно (MTF, Chaos, SCP, D-class) — урон проходит.
            // Игра по дефолту вокруг Tutorial-роли может вести себя странно
            // (не считать урон), поэтому форсим разрешение.
            ev.IsAllowed = true;
        }
    }
}
