using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.EventArgs.Scp1344;
using FermixAPI.Core;
using InventorySystem.Items.Usables.Scp1344;
using MEC;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.Wearables;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// SCRAMBLE-очки (SCP-1344) — глушитель цели SCP-096.
    ///
    /// v2.6.4: полная переделка по аналогии с <see cref="FermixNvg"/>:
    /// • Ванильная активация SCP-1344 (физическое надевание через USE) для
    ///   скрамбл-помеченных серийников ОТКЛЮЧЕНА
    ///   (<c>ChangingStatus.IsAllowed=false</c>).
    /// • Активация — через отдельный SSS-бинд «FermixAPI: SCRAMBLE» (по
    ///   умолчанию <c>N</c>). Игрок должен иметь скрамбл-помеченный SCP-1344
    ///   в инвентаре.
    /// • После активации SCRAMBLE действует <see cref="Config.ScrambleActiveDuration"/>
    ///   секунд (дефолт 30). На HUD показывается обратный отсчёт.
    /// • Когда активная фаза заканчивается — автодеактивация и кулдаун
    ///   <see cref="Config.ScrambleCooldown"/> секунд (дефолт 120 = 2 мин).
    ///   В кулдауне HUD пишет «Перезарядка: M:SS», SSS-бинд игнорируется.
    /// • Иммунитет к SCP-096: пока игрок в Active-фазе скрамбла —
    ///   <see cref="OnAddingTarget"/> отменяет добавление в цели 096. В
    ///   ванильном SCP:SL такого иммунитета нет, поэтому весь блок ОБЯЗАН
    ///   делать наш плагин (см. v2.6.3).
    /// </summary>
    public static class FermixScramble
    {
        private static readonly (RoomType Room, float Weight)[] SpawnPool =
        {
            (RoomType.HczArmory,    3f),
            (RoomType.Hcz049,       2f),
            (RoomType.HczTestRoom,  2f),
            (RoomType.EzGateA,      1f),
            (RoomType.EzGateB,      1f),
            (RoomType.EzShelter,    1f),
        };

        private const string GlowId = "fermix_scramble";

        // SSS bind id (>=322 чтобы не пересекаться с дефолтами FermixInput/106
        // и NVG (309/322). Берём 310/323).
        private const int ScrambleHeaderId = 310;
        private const int ScrambleToggleKeyId = 323;
        // Persistent hint id для индикатора SCRAMBLE.
        private const string HudHintId = "fermix_scramble_hud";

        private static readonly object _lock = new();
        private static readonly HashSet<ushort> _ourSerials = new();

        // userId → когда заканчивается active-фаза (UTC ticks).
        private static readonly Dictionary<string, DateTime> _activeUntil = new(StringComparer.Ordinal);
        // userId → когда заканчивается кулдаун (UTC ticks).
        private static readonly Dictionary<string, DateTime> _cooldownUntil = new(StringComparer.Ordinal);

        private static readonly List<SettingBase> _ownSettings = new();
        private static bool _initialized;
        private static CoroutineHandle _hudTick;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.ScrambleEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerSpawned += OnPlayerSpawned;
            FermixEvents.OnItemPickup += OnItemPickup;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnRoleChange += OnRoleChange;
            // Блокируем ванильное надевание SCP-1344 для НАШИХ серийников.
            // Обычные SCP-1344 (не помеченные как скрамбл и не как NVG)
            // продолжают работать как ваниль.
            Exiled.Events.Handlers.Scp1344.ChangingStatus += OnChangingStatus;
            // Гасим визуальную модель «очки на лице» на любых событиях
            // ChangedStatus — на случай, если игра их успела включить.
            Exiled.Events.Handlers.Scp1344.ChangedStatus += OnChangedStatus;
            // Иммунитет к SCP-096: пока игрок в Active-фазе.
            Exiled.Events.Handlers.Scp096.AddingTarget += OnAddingTarget;
            // ItemRemoved — если игрок выбрасывает скрамбл во время активной
            // фазы, можем оставить эффект (как у NVG-фикса) — но логичнее
            // деактивировать его на месте, чтобы не прятаться от 096 без
            // самого предмета. Делаем именно так.
            Exiled.Events.Handlers.Player.ItemRemoved += OnItemRemoved;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;

            FermixGlow.AddGlow(GlowId,
                serial => { lock (_lock) return _ourSerials.Contains(serial); },
                new Color(1f, 0.4f, 0.85f),
                intensity: 1.4f,
                range: 3.5f,
                pulseEffect: true,
                pulseSpeed: 1.2f);

            // SSS bind для активации SCRAMBLE.
            try
            {
                if (FermixInput.IsInitialized)
                {
                    var header = new HeaderSetting(ScrambleHeaderId, "FermixAPI: SCRAMBLE", string.Empty, false);
                    _ownSettings.Add(header);
                    _ownSettings.Add(new KeybindSetting(
                        id: ScrambleToggleKeyId,
                        label: "[SCRAMBLE] Активировать защиту от SCP-096",
                        suggested: KeyCode.N,
                        preventInteractionOnGUI: false,
                        allowSpectatorTrigger: false,
                        hintDescription: "Активирует SCRAMBLE-очки. Скрывает игрока от SCP-096 на " +
                                         "ограниченное время. После окончания заряда — кулдаун.",
                        collectionId: byte.MaxValue,
                        header: header,
                        onChanged: (player, setting) =>
                        {
                            if (setting is KeybindSetting kb && kb.IsPressed)
                                ToggleScramble(player);
                        }));
                    FermixInput.DropExistingByIds(new[] { ScrambleHeaderId, ScrambleToggleKeyId });
                    SettingBase.Register(_ownSettings);
                }
                else
                {
                    FermixLog.Warn("FermixScramble: FermixInput не инициализирован, SSS-бинд SCRAMBLE не зарегистрирован.");
                }
            }
            catch (Exception ex)
            {
                FermixLog.Error($"FermixScramble: не удалось зарегистрировать SSS keybind: {ex.Message}");
                _ownSettings.Clear();
            }

            // HUD-тик каждую 0.5с обновляет хинт у владельцев (active /
            // cooldown / ready) и автодеактивирует по истечении заряда.
            _hudTick = FermixScheduler.Repeat("fermix_scramble_hud_tick", 0.5f, HudTick);

            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerSpawned -= OnPlayerSpawned;
            FermixEvents.OnItemPickup -= OnItemPickup;
            FermixEvents.OnPlayerLeave -= OnPlayerLeave;
            FermixEvents.OnRoleChange -= OnRoleChange;
            Exiled.Events.Handlers.Scp1344.ChangingStatus -= OnChangingStatus;
            Exiled.Events.Handlers.Scp1344.ChangedStatus -= OnChangedStatus;
            Exiled.Events.Handlers.Scp096.AddingTarget -= OnAddingTarget;
            Exiled.Events.Handlers.Player.ItemRemoved -= OnItemRemoved;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;

            FermixScheduler.Cancel(_hudTick);

            FermixGlow.RemoveGlow(GlowId);

            lock (_lock) _ourSerials.Clear();
            lock (_activeUntil) _activeUntil.Clear();
            lock (_cooldownUntil) _cooldownUntil.Clear();

            try
            {
                if (_ownSettings.Count > 0) SettingBase.Unregister(settings: _ownSettings);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixScramble.Shutdown unregister: {ex.Message}");
            }
            _ownSettings.Clear();

            _initialized = false;
        }

        // ── публичный API ─────────────────────────────────────────────

        /// <summary>
        /// Является ли SCP-1344 с указанным serial — нашим скрамблером?
        /// </summary>
        public static bool IsScrambleSerial(ushort serial)
        {
            lock (_lock) return _ourSerials.Contains(serial);
        }

        /// <summary>
        /// Выдать SCRAMBLE-очки игроку (по команде <c>.item give … scramble</c>).
        /// </summary>
        public static bool GiveTo(Player p)
        {
            if (p == null || !p.IsConnected) return false;
            try
            {
                var item = p.AddItem(ItemType.SCP1344);
                if (item == null) return false;
                lock (_lock) _ourSerials.Add(item.Serial);

                int active = Mathf.RoundToInt(FermixCore.Config?.ScrambleActiveDuration ?? 30f);
                int cooldown = Mathf.RoundToInt(FermixCore.Config?.ScrambleCooldown ?? 120f);
                FermixHint.SendColored(p,
                    "<size=110%><b><color=#ff66cc>SCRAMBLE — очки защиты от SCP-096</color></b></size>\n" +
                    "Чтобы активировать — нажмите бинд из меню SSS «FermixAPI: SCRAMBLE»\n" +
                    "(по умолчанию <b>N</b>). Надевание физических очков отключено.\n" +
                    $"Заряд: <b>{active}с</b> активной защиты, далее <b>{cooldown / 60}:{cooldown % 60:D2}</b> перезарядки.",
                    "#ff66cc", 6f);
                return true;
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixScramble.GiveTo: {ex.Message}");
                return false;
            }
        }

        // ── round lifecycle ───────────────────────────────────────────

        private static void OnRoundStart()
        {
            int count = Mathf.Clamp(FermixCore.Config?.ScrambleSpawnCount ?? 2, 0, 8);
            FermixScheduler.Delay(FermixCore.Config?.ScrambleSpawnDelay ?? 4f, () => SpawnItems(count));
        }

        private static void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs _)
        {
            lock (_lock) _ourSerials.Clear();
            lock (_activeUntil) _activeUntil.Clear();
            lock (_cooldownUntil) _cooldownUntil.Clear();
        }

        private static void SpawnItems(int count)
        {
            if (count <= 0) return;

            var available = SpawnPool
                .Select(p => (Room: Room.Get(p.Room), p.Weight))
                .Where(t => t.Room != null)
                .ToList();

            if (available.Count == 0)
            {
                FermixLog.Warn("FermixScramble: ни одной из конфигурируемых комнат не существует на текущей seed — спавн пропущен.");
                return;
            }

            var usedRooms = new HashSet<Room>();

            for (int i = 0; i < count; i++)
            {
                var pool = available.Where(t => !usedRooms.Contains(t.Room)).ToList();
                if (pool.Count == 0) pool = available;

                float total = pool.Sum(p => p.Weight);
                float roll = UnityEngine.Random.value * total;
                Room chosen = null;
                foreach (var (room, w) in pool)
                {
                    roll -= w;
                    if (roll <= 0f) { chosen = room; break; }
                }
                chosen ??= pool[0].Room;
                usedRooms.Add(chosen);

                Vector3 pos = chosen.Position
                              + Vector3.up * 1.0f
                              + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));

                Pickup pickup;
                try
                {
                    pickup = Pickup.CreateAndSpawn(ItemType.SCP1344, pos);
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixScramble: spawn failed in {chosen.Name}: {ex.Message}");
                    continue;
                }
                if (pickup == null) continue;

                lock (_lock) _ourSerials.Add(pickup.Serial);
            }
        }

        // ── core: SCP-1344 status hooks ──────────────────────────────

        // Отменяем ванильное «надевание» очков ТОЛЬКО для скрамбл-помеченных
        // предметов. Активация физическим биндом USE отключена, активация
        // только через наш SSS-бинд (см. ToggleScramble).
        private static void OnChangingStatus(ChangingStatusEventArgs ev)
        {
            if (ev?.Player == null || ev.Scp1344 == null) return;
            if (!IsScrambleSerial(ev.Scp1344.Serial)) return;

            if (ev.Scp1344StatusNew == Scp1344Status.Activating
                || ev.Scp1344StatusNew == Scp1344Status.Active)
            {
                ev.IsAllowed = false;
            }
        }

        // Косметика: гасим визуальную модель «очки на лице», если игра
        // успела её включить (например для не-скрамбл SCP-1344).
        private static void OnChangedStatus(ChangedStatusEventArgs ev)
        {
            if (ev?.Player == null) return;
            HideGoggles(ev.Player);
            var captured = ev.Player;
            FermixScheduler.Delay(0.05f, () => HideGoggles(captured));
        }

        // ── гарантия иммунитета к SCP-096 ─────────────────────────────

        private static void OnAddingTarget(AddingTargetEventArgs ev)
        {
            if (ev?.Target?.UserId == null) return;
            if (!ev.IsLooking) return;

            // Иммунитет даёт ТОЛЬКО наша active-фаза. Ваниль 1344 в этом
            // плагине отключена — на ChangingStatus мы его блокируем.
            if (IsActive(ev.Target.UserId))
            {
                ev.IsAllowed = false;
            }
        }

        private static bool IsActive(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;
            lock (_activeUntil)
            {
                if (!_activeUntil.TryGetValue(userId, out var until)) return false;
                return DateTime.UtcNow < until;
            }
        }

        private static bool IsCoolingDown(string userId, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (string.IsNullOrEmpty(userId)) return false;
            lock (_cooldownUntil)
            {
                if (!_cooldownUntil.TryGetValue(userId, out var until)) return false;
                var now = DateTime.UtcNow;
                if (now >= until)
                {
                    _cooldownUntil.Remove(userId);
                    return false;
                }
                remaining = until - now;
                return true;
            }
        }

        // ── активация по биндy ────────────────────────────────────────

        private static void ToggleScramble(Player p)
        {
            if (p == null || !p.IsConnected || !p.IsAlive) return;
            if (p.UserId == null) return;

            // Должны быть скрамбл-помеченные очки в инвентаре.
            bool hasScramble = false;
            foreach (var it in p.Items)
            {
                if (it == null || it.Type != ItemType.SCP1344) continue;
                if (!IsScrambleSerial(it.Serial)) continue;
                hasScramble = true;
                break;
            }

            if (!hasScramble)
            {
                FermixHint.Send(p,
                    "<color=#ff8b8b>В инвентаре нет SCRAMBLE-очков.</color>",
                    2.5f);
                return;
            }

            // Если уже активны — позволяем выключить досрочно (уход
            // в кулдаун с момента выключения).
            if (IsActive(p.UserId))
            {
                DeactivateScramble(p, manual: true);
                return;
            }

            // Проверяем кулдаун.
            if (IsCoolingDown(p.UserId, out var remaining))
            {
                FermixHint.SendColored(p,
                    "<color=#ff8b8b><b>SCRAMBLE на перезарядке.</b></color>\n" +
                    $"Готов через <b>{FormatDuration(remaining)}</b>.",
                    "#ff8b8b", 2.5f);
                return;
            }

            ActivateScramble(p);
        }

        private static void ActivateScramble(Player p)
        {
            if (p?.UserId == null) return;
            try
            {
                float duration = Mathf.Max(1f, FermixCore.Config?.ScrambleActiveDuration ?? 30f);
                var until = DateTime.UtcNow.AddSeconds(duration);
                lock (_activeUntil) _activeUntil[p.UserId] = until;

                FermixHint.SendColored(p,
                    "<color=#ff66cc><b>SCRAMBLE активирован</b></color>\n" +
                    $"Защита от SCP-096 действует <b>{Mathf.RoundToInt(duration)}с</b>.",
                    "#ff66cc", 2.5f);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixScramble.ActivateScramble: {ex.Message}");
            }
        }

        private static void DeactivateScramble(Player p, bool manual)
        {
            if (p?.UserId == null) return;
            try
            {
                bool wasActive;
                lock (_activeUntil) wasActive = _activeUntil.Remove(p.UserId);

                float cd = Mathf.Max(0f, FermixCore.Config?.ScrambleCooldown ?? 120f);
                if (cd > 0f)
                {
                    var until = DateTime.UtcNow.AddSeconds(cd);
                    lock (_cooldownUntil) _cooldownUntil[p.UserId] = until;
                }

                if (wasActive)
                {
                    string reason = manual ? "выключен вручную" : "заряд исчерпан";
                    FermixHint.SendColored(p,
                        $"<color=#aaaaaa>SCRAMBLE: {reason}. Перезарядка <b>{FormatDuration(TimeSpan.FromSeconds(cd))}</b>.</color>",
                        "#aaaaaa", 3f);
                }
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixScramble.DeactivateScramble: {ex.Message}");
            }
        }

        // ── HUD tick: обратный отсчёт + автодеактивация ──────────────

        private static void HudTick()
        {
            var now = DateTime.UtcNow;

            // 1. Active-фаза — обратный отсчёт + автодеактивация.
            List<KeyValuePair<string, DateTime>> activeSnapshot;
            lock (_activeUntil) activeSnapshot = _activeUntil.ToList();

            foreach (var kv in activeSnapshot)
            {
                var p = Player.Get(kv.Key);
                if (p == null || !p.IsConnected || !p.IsAlive)
                {
                    lock (_activeUntil) _activeUntil.Remove(kv.Key);
                    SafeRemoveHud(kv.Key);
                    continue;
                }

                if (now >= kv.Value)
                {
                    DeactivateScramble(p, manual: false);
                    continue;
                }

                var left = kv.Value - now;
                FermixHintStack.ShowPersistentHint(p,
                    $"<color=#ff66cc><b>SCRAMBLE</b></color> · активен: <b>{left.TotalSeconds:F1}с</b>",
                    HudHintId, priority: 50, color: "#ff66cc", showBullet: false);
            }

            // 2. Cooldown-фаза — обратный отсчёт; снимаем по истечении.
            List<KeyValuePair<string, DateTime>> cooldownSnapshot;
            lock (_cooldownUntil) cooldownSnapshot = _cooldownUntil.ToList();

            foreach (var kv in cooldownSnapshot)
            {
                if (now >= kv.Value)
                {
                    lock (_cooldownUntil) _cooldownUntil.Remove(kv.Key);
                    var ready = Player.Get(kv.Key);
                    if (ready != null && ready.IsConnected && ready.IsAlive && HasScramble(ready))
                    {
                        FermixHintStack.ShowPersistentHint(ready,
                            "<color=#88ff88><b>SCRAMBLE: готов</b></color>",
                            HudHintId, priority: 50, color: "#88ff88", showBullet: false);
                    }
                    else SafeRemoveHud(kv.Key);
                    continue;
                }

                var p = Player.Get(kv.Key);
                if (p == null || !p.IsConnected || !p.IsAlive)
                {
                    SafeRemoveHud(kv.Key);
                    continue;
                }
                if (!HasScramble(p))
                {
                    SafeRemoveHud(kv.Key);
                    continue;
                }

                var left = kv.Value - now;
                FermixHintStack.ShowPersistentHint(p,
                    $"<color=#aaaaaa><b>SCRAMBLE</b></color> · перезарядка: <b>{FormatDuration(left)}</b>",
                    HudHintId, priority: 50, color: "#aaaaaa", showBullet: false);
            }
        }

        private static void SafeRemoveHud(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            var p = Player.Get(userId);
            if (p != null) FermixHintStack.RemoveHint(p, HudHintId);
        }

        private static bool HasScramble(Player p)
        {
            if (p == null) return false;
            foreach (var it in p.Items)
            {
                if (it == null || it.Type != ItemType.SCP1344) continue;
                if (IsScrambleSerial(it.Serial)) return true;
            }
            return false;
        }

        private static string FormatDuration(TimeSpan ts)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt((float)ts.TotalSeconds));
            int m = total / 60;
            int s = total % 60;
            return m > 0 ? $"{m}:{s:D2}" : $"{s}с";
        }

        // ── housekeeping ──────────────────────────────────────────────

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_activeUntil) _activeUntil.Remove(ev.Player.UserId);
            lock (_cooldownUntil) _cooldownUntil.Remove(ev.Player.UserId);
        }

        private static void OnRoleChange(ChangingRoleEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            // При смене роли — снимаем активную фазу, кулдаун оставляем.
            lock (_activeUntil) _activeUntil.Remove(ev.Player.UserId);
        }

        private static void OnPlayerDied(Exiled.Events.EventArgs.Player.DiedEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_activeUntil) _activeUntil.Remove(ev.Player.UserId);
            lock (_cooldownUntil) _cooldownUntil.Remove(ev.Player.UserId);
        }

        // Если игрок выбросил единственные SCRAMBLE-очки во время Active —
        // принудительно деактивируем (нельзя прятаться без предмета).
        private static void OnItemRemoved(Exiled.Events.EventArgs.Player.ItemRemovedEventArgs ev)
        {
            var p = ev?.Player;
            if (p?.UserId == null) return;
            if (ev.Item == null || ev.Item.Type != ItemType.SCP1344) return;
            if (!IsScrambleSerial(ev.Item.Serial)) return;

            if (HasScramble(p)) return; // ещё есть другие — оставляем
            if (IsActive(p.UserId))
                DeactivateScramble(p, manual: true);
        }

        // ── скрытие визуальной модели «очки на лице» ─────────────────

        private static void OnPlayerSpawned(SpawnedEventArgs ev)
        {
            if (ev?.Player == null) return;
            HideGoggles(ev.Player);
        }

        private static void OnItemPickup(PickingUpItemEventArgs ev)
        {
            if (ev == null || ev.Player == null || ev.Pickup == null) return;
            if (ev.Pickup.Type != ItemType.SCP1344) return;
            var p = ev.Player;
            FermixScheduler.Delay(0.05f, () => HideGoggles(p));
        }

        private static void HideGoggles(Player p)
        {
            if (p == null || !p.IsConnected) return;
            try
            {
                p.ReferenceHub?.DisableWearables(WearableElements.Scp1344Goggles);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixScramble.HideGoggles: {ex.Message}");
            }
        }
    }
}
