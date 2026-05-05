using System;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;

namespace FermixAPI.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public sealed class CallvoteCommand : ICommand
    {
        public string Command => "callvote";
        public string[] Aliases => new[] { "cv" };
        public string Description => "Начать голосование: .cv <kick|restart|ask> [имя/вопрос] [причина]";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player p = Player.Get(sender);
            if (p == null) { response = "Только для игроков."; return false; }
            if (arguments.Count < 1) { response = Description; return false; }

            string sub = arguments.Array[arguments.Offset].ToLowerInvariant();
            FermixCallvote.VoteKind kind;
            string targetArg = null;
            string reason = null;

            switch (sub)
            {
                case "kick":
                    if (arguments.Count < 2) { response = "Использование: .cv kick <имя> [причина]"; return false; }
                    kind = FermixCallvote.VoteKind.Kick;
                    targetArg = arguments.Array[arguments.Offset + 1];
                    if (arguments.Count > 2) reason = string.Join(" ", arguments.Array, arguments.Offset + 2, arguments.Count - 2);
                    break;
                case "restart":
                case "rr":
                    kind = FermixCallvote.VoteKind.Restart;
                    break;
                case "ask":
                case "custom":
                    if (arguments.Count < 2) { response = "Использование: .cv ask <вопрос>"; return false; }
                    kind = FermixCallvote.VoteKind.Custom;
                    reason = string.Join(" ", arguments.Array, arguments.Offset + 1, arguments.Count - 1);
                    break;
                default:
                    response = "Тип: kick | restart | ask";
                    return false;
            }

            if (!FermixCallvote.TryStart(p, kind, targetArg, reason, out string err))
            {
                response = err ?? "Не получилось.";
                return false;
            }

            response = "Голосование начато.";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public sealed class VoteCommand : ICommand
    {
        public string Command => "vote";
        public string[] Aliases => new[] { "v" };
        public string Description => "Проголосовать: .vote <yes|no>";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player p = Player.Get(sender);
            if (p == null) { response = "Только для игроков."; return false; }
            if (arguments.Count < 1) { response = "Использование: .vote <yes|no>"; return false; }

            string a = arguments.Array[arguments.Offset].ToLowerInvariant();
            bool? yes = a switch
            {
                "y" or "yes" or "+" or "за" or "да" => true,
                "n" or "no" or "-" or "против" or "нет" => false,
                _ => null,
            };
            if (yes == null) { response = "Используй yes/no."; return false; }

            if (!FermixCallvote.TryCast(p, yes.Value, out string err))
            {
                response = err ?? "Не вышло.";
                return false;
            }
            response = yes.Value ? "Голос ЗА учтён." : "Голос ПРОТИВ учтён.";
            return true;
        }
    }
}
