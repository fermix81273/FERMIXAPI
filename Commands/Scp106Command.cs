using System;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;

namespace FermixAPI.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public sealed class Scp106Command : ICommand
    {
        public string Command => "106";
        public string[] Aliases => new[] { "scp106" };
        public string Description => "Расширения 106: .106 stalk | .106 tp <комната>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player p = Player.Get(sender);
            if (p == null) { response = "Только для игроков."; return false; }
            if (arguments.Count < 1) { response = Description; return false; }

            string sub = arguments.Array[arguments.Offset].ToLowerInvariant();
            switch (sub)
            {
                case "stalk":
                    if (!FermixScp106Plus.TryToggleStalk(p, out string e1)) { response = e1; return false; }
                    response = "OK"; return true;
                case "tp":
                case "teleport":
                case "portal":
                    string room = arguments.Count > 1 ? arguments.Array[arguments.Offset + 1] : null;
                    if (!FermixScp106Plus.TryTeleport(p, room, out string e2)) { response = e2; return false; }
                    response = "OK"; return true;
                default:
                    response = Description; return false;
            }
        }
    }
}
