using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Configs;
using FermixAPI.Core;
using FermixAPI.Utils;
using InventorySystem.Items;
using PlayerRoles;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Локальный (per-player) опыт и подписи уровней. Портировано из
    /// Hazbin.NoRules.PlayerXp с подгонкой под архитектуру FermixAPI:
    /// • опыт хранится через <see cref="PlayerDataStore{T}"/> в JSON
    ///   (директория <c>Configs/FermixAPI/Data/</c>);
    /// • уровни лежат отдельным YAML <c>FermixAPI/levels.yml</c>
    ///   (см. <see cref="FermixLevelsConfig"/>) — отдельно от основного
    ///   конфига плагина, как просил пользователь;
    /// • хинты идут через <see cref="FermixHint"/>, чтобы не конфликтовать
    ///   с другими подсистемами и стэкаться по id;
    /// • пассивный набор «1 опыт за минуту жизни» реализован через
    ///   <see cref="FermixScheduler"/>, без MEC напрямую.
    ///
    /// Включение: <see cref="Config.PlayerXpEnabled"/>. Также внутри
    /// <see cref="FermixLevelsConfig.Enabled"/> можно отдельно отключить
    /// именно подписи уровней, оставив прокачку «в тихую».
    /// </summary>
    public static class FermixPlayerXp
    {
        public class PlayerXpData
        {
            public float Experience { get; set; }
        }

        private static readonly object _sync = new object();
        private static PlayerDataStore<PlayerXpData> _store;
        private static FermixLevelsConfig _levels;
        private static bool _initialized;

        /// <summary>Текущая загруженная конфигурация уровней (или null до Initialize).</summary>
        public static FermixLevelsConfig Levels => _levels;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.PlayerXpEnabled != true) return;

            _store = new PlayerDataStore<PlayerXpData>("playerxp");
            _levels = FermixConfigUtils.Load<FermixLevelsConfig>("levels");
            SortLevels();

            FermixEvents.OnPlayerJoin += OnJoined;
            FermixEvents.OnRoleChange += OnChangingRole;
            FermixEvents.OnPlayerSpawned += OnSpawned;
            FermixEvents.OnPlayerDied += OnDied;
            FermixEvents.OnEscape += OnEscaped;
            FermixEvents.OnActivateGenerator += OnGenerator;
            FermixEvents.OnLockerInteract += OnLocker;
            FermixEvents.OnDoorInteract += OnDoor;
            FermixEvents.OnItemPickup += OnItemPickup;
            FermixEvents.OnItemUse += OnItemUse;

            // Пассивный набор: один общий тикер на всех живых.
            if (_levels?.AliveTickSeconds > 0f)
            {
                FermixScheduler.Repeat("FermixPlayerXp.AliveTick", _levels.AliveTickSeconds, AliveTick);
            }

            _initialized = true;
            FermixLog.Info("FermixPlayerXp включён.");
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnPlayerJoin -= OnJoined;
            FermixEvents.OnRoleChange -= OnChangingRole;
            FermixEvents.OnPlayerSpawned -= OnSpawned;
            FermixEvents.OnPlayerDied -= OnDied;
            FermixEvents.OnEscape -= OnEscaped;
            FermixEvents.OnActivateGenerator -= OnGenerator;
            FermixEvents.OnLockerInteract -= OnLocker;
            FermixEvents.OnDoorInteract -= OnDoor;
            FermixEvents.OnItemPickup -= OnItemPickup;
            FermixEvents.OnItemUse -= OnItemUse;

            FermixScheduler.Cancel("FermixPlayerXp.AliveTick");

            _store?.Save();
            _store = null;
            _levels = null;
            _initialized = false;
        }

        // ── Public API ────────────────────────────────────────────────

        /// <summary>Получить накопленный опыт игрока (0, если записи нет).</summary>
        public static float GetXp(Player player)
        {
            if (player == null || _store == null) return 0f;
            return _store.Get(player).Experience;
        }

        /// <summary>Установить опыт игрока (без проверок и без хинта).</summary>
        public static void SetXp(Player player, float value)
        {
            if (player == null || _store == null) return;
            lock (_sync)
            {
                _store.Modify(player, d => d.Experience = Math.Max(0f, value));
            }
            UpdateCustomInfo(player);
        }

        /// <summary>
        /// Выдаёт игроку <paramref name="raw"/> опыта. Внутри делится на
        /// <see cref="FermixLevelsConfig.XpDivisor"/>, как в Hazbin (там было /3).
        /// Бонусный множитель применяется, если ник содержит <see cref="FermixLevelsConfig.SpecialTag"/>.
        /// </summary>
        public static void GiveXp(Player player, float raw)
        {
            if (player == null || _store == null || _levels == null) return;
            if (player.DoNotTrack) return;

            float divisor = _levels.XpDivisor <= 0f ? 1f : _levels.XpDivisor;
            float gain = raw / divisor;

            if (!string.IsNullOrEmpty(_levels.SpecialTag) &&
                player.Nickname?.IndexOf(_levels.SpecialTag, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                gain *= _levels.TaggedNicknameMultiplier;
            }

            if (gain <= 0f) return;

            lock (_sync) { _store.Modify(player, d => d.Experience += gain); }

            FermixHint.ShowStacked(
                player,
                $"<b>Вы получили <color=#ffe91f>{Math.Round(gain, 2)}</color> опыта!</b>",
                duration: 2.4f,
                priority: 3,
                id: "fermix_xp_gain",
                category: HintCategory.Custom);

            UpdateCustomInfo(player);
        }

        /// <summary>Возвращает уровень, соответствующий текущему опыту игрока.</summary>
        public static FermixLevel GetLevel(Player player)
        {
            if (player == null || _store == null || _levels?.Levels == null || _levels.Levels.Count == 0)
                return null;

            float xp = _store.Get(player).Experience;

            FermixLevel best = null;
            foreach (var lvl in _levels.Levels)
            {
                if (lvl == null) continue;
                if (xp >= lvl.MinXp && (best == null || lvl.MinXp > best.MinXp))
                    best = lvl;
            }
            return best;
        }

        /// <summary>Принудительно перерисовать CustomInfo с подписью уровня.</summary>
        public static void UpdateCustomInfo(Player player)
        {
            if (player == null || !player.IsConnected) return;
            if (_levels == null || !_levels.Enabled) return;

            try
            {
                string display = player.DisplayNickname ?? player.Nickname ?? string.Empty;
                display = display.Replace('[', '(').Replace(']', ')');

                string levelTag;
                string white = CustomInfoColor.White.GetHexColor();

                if (player.DoNotTrack)
                {
                    levelTag = $"<color=#{white}>(</color><color=#{_levels.UnknownColor.GetHexColor()}>{_levels.UnknownText}</color><color=#{white}>)</color>";
                }
                else
                {
                    var level = GetLevel(player);
                    if (level != null)
                    {
                        levelTag = $"<color=#{white}>(</color><color=#{level.Color.GetHexColor()}>{level.Text}</color><color=#{white}>)</color>";
                    }
                    else
                    {
                        levelTag = $"<color=#{white}>(</color><color=#{_levels.UnknownColor.GetHexColor()}>{_levels.UnknownText}</color><color=#{white}>)</color>";
                    }
                }

                player.CustomInfo = $"{levelTag}\n{display}";
                player.InfoArea = (PlayerInfoArea)~(int)PlayerInfoArea.Nickname;
            }
            catch (Exception ex) { FermixLog.Warn($"FermixPlayerXp.UpdateCustomInfo: {ex.Message}"); }
        }

        // ── Handlers ──────────────────────────────────────────────────

        private static void OnJoined(JoinedEventArgs ev)
        {
            var p = ev?.Player;
            if (p == null) return;

            // Создаём запись (через Get) — Modify=false, чтобы не плодить save'ы.
            _store?.Get(p);
            FermixScheduler.Delay(0.45f, () => UpdateCustomInfo(p));
        }

        private static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (ev?.Player == null) return;
            FermixScheduler.Delay(0.5f, () => UpdateCustomInfo(ev.Player));
        }

        private static void OnSpawned(SpawnedEventArgs ev)
        {
            if (ev?.Player == null) return;
            UpdateCustomInfo(ev.Player);
        }

        private static void OnGenerator(ActivatingGeneratorEventArgs ev)
        {
            if (ev?.Player == null || !ev.IsAllowed) return;
            GiveXp(ev.Player, _levels?.XpGenerator ?? 0f);
        }

        private static void OnLocker(InteractingLockerEventArgs ev)
        {
            if (ev?.Player == null || !ev.IsAllowed) return;
            GiveXp(ev.Player, _levels?.XpLocker ?? 0f);
        }

        private static void OnDoor(InteractingDoorEventArgs ev)
        {
            if (ev?.Player == null || !ev.IsAllowed) return;
            GiveXp(ev.Player, _levels?.XpLocker ?? 0f);
        }

        private static void OnItemPickup(PickingUpItemEventArgs ev)
        {
            if (ev?.Player == null || ev.Pickup == null || !ev.IsAllowed) return;
            if (_levels == null) return;

            // Хазбин различал SCP-предметы, спецоружие, стрелковое, ключ-карты.
            ItemType type = ev.Pickup.Type;
            float gain;

            if (type == ItemType.MicroHID || type == ItemType.Jailbird || type == ItemType.ParticleDisruptor)
                gain = 0.7f;
            else
            {
                var category = type.GetCategory();
                gain = category switch
                {
                    ItemCategory.SCPItem => _levels.XpScpItem,
                    ItemCategory.Firearm => _levels.XpFirearm,
                    ItemCategory.Keycard => _levels.XpKeycard,
                    _ => _levels.XpDefaultItem,
                };
            }

            GiveXp(ev.Player, gain);
        }

        private static void OnItemUse(UsingItemEventArgs ev)
        {
            if (ev?.Player == null || ev.Item == null) return;
            if (_levels == null) return;

            float gain = ev.Item.Type.GetCategory() == ItemCategory.SCPItem
                ? _levels.XpScpItemUsed
                : _levels.XpDefaultUsed;
            GiveXp(ev.Player, gain);
        }

        private static void OnDied(DiedEventArgs ev)
        {
            if (ev == null) return;
            var attacker = ev.Attacker;
            var victim = ev.Player;
            if (attacker == null || victim == null || attacker == victim) return;

            float gain;
            if (IsScp(victim))
                gain = _levels?.XpKillScp ?? 0f;
            else if (IsScp(attacker))
                gain = _levels?.XpKillByScp ?? 0f;
            else
                gain = _levels?.XpKillHuman ?? 0f;

            GiveXp(attacker, gain);
        }

        private static void OnEscaped(EscapingEventArgs ev)
        {
            if (ev?.Player == null || !ev.IsAllowed) return;

            GiveXp(ev.Player, _levels?.XpEscape ?? 0f);

            if (ev.Player.IsCuffed && ev.Player.Cuffer != null)
            {
                GiveXp(ev.Player.Cuffer, _levels?.XpEscapeDisarmer ?? 0f);
            }
        }

        private static void AliveTick()
        {
            if (_levels == null) return;
            foreach (var p in Player.List)
            {
                try
                {
                    if (p == null || !p.IsConnected || !p.IsAlive) continue;
                    if (p.DoNotTrack) continue;
                    GiveXp(p, _levels.XpDivisor);
                }
                catch (Exception ex) { FermixLog.Warn($"FermixPlayerXp.AliveTick: {ex.Message}"); }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static bool IsScp(Player p)
            => p?.Role != null && p.Role.Side == Side.Scp;

        private static void SortLevels()
        {
            if (_levels?.Levels == null) return;
            _levels.Levels = _levels.Levels
                .Where(l => l != null)
                .OrderBy(l => l.MinXp)
                .ToList();
        }
    }
}
