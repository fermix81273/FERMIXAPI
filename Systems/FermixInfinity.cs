using System;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;
using InventorySystem;
using InventorySystem.Items;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Бесконечные припасы и радио в стиле «no rules». Портировано из
    /// Hazbin.NoRules.InfinityStuff под событийную модель EXILED:
    /// • рация не разряжается;
    /// • выбрасывать патроны нельзя;
    /// • при перезарядке магазин дополняется недостающими патронами;
    /// • при смене роли каждому типу патрона выдаётся 1 шт. (затравка);
    /// • подбор патронов с пола заблокирован (смысла в нём нет);
    /// • попадание в наручники сбрасывает все патроны цели.
    ///
    /// Включается флагом <see cref="Config.InfinityStuffEnabled"/>. Никаких
    /// собственных хинтов/SSS-биндов не использует — поведение «тихое».
    /// </summary>
    public static class FermixInfinity
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.InfinityStuffEnabled != true) return;

            Exiled.Events.Handlers.Player.UsingRadioBattery += OnUsingRadio;
            Exiled.Events.Handlers.Player.DroppingAmmo += OnDroppingAmmo;
            Exiled.Events.Handlers.Player.ReloadingWeapon += OnReload;
            Exiled.Events.Handlers.Player.ChangingRole += OnChangingRole;
            Exiled.Events.Handlers.Player.Handcuffing += OnHandcuffing;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;

            _initialized = true;
            FermixLog.Info("FermixInfinity включён.");
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            Exiled.Events.Handlers.Player.UsingRadioBattery -= OnUsingRadio;
            Exiled.Events.Handlers.Player.DroppingAmmo -= OnDroppingAmmo;
            Exiled.Events.Handlers.Player.ReloadingWeapon -= OnReload;
            Exiled.Events.Handlers.Player.ChangingRole -= OnChangingRole;
            Exiled.Events.Handlers.Player.Handcuffing -= OnHandcuffing;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;

            _initialized = false;
        }

        // ── handlers ────────────────────────────────────────────────

        // Hazbin: ev.Drain = 0. EXILED предоставляет UsingRadioBatteryEventArgs.Drain.
        private static void OnUsingRadio(UsingRadioBatteryEventArgs ev)
        {
            try { if (ev != null) ev.Drain = 0f; }
            catch (Exception ex) { FermixLog.Warn($"FermixInfinity.OnUsingRadio: {ex.Message}"); }
        }

        private static void OnDroppingAmmo(DroppingAmmoEventArgs ev)
        {
            if (ev != null) ev.IsAllowed = false;
        }

        // Hazbin добавлял ровно (MaxMagazineAmmo - MagazineAmmo) патронов
        // прямо перед перезарядкой, чтобы перезарядка всегда наполняла
        // магазин до максимума «из ниоткуда».
        private static void OnReload(ReloadingWeaponEventArgs ev)
        {
            try
            {
                if (ev?.Player == null || ev.Firearm == null) return;
                var fa = ev.Firearm;
                int deficit = fa.MaxMagazineAmmo - fa.MagazineAmmo;
                if (deficit > 0)
                    ev.Player.AddAmmo(fa.AmmoType, (ushort)deficit);
            }
            catch (Exception ex) { FermixLog.Warn($"FermixInfinity.OnReload: {ex.Message}"); }
        }

        // На свеже-выданной роли через секунду подкидываем по одному патрону
        // каждого типа — чтобы после смерти/смены роли инвентарь не был
        // пустым и в дальнейшем работал авто-докид при перезарядке.
        private static void OnChangingRole(ChangingRoleEventArgs ev)
        {
            var p = ev?.Player;
            if (p == null) return;

            FermixScheduler.Delay(1f, () =>
            {
                if (p == null || !p.IsConnected) return;
                try
                {
                    foreach (AmmoType ammo in Enum.GetValues(typeof(AmmoType)))
                    {
                        if (ammo == AmmoType.None) continue;
                        p.SetAmmo(ammo, 1);
                    }
                }
                catch (Exception ex) { FermixLog.Warn($"FermixInfinity.OnChangingRole delayed: {ex.Message}"); }
            });
        }

        // При надевании наручников у цели сбрасываем все патроны: иначе
        // даже арестованный наносит урон через подсыпанную инфинити-стартовку.
        private static void OnHandcuffing(HandcuffingEventArgs ev)
        {
            try { ev?.Target?.ClearAmmo(); }
            catch (Exception ex) { FermixLog.Warn($"FermixInfinity.OnHandcuffing: {ex.Message}"); }
        }

        // Hazbin отдельно блокировал подбор предмета категории Ammo.
        // В EXILED 9.13 отдельного PickingUpAmmo нет — фильтруем по
        // категории прямо в общем PickingUpItem.
        private static void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            try
            {
                if (ev?.Pickup == null) return;
                if (ev.Pickup.Type.GetCategory() == ItemCategory.Ammo)
                    ev.IsAllowed = false;
            }
            catch (Exception ex) { FermixLog.Warn($"FermixInfinity.OnPickingUpItem: {ex.Message}"); }
        }
    }
}
