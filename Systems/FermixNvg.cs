using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp1344;
using FermixAPI.Core;
using InventorySystem.Items.Usables.Scp1344;
using MEC;
using Mirror;
using UnityEngine;
using ToyLight = Exiled.API.Features.Toys.Light;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Прибор ночного видения (NVG) — порт MS-crew/NightVisionGoggles (1.3.0)
    /// под FermixAPI без зависимости от Exiled.CustomItems.
    ///
    /// Базовый предмет — SCP-1344, но с особым serial-тегом. При активации:
    /// • применяет эффект <c>EffectType.NightVision</c>;
    /// • снимает «слепящий» эффект SCP-1344 (Remove1344Effect — конфигурится);
    /// • создаёт зелёный <see cref="ToyLight"/>-прожектор, прикреплённый к
    ///   камере игрока (через <see cref="MEC.Timing.RunCoroutine"/>);
    /// • прячет прожектор от всех, кроме носителя и его зрителей.
    /// При снятии всё гасится в обратном порядке.
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

        private static readonly object _lock = new();
        private static readonly HashSet<ushort> _nvgSerials = new();

        // userId → light/коrutine
        private static readonly Dictionary<string, ToyLight> _lights = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, CoroutineHandle> _trackHandles = new(StringComparer.Ordinal);

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.NvgEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnRoleChange += OnRoleChange;
            Exiled.Events.Handlers.Scp1344.ChangedStatus += OnChangedStatus;

            FermixGlow.AddGlow(GlowId,
                serial => { lock (_lock) return _nvgSerials.Contains(serial); },
                new Color(0.2f, 1f, 0.4f),
                intensity: 1.2f,
                range: 3f,
                pulseEffect: true,
                pulseSpeed: 1.0f);

            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerLeave -= OnPlayerLeave;
            FermixEvents.OnRoleChange -= OnRoleChange;
            Exiled.Events.Handlers.Scp1344.ChangedStatus -= OnChangedStatus;

            FermixGlow.RemoveGlow(GlowId);
            DestroyAllLights();
            lock (_lock) _nvgSerials.Clear();

            _initialized = false;
        }

        // ── публичный API ───────────────────────────────────────────

        /// <summary>
        /// Проверить, является ли SCP-1344 с данным serial — нашим NVG.
        /// </summary>
        public static bool IsNvgSerial(ushort serial)
        {
            lock (_lock) return _nvgSerials.Contains(serial);
        }

        /// <summary>
        /// Создать NVG-предмет в инвентаре игрока (для команды <c>.fermix give</c>).
        /// </summary>
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
                    "Используйте предмет, чтобы активировать ночное видение.",
                    "#33ff66", 4f);
                return true;
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.GiveTo: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Заспавнить NVG-предмет в указанной точке.
        /// </summary>
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
            DestroyAllLights();
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

        private static void OnChangedStatus(ChangedStatusEventArgs ev)
        {
            if (ev?.Player == null || ev.Scp1344 == null) return;
            if (!IsNvgSerial(ev.Scp1344.Serial)) return;

            switch (ev.Scp1344Status)
            {
                case Scp1344Status.Active:
                    EquipNvg(ev.Player);
                    break;
                case Scp1344Status.Idle:
                    UnequipNvg(ev.Player);
                    break;
            }
        }

        private static void EquipNvg(Player p)
        {
            if (p?.UserId == null) return;
            string id = p.UserId;

            try
            {
                p.EnableEffect(EffectType.NightVision,
                    intensity: (byte)Mathf.Clamp(FermixCore.Config?.NvgEffectIntensity ?? 1, 1, 255));

                if (FermixCore.Config?.NvgRemove1344Effect == true)
                    p.DisableEffect(EffectType.Scp1344);

                // Снимаем старый свет если был.
                DestroyLightFor(id);

                Vector3 camPos = p.CameraTransform != null ? p.CameraTransform.position : p.Position;
                Vector3 camRot = p.CameraTransform != null ? p.CameraTransform.eulerAngles : p.Rotation.eulerAngles;

                var light = ToyLight.Create(camPos, camRot, null, spawn: true,
                    color: new Color(0f, 1f, 0f, 1f));

                light.Range = FermixCore.Config?.NvgLightRange ?? 50f;
                light.Intensity = FermixCore.Config?.NvgLightIntensity ?? 4f;
                light.SpotAngle = FermixCore.Config?.NvgLightSpotAngle ?? 90f;
                light.InnerSpotAngle = FermixCore.Config?.NvgLightInnerAngle ?? 0f;
                light.LightType = UnityEngine.LightType.Spot;
                light.ShadowType = LightShadows.None;

                if (p.Transform != null)
                    light.Transform.SetParent(p.Transform, worldPositionStays: true);

                lock (_lights) _lights[id] = light;

                // Прячем прожектор от всех, кроме самого носителя и его зрителей.
                HideLightFromOthers(p, light);

                if (FermixCore.Config?.NvgTrackCamera == true)
                {
                    var handle = Timing.RunCoroutine(TrackCameraRotation(id));
                    lock (_trackHandles) _trackHandles[id] = handle;
                }
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.EquipNvg: {ex.Message}");
            }
        }

        private static void UnequipNvg(Player p)
        {
            if (p?.UserId == null) return;
            string id = p.UserId;

            try
            {
                p.DisableEffect(EffectType.NightVision);
                StopTrackingFor(id);
                DestroyLightFor(id);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixNvg.UnequipNvg: {ex.Message}");
            }
        }

        private static void HideLightFromOthers(Player wearer, ToyLight light)
        {
            if (light?.Base == null) return;
            var netId = light.Base.netIdentity;
            if (netId == null) return;

            foreach (var ply in Player.List)
            {
                if (ply == null || ply == wearer || !ply.IsConnected) continue;
                // Зрители носителя видят свет — не прячем от них.
                if (wearer.CurrentSpectatingPlayers != null
                    && wearer.CurrentSpectatingPlayers.Contains(ply)) continue;

                try
                {
                    ply.Connection?.Send(new ObjectHideMessage { netId = netId.netId });
                }
                catch { /* ignore — клиент может уже быть отключен */ }
            }
        }

        private static IEnumerator<float> TrackCameraRotation(string userId)
        {
            float interval = Mathf.Max(0.02f, FermixCore.Config?.NvgTrackInterval ?? 0.1f);
            while (true)
            {
                yield return Timing.WaitForSeconds(interval);
                ToyLight light;
                lock (_lights) _lights.TryGetValue(userId, out light);
                if (light == null) yield break;

                var p = Player.Get(userId);
                if (p == null || !p.IsConnected || !p.IsAlive) yield break;
                if (p.CameraTransform == null) continue;

                float pitch = p.CameraTransform.localRotation.eulerAngles.x;
                Quaternion target = Quaternion.AngleAxis(pitch, Vector3.right);
                if (light.Transform.localRotation != target)
                    light.Transform.localRotation = target;
            }
        }

        // ── housekeeping ────────────────────────────────────────────

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            UnequipNvg(ev.Player);
        }

        private static void OnRoleChange(ChangingRoleEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            UnequipNvg(ev.Player);
        }

        private static void StopTrackingFor(string userId)
        {
            lock (_trackHandles)
            {
                if (_trackHandles.TryGetValue(userId, out var h) && h.IsRunning)
                    Timing.KillCoroutines(h);
                _trackHandles.Remove(userId);
            }
        }

        private static void DestroyLightFor(string userId)
        {
            ToyLight light;
            lock (_lights)
            {
                if (!_lights.TryGetValue(userId, out light)) return;
                _lights.Remove(userId);
            }
            try
            {
                if (light?.GameObject != null) NetworkServer.Destroy(light.GameObject);
            }
            catch { /* ignore */ }
        }

        private static void DestroyAllLights()
        {
            string[] ids;
            lock (_lights) ids = _lights.Keys.ToArray();
            foreach (var id in ids)
            {
                StopTrackingFor(id);
                DestroyLightFor(id);
            }
        }
    }
}
