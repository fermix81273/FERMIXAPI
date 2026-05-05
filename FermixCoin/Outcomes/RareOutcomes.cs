using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using FermixAPI.Core;
using FermixAPI.Systems;
using PlayerRoles;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория I: редкие исходы (без I2 — мгновенной смерти).</summary>
    public static class RareOutcomes
    {
        private static readonly ItemType[] CharityItemPool =
        {
            ItemType.Medkit,
            ItemType.Adrenaline,
            ItemType.GunCOM18,
            ItemType.GrenadeFlash,
            ItemType.SCP500,
            ItemType.ArmorLight,
            ItemType.Painkillers,
        };

        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "I1",
                name: "Джекпот",
                rarity: Rarity.Legendary,
                message: "★ ДЖЕКПОТ! ★ SCP-предмет + полный HP + 30 сек скорости.",
                comment: "Сегодня твой день. Используй это с умом.",
                action: p =>
                {
                    var scpPool = new[]
                    {
                        ItemType.SCP500, ItemType.SCP207, ItemType.SCP268,
                        ItemType.SCP1853, ItemType.SCP018, ItemType.SCP2176,
                    };
                    p.AddItem(scpPool[UnityEngine.Random.Range(0, scpPool.Length)]);
                    p.Health = p.MaxHealth;
                    p.EnableEffect(EffectType.MovementBoost, 20, 30f);
                    p.EnableEffect(EffectType.Vitality, 1, 30f);
                }));

            sink.Add(new Outcome(
                id: "I3",
                name: "Воскрешение ближайшего мёртвого тиммейта",
                rarity: Rarity.Legendary,
                message: "Ближайший мёртвый союзник воскрешён рядом с тобой!",
                comment: "Чудо! Обними его. Или нет, ему ещё жарко после возрождения.",
                action: p =>
                {
                    var mySide = p.Role.Type.GetSide();
                    if (mySide == Side.None || mySide == Side.Scp)
                        return;

                    var dead = Player.List
                        .Where(x => x != p && !x.IsAlive && x.IsConnected)
                        .ToList();

                    if (dead.Count == 0)
                        return;

                    var target = dead[UnityEngine.Random.Range(0, dead.Count)];

                    var resurrectAs = mySide == Side.Mtf
                        ? RoleTypeId.NtfPrivate
                        : mySide == Side.ChaosInsurgency
                            ? RoleTypeId.ChaosConscript
                            : RoleTypeId.ClassD;

                    target.Role.Set(resurrectAs, SpawnReason.Respawn);
                    target.Teleport(p);
                    target.Health = target.MaxHealth;
                }));

            sink.Add(new Outcome(
                id: "I4",
                name: "Благотворительность",
                rarity: Rarity.Uncommon,
                message: "Случайный игрок получил предмет в подарок от тебя!",
                comment: "Карма +1. Может быть.",
                action: p =>
                {
                    var others = Player.List.Where(x => x != p && x.IsAlive && x.IsConnected).ToList();
                    if (others.Count == 0)
                        return;
                    var target = others[UnityEngine.Random.Range(0, others.Count)];
                    var item = CharityItemPool[UnityEngine.Random.Range(0, CharityItemPool.Length)];
                    target.AddItem(item);
                    FermixHint.SendColored(target, $"<color=#FFD700>{p.Nickname} прислал тебе подарочек!</color>", "#FFD700", 4f);
                }));

            sink.Add(new Outcome(
                id: "I5",
                name: "Тревога Alpha Warhead на 30 сек",
                rarity: Rarity.Legendary,
                message: "Ты нажал на кнопку. Тревога объявлена.",
                comment: "Через 30 секунд я её отменю. Если ты не передумаешь.",
                action: p =>
                {
                    if (Warhead.IsInProgress)
                        return;

                    Warhead.Start();

                    FermixScheduler.Delay(30f, () =>
                    {
                        if (!Warhead.IsInProgress)
                            return;
                        if (Warhead.IsDetonated)
                            return;
                        Warhead.Stop();
                        FermixServer.GlobalBroadcast(
                            "<color=#5BCB76>Тревога Alpha Warhead отменена... случайным образом.</color>",
                            duration: 6);
                    });
                }));
        }
    }
}
