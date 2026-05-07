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
        public const int VersionMinor = 6;
        public const int VersionPatch = 5;
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

            // SafeMode=true — поднимаем только минимум: пути, конфиг, базовые
            // EXILED-хуки и FermixEvents. Никаких подсистем, никаких Harmony-
            // патчей. Используется для A/B-теста, если игроки не могут зайти
            // на сервер с FermixAPI: в этом режиме плагин загружен, но не
            // вмешивается в gameplay.
            bool safeMode = Config?.SafeMode == true;
            if (safeMode)
                FermixLog.Warn("SafeMode=true — все подсистемы FermixAPI и Harmony-патчи отключены. Сброс через config.");

            // Каждая подсистема инициализируется в своём try/catch, чтобы
            // сбой одного модуля (например, при несовместимом обновлении
            // SCP:SL/EXILED/LabAPI) не останавливал инициализацию остальных
            // и не оставлял плагин в полузагруженном состоянии. Имя модуля
            // выводится в лог — пользователю проще локализовать проблему.
            SafeInit("FermixPaths",                () => FermixPaths.Initialize());
            SafeInit("FermixConfigUtils",          () => Utils.FermixConfigUtils.Initialize());
            SafeInit("FermixData",                 () => Utils.FermixData.Initialize());
            SafeInit("FermixConfigSplit",          Systems.FermixConfigSplit.Initialize);

            SafeInit("WaitingForPlayers hook",     () => Handlers.Server.WaitingForPlayers += OnWaitingForPlayers);
            SafeInit("Player.Left hook",           () => Handlers.Player.Left += OnPlayerLeft);

            SafeInit("CheckDependencies",          CheckDependencies);
            SafeInit("FermixEvents.Register",      FermixEvents.Register);
            SafeInit("FermixScheduler",            FermixScheduler.Initialize);

            if (!safeMode)
            {
                SafeInit("FermixHintStack",            FermixHintStack.Initialize);
                SafeInit("FermixInput",                Systems.FermixInput.Initialize);
                SafeInit("FermixGlow",                 Systems.FermixGlow.Initialize);

                SafeInit("FermixRemoteKeycard",        Systems.FermixRemoteKeycard.Initialize);
                SafeInit("FermixChat",                 Systems.FermixChat.Initialize);
                SafeInit("FermixGeneratorHud",         Systems.FermixGeneratorHud.Initialize);
                SafeInit("FermixScramble",             Systems.FermixScramble.Initialize);
                SafeInit("FermixNvg",                  Systems.FermixNvg.Initialize);
                SafeInit("FermixCustomItemHints",      Systems.FermixCustomItemHints.Initialize);
                SafeInit("FermixCallvote",             Systems.FermixCallvote.Initialize);
                SafeInit("FermixGoc",                  Systems.FermixGoc.Initialize);
                SafeInit("FermixSquadClasses",         Systems.FermixSquadClasses.Initialize);
                SafeInit("FermixScp106Bindings",       Systems.FermixScp106Bindings.Initialize);

                SafeInit("FermixInfinity",             Systems.FermixInfinity.Initialize);
                SafeInit("FermixHitmarkers",           Systems.FermixHitmarkers.Initialize);
                SafeInit("FermixPlayerXp",             Systems.FermixPlayerXp.Initialize);
                SafeInit("FermixScpSwap",              Systems.FermixScpSwap.Initialize);
                SafeInit("FermixTeleportRegistry",     Systems.FermixTeleportRegistry.Initialize);

                SafeInit("TpsCommand monitor",         Commands.TpsCommand.StartMonitor);
                SafeInit("RoundStart hook",            () => FermixEvents.OnRoundStart += OnRoundStartedHook);

                SafeInit("CoinManager",                CoinManager.Initialize);
            }

            IsInitialized = true;
            FermixLog.Info($"Ядро FermixAPI v{Version} успешно инициализировано{(safeMode ? " (SafeMode)" : "")}.");
        }

        private static void SafeInit(string moduleName, Action init)
        {
            try
            {
                init();
            }
            catch (Exception ex)
            {
                // Не пробрасываем дальше — это сломает остальные модули.
                // Логируем с именем модуля, чтобы пользователь сразу видел,
                // какую подсистему отключить через config для диагностики.
                FermixLog.Error($"Сбой инициализации модуля '{moduleName}': {ex}");
            }
        }

        private static void SafeShutdown(string moduleName, Action shutdown)
        {
            try
            {
                shutdown();
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"Сбой завершения модуля '{moduleName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Корректное завершение работы API.
        /// </summary>
        public static void Shutdown()
        {
            if (!IsInitialized) return;

            SafeShutdown("WaitingForPlayers hook", () => Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers);
            SafeShutdown("Player.Left hook",       () => Handlers.Player.Left -= OnPlayerLeft);

            SafeShutdown("Harmony unpatch", () =>
            {
                FermixAPI.Hints.Core.Utilities.Patch.Patcher.Unpatch();
                IsHintEnginePatched = false;
            });

            SafeShutdown("StopAllCoroutines",      StopAllCoroutines);
            SafeShutdown("FermixEvents.Unregister", FermixEvents.Unregister);
            SafeShutdown("RoundStart hook",        () => FermixEvents.OnRoundStart -= OnRoundStartedHook);
            SafeShutdown("TpsCommand monitor",     Commands.TpsCommand.StopMonitor);

            SafeShutdown("FermixTeleportRegistry", Systems.FermixTeleportRegistry.Shutdown);
            SafeShutdown("FermixScpSwap",          Systems.FermixScpSwap.Shutdown);
            SafeShutdown("FermixPlayerXp",         Systems.FermixPlayerXp.Shutdown);
            SafeShutdown("FermixHitmarkers",       Systems.FermixHitmarkers.Shutdown);
            SafeShutdown("FermixInfinity",         Systems.FermixInfinity.Shutdown);

            SafeShutdown("FermixScp106Bindings",   Systems.FermixScp106Bindings.Shutdown);
            SafeShutdown("FermixSquadClasses",     Systems.FermixSquadClasses.Shutdown);
            SafeShutdown("FermixGoc",              Systems.FermixGoc.Shutdown);
            SafeShutdown("FermixCallvote",         Systems.FermixCallvote.Shutdown);
            SafeShutdown("FermixNvg",              Systems.FermixNvg.Shutdown);
            SafeShutdown("FermixScramble",         Systems.FermixScramble.Shutdown);
            SafeShutdown("FermixGeneratorHud",     Systems.FermixGeneratorHud.Shutdown);
            SafeShutdown("FermixChat",             Systems.FermixChat.Shutdown);
            SafeShutdown("FermixRemoteKeycard",    Systems.FermixRemoteKeycard.Shutdown);

            SafeShutdown("FermixGlow",             Systems.FermixGlow.Shutdown);
            SafeShutdown("FermixInput",            Systems.FermixInput.Shutdown);
            SafeShutdown("FermixHintStack",        FermixHintStack.Shutdown);

            SafeShutdown("LabApiCommands.Clear",   Integration.LabApiCommands.Clear);
            SafeShutdown("LabApiEvents.ClearAll",  Integration.LabApiEvents.ClearAll);

            SafeShutdown("CoinManager",            CoinManager.Shutdown);
            SafeShutdown("FermixScheduler",        FermixScheduler.Shutdown);

            IsInitialized = false;
            PluginInstance = null;
            FermixLog.Info("FermixAPI успешно завершил работу.");
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
            //
            // SafeMode / EnableHintEnginePatches=false — патчи не применяем
            // вообще, hint-стек работать не будет, но базовый player.ShowHint
            // продолжит идти по родному пути игры. Используется для
            // диагностики: иногда наш Harmony-патч может конфликтовать с
            // другим плагином, и игроков не пускает на сервер.
            bool patchesEnabled = Config?.SafeMode != true && Config?.EnableHintEnginePatches != false;
            if (!patchesEnabled)
            {
                FermixLog.Warn("Harmony-патчи hint-движка пропущены (SafeMode=true или EnableHintEnginePatches=false).");
            }
            else
            {
                try
                {
                    FermixAPI.Hints.Core.Utilities.Patch.Patcher.Patch();
                    IsHintEnginePatched = true;
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"Не удалось применить Harmony-патчи hint-движка: {ex}");
                }
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
