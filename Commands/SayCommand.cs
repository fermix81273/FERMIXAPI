using System;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;

namespace FermixAPI.Commands
{
    /// <summary>
    /// Клиентская команда <c>.say</c> (псевдонимы <c>.s</c>, <c>.chat</c>) — общий
    /// текстовый чат для всего сервера. Сообщение попадает в общий буфер
    /// <see cref="FermixChat"/> и отображается всем игрокам в углу экрана.
    /// </summary>
    [CommandHandler(typeof(ClientCommandHandler))]
    public sealed class SayCommand : ICommand
    {
        public string Command => "say";

        public string[] Aliases => new[] { "s", "chat" };

        public string Description => "Отправить сообщение в общий чат сервера.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            if (player == null)
            {
                response = "Команда доступна только игрокам.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Использование: .say <текст>";
                return false;
            }

            string text = string.Join(" ", arguments);
            if (!FermixChat.TrySend(player, text, out string error))
            {
                response = error ?? "Сообщение не отправлено.";
                return false;
            }

            response = "Отправлено.";
            return true;
        }
    }
}
