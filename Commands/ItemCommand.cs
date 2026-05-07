using System;
using System.Collections.Generic;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using FermixAPI.Systems;

namespace FermixAPI.Commands
{
    /// <summary>RA-команда <c>item</c>: выдача кастомных предметов FermixAPI.</summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public sealed class ItemCommand : ICommand
    {
        public string Command => "item";
        public string[] Aliases => new[] { "fermixitem", "fitem" };
        public string Description => "item give <ник|id> <id-предмета> | item list";

        // Реестр кастомных предметов: ключ → (описание, action(Player) → bool)
        // Расширяется здесь — новые кастомные предметы регистрируют себя добавлением записи.
        private static readonly Dictionary<string, (string Desc, Func<Player, bool> Give)> _registry =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["nvg"] = ("Прибор ночного видения (FermixNvg)", FermixNvg.GiveTo),
            };

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (arguments.Count < 1) { response = Description; return false; }
            string sub = arguments.Array[arguments.Offset].ToLowerInvariant();

            switch (sub)
            {
                case "list":
                    response = "Доступные предметы:\n" +
                               string.Join("\n", _registry.Select(kv => $"  • {kv.Key} — {kv.Value.Desc}"));
                    return true;

                case "give":
                {
                    if (arguments.Count < 3) { response = "Использование: item give <ник|id> <id-предмета>"; return false; }
                    var p = ResolvePlayer(arguments.Array[arguments.Offset + 1]);
                    if (p == null) { response = "Игрок не найден."; return false; }
                    string id = arguments.Array[arguments.Offset + 2];

                    if (!_registry.TryGetValue(id, out var entry))
                    {
                        response = $"Неизвестный предмет '{id}'. Список: item list";
                        return false;
                    }

                    if (!entry.Give(p))
                    {
                        response = $"Не удалось выдать '{id}' игроку {p.Nickname}.";
                        return false;
                    }

                    response = $"{p.Nickname}: выдан '{id}'.";
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

        /// <summary>Зарегистрировать новый кастомный предмет (вызывается из модуля при инициализации).</summary>
        public static void Register(string id, string desc, Func<Player, bool> give)
        {
            if (string.IsNullOrWhiteSpace(id) || give == null) return;
            _registry[id] = (desc ?? id, give);
        }
    }
}
