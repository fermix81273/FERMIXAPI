using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;

namespace FermixAPI.Systems
{
    /// <summary>
    /// «Удалённая» проверка карт-доступа: позволяет открывать двери, шкафчики
    /// и снимать блокировку генераторов, если у игрока в инвентаре есть
    /// подходящая карта — даже если она сейчас не в руках. Поведение
    /// классического плагина RemoteKeycard, но переписанного под нашу
    /// архитектуру (через <see cref="FermixEvents"/>, <see cref="FermixHint"/>
    /// и общий конфиг).
    /// </summary>
    public static class FermixRemoteKeycard
    {
        private static bool _initialized;

        /// <summary>Подписаться на нужные события, если фича включена в конфиге.</summary>
        public static void Initialize()
        {
            if (_initialized) return;
            if (FermixCore.Config == null || !FermixCore.Config.RemoteKeycardEnabled) return;

            FermixEvents.OnDoorInteract += OnDoor;
            FermixEvents.OnLockerInteract += OnLocker;
            FermixEvents.OnUnlockGenerator += OnGenerator;

            _initialized = true;
        }

        /// <summary>Отписаться от событий и вернуть стандартное поведение.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnDoorInteract -= OnDoor;
            FermixEvents.OnLockerInteract -= OnLocker;
            FermixEvents.OnUnlockGenerator -= OnGenerator;

            _initialized = false;
        }

        private static void OnDoor(InteractingDoorEventArgs ev)
        {
            if (!FermixCore.Config.RemoteKeycardWorksOnDoors) return;
            if (ev == null || ev.IsAllowed || ev.Player == null || ev.Door == null) return;
            if (!ev.Door.IsKeycardDoor) return;

            if (TryFindMatchingKeycard(ev.Player, ev.Door.KeycardPermissions, out string cardName))
            {
                ev.IsAllowed = true;
                NotifyUnlock(ev.Player, cardName);
            }
        }

        private static void OnLocker(InteractingLockerEventArgs ev)
        {
            if (!FermixCore.Config.RemoteKeycardWorksOnLockers) return;
            if (ev == null || ev.IsAllowed || ev.Player == null || ev.InteractingChamber == null) return;

            KeycardPermissions required = ev.InteractingChamber.RequiredPermissions;
            if (required == KeycardPermissions.None) return;

            if (TryFindMatchingKeycard(ev.Player, required, out string cardName))
            {
                ev.IsAllowed = true;
                NotifyUnlock(ev.Player, cardName);
            }
        }

        private static void OnGenerator(UnlockingGeneratorEventArgs ev)
        {
            if (!FermixCore.Config.RemoteKeycardWorksOnGenerators) return;
            if (ev == null || ev.IsAllowed || ev.Player == null || ev.Generator == null) return;

            KeycardPermissions required = ev.Generator.KeycardPermissions;
            if (required == KeycardPermissions.None) return;

            if (TryFindMatchingKeycard(ev.Player, required, out string cardName))
            {
                ev.IsAllowed = true;
                NotifyUnlock(ev.Player, cardName);
            }
        }

        private static bool TryFindMatchingKeycard(Player player, KeycardPermissions required, out string cardName)
        {
            cardName = null;
            if (player?.Items == null) return false;

            foreach (Item item in player.Items)
            {
                if (item is Keycard card && (card.Permissions & required) == required)
                {
                    cardName = LocalizeKeycard(card.Type);
                    return true;
                }
            }

            return false;
        }

        private static void NotifyUnlock(Player player, string cardName)
        {
            if (player == null || !FermixCore.Config.RemoteKeycardShowHint) return;
            FermixHint.SendColored(player, $"Использована карта: {cardName}", FermixHint.Cyan, 2f);
        }

        private static string LocalizeKeycard(ItemType type) => type switch
        {
            ItemType.KeycardJanitor => "Уборщика",
            ItemType.KeycardScientist => "Учёного",
            ItemType.KeycardResearchCoordinator => "Координатора",
            ItemType.KeycardZoneManager => "Менеджера зоны",
            ItemType.KeycardGuard => "Охранника",
            ItemType.KeycardMTFPrivate => "МОГ-рядового",
            ItemType.KeycardContainmentEngineer => "Инженера",
            ItemType.KeycardMTFOperative => "МОГ-оператора",
            ItemType.KeycardMTFCaptain => "МОГ-капитана",
            ItemType.KeycardFacilityManager => "Управляющего",
            ItemType.KeycardChaosInsurgency => "Хаоса",
            ItemType.KeycardO5 => "О5",
            _ => "карта",
        };
    }
}
