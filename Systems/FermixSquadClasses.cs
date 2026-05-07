using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using FermixAPI.Core;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Кастомные классы внутри отрядов NTF и Chaos с пассивными способностями.
    ///
    /// Концепция: при каждом RespawnedTeam-событии (NTF/Chaos-волна) каждому
    /// прибывшему игроку случайно (по приоритету и MaxPerWave) выдаётся один из
    /// четырёх классов фракции. Логика Медика и Джаггернаута портирована
    /// из исходников «sosal»-плагина (MTFMedic / CIMedic / JuggernautSAT):
    /// <list type="bullet">
    ///   <item><b>Командир</b> — +20% исходящего урона союзникам.</item>
    ///   <item><b>Медик</b> — каждую секунду лечит союзников в радиусе на 2 HP через
    ///       персональную MEC-корутину (1:1 порт sosal MTFMedicModule.HealingLoop).</item>
    ///   <item><b>Джаггернаут</b> — 125 HP и Scale ×1.15, без damage-reduction
    ///       (JuggernautSATModule.ConvertToJuggernautSAT).</item>
    ///   <item><b>Стрелок/Подрывник</b> — базовый класс без пассивки.</item>
    /// </list>
    ///
    /// G.O.C.-волны интегрируются через <see cref="RegisterGoc(Player, SquadClassPassive, string, Func{Player, bool})"/>:
    /// <see cref="FermixGoc.Mark"/> вызывает Register с той же пассивкой,
    /// сопоставленной с конкретным GOC-званием. Tick хил-ауры и хук
    /// <see cref="OnPlayerHurt"/> тогда обслуживают и GOC-членов в том числе.
    /// </summary>
    public static class FermixSquadClasses
    {
        public enum SquadClassPassive
        {
            None,
            Medic,
            Juggernaut,
            Commander,
        }

        public sealed class SquadClass
        {
            public string Name;
            public string Description;
            public string Color;            // hex без '#'
            public string FactionLabel;     // отображается в хинте
            public int MaxPerWave;
            public float MaxHealth;         // 0 — оставить дефолтное HP роли
            public float ArtificialHealth;  // 0 — не накидывать
            public float Scale;             // 0 или 1 — без изменения; 1.15 — джаггернаут
            public ItemType[] Loadout;
            public SquadClassPassive Passive;
        }

        // ── пулы классов ────────────────────────────────────────────
        // Порядок важен: верхние раздаются первыми (Командир/Медик/Джагг
        // по 1, Стрелок-Подрывник — все остальные).

        private static readonly List<SquadClass> NtfPool = new()
        {
            new SquadClass
            {
                Name = "Командир NTF",
                Description = "Командир оперативной группы. Капитанский ключ.\n" +
                              "Пассивка: <b>+20% исходящего урона</b>.",
                Color = "ffd24a",
                FactionLabel = "Mobile Task Force — NTF",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunE11SR,
                    ItemType.GunFSP9,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.GrenadeFlash,
                    ItemType.KeycardMTFCaptain,
                    ItemType.Radio,
                },
                Passive = SquadClassPassive.Commander,
            },
            new SquadClass
            {
                Name = "МТФ-Медик",
                Description = "Полевой медик.\n" +
                              "Пассивка: лечит союзников вокруг себя на 2 HP в секунду.",
                Color = "8be3ff",
                FactionLabel = "Mobile Task Force — NTF",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.GunE11SR,
                    ItemType.ArmorHeavy,
                    ItemType.KeycardFacilityManager,
                    ItemType.Medkit,
                    ItemType.Medkit,
                    ItemType.SCP500,
                },
                Passive = SquadClassPassive.Medic,
            },
            new SquadClass
            {
                Name = "Джаггернаут СБ",
                Description = "Тяжёлый штурмовик. <b>125 HP</b>, размер ×1.15, " +
                              "тяжёлая броня и FR-MG-0.",
                Color = "8B4513",
                FactionLabel = "Mobile Task Force — NTF",
                MaxPerWave = 1,
                MaxHealth = 125f,
                Scale = 1.15f,
                Loadout = new[]
                {
                    ItemType.KeycardMTFPrivate,
                    ItemType.GunFRMG0,
                    ItemType.ArmorHeavy,
                    ItemType.Radio,
                    ItemType.GrenadeFlash,
                    ItemType.GrenadeFlash,
                    ItemType.Medkit,
                },
                Passive = SquadClassPassive.Juggernaut,
            },
            new SquadClass
            {
                Name = "Стрелок NTF",
                Description = "Базовый оперативник.\n" +
                              "Универсальное штурмовое снаряжение, без пассивки.",
                Color = "8effa3",
                FactionLabel = "Mobile Task Force — NTF",
                MaxPerWave = 99,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunE11SR,
                    ItemType.GunFSP9,
                    ItemType.GrenadeFlash,
                    ItemType.GrenadeHE,
                    ItemType.Medkit,
                    ItemType.KeycardMTFOperative,
                },
                Passive = SquadClassPassive.None,
            },
        };

        private static readonly List<SquadClass> ChaosPool = new()
        {
            new SquadClass
            {
                Name = "Командир Хаоса",
                Description = "Лидер ячейки Хаоса. Ключ Insurgency и AK.\n" +
                              "Пассивка: <b>+20% исходящего урона</b>.",
                Color = "ffd24a",
                FactionLabel = "Chaos Insurgency",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunAK,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.GrenadeFlash,
                    ItemType.KeycardChaosInsurgency,
                    ItemType.Radio,
                },
                Passive = SquadClassPassive.Commander,
            },
            new SquadClass
            {
                Name = "ПХ-Медик",
                Description = "Полевой санитар Хаоса.\n" +
                              "Пассивка: лечит союзников вокруг себя на 2 HP в секунду.",
                Color = "8be3ff",
                FactionLabel = "Chaos Insurgency",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.GunE11SR,
                    ItemType.ArmorHeavy,
                    ItemType.KeycardFacilityManager,
                    ItemType.Medkit,
                    ItemType.Medkit,
                    ItemType.SCP500,
                },
                Passive = SquadClassPassive.Medic,
            },
            new SquadClass
            {
                Name = "Джаггернаут Хаоса",
                Description = "Штурмовой танк Хаоса. <b>125 HP</b>, размер ×1.15, " +
                              "тяжёлая броня и FR-MG-0.",
                Color = "8B4513",
                FactionLabel = "Chaos Insurgency",
                MaxPerWave = 1,
                MaxHealth = 125f,
                Scale = 1.15f,
                Loadout = new[]
                {
                    ItemType.KeycardChaosInsurgency,
                    ItemType.GunFRMG0,
                    ItemType.ArmorHeavy,
                    ItemType.Radio,
                    ItemType.GrenadeFlash,
                    ItemType.GrenadeFlash,
                    ItemType.Medkit,
                },
                Passive = SquadClassPassive.Juggernaut,
            },
            new SquadClass
            {
                Name = "Подрывник Хаоса",
                Description = "Базовый боец Хаоса.\n" +
                              "AK, две HE-гранаты и флэш. Без пассивки.",
                Color = "8effa3",
                FactionLabel = "Chaos Insurgency",
                MaxPerWave = 99,
                Loadout = new[]
                {
                    ItemType.ArmorCombat,
                    ItemType.GunAK,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeHE,
                    ItemType.GrenadeFlash,
                    ItemType.Medkit,
                },
                Passive = SquadClassPassive.None,
            },
        };

        // ── runtime state ───────────────────────────────────────────

        private sealed class PassiveAssignment
        {
            public SquadClassPassive Passive;
            public string ClassName;
            public string ClassColor;
            public Func<Player, bool> IsTeammate;
        }

        private static readonly object _lock = new();
        private static readonly Dictionary<string, PassiveAssignment> _passives =
            new(StringComparer.Ordinal);

        // sosal-стиль per-medic корутины: одна хил-петля на каждого Медика,
        // ключ — UserId (чтобы не держать stale Player ref после disconnect).
        private static readonly Dictionary<string, CoroutineHandle> _medicCoroutines =
            new(StringComparer.Ordinal);

        private static bool _initialized;

        // ── lifecycle ───────────────────────────────────────────────

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.SquadClassesEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnPlayerDied += OnPlayerDied;
            FermixEvents.OnRoleChange += OnRoleChange;
            Exiled.Events.Handlers.Server.RespawnedTeam += OnRespawnedTeam;
            // OnPlayerHurt-хук намеренно отключён в v2.6.1: после рефакторинга
            // Commander +20% damage множитель оставлен только в виде
            // конфиг-параметра, но при подписке на Hurt у нас были регрессии
            // (урон не проходил по wave-spawned игрокам). Если возвращаем —
            // обязательно с unit-тестом.

            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerLeave -= OnPlayerLeave;
            FermixEvents.OnPlayerDied -= OnPlayerDied;
            FermixEvents.OnRoleChange -= OnRoleChange;
            Exiled.Events.Handlers.Server.RespawnedTeam -= OnRespawnedTeam;

            StopAllMedicCoroutines();
            lock (_lock) _passives.Clear();
            _initialized = false;
        }

        // ── публичный API ───────────────────────────────────────────

        /// <summary>
        /// Зарегистрировать пассивку для GOC-члена. Вызывается из
        /// <see cref="FermixGoc.Mark"/> после применения лоадаута, чтобы
        /// heal-tick и damage-hook знали о пассивке без дублирования
        /// логики между модулями.
        /// </summary>
        public static void RegisterGoc(Player p, SquadClassPassive passive,
                                        string rankName, Func<Player, bool> isTeammate)
        {
            if (p?.UserId == null) return;
            lock (_lock)
            {
                _passives[p.UserId] = new PassiveAssignment
                {
                    Passive = passive,
                    ClassName = rankName,
                    ClassColor = "ffd24a",
                    IsTeammate = isTeammate ?? (_ => false),
                };
            }
            if (passive == SquadClassPassive.Medic) StartMedicHealing(p);
            else StopMedicHealing(p.UserId);
        }

        public static void Unregister(Player p)
        {
            if (p?.UserId == null) return;
            lock (_lock) _passives.Remove(p.UserId);
            StopMedicHealing(p.UserId);
        }

        /// <summary>
        /// Перечислить все имена кастомных классов для команды
        /// <c>.fermix role list</c>.
        /// </summary>
        public static IEnumerable<string> ListAllClassNames()
        {
            foreach (var c in NtfPool) yield return c.Name;
            foreach (var c in ChaosPool) yield return c.Name;
        }

        /// <summary>
        /// Найти и применить класс к игроку по «короткому» алиасу
        /// (<c>medic</c>, <c>jugger</c>/<c>juggernaut</c>, <c>commander</c>,
        /// <c>rifleman</c>/<c>none</c>) с учётом текущей фракции игрока.
        /// </summary>
        public static bool ApplyRoleByAlias(Player p, string alias, out string error)
        {
            error = null;
            if (p == null || !p.IsConnected) { error = "Игрок не найден или оффлайн."; return false; }
            if (!p.IsAlive) { error = "Игрок мёртв — сначала заспавни."; return false; }
            if (string.IsNullOrWhiteSpace(alias)) { error = "Не указан класс."; return false; }

            var team = p.Role?.Team;
            List<SquadClass> pool;
            Func<Player, bool> mate;
            if (team == Team.FoundationForces)
            {
                pool = NtfPool;
                mate = ally => ally?.Role?.Team == Team.FoundationForces;
            }
            else if (team == Team.ChaosInsurgency)
            {
                pool = ChaosPool;
                mate = ally => ally?.Role?.Team == Team.ChaosInsurgency;
            }
            else
            {
                error = "Игрок не в NTF и не в Chaos. Сначала смени ему фракцию.";
                return false;
            }

            SquadClassPassive? wantedPassive = alias.ToLowerInvariant() switch
            {
                "medic" or "медик" => SquadClassPassive.Medic,
                "jugger" or "juggernaut" or "джагг" or "джаггернаут" => SquadClassPassive.Juggernaut,
                "commander" or "командир" => SquadClassPassive.Commander,
                "rifleman" or "none" or "стрелок" or "подрывник" => SquadClassPassive.None,
                _ => null,
            };
            if (wantedPassive == null) { error = $"Неизвестный класс '{alias}'. Доступно: medic, jugger, commander, rifleman."; return false; }

            var cls = pool.FirstOrDefault(c => c.Passive == wantedPassive);
            if (cls == null) { error = $"В пуле {team} нет класса '{alias}'."; return false; }

            Apply(p, cls, mate);
            return true;
        }

        // ── core: assignment on respawn ─────────────────────────────

        private static void OnRespawnedTeam(RespawnedTeamEventArgs ev)
        {
            if (FermixCore.Config?.SquadClassesEnabled != true) return;
            if (ev?.Players == null) return;

            // GOC может перехватить NTF-волну с задержкой 0.7s. Ждём 1.5s,
            // чтобы дать GOC завершить перехват, и только потом смотрим
            // итоговую команду игрока.
            var snapshot = ev.Players.ToList();
            FermixScheduler.Delay(1.5f, () => AssignWave(snapshot));
        }

        private static void AssignWave(List<Player> players)
        {
            // Счётчики на текущую волну: считаем, сколько уже выдано каждого
            // класса, чтобы Командир/Медик/Джагг были не больше MaxPerWave.
            var ntfCounts = NtfPool.ToDictionary(c => c, _ => 0);
            var chaosCounts = ChaosPool.ToDictionary(c => c, _ => 0);

            foreach (var p in players)
            {
                if (p == null || !p.IsConnected) continue;

                // GOC обрабатывает своих сам через FermixGoc.Mark →
                // RegisterGoc(). Не лезем поверх.
                if (FermixGoc.IsMember(p)) continue;

                var team = p.Role?.Team;
                List<SquadClass> pool = null;
                Dictionary<SquadClass, int> counts = null;
                Func<Player, bool> mate = null;

                if (team == Team.FoundationForces)
                {
                    pool = NtfPool;
                    counts = ntfCounts;
                    mate = ally => ally?.Role?.Team == Team.FoundationForces;
                }
                else if (team == Team.ChaosInsurgency)
                {
                    pool = ChaosPool;
                    counts = chaosCounts;
                    mate = ally => ally?.Role?.Team == Team.ChaosInsurgency;
                }
                else
                {
                    // Любая другая фракция (SCP, D-class, Tutorial-уже-GOC,
                    // спектатор) нас не интересует.
                    continue;
                }

                var cls = PickClass(pool, counts);
                counts[cls] = counts[cls] + 1;
                Apply(p, cls, mate);
            }
        }

        private static SquadClass PickClass(List<SquadClass> pool,
                                             Dictionary<SquadClass, int> counts)
        {
            foreach (var cls in pool)
            {
                if (counts[cls] < cls.MaxPerWave) return cls;
            }
            return pool[pool.Count - 1];
        }

        private static void Apply(Player p, SquadClass cls, Func<Player, bool> mate)
        {
            try
            {
                p.ClearInventory();
                foreach (var item in cls.Loadout) p.AddItem(item);

                if (cls.MaxHealth > 0f)
                {
                    p.MaxHealth = cls.MaxHealth;
                    p.Health = cls.MaxHealth;
                }
                if (cls.ArtificialHealth > 0f)
                {
                    p.ArtificialHealth = Mathf.Max(p.ArtificialHealth, cls.ArtificialHealth);
                }

                // Scale меняем ТОЛЬКО если у класса явно задан кастомный масштаб.
                // Безусловный сброс p.Scale = Vector3.one на каждом Apply ломал
                // hit-rate у SCP по wave-spawned игрокам (наблюдалось в v2.6.0).
                if (cls.Scale > 0f && Math.Abs(cls.Scale - 1f) > 0.001f)
                    p.Scale = new Vector3(cls.Scale, cls.Scale, cls.Scale);

                p.CustomInfo = $"<color=#{cls.Color}>{cls.FactionLabel} — {cls.Name}</color>";

                lock (_lock)
                {
                    _passives[p.UserId] = new PassiveAssignment
                    {
                        Passive = cls.Passive,
                        ClassName = cls.Name,
                        ClassColor = cls.Color,
                        IsTeammate = mate,
                    };
                }

                // sosal-стиль: при назначении Медика — запускаем ему персональную
                // хил-корутину (аналог MTFMedicModule.StartHealingCoroutine).
                if (cls.Passive == SquadClassPassive.Medic) StartMedicHealing(p);
                else StopMedicHealing(p.UserId);

                SendHint(p, cls);
            }
            catch (Exception e)
            {
                FermixLog.Warn($"[SquadClasses] Apply '{cls.Name}': {e.Message}");
            }
        }

        private static void SendHint(Player p, SquadClass cls)
        {
            string body =
                $"<size=120%><b><color=#{cls.Color}>{cls.Name}</color></b></size>\n" +
                $"<color=#{cls.Color}>{cls.FactionLabel}</color>\n\n" +
                $"{cls.Description}";

            FermixHint.SendColored(p, body, "#" + cls.Color, 12f);
        }

        // ── passive: heal aura ──────────────────────────────────────

        private static void StartMedicHealing(Player medic)
        {
            if (medic?.UserId == null) return;
            string id = medic.UserId;
            lock (_medicCoroutines)
            {
                if (_medicCoroutines.TryGetValue(id, out var existing) && existing.IsRunning)
                    Timing.KillCoroutines(existing);
                _medicCoroutines[id] = Timing.RunCoroutine(HealingLoop(id));
            }
        }

        private static void StopMedicHealing(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            lock (_medicCoroutines)
            {
                if (_medicCoroutines.TryGetValue(userId, out var h) && h.IsRunning)
                    Timing.KillCoroutines(h);
                _medicCoroutines.Remove(userId);
            }
        }

        private static void StopAllMedicCoroutines()
        {
            lock (_medicCoroutines)
            {
                foreach (var h in _medicCoroutines.Values)
                    if (h.IsRunning) Timing.KillCoroutines(h);
                _medicCoroutines.Clear();
            }
        }

        /// <summary>
        /// 1:1 порт <c>MTFMedicModule.HealingLoop</c>: пока Медик жив и всё
        /// ещё в реестре пассивок, каждые <c>HealInterval</c> секунды
        /// лечит союзников в радиусе.
        /// </summary>
        private static IEnumerator<float> HealingLoop(string medicUserId)
        {
            while (true)
            {
                if (FermixCore.Config?.SquadClassesEnabled != true) break;

                float interval = Mathf.Max(0.1f, FermixCore.Config?.SquadClassesMedicHealInterval ?? 1f);
                yield return Timing.WaitForSeconds(interval);

                if (!IsActiveMedic(medicUserId, out var medic)) break;
                HealNearbyAllies(medic, medicUserId);
            }
            lock (_medicCoroutines) _medicCoroutines.Remove(medicUserId);
        }

        private static bool IsActiveMedic(string userId, out Player medic)
        {
            medic = Player.Get(userId);
            if (medic == null || !medic.IsConnected || !medic.IsAlive) return false;
            lock (_lock)
            {
                return _passives.TryGetValue(userId, out var asg)
                       && asg.Passive == SquadClassPassive.Medic;
            }
        }

        /// <summary>
        /// 1:1 порт <c>MTFMedicModule.HealNearbyAllies</c>.
        /// </summary>
        private static void HealNearbyAllies(Player medic, string medicUserId)
        {
            float radius = Mathf.Max(0.5f, FermixCore.Config?.SquadClassesMedicRadius ?? 6f);
            float amount = Mathf.Max(0f, FermixCore.Config?.SquadClassesMedicHealPerSec ?? 2f);
            if (amount <= 0f) return;

            Func<Player, bool> isTeammate;
            lock (_lock)
            {
                if (!_passives.TryGetValue(medicUserId, out var asg)) return;
                isTeammate = asg.IsTeammate;
            }

            Vector3 origin = medic.Position;
            foreach (var ally in Player.List)
            {
                if (ally == null || ally == medic || !ally.IsAlive) continue;
                if (isTeammate?.Invoke(ally) != true) continue;
                if (ally.Health >= ally.MaxHealth) continue;
                if (Vector3.Distance(origin, ally.Position) > radius) continue;

                ally.Health = Mathf.Min(ally.Health + amount, ally.MaxHealth);
            }
        }

        // ── passive: damage scaling ─────────────────────────────────
        // Метод намеренно не подписан на FermixEvents.OnPlayerHurt в v2.6.1.
        // Раньше при подписке у нас наблюдалась регрессия: урон не проходил
        // по wave-spawned игрокам (NTF/Chaos/GOC), включая SCP-вход. Чтобы
        // не блокировать урон ни при каких условиях, OnPlayerHurt сейчас
        // мёртвый код — оставлен для истории и возможного возврата в будущем
        // (тогда обязательно с unit-тестом, что ev.Amount никогда не падает
        // ниже исходного без явной passive у атакующего).
        private static void OnPlayerHurt(HurtingEventArgs ev)
        {
            if (FermixCore.Config?.SquadClassesEnabled != true) return;
            if (ev == null || !ev.IsAllowed || ev.Amount <= 0f) return;

            float dmg = ev.Amount;

            if (ev.Attacker?.UserId != null)
            {
                lock (_lock)
                {
                    if (_passives.TryGetValue(ev.Attacker.UserId, out var atk)
                        && atk.Passive == SquadClassPassive.Commander)
                    {
                        float mult = FermixCore.Config?.SquadClassesCommanderDamageMult ?? 1.20f;
                        dmg *= Mathf.Max(0.01f, mult);
                    }
                }
            }

            if (Math.Abs(dmg - ev.Amount) > 0.01f) ev.Amount = dmg;
        }

        // ── housekeeping ────────────────────────────────────────────

        private static void OnRoundStart()
        {
            lock (_lock) _passives.Clear();
            StopAllMedicCoroutines();
        }

        private static void OnRoundEnd(RoundEndedEventArgs _)
        {
            lock (_lock) _passives.Clear();
            StopAllMedicCoroutines();
        }

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_lock) _passives.Remove(ev.Player.UserId);
            StopMedicHealing(ev.Player.UserId);
        }

        // sosal MTFMedicModule.OnPlayerDied / OnChangingRole — при смерти или
        // смене роли Медик больше не Медик, корутину глушим.
        private static void OnPlayerDied(DiedEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            StopMedicHealing(ev.Player.UserId);
        }

        private static void OnRoleChange(ChangingRoleEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            // При смене роли сбрасываем passive и хил-корутину — новый класс
            // переоткроется в RespawnedTeam, если это волна.
            lock (_lock) _passives.Remove(ev.Player.UserId);
            StopMedicHealing(ev.Player.UserId);
        }
    }
}
