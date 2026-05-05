namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Состояние одной монетки на сервере. Хранится в
    /// <see cref="CoinManager.CoinStates"/> по серийнику.
    /// </summary>
    public sealed class CoinState
    {
        /// <summary>Сколько раз эту монетку уже подкинули.</summary>
        public int Uses { get; set; }

        /// <summary>Случайный лимит бросков для этой монетки (1..CoinMaxUses).</summary>
        public int MaxUses { get; set; }

        /// <summary>Заранее свёрстанный следующий исход. Используется для подсветки.</summary>
        public Outcome NextOutcome { get; set; }
    }
}
