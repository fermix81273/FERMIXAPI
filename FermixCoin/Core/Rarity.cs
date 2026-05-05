namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Редкость исхода. Используется внутри для группировки и для подсветки
    /// монетки цветом следующего исхода (easter egg — про фичу мало кто знает).
    /// </summary>
    public enum Rarity
    {
        /// <summary>Обычные исходы (~50% от выпадений).</summary>
        Common,

        /// <summary>Не очень частые (~30%).</summary>
        Uncommon,

        /// <summary>Редкие (~12%). Включают слабые имбовые телепорты, тяжёлую броню.</summary>
        Rare,

        /// <summary>Эпические (~6%). Зомби, Pocket Dim, факшн-свап.</summary>
        Epic,

        /// <summary>Легендарные (~2%). Превращение в MTF/Хаос, джекпот, тревога.</summary>
        Legendary,

        /// <summary>Мифический (0.01%). Только для Mega-Jackpot.</summary>
        Mythic,
    }

    public static class RarityColors
    {
        // HEX-цвета подсветки. Подобраны так, чтобы быть различимыми на тёмной карте.
        public const string CommonHex    = "#FFE066"; // мягкий жёлтый
        public const string UncommonHex  = "#5BCB76"; // зелёный
        public const string RareHex      = "#4D9DFF"; // синий
        public const string EpicHex      = "#B66BFF"; // фиолетовый
        public const string LegendaryHex = "#FF7A33"; // красно-оранжевый
        public const string MythicHex    = "#FFFFFF"; // подсветка пойдёт через AddRainbowGlow

        public static string ToHex(this Rarity r) => r switch
        {
            Rarity.Common    => CommonHex,
            Rarity.Uncommon  => UncommonHex,
            Rarity.Rare      => RareHex,
            Rarity.Epic      => EpicHex,
            Rarity.Legendary => LegendaryHex,
            Rarity.Mythic    => MythicHex,
            _                => CommonHex,
        };
    }
}
