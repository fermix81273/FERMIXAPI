using System;
using Exiled.API.Features;

namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Один из ~40 возможных исходов подкидывания монетки.
    /// </summary>
    public sealed class Outcome
    {
        /// <summary>Внутренний идентификатор (например, "B1", "D5", "I1").</summary>
        public string Id { get; }

        /// <summary>Человеческое имя для логов.</summary>
        public string Name { get; }

        /// <summary>Редкость — определяет цвет свечения и относительный вес.</summary>
        public Rarity Rarity { get; }

        /// <summary>Дополнительный множитель веса. 1.0 = базовый для редкости. 0.2 = «мелкий шанс».</summary>
        public float WeightMultiplier { get; }

        /// <summary>Основное сообщение игроку (русский).</summary>
        public string Message { get; }

        /// <summary>Прикольный комментарий (отдельный хинт). null = нет комментария.</summary>
        public string Comment { get; }

        /// <summary>Действие, которое выполняется на игроке.</summary>
        public Action<Player> Action { get; }

        public Outcome(string id, string name, Rarity rarity, string message, string comment, Action<Player> action, float weightMultiplier = 1f)
        {
            Id = id;
            Name = name;
            Rarity = rarity;
            Message = message;
            Comment = comment;
            Action = action;
            WeightMultiplier = weightMultiplier <= 0f ? 1f : weightMultiplier;
        }

        /// <summary>Базовый вес для редкости (без учёта <see cref="WeightMultiplier"/>).</summary>
        public static int BaseWeight(Rarity r) => r switch
        {
            Rarity.Common    => 50,
            Rarity.Uncommon  => 30,
            Rarity.Rare      => 12,
            Rarity.Epic      => 6,
            Rarity.Legendary => 2,
            Rarity.Mythic    => 0, // мифический не идёт в обычный пул
            _                => 0,
        };

        /// <summary>Эффективный вес исхода с учётом множителя.</summary>
        public int EffectiveWeight => (int)System.Math.Max(1, BaseWeight(Rarity) * WeightMultiplier);
    }
}
