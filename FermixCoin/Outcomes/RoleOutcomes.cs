using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using FermixAPI.Core;
using PlayerRoles;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория D: смены ролей. Превращения в MTF/Chaos — редкие.</summary>
    public static class RoleOutcomes
    {
        private static readonly RoleTypeId[] MtfRoles =
        {
            RoleTypeId.NtfPrivate,
            RoleTypeId.NtfSergeant,
            RoleTypeId.NtfSpecialist,
            RoleTypeId.NtfCaptain,
        };

        private static readonly RoleTypeId[] ChaosRoles =
        {
            RoleTypeId.ChaosConscript,
            RoleTypeId.ChaosRifleman,
            RoleTypeId.ChaosRepressor,
            RoleTypeId.ChaosMarauder,
        };

        private static readonly RoleTypeId[] RandomRolePool =
        {
            RoleTypeId.ClassD,
            RoleTypeId.Scientist,
            RoleTypeId.FacilityGuard,
            RoleTypeId.NtfPrivate,
            RoleTypeId.NtfSergeant,
            RoleTypeId.NtfSpecialist,
            RoleTypeId.NtfCaptain,
            RoleTypeId.ChaosConscript,
            RoleTypeId.ChaosRifleman,
            RoleTypeId.ChaosRepressor,
            RoleTypeId.ChaosMarauder,
            RoleTypeId.Tutorial,
            RoleTypeId.Scp0492,
        };

        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "D1",
                name: "Превращение в зомби",
                rarity: Rarity.Epic,
                message: "Ты теперь зомби (SCP-049-2)!",
                comment: "Догоняй живых. Они узнают тебя по запаху.",
                action: p =>
                {
                    p.Role.Set(RoleTypeId.Scp0492, SpawnReason.ForceClass);
                }));

            sink.Add(new Outcome(
                id: "D2",
                name: "Туториал на 60 секунд (с инвентарём!)",
                rarity: Rarity.Epic,
                message: "Ты в туториале на 60 секунд. Инвентарь — на месте.",
                comment: "Пушки есть, тапки розовые — иди наводи хаос.",
                action: p =>
                {
                    var oldRole = p.Role.Type;
                    var oldHealth = p.Health;

                    p.Role.Set(RoleTypeId.Tutorial, RoleSpawnFlags.None);
                    p.Health = oldHealth;

                    FermixScheduler.Delay(60f, () =>
                    {
                        if (p == null || !p.IsConnected || !p.IsAlive)
                            return;
                        if (p.Role.Type != RoleTypeId.Tutorial)
                            return;
                        p.Role.Set(oldRole, RoleSpawnFlags.None);
                        p.Health = oldHealth;
                        FermixHint.SendColored(p, "Возвращаемся в реальность...", FermixHint.Cyan, 4f);
                    });
                }));

            sink.Add(new Outcome(
                id: "D3",
                name: "Превращение в случайного MTF",
                rarity: Rarity.Legendary,
                message: "Тебя завербовала Mobile Task Force.",
                comment: "Поздравляю с повышением. Не обкакайся.",
                weightMultiplier: 0.5f,
                action: p =>
                {
                    var role = MtfRoles[UnityEngine.Random.Range(0, MtfRoles.Length)];
                    p.Role.Set(role, SpawnReason.ForceClass);
                }));

            sink.Add(new Outcome(
                id: "D4",
                name: "Превращение в случайного Chaos",
                rarity: Rarity.Legendary,
                message: "Хаос принял тебя в свои ряды!",
                comment: "Бороду не дают, но патроны есть.",
                weightMultiplier: 0.5f,
                action: p =>
                {
                    var role = ChaosRoles[UnityEngine.Random.Range(0, ChaosRoles.Length)];
                    p.Role.Set(role, SpawnReason.ForceClass);
                }));

            sink.Add(new Outcome(
                id: "D5",
                name: "Faction swap: MTF↔Chaos",
                rarity: Rarity.Epic,
                message: "Ты только что переметнулся!",
                comment: "Бывшие тиммейты теперь твои враги. Удачи.",
                action: p =>
                {
                    var current = p.Role.Type;
                    RoleTypeId target;

                    if (current.GetSide() == Side.Mtf)
                        target = ChaosRoles[UnityEngine.Random.Range(0, ChaosRoles.Length)];
                    else if (current.GetSide() == Side.ChaosInsurgency)
                        target = MtfRoles[UnityEngine.Random.Range(0, MtfRoles.Length)];
                    else
                    {
                        var pool = UnityEngine.Random.value < 0.5f ? MtfRoles : ChaosRoles;
                        target = pool[UnityEngine.Random.Range(0, pool.Length)];
                    }

                    var hp = p.Health;
                    p.Role.Set(target, SpawnReason.ForceClass);
                    p.Health = hp;
                }));

            sink.Add(new Outcome(
                id: "D6",
                name: "Случайная роль",
                rarity: Rarity.Epic,
                message: "Тебя забросило в случайную роль!",
                comment: "Лотерея жизни. Зомби, учёный, MTF... да хоть туториал.",
                action: p =>
                {
                    var role = RandomRolePool[UnityEngine.Random.Range(0, RandomRolePool.Length)];
                    p.Role.Set(role, SpawnReason.ForceClass);
                }));
        }
    }
}
