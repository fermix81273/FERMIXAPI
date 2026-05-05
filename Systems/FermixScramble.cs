using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Scp096;
using FermixAPI.Core;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// SCP-1344 (Heart of Fortune) служит «глушителем» для триггера SCP-096:
    /// если предмет лежит в инвентаре — взгляд на лицо 096 не делает игрока
    /// его целью. Сам предмет рассыпается по нескольким взвешенным точкам
    /// HCZ/EZ при старте раунда.
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
        private static readonly HashSet<ushort> _ourSerials = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.ScrambleEnabled != true) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            Exiled.Events.Handlers.Scp096.AddingTarget += OnAddingTarget;

            FermixGlow.AddGlow(GlowId,
                serial => { lock (_ourSerials) return _ourSerials.Contains(serial); },
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
            Exiled.Events.Handlers.Scp096.AddingTarget -= OnAddingTarget;
            FermixGlow.RemoveGlow(GlowId);
            lock (_ourSerials) _ourSerials.Clear();
            _initialized = false;
        }

        private static void OnRoundStart()
        {
            int count = Mathf.Clamp(FermixCore.Config?.ScrambleSpawnCount ?? 2, 0, 8);
            FermixScheduler.Delay(FermixCore.Config?.ScrambleSpawnDelay ?? 4f, () => SpawnItems(count));
        }

        private static void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs _)
        {
            lock (_ourSerials) _ourSerials.Clear();
        }

        private static void SpawnItems(int count)
        {
            float total = SpawnPool.Sum(p => p.Weight);
            for (int i = 0; i < count; i++)
            {
                float roll = Random.value * total;
                Room room = null;
                foreach (var (rt, w) in SpawnPool)
                {
                    roll -= w;
                    if (roll <= 0f) { room = Room.Get(rt); break; }
                }
                if (room == null) continue;

                Vector3 pos = room.Position + Vector3.up * 1.0f + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f));
                Pickup pickup = Pickup.CreateAndSpawn(ItemType.SCP1344, pos);
                if (pickup == null) continue;

                lock (_ourSerials) _ourSerials.Add(pickup.Serial);
            }
        }

        private static void OnAddingTarget(AddingTargetEventArgs ev)
        {
            if (ev?.Target == null || !ev.IsLooking) return;
            if (ev.Target.Items == null) return;
            foreach (var item in ev.Target.Items)
            {
                if (item != null && item.Type == ItemType.SCP1344)
                {
                    ev.IsAllowed = false;
                    FermixHint.SendColored(ev.Target, "SCP-1344 заглушил взгляд 096", FermixHint.Magenta, 2f);
                    return;
                }
            }
        }
    }
}
