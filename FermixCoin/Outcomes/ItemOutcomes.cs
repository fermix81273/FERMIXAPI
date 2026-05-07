using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория A: «обычные» — предмет / эффект / лечение / SCP-предмет.</summary>
    public static class ItemOutcomes
    {
        private static readonly ItemType[] BasicItemPool =
        {
            ItemType.Adrenaline,
            ItemType.Medkit,
            ItemType.Painkillers,
            ItemType.GunCOM15,
            ItemType.GunCOM18,
            ItemType.Flashlight,
            ItemType.Radio,
            ItemType.ArmorLight,
            ItemType.GrenadeFlash,
            ItemType.Ammo9x19,
        };

        // Отдельный пул «пушек» для исхода A5 — выпадает чуть чаще обычной
        // монетки, чтобы оружие падало регулярно но не доминировало в раунде.
        private static readonly ItemType[] WeaponPool =
        {
            ItemType.GunCOM15,
            ItemType.GunCOM18,
            ItemType.GunFSP9,
            ItemType.GunCrossvec,
            ItemType.GunRevolver,
            ItemType.GunE11SR,
        };

        private static readonly (EffectType type, float duration, byte intensity, string label)[] BasicEffectPool =
        {
            (EffectType.MovementBoost, 15f, 10, "ускорение"),
            (EffectType.DamageReduction, 15f, 5, "броня х5"),
            (EffectType.BodyshotReduction, 15f, 5, "урон в тело срезан"),
            (EffectType.Vitality, 10f, 1, "регенерация"),
            (EffectType.Invigorated, 10f, 1, "выносливость"),
            (EffectType.Scp207, 8f, 1, "SCP-207 в крови"),
            (EffectType.Bleeding, 12f, 1, "кровотечение"),
            (EffectType.Burned, 8f, 1, "ожог"),
            (EffectType.Concussed, 6f, 1, "контузия"),
            (EffectType.Hemorrhage, 8f, 1, "хемор"),
            (EffectType.Poisoned, 6f, 1, "отравление"),
            (EffectType.Exhausted, 8f, 1, "усталость"),
            (EffectType.RainbowTaste, 12f, 1, "радужный вкус"),
        };

        private static readonly ItemType[] ScpItemPool =
        {
            ItemType.SCP500,
            ItemType.SCP207,
            ItemType.SCP268,
            ItemType.SCP1853,
            ItemType.SCP018,
            ItemType.SCP330,
            ItemType.SCP2176,
            ItemType.SCP1576,
        };

        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "A1",
                name: "Случайный предмет",
                rarity: Rarity.Common,
                message: "Тебе выпал случайный предмет!",
                comment: "Монетка щедра... но не очень разборчива.",
                action: p =>
                {
                    var t = BasicItemPool[UnityEngine.Random.Range(0, BasicItemPool.Length)];
                    p.AddItem(t);
                }));

            sink.Add(new Outcome(
                id: "A2",
                name: "Случайный эффект",
                rarity: Rarity.Common,
                message: "На тебе сработал эффект...",
                comment: "Эффекты — как погода: прогноз неточный.",
                action: p =>
                {
                    var (type, dur, intensity, label) = BasicEffectPool[UnityEngine.Random.Range(0, BasicEffectPool.Length)];
                    p.EnableEffect(type, intensity, dur);
                    Hint(p, $"эффект: {label} ({dur:0}с)");
                }));

            sink.Add(new Outcome(
                id: "A3",
                name: "Полное лечение",
                rarity: Rarity.Common,
                message: "Хилл! Полное здоровье восстановлено.",
                comment: "Доктор Брайт прислал привет.",
                action: p =>
                {
                    p.Health = p.MaxHealth;
                    p.EnableEffect(EffectType.Vitality, 1, 5f);
                }));

            sink.Add(new Outcome(
                id: "A4",
                name: "SCP-предмет",
                rarity: Rarity.Uncommon,
                message: "Тебе выпал SCP-предмет.",
                comment: "Возьми и не благодари. Или благодари, мне всё равно.",
                action: p =>
                {
                    var t = ScpItemPool[UnityEngine.Random.Range(0, ScpItemPool.Length)];
                    p.AddItem(t);
                }));

            // A5 — гарантированная пушка из расширенного пула.
            // Rarity.Common (50) + WeightMultiplier 1.4 → итоговый вес ~70.
            // Это чуть выше «случайного предмета» (50) и заметно выше
            // «случайного эффекта» (50), чтобы оружие падало регулярно.
            sink.Add(new Outcome(
                id: "A5",
                name: "Пушка",
                rarity: Rarity.Common,
                message: "Монетка выкатила тебе оружие!",
                comment: "Не пали по своим. По чужим — на здоровье.",
                action: p =>
                {
                    var t = WeaponPool[UnityEngine.Random.Range(0, WeaponPool.Length)];
                    p.AddItem(t);
                },
                weightMultiplier: 1.4f));
        }

        private static void Hint(Player p, string text)
        {
            FermixHint.SendColored(p, $"<size=80%><color=#aaaaaa>{text}</color></size>", "#aaaaaa", 4f);
        }
    }
}
