using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;
using PlayerRoles;

namespace FermixAPI.Commands
{
    /// <summary>RA-команда <c>goc</c> для ручного управления отрядом G.O.C.</summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class GocCommand : ICommand
    {
        public string Command => "goc";
        public string[] Aliases => new[] { "globaloccult" };
        public string Description => "Управление G.O.C.: goc spawn <ник|id> | goc unmark <ник|id> | goc list | goc wave";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = Description; return false; }
            string sub = arguments.Array[arguments.Offset].ToLowerInvariant();

            switch (sub)
            {
                case "list":
                {
                    var ids = FermixGoc.Members;
                    if (ids.Count == 0) { response = "G.O.C. пуст."; return true; }
                    response = "G.O.C.: " + string.Join(", ", ids.Select(id => Player.Get(id)?.Nickname ?? id));
                    return true;
                }
                case "spawn":
                case "mark":
                {
                    if (arguments.Count < 2) { response = "Использование: goc spawn <ник|id>"; return false; }
                    var p = ResolvePlayer(arguments.Array[arguments.Offset + 1]);
                    if (p == null) { response = "Игрок не найден."; return false; }
                    if (!p.IsAlive)
                    {
                        // Базово стартуем как Tutorial — это ожидаемая
                        // базовая роль G.O.C. (см. FermixGoc).
                        try { p.Role.Set(RoleTypeId.Tutorial); }
                        catch (Exception e) { response = $"Не удалось заспавнить: {e.Message}"; return false; }
                    }
                    FermixGoc.Mark(p, rank: null, announce: true);
                    response = $"{p.Nickname} помечен как G.O.C.";
                    return true;
                }
                case "unmark":
                case "remove":
                {
                    if (arguments.Count < 2) { response = "Использование: goc unmark <ник|id>"; return false; }
                    var p = ResolvePlayer(arguments.Array[arguments.Offset + 1]);
                    if (p == null) { response = "Игрок не найден."; return false; }
                    FermixGoc.Unmark(p);
                    response = $"{p.Nickname} больше не G.O.C.";
                    return true;
                }
                case "wave":
                {
                    int spawned = FermixGoc.TriggerWaveManual(out string err);
                    if (spawned <= 0)
                    {
                        response = string.IsNullOrWhiteSpace(err)
                            ? "Не удалось инициализировать волну G.O.C."
                            : err;
                        return false;
                    }
                    response = $"G.O.C.-волна инициирована: {spawned} оперативник(ов).";
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
