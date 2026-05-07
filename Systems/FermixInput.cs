using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using FermixAPI.Core;
using MEC;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Универсальная обёртка над Server Specific Settings (SSS) для регистрации
    /// клавиатурных биндов и подписки на их нажатия / отпускания / удержания.
    ///
    /// Не привязан к конкретным предметам — это чистый API. Потребитель плагина
    /// сам решает, что делать в обработчиках.
    /// </summary>
    public static class FermixInput
    {
        #region Default Button IDs - Стандартные ID кнопок

        /// <summary>ID заголовка раздела SSS.</summary>
        public const int HeaderId = 299;

        /// <summary>ID бинда левой кнопки мыши.</summary>
        public const int Lmb = 300;

        /// <summary>ID бинда правой кнопки мыши.</summary>
        public const int Rmb = 301;

        /// <summary>ID бинда клавиши R.</summary>
        public const int R = 302;

        /// <summary>ID бинда клавиши Alt.</summary>
        public const int Alt = 303;

        /// <summary>ID бинда клавиши Q.</summary>
        public const int Q = 304;

        /// <summary>ID бинда клавиши F.</summary>
        public const int F = 305;

        /// <summary>ID бинда клавиши T.</summary>
        public const int T = 306;

        #endregion

        #region State

        private static bool _initialized;

        // Зарегистрированные настройки SSS (для последующего unregister).
        private static readonly List<SettingBase> _settings = new List<SettingBase>();

        // Состояние нажатия каждой кнопки на каждого игрока.
        private static readonly Dictionary<Player, Dictionary<int, ButtonState>> _playerButtons
            = new Dictionary<Player, Dictionary<int, ButtonState>>();

        // Активные корутины hold-tracking (по игроку).
        private static readonly Dictionary<Player, CoroutineHandle> _holdCoroutines
            = new Dictionary<Player, CoroutineHandle>();

        // Дополнительные обработчики, регистрируемые потребителем по buttonId.
        private static readonly Dictionary<int, List<Action<Player>>> _pressedHandlers
            = new Dictionary<int, List<Action<Player>>>();

        private static readonly Dictionary<int, List<Action<Player>>> _releasedHandlers
            = new Dictionary<int, List<Action<Player>>>();

        private static readonly Dictionary<int, List<Action<Player>>> _heldHandlers
            = new Dictionary<int, List<Action<Player>>>();

        /// <summary>Инициализирована ли система.</summary>
        public static bool IsInitialized => _initialized;

        #endregion

        #region Events - Глобальные события

        /// <summary>Игрок нажал зарегистрированную кнопку.</summary>
        public static event Action<Player, int> OnPressed;

        /// <summary>Игрок отпустил зарегистрированную кнопку.</summary>
        public static event Action<Player, int> OnReleased;

        /// <summary>Игрок удерживает зарегистрированную кнопку (тик ~50 мс).</summary>
        public static event Action<Player, int> OnHeld;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Инициализирует систему ввода: регистрирует стандартные бинды (LMB/RMB/R/Alt/Q/F/T)
        /// и подписывается на события покидания игрока.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                FermixLog.Warn("FermixInput уже инициализирован.");
                return;
            }

            try
            {
                RegisterDefaultKeybinds();
                Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
                _initialized = true;
                FermixLog.Debug("FermixInput инициализирован.");
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Ошибка инициализации FermixInput: {ex}");
            }
        }

        /// <summary>
        /// Останавливает систему: снимает все настройки SSS, чистит обработчики и
        /// активные корутины.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            try
            {
                Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;

                if (_settings.Count > 0)
                {
                    SettingBase.Unregister(settings: _settings);
                    _settings.Clear();
                }

                foreach (var handle in _holdCoroutines.Values)
                {
                    Timing.KillCoroutines(handle);
                }
                _holdCoroutines.Clear();
                _playerButtons.Clear();
                _pressedHandlers.Clear();
                _releasedHandlers.Clear();
                _heldHandlers.Clear();

                _initialized = false;
                FermixLog.Debug("FermixInput остановлен.");
            }
            catch (Exception ex)
            {
                FermixLog.Error($"Ошибка остановки FermixInput: {ex}");
            }
        }

        #endregion

        #region Registration API

        /// <summary>
        /// Регистрирует стандартный набор биндов (LMB / RMB / R / Alt / Q / F / T).
        /// Вызывается автоматически из <see cref="Initialize"/>; повторно вызывать не нужно.
        /// </summary>
        private static void RegisterDefaultKeybinds()
        {
            var header = new HeaderSetting(HeaderId, "FermixAPI: бинды действий", string.Empty, false);
            _settings.Add(header);

            _settings.Add(MakeKeybind(Lmb, "ЛКМ",  KeyCode.Mouse0,    header, "Бинд для действия левой кнопкой мыши"));
            _settings.Add(MakeKeybind(Rmb, "ПКМ",  KeyCode.Mouse1,    header, "Бинд для действия правой кнопкой мыши"));
            _settings.Add(MakeKeybind(R,   "R",    KeyCode.R,         header, "Бинд для действия R"));
            _settings.Add(MakeKeybind(Alt, "ALT",  KeyCode.LeftAlt,   header, "Бинд для действия ALT"));
            _settings.Add(MakeKeybind(Q,   "Q",    KeyCode.Q,         header, "Бинд для действия Q"));
            _settings.Add(MakeKeybind(F,   "F",    KeyCode.F,         header, "Бинд для действия F"));
            _settings.Add(MakeKeybind(T,   "T",    KeyCode.T,         header, "Бинд для действия T"));

            // Идемпотентная регистрация: если EXILED перезагружал сборку, в
            // ServerSpecificSettingsSync.DefinedSettings мог остаться предыдущий
            // экземпляр наших настроек — выкидываем дубликаты по id, иначе в
            // SSS-меню игроку показывается «FERMIXAPI: БИНДЫ ДЕЙСТВИЙ» дважды.
            DropExistingByIds(_settings.Select(s => s.Id));
            SettingBase.Register(_settings);
        }

        /// <summary>
        /// Удалить из <see cref="ServerSpecificSettingsSync.DefinedSettings"/>
        /// все настройки, чей id входит в указанный набор. Используется перед
        /// повторной регистрацией, чтобы не получать дубликаты в SSS-меню.
        /// </summary>
        internal static void DropExistingByIds(IEnumerable<int> ids)
        {
            try
            {
                var idSet = new HashSet<int>(ids);
                var existing = ServerSpecificSettingsSync.DefinedSettings;
                if (existing == null || existing.Length == 0) return;

                var filtered = existing.Where(s => s != null && !idSet.Contains(s.SettingId)).ToArray();
                if (filtered.Length == existing.Length) return;

                ServerSpecificSettingsSync.DefinedSettings = filtered;
                SettingBase.SendToAll();
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixInput.DropExistingByIds: {ex.Message}");
            }
        }

        /// <summary>
        /// Регистрирует пользовательский бинд через FermixInput.
        /// </summary>
        /// <param name="id">Уникальный ID бинда (>= 307, чтобы не конфликтовать со стандартными).</param>
        /// <param name="label">Подпись в меню SSS.</param>
        /// <param name="defaultKey">Клавиша по умолчанию.</param>
        /// <param name="description">Описание (показывается в SSS как hint).</param>
        /// <returns>Зарегистрированный <see cref="KeybindSetting"/>.</returns>
        public static KeybindSetting RegisterCustomKeybind(int id, string label, KeyCode defaultKey, string description = "")
        {
            EnsureInitialized();
            var setting = MakeKeybind(id, label, defaultKey, header: null, description);
            _settings.Add(setting);
            SettingBase.Register(new[] { (SettingBase)setting });
            return setting;
        }

        /// <summary>
        /// Регистрирует обработчик нажатия конкретной кнопки.
        /// </summary>
        public static void RegisterPressedHandler(int buttonId, Action<Player> handler)
            => AddHandler(_pressedHandlers, buttonId, handler);

        /// <summary>
        /// Снимает обработчик нажатия конкретной кнопки.
        /// </summary>
        public static void UnregisterPressedHandler(int buttonId, Action<Player> handler)
            => RemoveHandler(_pressedHandlers, buttonId, handler);

        /// <summary>
        /// Регистрирует обработчик отпускания конкретной кнопки.
        /// </summary>
        public static void RegisterReleasedHandler(int buttonId, Action<Player> handler)
            => AddHandler(_releasedHandlers, buttonId, handler);

        /// <summary>
        /// Снимает обработчик отпускания конкретной кнопки.
        /// </summary>
        public static void UnregisterReleasedHandler(int buttonId, Action<Player> handler)
            => RemoveHandler(_releasedHandlers, buttonId, handler);

        /// <summary>
        /// Регистрирует обработчик удержания конкретной кнопки. Тик ~50 мс пока кнопка зажата.
        /// </summary>
        public static void RegisterHeldHandler(int buttonId, Action<Player> handler)
            => AddHandler(_heldHandlers, buttonId, handler);

        /// <summary>
        /// Снимает обработчик удержания конкретной кнопки.
        /// </summary>
        public static void UnregisterHeldHandler(int buttonId, Action<Player> handler)
            => RemoveHandler(_heldHandlers, buttonId, handler);

        #endregion

        #region Query API

        /// <summary>
        /// Зажата ли указанная кнопка у игрока в данный момент.
        /// </summary>
        public static bool IsButtonPressed(Player player, int buttonId)
        {
            if (player == null) return false;
            return _playerButtons.TryGetValue(player, out var buttons)
                && buttons.TryGetValue(buttonId, out var state)
                && state.IsPressed;
        }

        /// <summary>
        /// Возвращает время с момента последнего нажатия / отпускания кнопки или
        /// <see cref="TimeSpan.Zero"/>, если кнопку никогда не трогали.
        /// </summary>
        public static TimeSpan SinceLastChange(Player player, int buttonId)
        {
            if (player == null) return TimeSpan.Zero;
            if (_playerButtons.TryGetValue(player, out var buttons)
                && buttons.TryGetValue(buttonId, out var state))
            {
                return DateTime.UtcNow - state.LastChange;
            }
            return TimeSpan.Zero;
        }

        #endregion

        #region Internals

        /// <summary>Состояние одной кнопки одного игрока.</summary>
        private sealed class ButtonState
        {
            public bool IsPressed { get; set; }
            public DateTime LastChange { get; set; } = DateTime.UtcNow;
        }

        private static KeybindSetting MakeKeybind(int id, string label, KeyCode defaultKey, HeaderSetting header, string description)
        {
            return new KeybindSetting(
                id: id,
                label: label,
                suggested: defaultKey,
                preventInteractionOnGUI: false,
                allowSpectatorTrigger: false,
                hintDescription: description,
                collectionId: byte.MaxValue,
                header: header,
                onChanged: OnKeybindChanged);
        }

        private static void OnKeybindChanged(Player player, SettingBase setting)
        {
            if (player == null || setting is not KeybindSetting keybind)
                return;

            HandleKeybind(player, keybind.Id, keybind.IsPressed);
        }

        private static void HandleKeybind(Player player, int buttonId, bool isPressed)
        {
            if (!_playerButtons.TryGetValue(player, out var buttons))
            {
                buttons = new Dictionary<int, ButtonState>();
                _playerButtons[player] = buttons;
            }

            if (!buttons.TryGetValue(buttonId, out var state))
            {
                state = new ButtonState();
                buttons[buttonId] = state;
            }

            state.IsPressed = isPressed;
            state.LastChange = DateTime.UtcNow;

            if (isPressed)
            {
                FirePressed(player, buttonId);

                // Перезапускаем корутину удержания.
                if (_holdCoroutines.TryGetValue(player, out var existing))
                    Timing.KillCoroutines(existing);

                _holdCoroutines[player] = Timing.RunCoroutine(TrackHolding(player, buttonId));
            }
            else
            {
                FireReleased(player, buttonId);

                if (_holdCoroutines.TryGetValue(player, out var existing))
                {
                    Timing.KillCoroutines(existing);
                    _holdCoroutines.Remove(player);
                }
            }
        }

        private static IEnumerator<float> TrackHolding(Player player, int buttonId)
        {
            // Небольшая задержка перед началом срабатывания "hold", чтобы
            // одиночный клик не считался удержанием.
            yield return Timing.WaitForSeconds(0.1f);

            while (player != null && player.IsConnected
                && _playerButtons.TryGetValue(player, out var buttons)
                && buttons.TryGetValue(buttonId, out var state)
                && state.IsPressed)
            {
                FireHeld(player, buttonId);
                yield return Timing.WaitForSeconds(0.05f);
            }
        }

        private static void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            var player = ev?.Player;
            if (player == null) return;

            _playerButtons.Remove(player);
            if (_holdCoroutines.TryGetValue(player, out var handle))
            {
                Timing.KillCoroutines(handle);
                _holdCoroutines.Remove(player);
            }
        }

        private static void FirePressed(Player player, int buttonId)
        {
            try { OnPressed?.Invoke(player, buttonId); }
            catch (Exception ex) { FermixLog.Error($"FermixInput.OnPressed: {ex.Message}"); }

            InvokeHandlers(_pressedHandlers, buttonId, player, "Pressed");
        }

        private static void FireReleased(Player player, int buttonId)
        {
            try { OnReleased?.Invoke(player, buttonId); }
            catch (Exception ex) { FermixLog.Error($"FermixInput.OnReleased: {ex.Message}"); }

            InvokeHandlers(_releasedHandlers, buttonId, player, "Released");
        }

        private static void FireHeld(Player player, int buttonId)
        {
            try { OnHeld?.Invoke(player, buttonId); }
            catch (Exception ex) { FermixLog.Error($"FermixInput.OnHeld: {ex.Message}"); }

            InvokeHandlers(_heldHandlers, buttonId, player, "Held");
        }

        private static void AddHandler(Dictionary<int, List<Action<Player>>> dict, int buttonId, Action<Player> handler)
        {
            if (handler == null) return;
            if (!dict.TryGetValue(buttonId, out var list))
            {
                list = new List<Action<Player>>();
                dict[buttonId] = list;
            }
            list.Add(handler);
        }

        private static void RemoveHandler(Dictionary<int, List<Action<Player>>> dict, int buttonId, Action<Player> handler)
        {
            if (handler == null) return;
            if (dict.TryGetValue(buttonId, out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                    dict.Remove(buttonId);
            }
        }

        private static void InvokeHandlers(Dictionary<int, List<Action<Player>>> dict, int buttonId, Player player, string kind)
        {
            if (!dict.TryGetValue(buttonId, out var list)) return;

            // Снимок — чтобы безопасно отписаться/подписаться внутри обработчика.
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    handler?.Invoke(player);
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"FermixInput {kind} handler (button {buttonId}): {ex.Message}");
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
                throw new InvalidOperationException("FermixInput не инициализирован. Вызовите FermixInput.Initialize().");
        }

        #endregion
    }
}
