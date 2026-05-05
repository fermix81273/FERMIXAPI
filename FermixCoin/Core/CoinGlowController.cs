using System.Collections.Generic;
using FermixAPI.Core;
using FermixAPI.Systems;

namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Регистрирует в FermixGlow по одной подсветке на редкость. Каждая подсветка
    /// проверяет: если данный серийник лежит в реестре монеток и его следующий
    /// исход — этой редкости — подсвечивай. Так монетка плавно меняет цвет
    /// между бросками без внешнего тика.
    /// </summary>
    public static class CoinGlowController
    {
        private static readonly List<string> _glowIds = new();

        public static void Register()
        {
            if (!FermixCore.Config.RarityGlowEnabled)
                return;

            RegisterRarityGlow(Rarity.Common,    RarityColors.CommonHex,    intensity: 0.55f);
            RegisterRarityGlow(Rarity.Uncommon,  RarityColors.UncommonHex,  intensity: 0.85f);
            RegisterRarityGlow(Rarity.Rare,      RarityColors.RareHex,      intensity: 1.00f);
            RegisterRarityGlow(Rarity.Epic,      RarityColors.EpicHex,      intensity: 1.20f);
            RegisterRarityGlow(Rarity.Legendary, RarityColors.LegendaryHex, intensity: 1.40f);
        }

        public static void Unregister()
        {
            foreach (var id in _glowIds)
                FermixGlow.RemoveGlow(id);
            _glowIds.Clear();
        }

        private static void RegisterRarityGlow(Rarity rarity, string hex, float intensity)
        {
            var id = $"FermixCoin_Rarity_{rarity}";
            FermixGlow.AddGlowHex(
                id: id,
                itemCheck: serial =>
                {
                    if (!CoinManager.CoinStates.TryGetValue(serial, out var state))
                        return false;
                    return state?.NextOutcome != null && state.NextOutcome.Rarity == rarity;
                },
                hexColor: hex,
                intensity: intensity,
                range: 4f,
                updateInterval: 0.25f,
                glowInHands: true);
            _glowIds.Add(id);
        }
    }
}
