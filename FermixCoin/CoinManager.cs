using System.Collections.Generic;
using Exiled.API.Features;
using FermixAPI.Core;
using FermixAPI.FermixCoin.Outcomes;
using PlayerEvents = Exiled.Events.Handlers.Player;
using ServerEvents = Exiled.Events.Handlers.Server;

namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Менеджер модуля FermixCoin. Заменяет отдельный Plugin-класс;
    /// инициализируется и выключается из <see cref="FermixCore"/>.
    /// </summary>
    public static class CoinManager
    {
        /// <summary>Состояния всех «активных» монеток на сервере.</summary>
        public static Dictionary<ushort, CoinState> CoinStates { get; } = new Dictionary<ushort, CoinState>();

        private static CoinHandler _handler;

        public static void Initialize()
        {
            if (!FermixCore.Config.CoinEnabled)
                return;

            OutcomeRegistry.Initialize();
            CoinGlowController.Register();

            _handler = new CoinHandler();
            PlayerEvents.FlippingCoin += _handler.OnFlippingCoin;
            PlayerEvents.PickingUpItem += _handler.OnPickingUpItem;
            ServerEvents.RestartingRound += _handler.OnRestartingRound;

            Log.Info($"FermixCoin включён. Зарегистрировано исходов: {OutcomeRegistry.All.Count}.");
        }

        public static void Shutdown()
        {
            if (_handler != null)
            {
                PlayerEvents.FlippingCoin -= _handler.OnFlippingCoin;
                PlayerEvents.PickingUpItem -= _handler.OnPickingUpItem;
                ServerEvents.RestartingRound -= _handler.OnRestartingRound;
                _handler = null;
            }

            CoinGlowController.Unregister();
            CoinStates.Clear();
        }
    }
}
