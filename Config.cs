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

        [Description("Применять Harmony-патчи hint-движка (HintDisplay.Show, Player.ShowHint). Установи false для диагностики проблем подключения игроков, если есть подозрение на конфликт с игрой/другим плагином. При false наш hint-стек работать не будет, но базовые player.ShowHint от EXILED/LabAPI продолжат идти по родному пути игры.")]
        public bool EnableHintEnginePatches { get; set; } = true;

        [Description("САВНЫЙ РЕЖИМ: отключает ВСЕ подсистемы (Coin/Glow/Chat/Goc/...) и Harmony-патчи. Загружает только ядро + EXILED-привязки. Используй для A/B-теста, если игроки не могут зайти на сервер с FermixAPI.")]
        public bool SafeMode { get; set; } = false;

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

        [Description("Включить SSS-хоткеи для 106: Q — toggle stalk, F — портал к ближайшему человеку.")]
        public bool Scp106BindingsEnabled { get; set; } = true;

        // ── FermixGoc (Global Occult Coalition) ─────────────────────

        [Description("Включить G.O.C. — отдельный отряд, враждебный всем (MTF, Chaos, SCP).")]
        public bool GocEnabled { get; set; } = true;

        [Description("С какой минуты раунда может начаться волна G.O.C. Раньше этого времени отряд не прибывает ни при каких ролах.")]
        public float GocWaveStartMinuteThreshold { get; set; } = 15f;

        [Description("Шанс (0..1) того, что очередная MTF-волна после GocWaveStartMinuteThreshold перехватится как G.O.C.-волна. Игроки заспавнятся в MTF-точке, но будут ролью Tutorial и отрядом G.O.C.")]
        public float GocWaveChance { get; set; } = 0.35f;

        [Description("Разрешать только ОДНУ G.O.C.-волну за раунд. Остальные MTF-волны после неё будут обычными. false — каждая MTF-волна перехватывается по собственному роллу.")]
        public bool GocOneWavePerRound { get; set; } = true;

        [Description("Сколько оперативников брать из спектаторов, если команда `goc wave` вызвана, когда живых MTF нет. Спавнятся в MTF-точке.")]
        public int GocManualWaveSize { get; set; } = 5;

        [Description("CASSIE-phonemes для объявления прибытия G.O.C.. Пустое значение — используется встроенный текст. CASSIE говорит английскими фонемами — русский перевод идёт отдельными субтитрами.")]
        public string GocCassiePhonemes { get; set; } = string.Empty;

        [Description("Русские субтитры к CASSIE-объявлению о прибытии G.O.C.. Пустое значение — используется встроенный текст (в нём упоминаются хакерские атаки и неопознанная враждебная группировка).")]
        public string GocCassieSubtitles { get; set; } = string.Empty;

        // ── FermixSquadClasses (кастомные классы внутри отрядов) ────

        [Description("Включить кастомные классы для отрядов NTF и Chaos (Командир/Медик/Джаггернаут/Стрелок-Подрывник). G.O.C.-ранги тоже получают пассивки через эту систему.")]
        public bool SquadClassesEnabled { get; set; } = true;

        [Description("Радиус хил-ауры Медика в метрах. Союзники в этом радиусе с не-полным HP получают регенерацию каждую секунду.")]
        public float SquadClassesMedicRadius { get; set; } = 6f;

        [Description("Сколько HP в секунду восстанавливает Медик союзникам в радиусе. 0 — пассивка отключена.")]
        public float SquadClassesMedicHealPerSec { get; set; } = 5f;

        [Description("Множитель ИСХОДЯЩЕГО урона для Командира. 1.20 = +20% урона по всем целям.")]
        public float SquadClassesCommanderDamageMult { get; set; } = 1.20f;

        [Description("Множитель ВХОДЯЩЕГО урона для Джаггернаута. 0.90 = −10% получаемого урона.")]
        public float SquadClassesJuggernautIncomingMult { get; set; } = 0.90f;
    }
}
