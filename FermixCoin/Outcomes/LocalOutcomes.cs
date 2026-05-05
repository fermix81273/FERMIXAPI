using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using FermixAPI.Systems;
using UnityEngine;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория F: локальные эффекты вокруг игрока.</summary>
    public static class LocalOutcomes
    {
        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "F1",
                name: "Blackout в комнате на 10 секунд",
                rarity: Rarity.Uncommon,
                message: "В твоей комнате погас свет.",
                comment: "Свет вернётся через 10 секунд. Если доживёшь.",
                action: p =>
                {
                    var room = p.CurrentRoom;
                    room?.TurnOffLights(10f);
                }));

            sink.Add(new Outcome(
                id: "F2",
                name: "Ближайшая дверь залочена на 15 сек",
                rarity: Rarity.Uncommon,
                message: "Ближайшая дверь залочилась.",
                comment: "Никто не пройдёт. И ты тоже.",
                action: p =>
                {
                    var nearest = FermixDoors.GetNearest(p);
                    nearest?.LockFor(15f, DoorLockType.AdminCommand);
                }));

            sink.Add(new Outcome(
                id: "F3",
                name: "Все двери в комнате открыты",
                rarity: Rarity.Common,
                message: "Все двери в твоей комнате открыты.",
                comment: "Сквозняк. Очень удобно для вентиляции.",
                action: p =>
                {
                    var room = p.CurrentRoom;
                    if (room == null)
                        return;
                    foreach (var door in Door.List.Where(d => d.Rooms != null && d.Rooms.Contains(room)))
                    {
                        door.IsOpen = true;
                    }
                }));

            sink.Add(new Outcome(
                id: "F5",
                name: "Аптечка/адреналин у ног",
                rarity: Rarity.Common,
                message: "У твоих ног появились медикаменты.",
                comment: "Поделись с командой. Или схомячь сам, я не сужу.",
                action: p =>
                {
                    var pool = new[] { ItemType.Medkit, ItemType.Adrenaline, ItemType.Painkillers };
                    var t = pool[UnityEngine.Random.Range(0, pool.Length)];
                    Pickup.CreateAndSpawn(t, p.Position, Quaternion.identity);
                }));
        }
    }
}
