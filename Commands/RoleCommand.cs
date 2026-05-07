using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;

namespace FermixAPI.Commands
{
    /// <summary>RA-команда <c>role</c>: выдача кастомного класса (Медик/Джаггернаут/Командир/Стрелок) поверх MTF/Chaos.</summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class RoleCommand : ICommand
    {
        public string Command => "role";
        public string[] Aliases => new[] { "fermixrole", "frole" };
        public string Description => "role give <ник|id> <medic|jugger|commander|rifleman> | role list";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = Description; return false; }
            string sub = arguments.Array[arguments.Offset].ToLowerInvariant();

            switch (sub)
            {
                case "list":
                    response = "Доступные классы: " + string.Join(", ", FermixSquadClasses.ListAllClassNames());
                    return true;

                case "give":
                case "set":
                {
                    if (arguments.Count < 3) { response = "Использование: role give <ник|id> <medic|jugger|commander|rifleman>"; return false; }
                    var p = ResolvePlayer(arguments.Array[arguments.Offset + 1]);
                    if (p == null) { response = "Игрок не найден."; return false; }
                    string alias = arguments.Array[arguments.Offset + 2];

                    if (!FermixSquadClasses.ApplyRoleByAlias(p, alias, out string err))
                    {
                        response = err ?? "Не удалось применить класс.";
                        return false;
                    }

                    response = $"{p.Nickname}: класс «{alias}» применён.";
                    return true;
                }

                default:
                    response = Description;
                    return false;
            }
        }

        private static Player ResolvePlayer(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return null;
            arg = arg.Trim();
            return Player.Get(arg) ?? Player.List.FirstOrDefault(p =>
                p.Nickname?.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
