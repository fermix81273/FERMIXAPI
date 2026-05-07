using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;
using FermixAPI.Hints.Core.Enum;
using FermixAPI.Hints.Core.Utilities;
using MEC;
using HsmHint = FermixAPI.Hints.Core.Models.Hints.Hint;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Глобальный текстовый чат через консольную команду <c>.say</c>. Сообщения
    /// собираются в общий ring-буфер. В отличие от остальных подсистем,
    /// FermixChat использует СВОЙ собственный HsmHint в верхнем правом углу
    /// экрана (через прямой <see cref="PlayerDisplay"/>, в обход
    /// <see cref="FermixHintStack"/>), чтобы не наслаиваться поверх других
    /// хинтов посередине экрана. Включается флагом <c>ChatEnabled</c>.
    /// </summary>
    public static class FermixChat
    {
        private const string TagStripPattern = "<[^>]+>";
        private const string HsmGroupName = "FermixAPI.Chat";

        // Координаты чата — верхний правый угол, в пикселях экрана 1920x1080:
        //   YCoordinate = 100 + YCoordinateAlign.Top → строка начинается в 100px
        //                 ниже верхнего края (с запасом, чтобы не залезть под
        //                 capture-/raid-/round-info оверлеи игры).
        //   XCoordinate = -40 + Alignment.Right → правый край текста на 40px
        //                 левее правого края экрана (комфортный padding).
        //   FontSize    = 18 — компактнее центральных хинтов, чтобы не
        //                 загораживать обзор.
        private const float ChatYCoordinate = 100f;
        private const float ChatXCoordinate = -40f;
        private const int ChatFontSize = 18;
        private const float ChatTickInterval = 1f;

        private static readonly Regex _tagStrip = new(TagStripPattern, RegexOptions.Compiled);

        private static readonly object _lock = new();
        private static readonly LinkedList<ChatLine> _buffer = new();
        private static readonly Dictionary<string, DateTime> _lastSent = new(StringComparer.Ordinal);
        private static readonly Dictionary<Player, HsmHint> _chatHints = new();

        private static CoroutineHandle _tickHandle;
        private static bool _initialized;

        /// <summary>Подписаться на нужные события и подготовить буфер.</summary>
        public static void Initialize()
        {
            if (_initialized) return;
            if (FermixCore.Config == null || !FermixCore.Config.ChatEnabled) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnPlayerJoin += OnPlayerJoined;
            FermixEvents.OnPlayerLeave += OnPlayerLeft;

            // ВАЖНО: НЕ привязываем chat-хинт к уже подключённым игрокам прямо
            // здесь. Initialize вызывается из FermixCore при включении плагина,
            // когда живых игроков обычно ещё нет. Но если плагин перезагружают
            // на лету (reload), мы могли бы триггернуть создание PlayerDisplay
            // на полузапущенном Mirror'овском NetworkConnection и сломать
            // network pipeline. Хинты доцепятся при первом Player.Joined +
            // через FermixScheduler.Delay (см. OnPlayerJoined ниже).

            _tickHandle = FermixCore.RunCoroutine(TextUpdateLoop(), "FermixChat.TextUpdateLoop");

            _initialized = true;
        }

        /// <summary>Отписаться, очистить буфер и убрать хинты.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnPlayerJoin -= OnPlayerJoined;
            FermixEvents.OnPlayerLeave -= OnPlayerLeft;

            if (_tickHandle.IsValid) Timing.KillCoroutines(_tickHandle);

            lock (_lock)
            {
                foreach (var kv in _chatHints)
                {
                    DetachHintInternal(kv.Key, kv.Value);
                }
                _chatHints.Clear();
                _buffer.Clear();
                _lastSent.Clear();
            }

            _initialized = false;
        }

        /// <summary>
        /// Опубликовать сообщение от игрока. Возвращает <see langword="true"/>,
        /// если сообщение было принято; иначе <paramref name="error"/> заполнено
        /// причиной отказа.
        /// </summary>
        public static bool TrySend(Player author, string text, out string error)
        {
            error = null;

            if (!_initialized || FermixCore.Config?.ChatEnabled != true)
            {
                error = "Чат выключен на этом сервере.";
                return false;
            }

            if (author == null)
            {
                error = "Чат доступен только игрокам.";
                return false;
            }

            string sanitized = Sanitize(text);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                error = "Пустое сообщение.";
                return false;
            }

            int maxLen = Math.Max(20, FermixCore.Config.ChatMaxLength);
            if (sanitized.Length > maxLen)
                sanitized = sanitized.Substring(0, maxLen);

            string key = author.UserId ?? author.Nickname ?? "anon";
            float cooldown = Math.Max(0f, FermixCore.Config.ChatCooldown);
            DateTime now = DateTime.UtcNow;

            lock (_lock)
            {
                if (_lastSent.TryGetValue(key, out DateTime last) &&
                    (now - last).TotalSeconds < cooldown)
                {
                    double left = cooldown - (now - last).TotalSeconds;
                    error = $"Подожди ещё {Math.Max(1, (int)Math.Ceiling(left))} сек перед следующим сообщением.";
                    return false;
                }

                _lastSent[key] = now;

                var line = new ChatLine
                {
                    Author = author.Nickname ?? "?",
                    AuthorColor = ResolveColor(author),
                    Text = sanitized,
                    Created = now,
                };
                _buffer.AddLast(line);

                int historySize = Math.Max(1, FermixCore.Config.ChatHistorySize);
                while (_buffer.Count > historySize)
                    _buffer.RemoveFirst();
            }

            // Сразу прокидываем апдейт ко всем активным чат-хинтам, чтобы новое
            // сообщение появилось без задержки тикера (~1с).
            PushTextToAllHints();

            FermixLog.Info($"[CHAT] {author.Nickname}: {sanitized}");
            return true;
        }

        private static void OnRoundStart()
        {
            lock (_lock)
            {
                _buffer.Clear();
                _lastSent.Clear();
            }
            PushTextToAllHints();
        }

        private static void OnPlayerJoined(JoinedEventArgs ev)
        {
            if (ev?.Player == null) return;

            // КРИТИЧНО: ДО версии 2.5.5 здесь сразу шёл AttachHint(ev.Player),
            // и это ломало подключение игрокам. Handlers.Player.Joined в
            // EXILED 9.13.3 фаерится ПО ХОДУ инициализации player'а
            // (Mirror всё ещё досоздаёт NetworkBehaviour'ы на GameObject
            // игрока). Создание PlayerDisplay в Hints-движке запускает
            // фоновый PeriodicRunner (Task.Run), который начинает диспатчить
            // hint-сообщения через connectionToClient.Send из ThreadPool-треда.
            // Mirror NetworkConnection.Send НЕ thread-safe, и параллельный
            // Send во время инициализации связи с клиентом приводит к
            // рассинхронизации NetworkBehaviour'ов: RemoteAdmin.QueryProcessor
            // не успевает доустановить _playerId, после чего Mirror сносит
            // игрока, и QueryProcessor.OnDestroy падает с NRE при попытке
            // удалить null-ключ из ConcurrentDictionary. Игрока выкидывает
            // с пустым ником через ~6 секунд после preauth.
            //
            // Фикс: откладываем AttachHint на 5 секунд через FermixScheduler.
            // К этому моменту Mirror гарантированно достроит player.
            var player = ev.Player;
            FermixScheduler.Delay(5f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;
                    AttachHint(player);
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixChat: deferred AttachHint failed for {player?.Nickname}: {ex.Message}");
                }
            });
        }

        private static void OnPlayerLeft(LeftEventArgs ev)
        {
            if (ev?.Player == null) return;

            HsmHint hint = null;
            lock (_lock)
            {
                if (_chatHints.TryGetValue(ev.Player, out hint))
                    _chatHints.Remove(ev.Player);

                string key = ev.Player.UserId ?? ev.Player.Nickname;
                if (key != null) _lastSent.Remove(key);
            }

            if (hint != null) DetachHintInternal(ev.Player, hint);
        }

        private static void AttachHint(Player player)
        {
            if (player == null || player.ReferenceHub == null) return;

            HsmHint hint;
            lock (_lock)
            {
                if (_chatHints.ContainsKey(player)) return;

                // Создаём собственный HsmHint, прикрепляемый к PlayerDisplay
                // напрямую (а не через FermixHintStack). Ставим его в верхний
                // правый угол. FermixHintStack продолжает рулить хинтами
                // в центре экрана и вообще не знает про этот объект.
                hint = new HsmHint
                {
                    YCoordinate = ChatYCoordinate,
                    XCoordinate = ChatXCoordinate,
                    Alignment = HintAlignment.Right,
                    YCoordinateAlign = HintVerticalAlign.Top,
                    SyncSpeed = HintSyncSpeed.Normal,
                    FontSize = ChatFontSize,
                    Text = RenderForPlayer(),
                };

                _chatHints[player] = hint;
            }

            try
            {
                PlayerDisplay.Get(player.ReferenceHub).AddHint(hint, HsmGroupName);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixChat.AttachHint: {ex.Message}");
                lock (_lock) _chatHints.Remove(player);
            }
        }

        private static void DetachHintInternal(Player player, HsmHint hint)
        {
            if (player == null || hint == null) return;
            try
            {
                if (player.ReferenceHub != null)
                    PlayerDisplay.Get(player.ReferenceHub).RemoveHint(hint, HsmGroupName);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixChat.DetachHint: {ex.Message}");
            }
        }

        private static IEnumerator<float> TextUpdateLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(ChatTickInterval);

                try
                {
                    PushTextToAllHints();
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixChat.TextUpdateLoop: {ex.Message}");
                }
            }
        }

        private static void PushTextToAllHints()
        {
            string rendered = RenderForPlayer();

            // Снимаем snapshot пар (Player, HsmHint), чтобы не держать lock
            // во время обращения к PlayerDisplay/Text (внешние сеттеры могут
            // тригернуть update-цепочку).
            List<KeyValuePair<Player, HsmHint>> snapshot;
            lock (_lock)
            {
                snapshot = new List<KeyValuePair<Player, HsmHint>>(_chatHints);
            }

            foreach (var kv in snapshot)
            {
                var player = kv.Key;
                var hint = kv.Value;
                if (hint == null) continue;
                if (player == null || !player.IsConnected) continue;

                try
                {
                    if (string.IsNullOrEmpty(rendered))
                    {
                        if (!hint.Hide) hint.Hide = true;
                        hint.Text = string.Empty;
                    }
                    else
                    {
                        hint.Text = rendered;
                        if (hint.Hide) hint.Hide = false;
                    }
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixChat: text push failed for {player?.Nickname}: {ex.Message}");
                }
            }
        }

        private static string RenderForPlayer()
        {
            float lifetime = Math.Max(1f, FermixCore.Config?.ChatMessageLifetime ?? 12f);
            DateTime now = DateTime.UtcNow;
            var sb = new StringBuilder();

            lock (_lock)
            {
                foreach (var line in _buffer)
                {
                    if ((now - line.Created).TotalSeconds > lifetime) continue;
                    sb.Append("<size=").Append(ChatFontSize).Append("><color=").Append(line.AuthorColor).Append('>')
                      .Append(line.Author).Append("</color>: ")
                      .Append(line.Text).Append("</size>\n");
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd('\n') : string.Empty;
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            // Убираем любые TMP/HTML-теги, чтобы игрок не сломал вёрстку чужого хинта.
            string stripped = _tagStrip.Replace(raw, string.Empty);
            stripped = stripped.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return stripped;
        }

        private static string ResolveColor(Player player)
        {
            if (player == null) return "#cccccc";
            try
            {
                return player.Role?.Side switch
                {
                    Side.Mtf => "#42adf2",
                    Side.ChaosInsurgency => "#16d162",
                    Side.Scp => "#d11c1c",
                    Side.Tutorial => "#f0d24a",
                    Side.Flamingos => "#ff6ec7",
                    _ => "#dddddd",
                };
            }
            catch
            {
                return "#dddddd";
            }
        }

        private sealed class ChatLine
        {
            public string Author;
            public string AuthorColor;
            public string Text;
            public DateTime Created;
        }
    }
}
