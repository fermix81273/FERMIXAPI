using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Server;
using MEC;

namespace FermixAPI.Core
{
    /// <summary>
    /// Планировщик задач FermixAPI.
    /// Позволяет откладывать выполнение действий, создавать таймеры и повторяющиеся задачи.
    /// </summary>
    public static class FermixScheduler
    {
        private static readonly Dictionary<string, CoroutineHandle> _namedTasks = new Dictionary<string, CoroutineHandle>();
        private static readonly List<CoroutineHandle> _activeTasks = new List<CoroutineHandle>();
        private static bool _isInitialized;

        // Удаляет завершившиеся handle из трекинга и регистрирует новый.
        private static CoroutineHandle Track(CoroutineHandle handle)
        {
            _activeTasks.RemoveAll(h => !h.IsRunning);
            _activeTasks.Add(handle);
            return handle;
        }

        // Сохранённый делегат, чтобы корректно отписаться при Shutdown.
        private static Action<RoundEndedEventArgs> _onRoundEndHandler;

        #region Initialization

        /// <summary>
        /// Инициализирует планировщик.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            // Подписываемся на конец раунда для очистки задач (с возможностью корректной отписки).
            _onRoundEndHandler = _ => ClearRoundTasks();
            FermixEvents.OnRoundEnd += _onRoundEndHandler;

            _isInitialized = true;
            FermixLog.Debug("Планировщик задач инициализирован.");
        }

        /// <summary>
        /// Останавливает планировщик.
        /// </summary>
        public static void Shutdown()
        {
            CancelAll();

            if (_onRoundEndHandler != null)
            {
                FermixEvents.OnRoundEnd -= _onRoundEndHandler;
                _onRoundEndHandler = null;
            }

            _isInitialized = false;
        }

        #endregion

        #region Delay Methods

        /// <summary>
        /// Выполняет действие с задержкой.
        /// </summary>
        /// <param name="delay">Задержка в секундах</param>
        /// <param name="action">Действие для выполнения</param>
        /// <returns>Handle корутины для возможной отмены</returns>
        public static CoroutineHandle Delay(float delay, Action action)
        {
            return Track(Timing.RunCoroutine(DelayedAction(delay, action)));
        }

        /// <summary>
        /// Выполняет действие с задержкой (именованная задача).
        /// </summary>
        public static CoroutineHandle Delay(string name, float delay, Action action)
        {
            // Отменяем предыдущую задачу с таким именем
            Cancel(name);

            var handle = Track(Timing.RunCoroutine(DelayedAction(delay, action), name));
            _namedTasks[name] = handle;
            return handle;
        }

        private static IEnumerator<float> DelayedAction(float delay, Action action)
        {
            yield return Timing.WaitForSeconds(delay);

            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                FermixLog.Exception(ex, "Delay Action");
            }
        }

        #endregion

        #region Repeat Methods

        /// <summary>
        /// Выполняет действие повторно с интервалом.
        /// </summary>
        /// <param name="interval">Интервал в секундах</param>
        /// <param name="action">Действие для выполнения</param>
        /// <param name="count">Количество повторений (-1 = бесконечно)</param>
        public static CoroutineHandle Repeat(float interval, Action action, int count = -1)
        {
            return Track(Timing.RunCoroutine(RepeatedAction(interval, action, count)));
        }

        /// <summary>
        /// Выполняет действие повторно с интервалом (именованная задача).
        /// </summary>
        public static CoroutineHandle Repeat(string name, float interval, Action action, int count = -1)
        {
            Cancel(name);

            var handle = Track(Timing.RunCoroutine(RepeatedAction(interval, action, count), name));
            _namedTasks[name] = handle;
            return handle;
        }

        private static IEnumerator<float> RepeatedAction(float interval, Action action, int count)
        {
            int executed = 0;

            while (count < 0 || executed < count)
            {
                yield return Timing.WaitForSeconds(interval);

                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    FermixLog.Exception(ex, "Repeat Action");
                }

                executed++;
            }
        }

        #endregion

        #region Timer Methods

        /// <summary>
        /// Создает таймер обратного отсчета с callback на каждую секунду.
        /// </summary>
        /// <param name="duration">Длительность в секундах</param>
        /// <param name="onTick">Callback каждую секунду (передается оставшееся время)</param>
        /// <param name="onComplete">Callback по завершении</param>
        public static CoroutineHandle Countdown(float duration, Action<float> onTick, Action onComplete = null)
        {
            return Track(Timing.RunCoroutine(CountdownCoroutine(duration, onTick, onComplete)));
        }

        private static IEnumerator<float> CountdownCoroutine(float duration, Action<float> onTick, Action onComplete)
        {
            float remaining = duration;

            while (remaining > 0)
            {
                try
                {
                    onTick?.Invoke(remaining);
                }
                catch (Exception ex)
                {
                    FermixLog.Exception(ex, "Countdown Tick");
                }

                yield return Timing.WaitForSeconds(1f);
                remaining -= 1f;
            }

            try
            {
                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                FermixLog.Exception(ex, "Countdown Complete");
            }
        }

        #endregion

        #region Next Frame / Tick Methods

        /// <summary>
        /// Выполняет действие на следующем кадре.
        /// </summary>
        public static CoroutineHandle NextFrame(Action action)
        {
            return Timing.RunCoroutine(NextFrameCoroutine(action));
        }

        private static IEnumerator<float> NextFrameCoroutine(Action action)
        {
            yield return Timing.WaitForOneFrame;
            action?.Invoke();
        }

        /// <summary>
        /// Выполняет действие в конце текущего кадра.
        /// </summary>
        public static CoroutineHandle EndOfFrame(Action action)
        {
            return Timing.CallDelayed(0f, action);
        }

        #endregion

        #region Conditional Methods

        /// <summary>
        /// Ждет пока условие станет истинным, затем выполняет действие.
        /// </summary>
        /// <param name="condition">Условие для проверки</param>
        /// <param name="action">Действие для выполнения</param>
        /// <param name="checkInterval">Интервал проверки в секундах</param>
        /// <param name="timeout">Максимальное время ожидания (-1 = бесконечно)</param>
        public static CoroutineHandle WaitUntil(Func<bool> condition, Action action, float checkInterval = 0.1f, float timeout = -1f)
        {
            return Track(Timing.RunCoroutine(WaitUntilCoroutine(condition, action, checkInterval, timeout)));
        }

        private static IEnumerator<float> WaitUntilCoroutine(Func<bool> condition, Action action, float checkInterval, float timeout)
        {
            float elapsed = 0f;

            while (true)
            {
                bool done;
                try
                {
                    done = condition?.Invoke() == true;
                }
                catch (Exception ex)
                {
                    // Не валим всю корутину — пользователь мог положить в условие
                    // обращение к Player, который уже отвалился. Просто завершаем.
                    FermixLog.Exception(ex, "WaitUntil Condition");
                    yield break;
                }

                if (done) break;

                yield return Timing.WaitForSeconds(checkInterval);
                elapsed += checkInterval;

                if (timeout > 0 && elapsed >= timeout)
                {
                    FermixLog.Debug("WaitUntil: Таймаут");
                    yield break;
                }
            }

            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                FermixLog.Exception(ex, "WaitUntil Action");
            }
        }

        /// <summary>
        /// Выполняет действие пока условие истинно.
        /// </summary>
        public static CoroutineHandle While(Func<bool> condition, Action action, float interval = 0.1f)
        {
            return Track(Timing.RunCoroutine(WhileCoroutine(condition, action, interval)));
        }

        private static IEnumerator<float> WhileCoroutine(Func<bool> condition, Action action, float interval)
        {
            while (true)
            {
                bool keep;
                try
                {
                    keep = condition?.Invoke() == true;
                }
                catch (Exception ex)
                {
                    // Условие швырнуло — завершаем корутину штатно вместо
                    // тихой гибели в MEC.
                    FermixLog.Exception(ex, "While Condition");
                    yield break;
                }

                if (!keep) yield break;

                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    FermixLog.Exception(ex, "While Action");
                }

                yield return Timing.WaitForSeconds(interval);
            }
        }

        #endregion

        #region Player-Specific Tasks

        /// <summary>
        /// Выполняет действие для игрока с задержкой.
        /// </summary>
        public static CoroutineHandle DelayForPlayer(Player player, float delay, Action<Player> action)
        {
            return Delay(delay, () =>
            {
                if (player != null && player.IsConnected)
                {
                    action?.Invoke(player);
                }
            });
        }

        /// <summary>
        /// Повторяет действие для игрока пока он онлайн.
        /// </summary>
        public static CoroutineHandle RepeatForPlayer(Player player, float interval, Action<Player> action)
        {
            return While(
                () => player != null && player.IsConnected,
                () => action?.Invoke(player),
                interval
            );
        }

        #endregion

        #region Cancellation

        /// <summary>
        /// Отменяет именованную задачу.
        /// </summary>
        public static bool Cancel(string name)
        {
            if (_namedTasks.TryGetValue(name, out var handle))
            {
                Timing.KillCoroutines(handle);
                _namedTasks.Remove(name);
                _activeTasks.Remove(handle);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Отменяет задачу по handle.
        /// </summary>
        public static void Cancel(CoroutineHandle handle)
        {
            Timing.KillCoroutines(handle);
            _activeTasks.Remove(handle);
        }

        /// <summary>
        /// Отменяет все задачи.
        /// </summary>
        public static void CancelAll()
        {
            foreach (var handle in _activeTasks)
            {
                Timing.KillCoroutines(handle);
            }

            _activeTasks.Clear();
            _namedTasks.Clear();

            FermixLog.Debug("Все запланированные задачи отменены.");
        }

        /// <summary>
        /// Очищает задачи текущего раунда.
        /// </summary>
        private static void ClearRoundTasks()
        {
            CancelAll();
            FermixLog.Debug("Задачи раунда очищены.");
        }

        #endregion

        #region Info

        /// <summary>
        /// Количество активных задач.
        /// </summary>
        public static int ActiveTaskCount
        {
            get
            {
                _activeTasks.RemoveAll(h => !h.IsRunning);
                return _activeTasks.Count;
            }
        }

        /// <summary>
        /// Проверяет, существует ли именованная задача.
        /// </summary>
        public static bool HasTask(string name) => _namedTasks.ContainsKey(name);

        #endregion
    }
}
