using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория E: здоровье / экипировка.</summary>
    public static class HealthOutcomes
    {
        private static readonly ItemType[] ShotgunPool =
        {
            ItemType.GunShotgun,
        };

        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "E1",
                name: "1 HP — русская рулетка",
                rarity: Rarity.Uncommon,
                message: "У тебя 1 HP. Один выстрел — и привет.",
                comment: "Можно бегать и кричать. Очень рекомендую.",
                action: p =>
                {
                    p.Health = 1f;
                }));

            sink.Add(new Outcome(
                id: "E2",
                name: "Двойной HP",
                rarity: Rarity.Uncommon,
                message: "Тебе удвоили здоровье!",
                comment: "Временный титан. Не зазнавайся.",
                action: p =>
                {
                    var newMax = p.MaxHealth * 2f;
                    p.MaxHealth = newMax;
                    p.Health = newMax;
                }));

            sink.Add(new Outcome(
                id: "E3",
                name: "Случайный шотган + патроны",
                rarity: Rarity.Rare,
                message: "Тебе выпал шотган.",
                comment: "Помповый или нет — узнаешь по звуку.",
                weightMultiplier: 0.15f,
                action: p =>
                {
                    var gun = ShotgunPool[UnityEngine.Random.Range(0, ShotgunPool.Length)];
                    p.AddItem(gun);
                    p.AddAmmo(AmmoType.Ammo12Gauge, 30);
                }));

            sink.Add(new Outcome(
                id: "E4",
                name: "Полное обнуление инвентаря",
                rarity: Rarity.Uncommon,
                message: "У тебя забрали всё кроме монетки.",
                comment: "Что? Думал, монетка — твой друг? Хах.",
                action: p =>
                {
                    var coins = p.Items.Where(i => i.Type == ItemType.Coin).ToList();
                    p.ClearInventory();
                    foreach (var coin in coins)
                        p.AddItem(coin.Type);
                }));

            sink.Add(new Outcome(
                id: "E5",
                name: "Полная аптечка",
                rarity: Rarity.Uncommon,
                message: "Аптечка джентльмена: Medkit + Adrenaline + Painkillers + SCP-500.",
                comment: "Не используй сразу. Подели на потом.",
                weightMultiplier: 0.4f,
                action: p =>
                {
                    p.AddItem(ItemType.Medkit);
                    p.AddItem(ItemType.Adrenaline);
                    p.AddItem(ItemType.Painkillers);
                    p.AddItem(ItemType.SCP500);
                }));

            sink.Add(new Outcome(
                id: "E6",
                name: "Тяжёлая броня + 2 HE",
                rarity: Rarity.Uncommon,
                message: "Тяжёлая броня и 2 HE-гранаты.",
                comment: "Танк-режим включён. Не подведи.",
                weightMultiplier: 0.15f,
                action: p =>
                {
                    p.AddItem(ItemType.ArmorHeavy);
                    p.AddItem(ItemType.GrenadeHE);
                    p.AddItem(ItemType.GrenadeHE);
                }));

            sink.Add(new Outcome(
                id: "E7",
                name: "Дубль текущего предмета",
                rarity: Rarity.Uncommon,
                message: "Твой текущий предмет — продублирован!",
                comment: "Удвой удовольствие. Или удвой проблему.",
                action: p =>
                {
                    if (p.CurrentItem == null)
                    {
                        FermixHint.SendColored(p, "<i>(но у тебя ничего в руке нет, увы)</i>", "#888888", 3f);
                        return;
                    }
                    p.AddItem(p.CurrentItem.Type);
                }));
        }
    }
}
