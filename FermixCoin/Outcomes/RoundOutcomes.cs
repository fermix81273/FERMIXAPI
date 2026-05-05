using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using PlayerRoles;

namespace FermixAPI.FermixCoin.Outcomes
{
    /// <summary>Категория G: эффекты на других игроков и зону.</summary>
    public static class RoundOutcomes
    {
        public static void Register(List<Outcome> sink)
        {
            sink.Add(new Outcome(
                id: "G2",
                name: "Зона +5 HP",
                rarity: Rarity.Rare,
                message: "Все игроки в твоей зоне получили +5 HP.",
                comment: "Командный плюс. Не благодари.",
                action: p =>
                {
                    var zone = p.Zone;
                    if (zone == ZoneType.Unspecified)
                        return;

                    foreach (var other in Player.List.Where(x => x.IsAlive && x.Zone == zone))
                    {
                        other.Heal(5f);
                        FermixHint.SendColored(other, $"<color=#5BCB76>+5 HP — щедрость монетки {p.Nickname}</color>", "#5BCB76", 3f);
                    }
                }));

            sink.Add(new Outcome(
                id: "G3",
                name: "Воскрешение случайного союзника",
                rarity: Rarity.Epic,
                message: "Случайный мёртвый союзник возвращается!",
                comment: "Молись, чтоб это был не самый бестолковый.",
                action: p =>
                {
                    var mySide = p.Role.Type.GetSide();
                    if (mySide == Side.None || mySide == Side.Scp)
                        return;

                    var candidates = Player.List
                        .Where(x => x != p && !x.IsAlive && x.IsConnected)
                        .ToList();

                    if (candidates.Count == 0)
                    {
                        FermixHint.SendColored(p, "<i>(но мертвых союзников нет)</i>", "#888888", 3f);
                        return;
                    }

                    var target = candidates[UnityEngine.Random.Range(0, candidates.Count)];

                    RoleTypeId resurrectAs = mySide == Side.Mtf
                        ? RoleTypeId.NtfPrivate
                        : mySide == Side.ChaosInsurgency
                            ? RoleTypeId.ChaosConscript
                            : RoleTypeId.ClassD;

                    target.Role.Set(resurrectAs, SpawnReason.Respawn);
                    target.Teleport(p);

                    FermixHint.SendColored(target, $"<b><color=#FFD700>Тебя воскресил {p.Nickname}!</color></b>", "#FFD700", 5f);
                }));
        }
    }
}
