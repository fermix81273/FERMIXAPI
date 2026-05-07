using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using FermixAPI.Core;
using PlayerRoles;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Свап SCP-роли в первые секунды раунда. Портировано из
    /// Hazbin.NoRules.ScpSwap, но переехало с LabAPI ClientCommandHandler
    /// на стандартный EXILED command-handler (см. <c>Commands/SwapCommand.cs</c>).
    ///
    /// Логика:
    /// • разрешён в первые <see cref="Config.ScpSwapWindowSeconds"/> секунд раунда;
    /// • вызывать может только живой SCP;
    /// • SCP-079 разрешён в максимум 1 экземпляре;
    /// • при онлайне &lt;30 один тип SCP может быть один; при ≥30 — максимум 2.
    /// Сами проверки исполняются в <see cref="TrySwap"/>; команда — тонкая обёртка.
    /// </summary>
    public static class FermixScpSwap
    {
        /// <summary>Включён ли модуль администратором (рантайм-флаг для рестартов между раундами).</summary>
        public static bool RuntimeEnabled { get; set; } = true;

        public static readonly Dictionary<string, RoleTypeId> AllowedRoles = new(System.StringComparer.OrdinalIgnoreCase)
        {
            { "173", RoleTypeId.Scp173 },
            { "079", RoleTypeId.Scp079 },
            { "106", RoleTypeId.Scp106 },
            { "049", RoleTypeId.Scp049 },
            { "096", RoleTypeId.Scp096 },
            { "939", RoleTypeId.Scp939 },
            { "3114", RoleTypeId.Scp3114 },
        };

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.ScpSwapEnabled != true) return;
            FermixEvents.OnRoundStart += OnRoundStart;
            _initialized = true;
            FermixLog.Info("FermixScpSwap включён.");
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixEvents.OnRoundStart -= OnRoundStart;
            _initialized = false;
        }

        private static void OnRoundStart()
        {
            // Каждый раунд начинаем с разрешённого свапа: возможные .swap off
            // от админов сбрасываются. Если хочется «забанить навсегда» — есть
            // ScpSwapEnabled в основном конфиге.
            RuntimeEnabled = true;
        }

        /// <summary>
        /// Попытаться свапнуть роль игроку. Возвращает true и пустой <paramref name="error"/> при успехе.
        /// </summary>
        public static bool TrySwap(Player player, string roleArg, out string error)
        {
            error = string.Empty;
            if (player == null) { error = "Только для игроков."; return false; }

            if (FermixCore.Config?.ScpSwapEnabled != true || !RuntimeEnabled)
            { error = ".swap отключён администратором или конфигом."; return false; }

            float window = FermixCore.Config?.ScpSwapWindowSeconds ?? 90f;
            if (Round.ElapsedTime.TotalSeconds >= window)
            { error = $"Прошло уже больше {(int)window} секунд с начала раунда!"; return false; }

            if (player.Role?.Side != Side.Scp)
            { error = "Вы не SCP и не можете воспользоваться сменой роли."; return false; }

            if (string.IsNullOrWhiteSpace(roleArg))
            { error = "Используй: .swap <SCP>. Доступные: " + string.Join(", ", AllowedRoles.Keys); return false; }

            if (!AllowedRoles.TryGetValue(roleArg.Trim(), out RoleTypeId targetRole))
            { error = "Такой SCP не найден в списке разрешённых."; return false; }

            if (targetRole == player.Role.Type)
            { error = "Вы уже играете за этого SCP."; return false; }

            int sameRoleCount = Player.List.Count(x => x?.Role?.Type == targetRole);

            if (targetRole == RoleTypeId.Scp079 && sameRoleCount >= 1)
            { error = "SCP-079 может быть только один!"; return false; }

            int onlineCount = Player.List.Count();
            if (onlineCount < 30 && sameRoleCount >= 1)
            { error = "Такой SCP уже есть в вашей команде!"; return false; }

            if (onlineCount >= 30 && sameRoleCount >= 2)
            { error = "Таких SCP уже слишком много!"; return false; }

            player.Role.Set(targetRole, SpawnReason.RoundStart);
            return true;
        }
    }
}
