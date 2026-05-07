using System;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Хит-маркеры в стиле Hazbin.NoRules.Hitmarkers: при попадании по
    /// игроку атакующему показывается короткий хинт со значением урона,
    /// а при убийстве — пометка «Убит». В оригинале хинты позиционировались
    /// через HintServiceMeow, здесь же используется FermixHintStack
    /// (id-based, чтобы новые попадания заменяли предыдущие, а не накапливались).
    /// </summary>
    public static class FermixHitmarkers
    {
        private const string HitId = "fermix_hitmarker_hit";
        private const string KillId = "fermix_hitmarker_kill";

        private const float HitDuration = 0.7f;
        private const float KillDuration = 1.4f;

        // Приоритет ниже системного, чтобы хитмаркер не перебивал важные
        // уведомления (подбор предмета, GoC-волна и т.п.).
        private const int HitPriority = 5;
        private const int KillPriority = 10;

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.HitmarkersEnabled != true) return;
            FermixEvents.OnPlayerHurt += OnHurt;
            FermixEvents.OnPlayerDied += OnDied;
            _initialized = true;
            FermixLog.Info("FermixHitmarkers включён.");
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixEvents.OnPlayerHurt -= OnHurt;
            FermixEvents.OnPlayerDied -= OnDied;
            _initialized = false;
        }

        private static void OnHurt(HurtingEventArgs ev)
        {
            try
            {
                var attacker = ev?.Attacker;
                var victim = ev?.Player;
                if (attacker == null || victim == null) return;
                if (!attacker.IsConnected || attacker == victim) return;

                int dmg = Mathf.RoundToInt(ev.Amount);
                if (dmg <= 0) return;

                FermixHint.ShowStacked(
                    attacker,
                    $"<color=#ffd24a>-{dmg}</color>",
                    duration: HitDuration,
                    priority: HitPriority,
                    id: HitId,
                    category: HintCategory.Custom);
            }
            catch (Exception ex) { FermixLog.Warn($"FermixHitmarkers.OnHurt: {ex.Message}"); }
        }

        private static void OnDied(DiedEventArgs ev)
        {
            try
            {
                var attacker = ev?.Attacker;
                var victim = ev?.Player;
                if (attacker == null || victim == null) return;
                if (!attacker.IsConnected || attacker == victim) return;

                FermixHint.ShowStacked(
                    attacker,
                    "<b><color=#ff4444>Убит!</color></b>",
                    duration: KillDuration,
                    priority: KillPriority,
                    id: KillId,
                    category: HintCategory.Custom);
            }
            catch (Exception ex) { FermixLog.Warn($"FermixHitmarkers.OnDied: {ex.Message}"); }
        }
    }
}
