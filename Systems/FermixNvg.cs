using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp1344;
using FermixAPI.Core;
using InventorySystem.Items.Usables.Scp1344;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Прибор ночного видения (NVG) — кастомный предмет на базе SCP-1344.
    ///
    /// v2.6.1: НЕ зависит от ванильной активации SCP-1344.
    /// • Ивент <see cref="Exiled.Events.Handlers.Scp1344.ChangingStatus"/> для
    ///   NVG-помеченного предмета принудительно отменяется (IsAllowed=false),
    ///   так что ванильное «надевание очков» физически отключено.
    /// • Активация — через отдельный SSS-бинд (видно в меню Server Specific
    ///   Settings: «FermixAPI: NVG»). По нажатию переключается включение.
    /// • При активации игрок получает <c>EffectType.NightVision</c> — это
    ///   чисто клиентский эффект яркого зрения, никакого внешнего ToyLight
    ///   не создаётся, и другие игроки в комнате никакого свечения не видят.
    /// • Дроп в мире остаётся подсвеченным через FermixGlow, чтобы предмет
    ///   на полу был заметен.
    /// </summary>
    public static class FermixNvg
    {
        private static readonly (RoomType Room, float Weight)[] SpawnPool =
        {
            (RoomType.HczArmory,    3f),
            (RoomType.Hcz939,       2f),
            (RoomType.HczNuke,      2f),
            (RoomType.LczArmory,    1f),
            (RoomType.EzGateA,      1f),
            (RoomType.EzGateB,      1f),
        };

        private const string GlowId = "fermix_nvg";

        // SSS bind id (>=322 чтобы не пересекаться с FermixInput-дефолтами 300-306,
        // FermixScp106Bindings 308/320 и пользовательскими 307+).
        private const int NvgHeaderId = 309;
        private const int NvgToggleKeyId = 322;

        private static readonly object _lock = new();
        private static readonly HashSet<ushort> _nvgSerials = new();
        private static readonly HashSet<string> _activeUsers = new(StringComparer.Ordinal);

        private static readonly List<SettingBase> _ownSettings = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.NvgEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnRoleChange += OnRoleChange;
            Exiled.Events.Handlers.Scp1344.ChangingStatus += OnChangingStatus;
            Exiled.Events.Handlers.Player.ItemRemoved += OnItemRemoved;
            Exiled.Events.Handlers.Player.Died += OnPlayerDied;

            FermixGlow.AddGlow(GlowId,
                serial => { lock (_lock) return _nvgSerials.Contains(serial); },
                new Color(0.2f, 1f, 0.4f),
                intensity: 1.2f,
                range: 3f,
                pulseEffect: true,
                pulseSpeed: 1.0f);

            // SSS bind для активации NVG. Регистрируем СВОЮ секцию (не лезем
            // в FermixInput-дефолты), чтобы кнопка появилась в SSS как
            // «FermixAPI: NVG» отдельным блоком.
            try
            {
                if (FermixInput.IsInitialized)
                {
                    var header = new HeaderSetting(NvgHeaderId, "FermixAPI: NVG", string.Empty, false);
                    _ownSettings.Add(header);
                    _ownSettings.Add(new KeybindSetting(
                        id: NvgToggleKeyId,
                        label: "[NVG] Включить/выключить ночное видение",
                        suggested: KeyCode.B,
                        preventInteractionOnGUI: false,
                        allowSpectatorTrigger: false,
                        hintDescription: "Активирует или выключает прибор ночного видения. " +
                                         "Очки должны быть в инвентаре.",
                        collectionId: byte.MaxValue,
                        header: header,
                        onChanged: (player, setting) =>
                        {
                            if (setting is KeybindSetting kb && kb.IsPressed)
                                ToggleNvg(player);
                        }));
                    FermixInput.DropExistingByIds(new[] { NvgHeaderId, NvgToggleKeyId });
                    SettingBase.Register(_ownSettings);
                }
                else
                {
                    FermixLog.Warn("FermixNvg: FermixInput не инициализирован, SSS-бинд NVG не зарегистрирован.");
                }
            }
            catch (Exception ex)
            {
                FermixLog.Error($"FermixNvg: не удалось зарегистрировать SSS keybind: {ex.Message}");
                _ownSettings.Clear();
            }

            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerLeave -= OnPlayerLeave;
            FermixEvents.OnRoleChange -= OnRoleChange;
            Exiled.Events.Handlers.Scp1344.ChangingStatus -= OnChangingStatus;
            Exiled.Events.Handlers.Player.ItemRemoved -= OnItemRemoved;
            Exiled.Events.Handlers.Player.Died -= OnPlayerDied;

            FermixGlow.RemoveGlow(GlowId);
            DisableForAll();
            lock (_lock) _nvgSerials.Clear();

            try
            {
                if (_ownSettings.Count > 0) SettingBase.Unregister(settings: _ownSettings);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.Shutdown unregister: {ex.Message}");
            }
            _ownSettings.Clear();

            _initialized = false;
        }

        // ── публичный API ───────────────────────────────────────────

        public static bool IsNvgSerial(ushort serial)
        {
            lock (_lock) return _nvgSerials.Contains(serial);
        }

        public static bool GiveTo(Player p)
        {
            if (p == null || !p.IsConnected) return false;
            try
            {
                var item = p.AddItem(ItemType.SCP1344);
                if (item == null) return false;
                lock (_lock) _nvgSerials.Add(item.Serial);
                FermixHint.SendColored(p,
                    $"<size=110%><b><color=#33ff66>Прибор ночного видения</color></b></size>\n" +
                    "Чтобы активировать — нажмите бинд из меню SSS «FermixAPI: NVG»\n" +
                    "(по умолчанию <b>B</b>). Надевание физических очков отключено.",
                    "#33ff66", 5f);
                return true;
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.GiveTo: {ex.Message}");
                return false;
            }
        }

        public static bool SpawnAt(Vector3 pos)
        {
            try
            {
                var pickup = Pickup.CreateAndSpawn(ItemType.SCP1344, pos);
                if (pickup == null) return false;
                lock (_lock) _nvgSerials.Add(pickup.Serial);
                return true;
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.SpawnAt: {ex.Message}");
                return false;
            }
        }

        // ── round lifecycle ─────────────────────────────────────────

        private static void OnRoundStart()
        {
            int count = Mathf.Clamp(FermixCore.Config?.NvgSpawnCount ?? 2, 0, 8);
            FermixScheduler.Delay(FermixCore.Config?.NvgSpawnDelay ?? 5f, () => SpawnItems(count));
        }

        private static void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs _)
        {
            DisableForAll();
            lock (_lock) _nvgSerials.Clear();
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
                FermixLog.Warn("FermixNvg: ни одной комнаты из пула — спавн пропущен.");
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
                              + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f,
                                            UnityEngine.Random.Range(-1.5f, 1.5f));
                SpawnAt(pos);
            }
        }

        // ── core: SCP-1344 status hook ──────────────────────────────

        // Отменяем ванильное «надевание» очков для NVG-помеченных предметов.
        // Игрок физически не сможет активировать SCP-1344 как очки — только
        // через наш SSS-бинд (см. ToggleNvg). Обычные SCP-1344, которые не
        // помечены как NVG, продолжают работать как ваниль.
        private static void OnChangingStatus(ChangingStatusEventArgs ev)
        {
            if (ev?.Player == null || ev.Scp1344 == null) return;
            if (!IsNvgSerial(ev.Scp1344.Serial)) return;

            // Отмена только для перехода в Activating/Active (попытка
            // надеть). Завершающие переходы (Idle/Dropping) не блокируем,
            // чтобы не залипало внутреннее state.
            if (ev.Scp1344StatusNew == Scp1344Status.Activating
                || ev.Scp1344StatusNew == Scp1344Status.Active)
            {
                ev.IsAllowed = false;
            }
        }

        // ── активация по биндy ──────────────────────────────────────

        private static void ToggleNvg(Player p)
        {
            if (p == null || !p.IsConnected || !p.IsAlive) return;
            if (p.UserId == null) return;

            // Должен быть NVG в инвентаре (любой ItemType.SCP1344 с нашим serial).
            bool hasNvg = false;
            foreach (var it in p.Items)
            {
                if (it == null || it.Type != ItemType.SCP1344) continue;
                if (!IsNvgSerial(it.Serial)) continue;
                hasNvg = true;
                break;
            }

            if (!hasNvg)
            {
                FermixHint.Send(p,
                    "<color=#ff8b8b>В инвентаре нет прибора ночного видения.</color>",
                    2.5f);
                return;
            }

            string id = p.UserId;
            bool nowActive;
            lock (_activeUsers) nowActive = !_activeUsers.Contains(id);

            if (nowActive)
                EnableNvg(p);
            else
                DisableNvg(p);
        }

        private static void EnableNvg(Player p)
        {
            if (p?.UserId == null) return;
            try
            {
                p.EnableEffect(EffectType.NightVision,
                    intensity: (byte)Mathf.Clamp(FermixCore.Config?.NvgEffectIntensity ?? 1, 1, 255));

                if (FermixCore.Config?.NvgRemove1344Effect == true)
                    p.DisableEffect(EffectType.Scp1344);

                lock (_activeUsers) _activeUsers.Add(p.UserId);

                FermixHint.SendColored(p,
                    "<color=#33ff66><b>NVG активирован</b></color>\n" +
                    "Зрение усилено. Окружающие никакого свечения не видят.",
                    "#33ff66", 2.5f);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.EnableNvg: {ex.Message}");
            }
        }

        private static void DisableNvg(Player p)
        {
            if (p?.UserId == null) return;
            try
            {
                p.DisableEffect(EffectType.NightVision);
                lock (_activeUsers) _activeUsers.Remove(p.UserId);

                FermixHint.SendColored(p,
                    "<color=#aaaaaa>NVG выключен.</color>",
                    "#aaaaaa", 2f);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.DisableNvg: {ex.Message}");
            }
        }

        private static void DisableForAll()
        {
            string[] users;
            lock (_activeUsers) users = _activeUsers.ToArray();
            foreach (var id in users)
            {
                var p = Player.Get(id);
                if (p != null) DisableNvg(p);
                else lock (_activeUsers) _activeUsers.Remove(id);
            }
        }

        // ── housekeeping ────────────────────────────────────────────

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_activeUsers) _activeUsers.Remove(ev.Player.UserId);
        }

        private static void OnRoleChange(ChangingRoleEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            // При смене роли гасим NVG (новая роль может быть SCP / спектатор).
            DisableNvg(ev.Player);
        }

        // Когда игрок выбрасывает / отдаёт / иначе теряет предмет — если
        // удалённый предмет был NVG-помеченным SCP-1344 и в инвентаре больше
        // нет ни одного NVG-предмета, гасим эффект NightVision.
        private static void OnItemRemoved(Exiled.Events.EventArgs.Player.ItemRemovedEventArgs ev)
        {
            var p = ev?.Player;
            if (p?.UserId == null) return;
            if (ev.Item == null || ev.Item.Type != ItemType.SCP1344) return;
            if (!IsNvgSerial(ev.Item.Serial)) return;

            // Проверяем: остался ли в инвентаре ещё хоть один NVG-предмет.
            // Если нет — снимаем эффект.
            bool stillHasNvg = false;
            foreach (var it in p.Items)
            {
                if (it == null || it.Type != ItemType.SCP1344) continue;
                if (it.Serial == ev.Item.Serial) continue;
                if (!IsNvgSerial(it.Serial)) continue;
                stillHasNvg = true;
                break;
            }

            if (!stillHasNvg)
            {
                bool wasActive;
                lock (_activeUsers) wasActive = _activeUsers.Contains(p.UserId);
                if (wasActive) DisableNvg(p);
            }
        }

        private static void OnPlayerDied(Exiled.Events.EventArgs.Player.DiedEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            DisableNvg(ev.Player);
        }
    }
}
