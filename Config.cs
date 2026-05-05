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

        [Description("Автоспавн монеток в случайных комнатах комплекса после старта раунда.")]
        public bool CoinAutoSpawnEnabled { get; set; } = true;

        [Description("Сколько монеток рассыпать по комплексу при автоспавне.")]
        public int CoinAutoSpawnCount { get; set; } = 8;

        [Description("Задержка после старта раунда (в секундах) перед автоспавном монеток.")]
        public float CoinAutoSpawnDelay { get; set; } = 5f;

        // ── FermixRemoteKeycard ─────────────────────────────────────

        [Description("Разрешить открывать двери/шкафы/генераторы, когда подходящая карта лежит в инвентаре, но не в руках.")]
        public bool RemoteKeycardEnabled { get; set; } = true;

        [Description("Применять «удалённую» карту к дверям.")]
        public bool RemoteKeycardWorksOnDoors { get; set; } = true;

        [Description("Применять «удалённую» карту к шкафчикам.")]
        public bool RemoteKeycardWorksOnLockers { get; set; } = true;

        [Description("Применять «удалённую» карту к генераторам (баланс — может быть слишком сильно).")]
        public bool RemoteKeycardWorksOnGenerators { get; set; } = false;

        [Description("Показывать игроку короткий хинт, какая карта была применена.")]
        public bool RemoteKeycardShowHint { get; set; } = true;

        // ── FermixChat ──────────────────────────────────────────────

        [Description("Включить глобальный текстовый чат через консольную команду .say (псевдоним .s).")]
        public bool ChatEnabled { get; set; } = true;

        [Description("Сколько последних сообщений показывать в окне чата.")]
        public int ChatHistorySize { get; set; } = 6;

        [Description("Сколько секунд каждое сообщение остаётся в окне чата.")]
        public float ChatMessageLifetime { get; set; } = 12f;

        [Description("Минимальный интервал между сообщениями одного игрока (анти-флуд, секунды).")]
        public float ChatCooldown { get; set; } = 3f;

        [Description("Максимальная длина одного сообщения (символов).")]
        public int ChatMaxLength { get; set; } = 160;

        // ── FermixGeneratorHud ──────────────────────────────────────

        [Description("Показывать SCP-командe HUD с активирующимися генераторами и таймером до окончательного запуска.")]
        public bool GeneratorHudEnabled { get; set; } = true;

        [Description("Интервал обновления HUD генераторов (секунды).")]
        public float GeneratorHudUpdateInterval { get; set; } = 1f;

        // ── FermixScramble (SCP-1344 как глушитель 096) ─────────────

        [Description("Включить SCP-1344 как глушитель триггера SCP-096 (взгляд на лицо не делает целью, если предмет в инвентаре).")]
        public bool ScrambleEnabled { get; set; } = true;

        [Description("Сколько SCP-1344 рассыпать по комплексу при старте раунда.")]
        public int ScrambleSpawnCount { get; set; } = 2;

        [Description("Задержка после старта раунда перед спавном SCP-1344 (секунды).")]
        public float ScrambleSpawnDelay { get; set; } = 4f;

        // ── FermixCallvote ──────────────────────────────────────────

        [Description("Включить голосования игроков (.cv kick/restart/ask + .vote yes/no).")]
        public bool CallvoteEnabled { get; set; } = true;

        [Description("Длительность одного голосования (секунды).")]
        public float CallvoteDuration { get; set; } = 30f;

        [Description("Минимальный интервал между голосованиями (секунды).")]
        public float CallvoteCooldown { get; set; } = 60f;

        // ── FermixScp106Plus ────────────────────────────────────────

        [Description("Включить расширения SCP-106: .106 stalk и .106 tp <комната>.")]
        public bool Scp106PlusEnabled { get; set; } = true;

        [Description("Стоимость Vigor для телепорта 106 в выбранную комнату.")]
        public float Scp106PlusVigorCost { get; set; } = 0.3f;

        // ── FermixGoc (Global Occult Coalition) ─────────────────────

        [Description("Включить G.O.C. — отдельный отряд, враждебный всем (MTF, Chaos, SCP).")]
        public bool GocEnabled { get; set; } = true;

        [Description("Шанс (0..1) того, что прибывшая волна MTF превратится в G.O.C.")]
        public float GocWaveChance { get; set; } = 0.1f;
    }
}
