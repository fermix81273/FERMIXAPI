using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Warhead;
using FermixAPI.Core;
using PlayerRoles;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Реестр валидных целей для телепортации игроков. Портировано из
    /// <c>Hazbin.Teleports</c> (см. <c>TeleportExtensions</c> + <c>EventHandlers</c>):
    /// держим два allow-листа — комнат и игроков — которые автоматически
    /// меняются на ключевых событиях раунда:
    /// <list type="bullet">
    ///   <item><description><see cref="FermixEvents.OnRoundStart"/> — все комнаты разрешены, allow-лист игроков пересобирается;</description></item>
    ///   <item><description><see cref="FermixEvents.OnDecontamination"/> — комнаты LCZ убираются (туда нельзя слать);</description></item>
    ///   <item><description><see cref="FermixEvents.OnWarheadStart"/> — всё кроме Surface убирается;</description></item>
    ///   <item><description><see cref="FermixEvents.OnRoleChange"/> — игрок add/remove зависит от <see cref="ChangingRoleEventArgs.NewRole"/>;</description></item>
    ///   <item><description><see cref="FermixEvents.OnPlayerLeave"/> — игрок убирается из allow-листа.</description></item>
    /// </list>
    /// Используется исходами FermixCoin (<see cref="FermixAPI.FermixCoin.Outcomes.TeleportOutcomes"/>),
    /// чтобы избегать «ненужных» телепортов: в LCZ во время декона, во время
    /// взрыва БГ — куда угодно, кроме Surface, в спектаторов/мёртвых и т.п.
    /// </summary>
    public static class FermixTeleportRegistry
    {
        private static readonly HashSet<Room> _rooms = new HashSet<Room>();
        private static readonly HashSet<Player> _players = new HashSet<Player>();
        private static bool _initialized;

        /// <summary>Снимок текущего allow-листа комнат (read-only).</summary>
        public static IReadOnlyCollection<Room> AllowedRooms => _rooms;

        /// <summary>Снимок текущего allow-листа игроков (read-only).</summary>
        public static IReadOnlyCollection<Player> AllowedPlayers => _players;

        public static void Initialize()
        {
            if (_initialized) return;

            FermixEvents.OnRoundStart       += OnRoundStart;
            FermixEvents.OnDecontamination  += OnDecontamination;
            FermixEvents.OnWarheadStart     += OnWarheadStart;
            FermixEvents.OnRoleChange       += OnRoleChange;
            FermixEvents.OnPlayerLeave      += OnPlayerLeave;

            _initialized = true;
            FermixLog.Info("FermixTeleportRegistry готов.");
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart       -= OnRoundStart;
            FermixEvents.OnDecontamination  -= OnDecontamination;
            FermixEvents.OnWarheadStart     -= OnWarheadStart;
            FermixEvents.OnRoleChange       -= OnRoleChange;
            FermixEvents.OnPlayerLeave      -= OnPlayerLeave;

            _rooms.Clear();
            _players.Clear();
            _initialized = false;
        }

        // ── Public API ───────────────────────────────────────────────

        /// <summary>
        /// Случайная разрешённая комната. <paramref name="ignorePocket"/>=true
        /// исключает Pocket Dimension (для исхода C1 — Pocket это отдельный
        /// исход C3). Если allow-лист пуст — fallback на <see cref="Room.List"/>.
        /// </summary>
        public static Room RandomRoom(bool ignorePocket = true)
        {
            // Если allow-лист пуст (например, регистрация ещё не успела
            // инициализироваться на этом раунде), мягко падаем на Room.List.
            IEnumerable<Room> source = _rooms.Count > 0
                ? _rooms.Where(r => r != null)
                : Room.List.Where(r => r != null);

            var pool = source
                .Where(r => !ignorePocket || r.Type != RoomType.Pocket)
                .Where(r => r.Type != RoomType.Unknown)
                .ToList();

            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// Случайный разрешённый игрок (живой, в подходящей роли),
        /// исключая <paramref name="except"/>. Если allow-лист пуст —
        /// fallback на <see cref="Player.List"/>.
        /// </summary>
        public static Player RandomPlayer(Player except = null)
        {
            IEnumerable<Player> source = _players.Count > 0
                ? _players.Where(p => p != null)
                : Player.List.Where(p => p != null && IsTeleportablePlayer(p));

            var pool = source
                .Where(p => p.IsConnected)
                .Where(p => except == null
                            || (p.UserId != null && except.UserId != null && p.UserId != except.UserId))
                .ToList();

            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// Можно ли в принципе телепортировать игрока с такой ролью —
        /// фильтр копирует <c>Hazbin.Teleports.EventHandlers.OnPlayerChangingRole</c>.
        /// </summary>
        public static bool IsTeleportableRole(RoleTypeId role)
        {
            return role != RoleTypeId.None
                && role != RoleTypeId.Spectator
                && role != RoleTypeId.Overwatch
                && role != RoleTypeId.Filmmaker;
        }

        private static bool IsTeleportablePlayer(Player p)
        {
            if (p == null || p.IsHost) return false;
            return IsTeleportableRole(p.Role.Type);
        }

        // ── Event handlers ───────────────────────────────────────────

        private static void OnRoundStart()
        {
            // Раунд начался — всё разрешено заново.
            _rooms.Clear();
            foreach (var room in Room.List)
            {
                if (room == null) continue;
                _rooms.Add(room);
            }

            _players.Clear();
            foreach (var p in Player.List)
            {
                if (p == null || p.IsHost) continue;
                if (IsTeleportableRole(p.Role.Type))
                    _players.Add(p);
            }
        }

        private static void OnDecontamination(DecontaminatingEventArgs ev)
        {
            if (ev == null || !ev.IsAllowed) return;

            // LCZ становится непригодной для телепорта — копия
            // OnServerLczDecontaminationStarting из Hazbin.Teleports.
            var lcz = _rooms
                .Where(r => r != null && r.Zone == ZoneType.LightContainment)
                .ToList();
            foreach (var r in lcz) _rooms.Remove(r);
        }

        private static void OnWarheadStart(StartingEventArgs ev)
        {
            if (ev == null || !ev.IsAllowed) return;

            // Боеголовка пошла — отрезаем все комнаты КРОМЕ Surface.
            // Hazbin.Teleports делал это на OnWarheadDetonating, но у нас
            // удобнее цепляться за Starting (в EXILED 9.13.3 именно он
            // экспонирован через FermixEvents.OnWarheadStart).
            var nonSurface = _rooms
                .Where(r => r != null && r.Type != RoomType.Surface)
                .ToList();
            foreach (var r in nonSurface) _rooms.Remove(r);
        }

        private static void OnRoleChange(ChangingRoleEventArgs ev)
        {
            if (ev?.Player == null || ev.Player.IsHost) return;

            if (IsTeleportableRole(ev.NewRole))
                _players.Add(ev.Player);
            else
                _players.Remove(ev.Player);
        }

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            _players.Remove(ev.Player);
        }
    }
}
