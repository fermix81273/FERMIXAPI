using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using FermixAPI.Core;
using PlayerRoles;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Кастомные классы внутри отрядов NTF и Chaos с пассивными способностями.
    ///
    /// Концепция: при каждом RespawnedTeam-событии (NTF/Chaos-волна) каждому
    /// прибывшему игроку случайно (по приоритету и MaxPerWave) выдаётся один из
    /// четырёх классов фракции. Каждый класс — это уникальный лоадаут плюс
    /// пассивная способность:
    /// <list type="bullet">
    ///   <item><b>Командир</b> — +20% исходящего урона союзникам.</item>
    ///   <item><b>Медик</b> — лечит союзников в радиусе 6 м на 5 HP/с.</item>
    ///   <item><b>Джаггернаут</b> — 200 HP и −10% входящего урона.</item>
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
                Description = "Командир оперативной группы. Координирует звено, " +
                              "несёт тяжёлое штурмовое снаряжение и капитанский " +
                              "ключ. Пассивка: <b>+20% исходящего урона</b>.",
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
                Name = "Медик NTF",
                Description = "Полевой медик. Возит на себе аптечки и " +
                              "адреналин, обладает SCP-500. Пассивка: " +
                              "<b>лечит союзников в радиусе 6 м на 5 HP/с</b>.",
                Color = "8be3ff",
                FactionLabel = "Mobile Task Force — NTF",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.ArmorLight,
                    ItemType.GunCOM18,
                    ItemType.Medkit,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.Adrenaline,
                    ItemType.SCP500,
                    ItemType.KeycardMTFOperative,
                    ItemType.Radio,
                },
                Passive = SquadClassPassive.Medic,
            },
            new SquadClass
            {
                Name = "Джаггернаут NTF",
                Description = "Тяжёлый штурмовик. Носит броню класса HEAVY и " +
                              "Logicer. Пассивка: <b>200 HP</b> и " +
                              "<b>−10% входящего урона</b>.",
                Color = "ff8b8b",
                FactionLabel = "Mobile Task Force — NTF",
                MaxPerWave = 1,
                MaxHealth = 200f,
                Loadout = new[]
                {
                    ItemType.ArmorHeavy,
                    ItemType.GunLogicer,
                    ItemType.GunRevolver,
                    ItemType.Medkit,
                    ItemType.KeycardMTFOperative,
                },
                Passive = SquadClassPassive.Juggernaut,
            },
            new SquadClass
            {
                Name = "Стрелок NTF",
                Description = "Базовый оперативник. Универсальное штурмовое " +
                              "снаряжение. Пассивных способностей нет — " +
                              "сила в дисциплине и количестве.",
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
                Description = "Лидер ячейки Хаоса. Координирует штурм, " +
                              "имеет ключ Insurgency и AK. Пассивка: " +
                              "<b>+20% исходящего урона</b>.",
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
                Name = "Медик Хаоса",
                Description = "Полевой санитар Хаоса. Аптечки, адреналин, " +
                              "SCP-500. Пассивка: <b>лечит союзников в " +
                              "радиусе 6 м на 5 HP/с</b>.",
                Color = "8be3ff",
                FactionLabel = "Chaos Insurgency",
                MaxPerWave = 1,
                Loadout = new[]
                {
                    ItemType.ArmorLight,
                    ItemType.GunCOM18,
                    ItemType.Medkit,
                    ItemType.Medkit,
                    ItemType.Adrenaline,
                    ItemType.Adrenaline,
                    ItemType.SCP500,
                    ItemType.Radio,
                },
                Passive = SquadClassPassive.Medic,
            },
            new SquadClass
            {
                Name = "Джаггернаут Хаоса",
                Description = "Штурмовой танк Хаоса. Тяжёлая броня, дробовик и " +
                              "револьвер. Пассивка: <b>200 HP</b> и " +
                              "<b>−10% входящего урона</b>.",
                Color = "ff8b8b",
                FactionLabel = "Chaos Insurgency",
                MaxPerWave = 1,
                MaxHealth = 200f,
                Loadout = new[]
                {
                    ItemType.ArmorHeavy,
                    ItemType.GunShotgun,
                    ItemType.GunRevolver,
                    ItemType.Medkit,
                    ItemType.KeycardChaosInsurgency,
                },
                Passive = SquadClassPassive.Juggernaut,
            },
            new SquadClass
            {
                Name = "Подрывник Хаоса",
                Description = "Базовый боец Хаоса. AK, две HE-гранаты и " +
                              "флэш для зачистки помещений. Пассивных " +
                              "способностей нет.",
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

        private static bool _initialized;
        private static MEC.CoroutineHandle _healTickHandle;

        // ── lifecycle ───────────────────────────────────────────────

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.SquadClassesEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnPlayerHurt += OnPlayerHurt;
            Exiled.Events.Handlers.Server.RespawnedTeam += OnRespawnedTeam;

            _healTickHandle = FermixScheduler.Repeat("squadclasses-heal", 1f, HealAuraTick);
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

            FermixScheduler.Cancel("squadclasses-heal");
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
        }

        public static void Unregister(Player p)
        {
            if (p?.UserId == null) return;
            lock (_lock) _passives.Remove(p.UserId);
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

        private static void HealAuraTick()
        {
            if (FermixCore.Config?.SquadClassesEnabled != true) return;

            float radius = Mathf.Max(0.5f, FermixCore.Config?.SquadClassesMedicRadius ?? 6f);
            float perSec = Mathf.Max(0f, FermixCore.Config?.SquadClassesMedicHealPerSec ?? 5f);
            if (perSec <= 0f) return;

            KeyValuePair<string, PassiveAssignment>[] snapshot;
            lock (_lock) snapshot = _passives.ToArray();

            foreach (var kvp in snapshot)
            {
                if (kvp.Value.Passive != SquadClassPassive.Medic) continue;
                var medic = Player.Get(kvp.Key);
                if (medic == null || !medic.IsConnected || !medic.IsAlive) continue;

                Vector3 origin = medic.Position;
                foreach (var ally in Player.List)
                {
                    if (ally == null || ally == medic || !ally.IsAlive) continue;
                    if (kvp.Value.IsTeammate?.Invoke(ally) != true) continue;
                    if (ally.Health >= ally.MaxHealth) continue;
                    if (Vector3.Distance(origin, ally.Position) > radius) continue;

                    ally.Health = Mathf.Min(ally.Health + perSec, ally.MaxHealth);
                }
            }
        }

        // ── passive: damage scaling ─────────────────────────────────

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

            if (ev.Player?.UserId != null)
            {
                lock (_lock)
                {
                    if (_passives.TryGetValue(ev.Player.UserId, out var tgt)
                        && tgt.Passive == SquadClassPassive.Juggernaut)
                    {
                        float mult = FermixCore.Config?.SquadClassesJuggernautIncomingMult ?? 0.90f;
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
        }

        private static void OnRoundEnd(RoundEndedEventArgs _)
        {
            lock (_lock) _passives.Clear();
        }

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_lock) _passives.Remove(ev.Player.UserId);
        }
    }
}
