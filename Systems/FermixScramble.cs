using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using Exiled.Events.EventArgs.Scp1344;
using FermixAPI.Core;
using InventorySystem.Items.Usables.Scp1344;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.Wearables;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// SCP-1344 (Heart of Fortune) — «скрамблер» взгляда SCP-096.
    /// • спавнятся в РАЗНЫЕ комнаты (без дублей в одной точке);
    /// • вся ванильная механика очков ОСТАВЛЕНА: активация
    ///   через игровой бинд использования предмета, заряд, иммунитет к 096
    ///   в состоянии Active, ванильные хинты и т. д.;
    /// • ЕДИНСТВЕННОЕ отличие от ванильной игры: «очки на лице» не
    ///   отображаются ни в первом, ни в третьем лице — мы гасим
    ///   <see cref="WearableElements.Scp1344Goggles"/> всегда, поэтому
    ///   игрок «пользуется» эффектом без визуального надевания.
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

        private static readonly object _lock = new();
        private static readonly HashSet<ushort> _ourSerials = new();

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.ScrambleEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerSpawned += OnPlayerSpawned;
            FermixEvents.OnItemPickup += OnItemPickup;
            // ChangingStatus/AddingTarget НЕ подписываемся — ванильная механика SCP-1344
            // работает как есть (активация, заряд, иммунитет к 096 в Active).
            // Реагируем только на изменение статуса, чтобы скрыть очки на лице.
            Exiled.Events.Handlers.Scp1344.ChangedStatus += OnChangedStatus;

            FermixGlow.AddGlow(GlowId,
                serial => { lock (_lock) return _ourSerials.Contains(serial); },
                new Color(1f, 0.4f, 0.85f),
                intensity: 1.4f,
                range: 3.5f,
                pulseEffect: true,
                pulseSpeed: 1.2f);

            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerSpawned -= OnPlayerSpawned;
            FermixEvents.OnItemPickup -= OnItemPickup;
            Exiled.Events.Handlers.Scp1344.ChangedStatus -= OnChangedStatus;

            FermixGlow.RemoveGlow(GlowId);

            lock (_lock) _ourSerials.Clear();

            _initialized = false;
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

        // ── скрытие визуальной модели «очки на лице» ─────────────────
        //
        // Ванильный SCP-1344 при Activating/Active/Stabbing/Dropping включает
        // на игроке wearable «Scp1344Goggles» (в третьем лице — модель очков
        // на лбу/глазах). Мы хотим, чтобы СНАРУЖИ игрока никто никогда не
        // видел эту модель: предмет «работает только через бинд», без
        // визуального надевания. Для этого мы:
        //   1) на спавн игрока — гасим wearable (на случай старого state'а);
        //   2) при подборе предмета — гасим wearable (через тик);
        //   3) после каждого изменения статуса 1344 — ещё раз гасим wearable.
        // Сам статус, заряд и иммунитет к 096 в Active НЕ трогаем — это
        // ванильная механика и она нужна.

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
            // На следующий тик — после того как предмет уехал в инвентарь —
            // сбрасываем визуальный wearable.
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

        private static void OnChangedStatus(ChangedStatusEventArgs ev)
        {
            if (ev?.Player == null) return;

            // НЕ форсим Idle — пусть ванильный 1344 идёт по своему циклу
            // (Activating → Active → Dropping → Idle), отрабатывает заряд и
            // даёт иммунитет к 096 в Active. Просто прячем визуальную модель.
            //
            // На всякий случай гасим ещё раз через тик: в момент ChangedStatus
            // wearable может быть выставлен ПОСЛЕ нашего вызова в той же
            // фрейме — задержанный сброс гарантирует что снаружи модели нет.
            HideGoggles(ev.Player);
            var captured = ev.Player;
            FermixScheduler.Delay(0.05f, () => HideGoggles(captured));
        }
    }
}
