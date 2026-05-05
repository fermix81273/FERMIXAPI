using Exiled.API.Interfaces;
using System.ComponentModel;

namespace FermixAPI
{
    /// <summary>
    /// Конфигурация FermixAPI с расширенными настройками.
    /// </summary>
    public sealed class Config : IConfig
    {
        [Description("Включить или выключить FermixAPI")]
        public bool IsEnabled { get; set; } = true;

        [Description("Режим отладки - выводит дополнительную информацию в консоль")]
        public bool Debug { get; set; } = false;

        [Description("Показывать ASCII-логотип при запуске")]
        public bool ShowLogo { get; set; } = true;

        [Description("Показывать информацию о зависимостях при запуске")]
        public bool ShowDependencyInfo { get; set; } = true;

        [Description("Автоматически интегрироваться с HintServiceMeow если доступен")]
        public bool AutoIntegrateHSM { get; set; } = true;

        [Description("Автоматически интегрироваться с LabAPI если доступен")]
        public bool AutoIntegrateLabAPI { get; set; } = true;

        [Description("Логировать все действия API (для отладки)")]
        public bool LogAllActions { get; set; } = false;

        [Description("Максимальное количество отложенных задач в очереди")]
        public int MaxScheduledTasks { get; set; } = 100;

        // ── FermixCoin ──────────────────────────────────────────────

        [Description("Включить модуль FermixCoin (монетка).")]
        public bool CoinEnabled { get; set; } = true;

        [Description("Максимальное количество подкидываний одной монетки до того как она «истратится» и пропадёт. Реальное число для конкретной монетки — случайное от 1 до этого значения.")]
        public int CoinMaxUses { get; set; } = 5;

        [Description("Шанс мега-джекпота: одновременно срабатывают ВСЕ одобренные исходы. Дробное значение (1.0 = 100%, 0.0001 = 0.01%).")]
        public double MegaJackpotChance { get; set; } = 0.0001;

        [Description("Подсветка монетки цветом редкости следующего исхода. Easter egg — про фичу мало кто знает.")]
        public bool RarityGlowEnabled { get; set; } = true;

        [Description("Показывать ли при выпадении исхода прикольный комментарий (хинт) дополнительно к основному сообщению.")]
        public bool ShowCommentHints { get; set; } = true;

        [Description("Глобальный broadcast при срабатывании мега-джекпота.")]
        public bool BroadcastMegaJackpot { get; set; } = true;
    }
}
