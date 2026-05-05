using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using FermixAPI.Core;
using PlayerRoles;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// SSS-биндинги поверх <see cref="FermixScp106Plus"/>:
    /// Q (по умолчанию) — toggle Stalk-режим;
    /// F (по умолчанию) — портал в комнату ближайшего игрока-человека.
    /// Клавиши перенастраиваются игроком в Server Specific Settings меню.
    /// </summary>
    public static class FermixScp106Bindings
    {
        private const float CooldownSeconds = 1.5f;
        private static readonly Dictionary<string, DateTime> _lastUse = new(StringComparer.Ordinal);
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.Scp106PlusEnabled != true) return;
            if (FermixCore.Config?.Scp106BindingsEnabled != true) return;

            FermixInput.RegisterPressedHandler(FermixInput.Q, OnQ);
            FermixInput.RegisterPressedHandler(FermixInput.F, OnF);
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixInput.UnregisterPressedHandler(FermixInput.Q, OnQ);
            FermixInput.UnregisterPressedHandler(FermixInput.F, OnF);
            _lastUse.Clear();
            _initialized = false;
        }

        private static bool Allowed(Player p)
        {
            if (p == null) return false;
            if (p.Role?.Type != RoleTypeId.Scp106) return false;
            string id = p.UserId ?? p.Nickname;
            if (_lastUse.TryGetValue(id, out var t) && (DateTime.UtcNow - t).TotalSeconds < CooldownSeconds)
                return false;
            _lastUse[id] = DateTime.UtcNow;
            return true;
        }

        private static void OnQ(Player p)
        {
            if (!Allowed(p)) return;
            FermixScp106Plus.TryToggleStalk(p, out _);
        }

        private static void OnF(Player p)
        {
            if (!Allowed(p)) return;

            Player target = Player.List
                .Where(o => o != null && o != p && o.IsAlive && o.Role?.Side != Side.Scp && o.Role?.Side != Side.None)
                .OrderBy(o => Vector3.Distance(o.Position, p.Position))
                .FirstOrDefault();

            if (target == null)
            {
                FermixHint.SendColored(p, "Нет живых людей для портала.", FermixHint.Yellow, 2f);
                return;
            }

            string roomArg = target.CurrentRoom?.Type.ToString() ?? "EzGateA";
            if (!FermixScp106Plus.TryTeleport(p, roomArg, out string err))
                FermixHint.SendColored(p, err ?? "Портал не сработал.", FermixHint.Yellow, 2f);
        }
    }
}
