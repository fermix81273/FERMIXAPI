using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Глобальный текстовый чат через консольную команду <c>.say</c>. Сообщения
    /// собираются в общий ring-буфер и отображаются всем живым игрокам в одном
    /// persistent-хинте через <see cref="FermixHintStack"/>. Таймстамп-фильтрация
    /// убирает старые строки сама. Включается флагом <c>ChatEnabled</c>.
    /// </summary>
    public static class FermixChat
    {
        private const string HintId = "fermix_chat";
        private const string TagStripPattern = "<[^>]+>";
        private static readonly Regex _tagStrip = new(TagStripPattern, RegexOptions.Compiled);

        private static readonly object _lock = new();
        private static readonly LinkedList<ChatLine> _buffer = new();
        private static readonly Dictionary<string, DateTime> _lastSent = new(StringComparer.Ordinal);

        private static bool _initialized;

        /// <summary>Подписаться на нужные события и подготовить буфер.</summary>
        public static void Initialize()
        {
            if (_initialized) return;
            if (FermixCore.Config == null || !FermixCore.Config.ChatEnabled) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnPlayerJoin += OnPlayerJoined;
            FermixEvents.OnPlayerLeave += OnPlayerLeft;

            foreach (Player p in Player.List)
                AttachHint(p);

            _initialized = true;
        }

        /// <summary>Отписаться, очистить буфер и убрать хинты.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnPlayerJoin -= OnPlayerJoined;
            FermixEvents.OnPlayerLeave -= OnPlayerLeft;

            foreach (Player p in Player.List)
                FermixHintStack.RemoveHint(p, HintId);

            lock (_lock)
            {
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
        }

        private static void OnPlayerJoined(JoinedEventArgs ev)
        {
            if (ev?.Player == null) return;
            AttachHint(ev.Player);
        }

        private static void OnPlayerLeft(LeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            FermixHintStack.RemoveHint(ev.Player, HintId);
            string key = ev.Player.UserId ?? ev.Player.Nickname;
            if (key == null) return;
            lock (_lock) _lastSent.Remove(key);
        }

        private static void AttachHint(Player player)
        {
            if (player == null) return;
            FermixHintStack.ShowPersistentDynamicHint(
                player,
                _ => RenderForPlayer(),
                HintId,
                updateInterval: 1f,
                priority: -50,
                category: HintCategory.Custom,
                color: FermixHint.White,
                showBullet: false);
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
                    sb.Append("<size=20><color=").Append(line.AuthorColor).Append('>')
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
