using System;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;

namespace FermixAPI.Commands
{
    /// <summary>
    /// Клиентская команда .swap &lt;SCP&gt; — позволяет SCP в начале раунда
    /// сменить роль на другого SCP. Реализация — <see cref="FermixScpSwap.TrySwap"/>.
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public sealed class SwapCommand : ICommand
    {
        public string Command => "swap";
        public string[] Aliases => new[] { "sw" };
        public string Description => "Сменить SCP-роль в первые секунды раунда. Использование: .swap <173|079|106|049|096|939|3114>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            var player = Player.Get(sender);
            if (player == null) { response = "Только для игроков."; return false; }

            string arg = arguments.Count >= 1 ? arguments.Array[arguments.Offset] : null;
            if (FermixScpSwap.TrySwap(player, arg, out string err))
            {
                response = "Успех! Вы изменили роль SCP на другую!";
                return true;
            }

            response = err;
            return false;
        }
    }
}
