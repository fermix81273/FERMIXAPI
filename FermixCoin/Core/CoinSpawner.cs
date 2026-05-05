using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using FermixAPI.Core;
using UnityEngine;

namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Автоспавн монеток в случайных комнатах комплекса после старта раунда.
    /// Подписывается на <see cref="FermixEvents.OnRoundStart"/>, ждёт настроенную
    /// задержку и раскидывает <see cref="ItemType.Coin"/> по полу разных комнат.
    /// </summary>
    public static class CoinSpawner
    {
        private const float FloorOffset = 1.0f;

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            if (!FermixCore.Config.CoinEnabled || !FermixCore.Config.CoinAutoSpawnEnabled) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixEvents.OnRoundStart -= OnRoundStart;
            _initialized = false;
        }

        private static void OnRoundStart()
        {
            var delay = Mathf.Max(0f, FermixCore.Config.CoinAutoSpawnDelay);
            FermixScheduler.Delay(delay, SpawnCoins);
        }

        private static void SpawnCoins()
        {
            int requested = FermixCore.Config.CoinAutoSpawnCount;
            if (requested <= 0) return;

            var rooms = Room.List
                .Where(IsSpawnableRoom)
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(requested)
                .ToList();

            if (rooms.Count == 0)
            {
                FermixLog.Warn("FermixCoin: автоспавн монеток — не нашёл подходящих комнат.");
                return;
            }

            int spawned = 0;
            foreach (var room in rooms)
            {
                try
                {
                    var pos = room.Position + Vector3.up * FloorOffset;
                    var pickup = Item.Create(ItemType.Coin).CreatePickup(pos);
                    if (pickup != null)
                        spawned++;
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixCoin: не удалось заспавнить монетку в {room.Type}: {ex.Message}");
                }
            }

            FermixLog.Info($"FermixCoin: автоспавн монеток — разложено {spawned}/{requested}.");
        }

        private static bool IsSpawnableRoom(Room room)
        {
            if (room == null) return false;
            if (room.Type == RoomType.Unknown) return false;
            if (room.Type == RoomType.Pocket) return false;
            if (room.Zone == ZoneType.Pocket) return false;
            if (room.Zone == ZoneType.Unspecified) return false;
            return true;
        }
    }
}
