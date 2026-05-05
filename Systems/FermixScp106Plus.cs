using System;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using FermixAPI.Core;
using PlayerRoles;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Расширенные действия SCP-106 в духе BetterScp106: телепорт через
    /// портал в выбранную комнату HCZ/EZ и переключение Stalk-режима.
    /// Точка входа — команда <c>.106</c> (см. <c>Scp106Command</c>).
    /// </summary>
    public static class FermixScp106Plus
    {
        public static bool RequireScp106(Player p, out Scp106Role role, out string error)
        {
            role = null; error = null;
            if (p == null) { error = "Только для игроков."; return false; }
            if (p.Role?.Type != RoleTypeId.Scp106) { error = "Команда доступна только SCP-106."; return false; }
            role = p.Role.As<Scp106Role>();
            if (role == null) { error = "Не удалось получить роль 106."; return false; }
            return true;
        }

        public static bool TryToggleStalk(Player p, out string error)
        {
            error = null;
            if (FermixCore.Config?.Scp106PlusEnabled != true) { error = "Фича выключена."; return false; }
            if (!RequireScp106(p, out var role, out error)) return false;
            role.IsStalking = !role.IsStalking;
            FermixHint.SendColored(p, role.IsStalking ? "Stalk: ВКЛ" : "Stalk: ВЫКЛ", FermixHint.Magenta, 2f);
            return true;
        }

        public static bool TryTeleport(Player p, string roomArg, out string error)
        {
            error = null;
            if (FermixCore.Config?.Scp106PlusEnabled != true) { error = "Фича выключена."; return false; }
            if (!RequireScp106(p, out var role, out error)) return false;
            if (string.IsNullOrWhiteSpace(roomArg)) { error = "Использование: .106 tp <часть имени комнаты>"; return false; }

            var room = ResolveRoom(roomArg);
            if (room == null) { error = $"Комната по запросу '{roomArg}' не найдена."; return false; }

            float cost = FermixCore.Config?.Scp106PlusVigorCost ?? 0.3f;
            if (role.Vigor < cost) { error = $"Не хватает Vigor (нужно {cost:F2})."; return false; }

            if (!role.UsePortal(room.Position + UnityEngine.Vector3.up * 1.0f, cost))
            {
                error = "Портал не сработал.";
                return false;
            }

            FermixHint.SendColored(p, $"Портал в {room.Type}", FermixHint.Magenta, 2f);
            return true;
        }

        private static Room ResolveRoom(string arg)
        {
            arg = arg.Trim();
            if (Enum.TryParse(arg, ignoreCase: true, out RoomType rt))
            {
                var exact = Room.Get(rt);
                if (exact != null) return exact;
            }
            return Room.List.FirstOrDefault(r =>
                r.Type.ToString().IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
