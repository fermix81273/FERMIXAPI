using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using FermixAPI.Core;
using FermixAPI.Systems;
using PlayerApi = Exiled.API.Features.Player;

namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Хук на <see cref="Exiled.Events.Handlers.Player.FlippingCoin"/>:
    /// 1) Применяет заранее свёрстанный исход.
    /// 2) Увеличивает счётчик бросков; на исчерпании монетка испаряется.
    /// 3) Если есть ещё броски — обновляет следующий исход (и подсветку).
    /// </summary>
    public sealed class CoinHandler
    {
        public void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            try
            {
                if (ev == null || !ev.IsAllowed || ev.Pickup == null)
                    return;

                if (ev.Pickup.Type != ItemType.Coin)
                    return;

                var states = CoinManager.CoinStates;
                if (!states.ContainsKey(ev.Pickup.Serial))
                {
                    var maxUses = UnityEngine.Random.Range(1, FermixCore.Config.CoinMaxUses + 1);
                    states[ev.Pickup.Serial] = new CoinState
                    {
                        Uses = 0,
                        MaxUses = maxUses,
                        NextOutcome = OutcomeRegistry.RollOne(),
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[FermixCoin] OnPickingUpItem упал: {ex}");
            }
        }

        public void OnFlippingCoin(FlippingCoinEventArgs ev)
        {
            try
            {
                if (ev == null || !ev.IsAllowed || ev.Player == null || ev.Item == null)
                    return;

                var states = CoinManager.CoinStates;
                var serial = ev.Item.Serial;

                if (!states.TryGetValue(serial, out var state))
                {
                    state = new CoinState
                    {
                        Uses = 0,
                        MaxUses = UnityEngine.Random.Range(1, FermixCore.Config.CoinMaxUses + 1),
                        NextOutcome = OutcomeRegistry.RollOne(),
                    };
                    states[serial] = state;
                }

                state.Uses++;

                var rng = UnityEngine.Random.value;
                bool isMega = rng < (float)FermixCore.Config.MegaJackpotChance;

                try
                {
                    if (isMega)
                        ApplyMegaJackpot(ev.Player);
                    else
                        ApplyOutcome(ev.Player, state.NextOutcome);
                }
                catch (Exception ex)
                {
                    Log.Error($"[FermixCoin] исход '{state.NextOutcome?.Id}' упал: {ex}");
                }

                if (state.Uses >= state.MaxUses)
                {
                    ev.Player.RemoveItem(ev.Item);
                    states.Remove(serial);
                    FermixHint.Send(ev.Player, "<color=#888888>Монетка рассыпалась в труху...</color>", 4f);
                }
                else
                {
                    state.NextOutcome = OutcomeRegistry.RollOne();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[FermixCoin] OnFlippingCoin упал: {ex}");
            }
        }

        public void OnRestartingRound()
        {
            try
            {
                CoinManager.CoinStates.Clear();
            }
            catch (Exception ex)
            {
                Log.Warn($"[FermixCoin] OnRestartingRound: {ex.Message}");
            }
        }

        private static void ApplyOutcome(PlayerApi player, Outcome outcome)
        {
            if (player == null || outcome == null)
                return;

            outcome.Action(player);

            var color = outcome.Rarity.ToHex();
            FermixHint.SendColored(player, $"<b><color={color}>{outcome.Message}</color></b>", color, 5f);

            if (FermixCore.Config.ShowCommentHints && !string.IsNullOrEmpty(outcome.Comment))
            {
                FermixHint.SendColored(player, $"<i><color=#cccccc>{outcome.Comment}</color></i>", FermixHint.Gray, 5f);
            }
        }

        private static void ApplyMegaJackpot(PlayerApi player)
        {
            if (player == null)
                return;

            FermixHint.SendColored(player, "<b><color=#FF00FF>★ МЕГА-ДЖЕКПОТ ★</color></b>", "#FF00FF", 8f);

            if (FermixCore.Config.BroadcastMegaJackpot)
            {
                FermixServer.GlobalBroadcast(
                    $"<color=#FF00FF>★ МЕГА-ДЖЕКПОТ ★</color> у игрока <b>{player.Nickname}</b>! Сейчас будет весело.",
                    duration: 8);
            }

            int idx = 0;
            foreach (var outcome in OutcomeRegistry.All)
            {
                var captured = outcome;
                FermixScheduler.Delay(0.05f * idx, () =>
                {
                    if (player == null || !player.IsConnected || !player.IsAlive)
                        return;
                    try
                    {
                        captured.Action(player);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"[FermixCoin] mega-jackpot: '{captured.Id}' упал: {ex.Message}");
                    }
                });
                idx++;
            }
        }
    }
}
