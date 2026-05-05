using System.Collections.Generic;
using System.Linq;
using FermixAPI.FermixCoin.Outcomes;

namespace FermixAPI.FermixCoin
{
    /// <summary>
    /// Реестр всех зарегистрированных исходов. Заполняется при инициализации CoinManager.
    /// </summary>
    public static class OutcomeRegistry
    {
        private static readonly List<Outcome> _all = new();

        public static IReadOnlyList<Outcome> All => _all;

        /// <summary>Сумма эффективных весов — для weighted-random.</summary>
        public static int TotalWeight { get; private set; }

        public static void Initialize()
        {
            _all.Clear();

            ItemOutcomes.Register(_all);
            GrenadeOutcomes.Register(_all);
            TeleportOutcomes.Register(_all);
            RoleOutcomes.Register(_all);
            HealthOutcomes.Register(_all);
            LocalOutcomes.Register(_all);
            RoundOutcomes.Register(_all);
            RareOutcomes.Register(_all);

            TotalWeight = _all.Sum(o => o.EffectiveWeight);
        }

        /// <summary>Бросает d(TotalWeight) и возвращает выпавший исход.</summary>
        public static Outcome RollOne()
        {
            if (_all.Count == 0)
                return null;

            int roll = UnityEngine.Random.Range(0, TotalWeight);
            int acc = 0;

            foreach (var o in _all)
            {
                acc += o.EffectiveWeight;
                if (acc > roll)
                    return o;
            }

            return _all[_all.Count - 1];
        }
    }
}
