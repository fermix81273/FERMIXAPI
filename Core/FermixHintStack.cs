using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Features;
using FermixAPI.Hints.Core.Enum;
using FermixAPI.Hints.Core.Models.Hints;
using FermixAPI.Hints.Core.Utilities;
using MEC;
using HsmHint = FermixAPI.Hints.Core.Models.Hints.Hint;

namespace FermixAPI.Core
{
    /// <summary>
    /// Категория хинта — задаёт цвет по умолчанию и помогает различать сообщения визуально.
    /// </summary>
    public enum HintCategory
    {
        /// <summary>Системное сообщение (cyan).</summary>
        System,
        /// <summary>Информационное сообщение.</summary>
        Info,
        /// <summary>Предупреждение (yellow).</summary>
        Warning,
        /// <summary>Успех (green).</summary>
        Success,
        /// <summary>Ошибка (red).</summary>
        Error,
        /// <summary>Пользовательская категория.</summary>
        Custom,
    }

    /// <summary>
    /// Стековая система хинтов FermixAPI. Позволяет одновременно показывать игроку
    /// несколько хинтов с приоритетами, категориями, повторами, постоянными (persistent)
    /// и динамическими (с функцией обновления) сообщениями.
    ///
    /// В отличие от плоского <c>player.ShowHint</c> (который перезаписывает предыдущее
    /// сообщение), этот модуль агрегирует все активные хинты в единый блок и периодически
    /// перерисовывает экран игрока.
    /// </summary>
    public static class FermixHintStack
    {
        #region Display Settings

        /// <summary>
        /// Глобальные настройки отображения стека хинтов.
        /// </summary>
        public static class DisplaySettings
        {
            /// <summary>Символ-заполнитель для верхней рамки (используется при >3 хинтах).</summary>
            public static string HeaderSymbol { get; set; } = "═";

            /// <summary>Символ-заполнитель для нижней рамки.</summary>
            public static string FooterSymbol { get; set; } = "═";

            /// <summary>Длина рамки (в символах).</summary>
            public static int HeaderLength { get; set; } = 30;

            /// <summary>Маркер списка (по умолчанию пусто).</summary>
            public static string BulletPoint { get; set; } = "";

            /// <summary>Разделитель между хинтами (по умолчанию перенос строки).</summary>
            public static string Separator { get; set; } = "\n";

            /// <summary>Показывать ли счётчик повторов (x2/x3/...).</summary>
            public static bool ShowRepeatCounter { get; set; } = true;

            /// <summary>Показывать ли остаток времени до исчезновения хинта.</summary>
            public static bool ShowTimeRemaining { get; set; } = false;

            /// <summary>Максимальное число одновременно показываемых хинтов на одного игрока.</summary>
            public static int MaxHintsPerPlayer { get; set; } = 10;

            /// <summary>Размер шрифта по умолчанию.</summary>
            public static int DefaultFontSize { get; set; } = 25;
        }

        #endregion

        #region Internal Types

        private sealed class HintData
        {
            public string Message { get; set; }
            public float Duration { get; set; }
            public DateTime StartTime { get; set; }
            public int Priority { get; set; }
            public string Id { get; set; }
            public bool IsPersistent { get; set; }
            public HintCategory Category { get; set; }
            public string Color { get; set; }
            public bool ShowBullet { get; set; }
            public string CustomPrefix { get; set; }
            public int FontSize { get; set; }
            public int RepeatCount { get; set; } = 1;
            public DateTime LastRepeatTime { get; set; }
            public Func<Player, string> UpdateFunction { get; set; }
            public float UpdateInterval { get; set; } = 1f;
            public DateTime LastUpdateTime { get; set; }

            public bool IsExpired => !IsPersistent
                && (DateTime.UtcNow - StartTime).TotalSeconds >= Duration;

            public float TimeRemaining => IsPersistent
                ? -1f
                : Math.Max(0f, Duration - (float)(DateTime.UtcNow - StartTime).TotalSeconds);

            public bool NeedsUpdate => UpdateFunction != null
                && (DateTime.UtcNow - LastUpdateTime).TotalSeconds >= UpdateInterval;

            public string GetFormattedMessage(Player player)
            {
                var sb = new StringBuilder();
                sb.Append($"<size={FontSize}>");

                if (!string.IsNullOrEmpty(Color))
                    sb.Append("<color=").Append(Color).Append('>');

                if (!string.IsNullOrEmpty(CustomPrefix))
                {
                    sb.Append(CustomPrefix).Append(' ');
                }
                else if (ShowBullet && !string.IsNullOrEmpty(DisplaySettings.BulletPoint))
                {
                    sb.Append(DisplaySettings.BulletPoint).Append(' ');
                }

                var text = UpdateFunction != null ? UpdateFunction(player) : Message;
                sb.Append(text);

                if (DisplaySettings.ShowRepeatCounter && RepeatCount > 1)
                    sb.Append($" <color=#808080>[x{RepeatCount}]</color>");

                if (DisplaySettings.ShowTimeRemaining && !IsPersistent && TimeRemaining > 0f)
                    sb.Append($" <size=18>({TimeRemaining:F0}s)</size>");

                if (!string.IsNullOrEmpty(Color))
                    sb.Append("</color>");

                sb.Append("</size>");
                return sb.ToString();
            }

            public void IncrementRepeat()
            {
                RepeatCount++;
                LastRepeatTime = DateTime.UtcNow;
                StartTime = DateTime.UtcNow;
            }

            public void UpdateContent(Player player)
            {
                if (UpdateFunction == null) return;
                Message = UpdateFunction(player);
                LastUpdateTime = DateTime.UtcNow;
            }
        }

        private sealed class PlayerHintCollection
        {
            private readonly List<HintData> _hints = new List<HintData>();
            private readonly Dictionary<string, HintData> _byMessage = new Dictionary<string, HintData>();
            private readonly Player _player;
            private string _cached = string.Empty;
            private DateTime _lastBuild = DateTime.MinValue;

            public int Count => _hints.Count;

            public PlayerHintCollection(Player player) => _player = player;

            public void AddHint(HintData hint)
            {
                if (!string.IsNullOrEmpty(hint.Id))
                {
                    var removed = _hints.RemoveAll(h => h.Id == hint.Id);
                    if (removed > 0)
                    {
                        // Перестраиваем индекс по сообщению, чтобы _byMessage
                        // не указывал на уже удалённые хинты.
                        _byMessage.Clear();
                        foreach (var h in _hints)
                        {
                            if (!h.IsPersistent)
                                _byMessage[MessageKey(h)] = h;
                        }
                    }
                }

                var key = MessageKey(hint);
                if (!hint.IsPersistent
                    && _byMessage.TryGetValue(key, out var existing)
                    && (DateTime.UtcNow - existing.LastRepeatTime).TotalSeconds < 1.0)
                {
                    existing.IncrementRepeat();
                    Invalidate();
                    return;
                }

                if (_hints.Count >= DisplaySettings.MaxHintsPerPlayer && !hint.IsPersistent)
                {
                    var victim = _hints
                        .Where(h => !h.IsPersistent)
                        .OrderBy(h => h.Priority)
                        .ThenBy(h => h.StartTime)
                        .FirstOrDefault();

                    if (victim != null)
                    {
                        _hints.Remove(victim);
                        _byMessage.Remove(MessageKey(victim));
                    }
                }

                _hints.Add(hint);
                if (!hint.IsPersistent)
                    _byMessage[key] = hint;
                Invalidate();
            }

            public void RemoveHint(string id)
            {
                if (string.IsNullOrEmpty(id)) return;

                var removed = _hints.RemoveAll(h => h.Id == id);
                if (removed == 0) return;

                // Перестраиваем индекс по сообщению.
                _byMessage.Clear();
                foreach (var h in _hints)
                {
                    if (!h.IsPersistent)
                        _byMessage[MessageKey(h)] = h;
                }
                Invalidate();
            }

            public bool RemoveExpired()
            {
                var removed = _hints.RemoveAll(h => h.IsExpired);
                if (removed == 0) return false;

                _byMessage.Clear();
                foreach (var h in _hints)
                {
                    if (!h.IsPersistent)
                        _byMessage[MessageKey(h)] = h;
                }
                Invalidate();
                return true;
            }

            public bool UpdateDynamicHints()
            {
                bool changed = false;
                foreach (var h in _hints.Where(h => h.NeedsUpdate))
                {
                    h.UpdateContent(_player);
                    changed = true;
                }
                if (changed) Invalidate();
                return changed;
            }

            public void Clear()
            {
                _hints.Clear();
                _byMessage.Clear();
                Invalidate();
            }

            public bool HasHint(string id) => !string.IsNullOrEmpty(id) && _hints.Any(h => h.Id == id);

            public string GetDisplay()
            {
                // Кэш на 0.5 сек, чтобы не перестраивать строку на каждый тик.
                if (!string.IsNullOrEmpty(_cached)
                    && (DateTime.UtcNow - _lastBuild).TotalSeconds < 0.5)
                {
                    return _cached;
                }

                if (_hints.Count == 0)
                {
                    _cached = string.Empty;
                    return _cached;
                }

                var ordered = _hints
                    .OrderByDescending(h => h.Priority)
                    .ThenBy(h => h.Category)
                    .ThenBy(h => h.StartTime)
                    .ToList();

                var sb = new StringBuilder();
                bool useFrame = _hints.Count > 3;

                if (useFrame && !string.IsNullOrEmpty(DisplaySettings.HeaderSymbol))
                {
                    var bar = new string(DisplaySettings.HeaderSymbol[0], DisplaySettings.HeaderLength);
                    sb.Append("<size=20>").Append(bar).Append("</size>\n");
                }

                for (int i = 0; i < ordered.Count; i++)
                {
                    if (i > 0) sb.Append(DisplaySettings.Separator);
                    sb.Append(ordered[i].GetFormattedMessage(_player));
                }

                if (useFrame && !string.IsNullOrEmpty(DisplaySettings.FooterSymbol))
                {
                    var bar = new string(DisplaySettings.FooterSymbol[0], DisplaySettings.HeaderLength);
                    sb.Append('\n').Append("<size=20>").Append(bar).Append("</size>");
                }

                _cached = sb.ToString();
                _lastBuild = DateTime.UtcNow;
                return _cached;
            }

            private void Invalidate()
            {
                _cached = string.Empty;
                _lastBuild = DateTime.MinValue;
            }

            private static string MessageKey(HintData h)
                => $"{h.Message}_{h.Category}_{h.Color}_{h.Priority}";
        }

        #endregion

        #region State

        private static readonly Dictionary<Player, PlayerHintCollection> _playerHints
            = new Dictionary<Player, PlayerHintCollection>();

        // Один HSM-хинт на игрока. Им мы управляем единолично — текст обновляется
        // на месте, чтобы не плодить хинты в HintCollection HSM. PlayerDisplay
        // (и connection) живут на стороне Hints/, нам достаточно держать
        // ссылку на наш собственный объект Hint, чтобы менять Text/Hide.
        private static readonly Dictionary<Player, HsmHint> _hsmHints
            = new Dictionary<Player, HsmHint>();

        // Идентификатор группы для AddHint(hint, groupName). Используем имя
        // FermixAPI-сборки, чтобы наш хинт жил в собственной группе и не
        // конфликтовал с CompatibilityAdaptor (который рисует чужие хинты
        // под именем их вызывающей сборки).
        private const string HsmGroupName = "FermixAPI.HintStack";

        private static readonly object _lock = new object();

        private static CoroutineHandle _updateLoop;
        private static bool _initialized;

        /// <summary>Запущена ли система стека хинтов.</summary>
        public static bool IsInitialized => _initialized;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Запускает фоновый цикл обновления стека хинтов. Вызывается автоматически из
        /// <see cref="FermixCore.Initialize"/>; повторно вызывать не нужно.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _updateLoop = FermixCore.RunCoroutine(UpdateLoop(), "FermixHintStack.UpdateLoop");
            _initialized = true;
        }

        /// <summary>
        /// Останавливает цикл обновления и очищает все хинты.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixCore.StopCoroutine(_updateLoop);
            _updateLoop = default;

            ClearAllHints();
            _initialized = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Показывает обычный хинт со временем жизни <paramref name="duration"/> секунд.
        /// При повторном вызове с тем же сообщением и категорией в течение секунды —
        /// увеличивает счётчик повторов вместо добавления нового хинта.
        /// </summary>
        public static void ShowHint(
            Player player,
            string message,
            float duration = 5f,
            int priority = 0,
            string id = null,
            HintCategory category = HintCategory.Custom,
            string color = null,
            bool showBullet = true,
            string customPrefix = null,
            int fontSize = 0)
        {
            if (player == null || string.IsNullOrEmpty(message)) return;
            EnsureRunning();

            lock (_lock)
            {
                var collection = GetOrCreate(player);
                var hint = new HintData
                {
                    Message = message,
                    Duration = duration,
                    StartTime = DateTime.UtcNow,
                    Priority = priority,
                    Id = id ?? Guid.NewGuid().ToString(),
                    IsPersistent = false,
                    Category = category,
                    Color = color ?? DefaultColor(category),
                    ShowBullet = showBullet,
                    CustomPrefix = customPrefix,
                    FontSize = fontSize > 0 ? fontSize : DisplaySettings.DefaultFontSize,
                    LastRepeatTime = DateTime.UtcNow,
                };
                collection.AddHint(hint);
                UpdatePlayerDisplay(player, collection);
            }
        }

        /// <summary>
        /// Показывает динамический хинт. <paramref name="updateFunction"/> вызывается
        /// каждые <paramref name="updateInterval"/> секунд для обновления текста.
        /// </summary>
        public static void ShowDynamicHint(
            Player player,
            Func<Player, string> updateFunction,
            float duration = 5f,
            float updateInterval = 1f,
            int priority = 0,
            string id = null,
            HintCategory category = HintCategory.Custom,
            string color = null,
            bool showBullet = true,
            string customPrefix = null,
            int fontSize = 0)
        {
            if (player == null || updateFunction == null) return;
            EnsureRunning();

            lock (_lock)
            {
                var collection = GetOrCreate(player);
                var hint = new HintData
                {
                    Message = updateFunction(player),
                    UpdateFunction = updateFunction,
                    UpdateInterval = updateInterval,
                    Duration = duration,
                    StartTime = DateTime.UtcNow,
                    Priority = priority,
                    Id = id ?? Guid.NewGuid().ToString(),
                    IsPersistent = false,
                    Category = category,
                    Color = color ?? DefaultColor(category),
                    ShowBullet = showBullet,
                    CustomPrefix = customPrefix,
                    FontSize = fontSize > 0 ? fontSize : DisplaySettings.DefaultFontSize,
                    LastRepeatTime = DateTime.UtcNow,
                    LastUpdateTime = DateTime.UtcNow,
                };
                collection.AddHint(hint);
                UpdatePlayerDisplay(player, collection);
            }
        }

        /// <summary>
        /// Показывает persistent-хинт (без таймера) с уникальным <paramref name="id"/>.
        /// Сам по себе не исчезает — снимается явным <see cref="RemoveHint"/>
        /// или <see cref="ClearAllHints(Player)"/>.
        /// </summary>
        public static void ShowPersistentHint(
            Player player,
            string message,
            string id,
            int priority = 0,
            HintCategory category = HintCategory.Custom,
            string color = null,
            bool showBullet = true,
            string customPrefix = null,
            int fontSize = 0)
        {
            if (player == null || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(id)) return;
            EnsureRunning();

            lock (_lock)
            {
                var collection = GetOrCreate(player);
                var hint = new HintData
                {
                    Message = message,
                    Duration = 0f,
                    StartTime = DateTime.UtcNow,
                    Priority = priority,
                    Id = id,
                    IsPersistent = true,
                    Category = category,
                    Color = color ?? DefaultColor(category),
                    ShowBullet = showBullet,
                    CustomPrefix = customPrefix,
                    FontSize = fontSize > 0 ? fontSize : DisplaySettings.DefaultFontSize,
                    LastRepeatTime = DateTime.UtcNow,
                };
                collection.AddHint(hint);
                UpdatePlayerDisplay(player, collection);
            }
        }

        /// <summary>
        /// Показывает persistent-хинт с динамическим обновлением. Полезно для индикаторов
        /// (HP, патроны, кулдаун и т.п.).
        /// </summary>
        public static void ShowPersistentDynamicHint(
            Player player,
            Func<Player, string> updateFunction,
            string id,
            float updateInterval = 1f,
            int priority = 0,
            HintCategory category = HintCategory.Custom,
            string color = null,
            bool showBullet = true,
            string customPrefix = null,
            int fontSize = 0)
        {
            if (player == null || updateFunction == null || string.IsNullOrEmpty(id)) return;
            EnsureRunning();

            lock (_lock)
            {
                var collection = GetOrCreate(player);
                var hint = new HintData
                {
                    Message = updateFunction(player),
                    UpdateFunction = updateFunction,
                    UpdateInterval = updateInterval,
                    Duration = 0f,
                    StartTime = DateTime.UtcNow,
                    Priority = priority,
                    Id = id,
                    IsPersistent = true,
                    Category = category,
                    Color = color ?? DefaultColor(category),
                    ShowBullet = showBullet,
                    CustomPrefix = customPrefix,
                    FontSize = fontSize > 0 ? fontSize : DisplaySettings.DefaultFontSize,
                    LastRepeatTime = DateTime.UtcNow,
                    LastUpdateTime = DateTime.UtcNow,
                };
                collection.AddHint(hint);
                UpdatePlayerDisplay(player, collection);
            }
        }

        /// <summary>
        /// Удаляет хинт по <paramref name="id"/>.
        /// </summary>
        public static void RemoveHint(Player player, string id)
        {
            if (player == null || string.IsNullOrEmpty(id)) return;

            lock (_lock)
            {
                if (_playerHints.TryGetValue(player, out var collection))
                {
                    collection.RemoveHint(id);
                    UpdatePlayerDisplay(player, collection);
                }
            }
        }

        /// <summary>
        /// Очищает все хинты у указанного игрока.
        /// </summary>
        public static void ClearAllHints(Player player)
        {
            if (player == null) return;

            lock (_lock)
            {
                if (!_playerHints.TryGetValue(player, out var collection)) return;
                collection.Clear();
                ClearForPlayer(player);
            }
        }

        /// <summary>
        /// Очищает все хинты у всех игроков.
        /// </summary>
        public static void ClearAllHints()
        {
            lock (_lock)
            {
                foreach (var p in _playerHints.Keys.ToList())
                {
                    RemoveFromPlayer(p);
                }
                _playerHints.Clear();
            }
        }

        /// <summary>
        /// Есть ли у игрока хинт с указанным <paramref name="id"/>.
        /// </summary>
        public static bool HasHint(Player player, string id)
        {
            if (player == null || string.IsNullOrEmpty(id)) return false;
            lock (_lock)
            {
                return _playerHints.TryGetValue(player, out var c) && c.HasHint(id);
            }
        }

        /// <summary>
        /// Сколько хинтов сейчас активно у игрока.
        /// </summary>
        public static int GetHintCount(Player player)
        {
            if (player == null) return 0;
            lock (_lock)
            {
                return _playerHints.TryGetValue(player, out var c) ? c.Count : 0;
            }
        }

        #endregion

        #region Update Loop

        private static IEnumerator<float> UpdateLoop()
        {
            while (true)
            {
                try
                {
                    Tick();
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"FermixHintStack.UpdateLoop: {ex}");
                }
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        private static void Tick()
        {
            HashSet<Player> toUpdate = null;
            List<Player> toRemove = null;

            lock (_lock)
            {
                foreach (var kvp in _playerHints.ToList())
                {
                    var player = kvp.Key;
                    var collection = kvp.Value;

                    if (player == null || !player.IsConnected)
                    {
                        (toRemove ??= new List<Player>()).Add(player);
                        continue;
                    }

                    collection.RemoveExpired();
                    collection.UpdateDynamicHints();

                    if (collection.Count == 0)
                    {
                        (toRemove ??= new List<Player>()).Add(player);
                    }
                    else
                    {
                        // Перерисовываем каждый тик: коллекция могла измениться
                        // (истёк репит, динамическая функция вернула новый текст),
                        // поэтому пересобираем строку и пушим её в HSM-хинт.
                        // Сам HSM держит хинт на экране без таймаута — таймауты
                        // считаем мы здесь, в HintData.IsExpired / RemoveExpired.
                        (toUpdate ??= new HashSet<Player>()).Add(player);
                    }
                }

                if (toRemove != null)
                {
                    foreach (var p in toRemove)
                    {
                        _playerHints.Remove(p);
                        RemoveFromPlayer(p);
                    }
                }

                if (toUpdate != null)
                {
                    foreach (var p in toUpdate)
                    {
                        if (_playerHints.TryGetValue(p, out var c))
                            UpdatePlayerDisplay(p, c);
                    }
                }
            }
        }

        #endregion

        #region Internals

        private static PlayerHintCollection GetOrCreate(Player player)
        {
            if (!_playerHints.TryGetValue(player, out var c))
            {
                c = new PlayerHintCollection(player);
                _playerHints[player] = c;
            }
            return c;
        }

        private static void UpdatePlayerDisplay(Player player, PlayerHintCollection collection)
        {
            if (player == null || !player.IsConnected) return;
            var display = collection.GetDisplay();
            RenderToPlayer(player, display);
        }

        // Передаёт текст хинта в FermixAPI.Hints (бывший HintServiceMeow):
        // у нас один HsmHint на игрока, мы только меняем его Text/Hide,
        // чтобы кооперативно жить с другими плагинами на сервере.
        private static void RenderToPlayer(Player player, string display)
        {
            if (player == null || !player.IsConnected || player.ReferenceHub == null) return;

            // Получаем (или создаём) наш персональный HsmHint для этого игрока.
            if (!_hsmHints.TryGetValue(player, out var hsmHint))
            {
                hsmHint = new HsmHint
                {
                    YCoordinate = 700f,
                    Alignment = HintAlignment.Center,
                    YCoordinateAlign = HintVerticalAlign.Middle,
                    SyncSpeed = HintSyncSpeed.Fastest,
                };

                try
                {
                    PlayerDisplay.Get(player.ReferenceHub).AddHint(hsmHint, HsmGroupName);
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"FermixHintStack.RenderToPlayer add: {ex.Message}");
                    return;
                }

                _hsmHints[player] = hsmHint;
            }

            if (string.IsNullOrEmpty(display))
            {
                hsmHint.Hide = true;
                hsmHint.Text = string.Empty;
            }
            else
            {
                hsmHint.Text = display;
                hsmHint.Hide = false;
            }
        }

        // Скрывает наш HSM-хинт у игрока — оставляет объект в HintCollection
        // HSM, но делает невидимым. Используется при ClearAllHints.
        private static void ClearForPlayer(Player player)
        {
            if (player == null) return;
            if (_hsmHints.TryGetValue(player, out var hsmHint))
            {
                hsmHint.Hide = true;
                hsmHint.Text = string.Empty;
            }
        }

        // Полностью убирает наш HSM-хинт у игрока (вызывается при отключении
        // и при Shutdown). Сам PlayerDisplay HSM удалит при Player.Left,
        // нам важно не держать на него ссылку.
        private static void RemoveFromPlayer(Player player)
        {
            if (player == null) return;
            if (_hsmHints.TryGetValue(player, out var hsmHint))
            {
                hsmHint.Hide = true;
                hsmHint.Text = string.Empty;
                _hsmHints.Remove(player);
            }
        }

        private static string DefaultColor(HintCategory category) => category switch
        {
            HintCategory.System  => "#00ffff",
            HintCategory.Warning => "#ffff00",
            HintCategory.Success => "#00ff00",
            HintCategory.Error   => "#ff0000",
            HintCategory.Info    => "#7fdbff",
            _ => null,
        };

        private static void EnsureRunning()
        {
            if (!_initialized) Initialize();
        }

        #endregion
    }
}
