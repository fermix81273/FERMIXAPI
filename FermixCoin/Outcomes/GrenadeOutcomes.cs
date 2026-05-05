using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using FermixAPI.Core;
using UnityEngine;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория B: гранаты, бабахи и шары.</summary>
    public static class GrenadeOutcomes
    {
        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "B1",
                name: "Активированная HE-граната",
                rarity: Rarity.Rare,
                message: "ВНИМАНИЕ! Активированная HE-граната у твоих ног!",
                comment: "У тебя ровно 3 секунды, чтобы извиниться перед командой.",
                weightMultiplier: 0.15f,
                action: p =>
                {
                    var grenade = (ExplosiveGrenade)Item.Create(ItemType.GrenadeHE, p);
                    grenade.FuseTime = 3f;
                    grenade.SpawnActive(p.Position, p);
                }));

            sink.Add(new Outcome(
                id: "B2",
                name: "Активированная Flash-граната",
                rarity: Rarity.Uncommon,
                message: "Flash-граната у твоих ног!",
                comment: "Зажмурься. Сейчас будет дискотека.",
                weightMultiplier: 0.2f,
                action: p =>
                {
                    var grenade = (FlashGrenade)Item.Create(ItemType.GrenadeFlash, p);
                    grenade.FuseTime = 1.5f;
                    grenade.SpawnActive(p.Position, p);
                }));

            sink.Add(new Outcome(
                id: "B3",
                name: "SCP-018 у ног",
                rarity: Rarity.Rare,
                message: "SCP-018 покатился вокруг тебя.",
                comment: "Чем дольше летает — тем сильнее бьёт. Удачи.",
                weightMultiplier: 0.2f,
                action: p =>
                {
                    var ball = (Scp018)Item.Create(ItemType.SCP018, p);
                    ball.SpawnActive(p.Position, p);
                }));

            sink.Add(new Outcome(
                id: "B4",
                name: "SCP-2176 (лампочка) у ног",
                rarity: Rarity.Uncommon,
                message: "SCP-2176 разбилась рядом!",
                comment: "Все вокруг на пару секунд оглохли. Включая тебя.",
                weightMultiplier: 0.25f,
                action: p =>
                {
                    var lamp = (Scp2176)Item.Create(ItemType.SCP2176, p);
                    lamp.SpawnActive(p.Position, p);
                }));

            sink.Add(new Outcome(
                id: "B5",
                name: "Disruptor выстрел",
                rarity: Rarity.Epic,
                message: "Из ниоткуда пришёл Particle Disruptor!",
                comment: "Кому-то сейчас будет очень плохо. Возможно, тебе.",
                weightMultiplier: 0.15f,
                action: p =>
                {
                    var disruptor = p.AddItem(ItemType.ParticleDisruptor);
                    if (disruptor != null)
                    {
                        FermixScheduler.Delay(10f, () =>
                        {
                            if (p != null && p.IsConnected)
                                p.RemoveItem(disruptor);
                        });
                    }
                }));

            sink.Add(new Outcome(
                id: "B6",
                name: "Тесла-разряд",
                rarity: Rarity.Rare,
                message: "Тесла-гейт сработал прямо сейчас!",
                comment: "Если ты возле двери — лучше отойди, я не уверен в радиусе.",
                action: p =>
                {
                    var nearest = Exiled.API.Features.TeslaGate.List
                        .OrderBy(t => Vector3.Distance(t.Position, p.Position))
                        .FirstOrDefault();
                    nearest?.Trigger(true);
                }));
        }
    }
}
