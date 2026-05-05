using System;
using System.Linq;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using MEC;
using FermixAPI.FermixCoin;
using Handlers = Exiled.Events.Handlers;

// Alias to avoid ambiguity
using FermixPlugin = FermixAPI.Plugin;

namespace FermixAPI.Core
{
    /// <summary>
    /// Центральное ядро FermixAPI.
    /// Управляет инициализацией, зависимостями и жизненным циклом API.
    /// </summary>
    public static class FermixCore
    {
        #region Version Info

        public const int VersionMajor = 2;
        public const int VersionMinor = 4;
        public const int VersionPatch = 0;
        public const string VersionSuffix = "release";

        /// <summary>
        /// Минимальная требуемая версия EXILED.
        /// </summary>
        public static readonly Version MinimumExiledVersion = new Version(9, 13, 3);

        /// <summary>
        /// Минимальная требуемая версия LabAPI.
        /// </summary>
        public static readonly Version MinimumLabApiVersion = new Version(1, 1, 6);

        /// <summary>
        /// Полная строка версии API.
        /// </summary>
        public static string Version => $"{VersionMajor}.{VersionMinor}.{VersionPatch}-{VersionSuffix}";

        #endregion

        #region State

        /// <summary>
        /// Ссылка на экземпляр плагина.
        /// </summary>
        public static FermixPlugin PluginInstance { get; private set; }

        /// <summary>
        /// Конфигурация API.
        /// </summary>
        public static Config Config => PluginInstance?.Config;

        /// <summary>
        /// Инициализировано ли ядро.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// Активные корутины API.
        /// </summary>
        private static readonly List<CoroutineHandle> _activeCoroutines = new List<CoroutineHandle>();

        #endregion

        #region Dependencies

        /// <summary>
        /// Доступен ли HintServiceMeow.
        /// </summary>
        public static bool IsHintServiceMeowAvailable { get; private set; }

        /// <summary>
        /// Доступен ли LabAPI.
        /// </summary>
        public static bool IsLabAPIAvailable { get; private set; }

        /// <summary>
        /// Доступен ли MapEditorReborn.
        /// </summary>
        public static bool IsMapEditorRebornAvailable { get; private set; }

        /// <summary>
        /// Доступен ли SCPStats.
        /// </summary>
        public static bool IsSCPStatsAvailable { get; private set; }

        /// <summary>
        /// Доступен ли RespawnTimer.
        /// </summary>
        public static bool IsRespawnTimerAvailable { get; private set; }

        #endregion

        #region Initialization

        /// <summary>
        /// Инициализация ядра FermixAPI.
        /// </summary>
        /// <param name="plugin">Экземпляр плагина</param>
        public static void Initialize(FermixPlugin plugin)
        {
            if (IsInitialized)
            {
                FermixLog.Warn("Ядро уже инициализировано. Пропуск повторной инициализации.");
                return;
            }

            PluginInstance = plugin;

            try
            {
                // Создаём стандартную структуру каталогов FermixAPI
                FermixPaths.Initialize();
                Utils.FermixConfigUtils.Initialize();
                Utils.FermixData.Initialize();

                // Подписываемся на событие ожидания игроков для вывода логотипа
                // и инициализации движка хинтов (FermixAPI.Hints).
                Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;

                // На уход игрока — освобождаем его PlayerDisplay в hint-движке.
                Handlers.Player.Left += OnPlayerLeft;

                // Проверяем зависимости
                CheckDependencies();

                // Регистрируем события
                FermixEvents.Register();

                // Инициализируем планировщик задач
                FermixScheduler.Initialize();

                // Запускаем стек хинтов, SSS-биндинги и кастом-подсветку
                FermixHintStack.Initialize();
                Systems.FermixInput.Initialize();
                Systems.FermixGlow.Initialize();

                // Адаптации сторонних плагинов под нашу архитектуру
                Systems.FermixRemoteKeycard.Initialize();
                Systems.FermixChat.Initialize();
                Systems.FermixGeneratorHud.Initialize();
                Systems.FermixScramble.Initialize();
                Systems.FermixCallvote.Initialize();
                Systems.FermixGoc.Initialize();

                // Монитор TPS + сброс кулдауна воскрешения на старте раунда
                Commands.TpsCommand.StartMonitor();
                FermixEvents.OnRoundStart += OnRoundStartedHook;

                // Инициализация встроенного модуля FermixCoin
                CoinManager.Initialize();

                IsInitialized = true;

                FermixLog.Info($"Ядро FermixAPI v{Version} успешно инициализировано.");
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Критическая ошибка при инициализации ядра: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Корректное завершение работы API.
        /// </summary>
        public static void Shutdown()
        {
            if (!IsInitialized) return;

            try
            {
                // Отписываемся от событий сервера
                Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
                Handlers.Player.Left -= OnPlayerLeft;

                // Снимаем все Harmony-патчи hint-движка, чтобы при reload'е
                // плагина не остаться с битыми ссылками внутри Exiled.API.
                try
                {
                    FermixAPI.Hints.Core.Utilities.Patch.Patcher.Unpatch();
                    IsHintEnginePatched = false;
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"Не удалось снять патчи hint-движка: {ex.Message}");
                }

                // Останавливаем все корутины
                StopAllCoroutines();

                // Отписываемся от событий
                FermixEvents.Unregister();

                // Останавливаем стек хинтов, SSS-биндинги и подсветку
                FermixEvents.OnRoundStart -= OnRoundStartedHook;
                Commands.TpsCommand.StopMonitor();

                // Адаптации сторонних плагинов
                Systems.FermixGoc.Shutdown();
                Systems.FermixCallvote.Shutdown();
                Systems.FermixScramble.Shutdown();
                Systems.FermixGeneratorHud.Shutdown();
                Systems.FermixChat.Shutdown();
                Systems.FermixRemoteKeycard.Shutdown();

                Systems.FermixGlow.Shutdown();
                Systems.FermixInput.Shutdown();
                FermixHintStack.Shutdown();

                // Очищаем LabAPI-регистрации
                Integration.LabApiCommands.Clear();
                Integration.LabApiEvents.ClearAll();

                // Выключаем встроенный модуль FermixCoin
                CoinManager.Shutdown();

                // Останавливаем планировщик
                FermixScheduler.Shutdown();

                IsInitialized = false;
                PluginInstance = null;

                FermixLog.Info("FermixAPI успешно завершил работу.");
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Ошибка при завершении работы: {ex}");
            }
        }

        /// <summary>
        /// Проверяет наличие зависимостей.
        /// </summary>
        private static void CheckDependencies()
        {
            var plugins = Exiled.Loader.Loader.Plugins;

            IsHintServiceMeowAvailable = plugins.Any(p => p.Name.Contains("HintServiceMeow"));
            IsMapEditorRebornAvailable = plugins.Any(p => p.Name.Contains("MapEditorReborn"));
            IsSCPStatsAvailable = plugins.Any(p => p.Name.Contains("SCPStats"));
            IsRespawnTimerAvailable = plugins.Any(p => p.Name.Contains("RespawnTimer"));

            // LabAPI обнаруживается по сборке, а не по плагину EXILED:
            // в актуальных версиях SCP:SL LabAPI грузится отдельным
            // загрузчиком и в Exiled.Loader.Loader.Plugins может отсутствовать.
            Integration.LabApiIntegration.Initialize();
            IsLabAPIAvailable = Integration.LabApiIntegration.IsAvailable
                                || plugins.Any(p => p.Name.Contains("LabAPI") || p.Name.Contains("Lab API"));
        }

        /// <summary>
        /// Обработчик старта раунда — сбрасывает кулдауны воскрешения.
        /// </summary>
        private static void OnRoundStartedHook()
        {
            Commands.ResurrectCommand.ResetCooldowns();
        }

        // Считаем, что патчи hint-движка применены — нужно для корректного
        // снятия в Shutdown и для проверки в логах. Повторный Patcher.Patch()
        // безопасен (он сам делает Unpatch перед Patch).
        /// <summary>
        /// Применены ли Harmony-патчи hint-движка (FermixAPI.Hints).
        /// </summary>
        public static bool IsHintEnginePatched { get; private set; }

        /// <summary>
        /// Обработчик события ожидания игроков.
        /// </summary>
        private static void OnWaitingForPlayers()
        {
            // Patcher hint-движка должен быть применён ДО первого вызова
            // player.ShowHint в раунде, иначе мы пропустим первые хинты
            // (стартовые spawn-сообщения и т.п.). WaitingForPlayers — самое
            // раннее серверное событие, на котором всё уже инициализировано.
            try
            {
                FermixAPI.Hints.Core.Utilities.Patch.Patcher.Patch();
                IsHintEnginePatched = true;
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Не удалось применить Harmony-патчи hint-движка: {ex}");
            }

            if (Config?.ShowLogo == true)
            {
                FermixLog.DrawLogo();
            }

            if (Config?.ShowDependencyInfo == true)
            {
                LogDependencies();
            }
        }

        /// <summary>
        /// При уходе игрока освобождаем его PlayerDisplay у hint-движка,
        /// чтобы не утекали ссылки на ReferenceHub после disconnect'а.
        /// </summary>
        private static void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            try
            {
                FermixAPI.Hints.Core.Utilities.PlayerDisplay.Destruct(ev?.Player?.ReferenceHub);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"PlayerDisplay.Destruct: {ex.Message}");
            }
        }

        /// <summary>
        /// Выводит информацию о зависимостях.
        /// </summary>
        private static void LogDependencies()
        {
            FermixLog.Info("=== Обнаруженные интеграции ===");
            FermixLog.Info($"  Минимальная версия EXILED:  {MinimumExiledVersion}");
            FermixLog.Info($"  Минимальная версия LabAPI:  {MinimumLabApiVersion}");

            LogDependency("HintServiceMeow", IsHintServiceMeowAvailable);

            var labApiVersion = Integration.LabApiIntegration.Version;
            var labApiSuffix = labApiVersion != null ? $" v{labApiVersion}" : string.Empty;
            LogDependency($"LabAPI{labApiSuffix}", IsLabAPIAvailable);

            LogDependency("MapEditorReborn", IsMapEditorRebornAvailable);
            LogDependency("SCPStats", IsSCPStatsAvailable);
            LogDependency("RespawnTimer", IsRespawnTimerAvailable);

            FermixLog.Success($"FermixAPI v{Version} готов к работе!");
        }

        private static void LogDependency(string name, bool available)
        {
            if (available)
                FermixLog.Success($"  {name}: АКТИВЕН");
            else if (Config?.Debug == true)
                FermixLog.Info($"  {name}: не найден");
        }

        #endregion

        #region Coroutine Management

        /// <summary>
        /// Запускает корутину и отслеживает её.
        /// </summary>
        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, string tag = null)
        {
            PruneCompletedCoroutines();

            var handle = string.IsNullOrEmpty(tag)
                ? Timing.RunCoroutine(coroutine)
                : Timing.RunCoroutine(coroutine, tag);

            _activeCoroutines.Add(handle);
            return handle;
        }

        /// <summary>
        /// Останавливает корутину.
        /// </summary>
        public static void StopCoroutine(CoroutineHandle handle)
        {
            Timing.KillCoroutines(handle);
            _activeCoroutines.Remove(handle);
        }

        /// <summary>
        /// Останавливает все корутины API.
        /// </summary>
        public static void StopAllCoroutines()
        {
            foreach (var handle in _activeCoroutines)
            {
                Timing.KillCoroutines(handle);
            }
            _activeCoroutines.Clear();
        }

        /// <summary>
        /// Удаляет из трекинга корутины, которые уже завершились естественно.
        /// </summary>
        private static void PruneCompletedCoroutines()
        {
            _activeCoroutines.RemoveAll(h => !h.IsRunning);
        }

        /// <summary>
        /// Текущее количество активных корутин (после очистки от завершившихся).
        /// </summary>
        public static int ActiveCoroutineCount
        {
            get
            {
                PruneCompletedCoroutines();
                return _activeCoroutines.Count;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Гарантирует, что API инициализирован.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("FermixAPI не инициализирован. Убедитесь, что плагин включен.");
            }
        }

        /// <summary>
        /// Проверяет плагин по имени.
        /// </summary>
        public static bool IsPluginLoaded(string pluginName)
        {
            return Exiled.Loader.Loader.Plugins.Any(p =>
                p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(pluginName));
        }

        /// <summary>
        /// Получает плагин по имени.
        /// </summary>
        public static IPlugin<IConfig> GetPlugin(string pluginName)
        {
            return Exiled.Loader.Loader.Plugins.FirstOrDefault(p =>
                p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(pluginName));
        }

        #endregion
    }
}
