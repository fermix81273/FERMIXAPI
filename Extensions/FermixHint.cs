using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using FermixAPI.Core;
using MEC;

namespace FermixAPI
{
    /// <summary>
    /// Расширенная система хинтов с поддержкой форматирования, анимаций, приоритетов и стека.
    ///
    /// Все базовые методы (Send / Success / Error / Warning / Info / SendToAll / ...) под капотом
    /// направлены в <see cref="FermixHintStack"/>: их можно вызывать одновременно, и они
    /// будут аккуратно стэкаться, а не перезатирать друг друга. Анимационные методы
    /// (SendTyping / SendBlinking / SendFade и т.д.) по-прежнему пишут в <c>player.ShowHint</c>
    /// напрямую — на время анимации они временно перекрывают стек.
    /// </summary>
    public static class FermixHint
    {
        #region Colors - Цвета

        public const string White = "white";
        public const string Black = "black";
        public const string Red = "red";
        public const string Green = "green";
        public const string Blue = "blue";
        public const string Yellow = "yellow";
        public const string Cyan = "#00FFFF";
        public const string Magenta = "#FF00FF";
        public const string Orange = "orange";
        public const string Pink = "#FFC0CB";
        public const string Purple = "purple";
        public const string Gray = "#808080";
        public const string Gold = "#FFD700";
        public const string Silver = "#C0C0C0";
        public const string Lime = "#00FF00";
        public const string Aqua = "#00FFFF";

        #endregion

        #region Basic Hints - Базовые Хинты

        /// <summary>
        /// Отправляет простой хинт. Стэкается с другими хинтами.
        /// </summary>
        public static void Send(Player player, string message, float duration = 5f)
        {
            FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Custom);
        }

        /// <summary>
        /// Отправляет цветной хинт. Стэкается с другими хинтами.
        /// </summary>
        public static void SendColored(Player player, string message, string color, float duration = 5f)
        {
            FermixHintStack.ShowHint(player, message, duration, color: color, category: HintCategory.Custom);
        }

        /// <summary>
        /// Отправляет хинт успеха.
        /// </summary>
        public static void Success(Player player, string message, float duration = 3f)
        {
            FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Success);
        }

        /// <summary>
        /// Отправляет хинт ошибки.
        /// </summary>
        public static void Error(Player player, string message, float duration = 3f)
        {
            FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Error, customPrefix: "[!]");
        }

        /// <summary>
        /// Отправляет хинт предупреждения.
        /// </summary>
        public static void Warning(Player player, string message, float duration = 3f)
        {
            FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Warning, customPrefix: "[!]");
        }

        /// <summary>
        /// Отправляет информационный хинт.
        /// </summary>
        public static void Info(Player player, string message, float duration = 3f)
        {
            FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Info);
        }

        #endregion

        #region Global Hints - Глобальные Хинты

        /// <summary>
        /// Отправляет хинт всем игрокам. Стэкается у каждого индивидуально.
        /// </summary>
        public static void SendToAll(string message, float duration = 5f)
        {
            foreach (var player in Player.List)
            {
                FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Custom);
            }
        }

        /// <summary>
        /// Отправляет цветной хинт всем игрокам.
        /// </summary>
        public static void SendToAllColored(string message, string color, float duration = 5f)
        {
            foreach (var player in Player.List)
            {
                FermixHintStack.ShowHint(player, message, duration, color: color, category: HintCategory.Custom);
            }
        }

        /// <summary>
        /// Отправляет хинт успеха всем.
        /// </summary>
        public static void SuccessToAll(string message, float duration = 3f)
        {
            foreach (var player in Player.List)
            {
                FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Success);
            }
        }

        /// <summary>
        /// Отправляет хинт ошибки всем.
        /// </summary>
        public static void ErrorToAll(string message, float duration = 3f)
        {
            foreach (var player in Player.List)
            {
                FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Error, customPrefix: "[!]");
            }
        }

        /// <summary>
        /// Отправляет хинт по условию.
        /// </summary>
        public static void SendWhere(Func<Player, bool> predicate, string message, float duration = 5f)
        {
            foreach (var player in Player.List)
            {
                if (predicate(player))
                {
                    FermixHintStack.ShowHint(player, message, duration, category: HintCategory.Custom);
                }
            }
        }

        #endregion

        #region Stacked Hints - Хинты со стеком и приоритетами

        /// <summary>
        /// Показывает хинт через <see cref="FermixHintStack"/> с явным приоритетом, категорией и id.
        /// Идеально, когда нужно несколько одновременных хинтов, не затирающих друг друга.
        /// </summary>
        public static void ShowStacked(
            Player player,
            string message,
            float duration = 5f,
            int priority = 0,
            string id = null,
            HintCategory category = HintCategory.Custom,
            string color = null,
            string customPrefix = null,
            int fontSize = 0)
        {
            FermixHintStack.ShowHint(
                player, message, duration,
                priority: priority,
                id: id,
                category: category,
                color: color,
                customPrefix: customPrefix,
                fontSize: fontSize);
        }

        /// <summary>
        /// Показывает динамический хинт. Текст обновляется через <paramref name="updateFunction"/>
        /// каждые <paramref name="updateInterval"/> секунд (например, для индикаторов HP/патронов).
        /// </summary>
        public static void ShowDynamic(
            Player player,
            Func<Player, string> updateFunction,
            float duration = 5f,
            float updateInterval = 1f,
            int priority = 0,
            string id = null,
            HintCategory category = HintCategory.Custom,
            string color = null)
        {
            FermixHintStack.ShowDynamicHint(
                player, updateFunction, duration, updateInterval,
                priority: priority, id: id, category: category, color: color);
        }

        /// <summary>
        /// Показывает persistent-хинт (без таймера) с уникальным <paramref name="id"/>.
        /// Снимается явным <see cref="RemoveStacked"/> или <see cref="ClearStacked"/>.
        /// </summary>
        public static void ShowPersistent(
            Player player,
            string message,
            string id,
            int priority = 0,
            HintCategory category = HintCategory.Custom,
            string color = null,
            string customPrefix = null,
            int fontSize = 0)
        {
            FermixHintStack.ShowPersistentHint(
                player, message, id,
                priority: priority,
                category: category,
                color: color,
                customPrefix: customPrefix,
                fontSize: fontSize);
        }

        /// <summary>
        /// Persistent-хинт с динамическим обновлением. Полезно для постоянных индикаторов.
        /// </summary>
        public static void ShowPersistentDynamic(
            Player player,
            Func<Player, string> updateFunction,
            string id,
            float updateInterval = 1f,
            int priority = 0,
            HintCategory category = HintCategory.Custom,
            string color = null)
        {
            FermixHintStack.ShowPersistentDynamicHint(
                player, updateFunction, id, updateInterval,
                priority: priority, category: category, color: color);
        }

        /// <summary>
        /// Удаляет stacked-хинт по <paramref name="id"/>.
        /// </summary>
        public static void RemoveStacked(Player player, string id)
            => FermixHintStack.RemoveHint(player, id);

        /// <summary>
        /// Очищает все stacked-хинты у игрока.
        /// </summary>
        public static void ClearStacked(Player player)
            => FermixHintStack.ClearAllHints(player);

        /// <summary>
        /// Очищает все stacked-хинты у всех игроков.
        /// </summary>
        public static void ClearStackedAll()
            => FermixHintStack.ClearAllHints();

        /// <summary>
        /// Есть ли у игрока stacked-хинт с указанным id.
        /// </summary>
        public static bool HasStacked(Player player, string id)
            => FermixHintStack.HasHint(player, id);

        /// <summary>
        /// Сколько stacked-хинтов сейчас активно у игрока.
        /// </summary>
        public static int StackedCount(Player player)
            => FermixHintStack.GetHintCount(player);

        #endregion

        #region Formatted Hints - Форматированные Хинты

        /// <summary>
        /// Отправляет хинт с заголовком.
        /// </summary>
        public static void SendWithTitle(Player player, string title, string message, float duration = 5f)
        {
            var formatted = $"{Bold(Size(title, 30))}\n{message}";
            FermixHintStack.ShowHint(player, formatted, duration, category: HintCategory.Custom, showBullet: false);
        }

        /// <summary>
        /// Отправляет хинт со списком.
        /// </summary>
        public static void SendList(Player player, string title, IEnumerable<string> items, float duration = 5f)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Bold(title));

            foreach (var item in items)
                sb.AppendLine($"• {item}");

            FermixHintStack.ShowHint(player, sb.ToString(), duration, category: HintCategory.Custom, showBullet: false);
        }

        /// <summary>
        /// Отправляет хинт с прогресс-баром. Часто обновляется — рекомендуется один и тот же id.
        /// </summary>
        public static void SendProgress(Player player, string label, float progress, int barLength = 20, float duration = 1f)
        {
            var clamped = Math.Max(0f, Math.Min(1f, progress));
            var filled = (int)(clamped * barLength);
            var empty = barLength - filled;

            var bar = $"[{new string('█', filled)}{new string('░', empty)}] {(clamped * 100):F0}%";
            var formatted = $"{label}\n{bar}";

            FermixHintStack.ShowHint(
                player, formatted, duration,
                id: $"fermix_progress_{label}",
                category: HintCategory.Custom,
                showBullet: false);
        }

        /// <summary>
        /// Отправляет многострочный хинт.
        /// </summary>
        public static void SendMultiline(Player player, float duration, params string[] lines)
        {
            FermixHintStack.ShowHint(player, string.Join("\n", lines), duration, category: HintCategory.Custom, showBullet: false);
        }

        #endregion

        #region Animated Hints - Анимированные Хинты

        /// <summary>
        /// Отправляет печатающийся хинт.
        /// </summary>
        public static CoroutineHandle SendTyping(Player player, string message, float charDelay = 0.05f, float finalDuration = 2f)
        {
            return FermixCore.RunCoroutine(TypingCoroutine(player, message, charDelay, finalDuration));
        }

        private static IEnumerator<float> TypingCoroutine(Player player, string message, float charDelay, float finalDuration)
        {
            var current = "";
            
            foreach (var c in message)
            {
                current += c;
                player.ShowHint(current + "_", 1f);
                yield return Timing.WaitForSeconds(charDelay);
            }
            
            player.ShowHint(current, finalDuration);
        }

        /// <summary>
        /// Отправляет мигающий хинт.
        /// </summary>
        public static CoroutineHandle SendBlinking(Player player, string message, float interval = 0.5f, int blinks = 5)
        {
            return FermixCore.RunCoroutine(BlinkingCoroutine(player, message, interval, blinks));
        }

        private static IEnumerator<float> BlinkingCoroutine(Player player, string message, float interval, int blinks)
        {
            for (int i = 0; i < blinks * 2; i++)
            {
                player.ShowHint(i % 2 == 0 ? message : "", interval);
                yield return Timing.WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// Отправляет хинт с обратным отсчётом.
        /// </summary>
        public static CoroutineHandle SendCountdown(Player player, string format, int seconds, Action onComplete = null)
        {
            return FermixCore.RunCoroutine(CountdownCoroutine(player, format, seconds, onComplete));
        }

        private static IEnumerator<float> CountdownCoroutine(Player player, string format, int seconds, Action onComplete)
        {
            for (int i = seconds; i > 0; i--)
            {
                player.ShowHint(string.Format(format, i), 1.1f);
                yield return Timing.WaitForSeconds(1f);
            }
            
            onComplete?.Invoke();
        }

        /// <summary>
        /// Отправляет последовательность хинтов.
        /// </summary>
        public static CoroutineHandle SendSequence(Player player, float interval, params string[] messages)
        {
            return FermixCore.RunCoroutine(SequenceCoroutine(player, interval, messages));
        }

        private static IEnumerator<float> SequenceCoroutine(Player player, float interval, string[] messages)
        {
            foreach (var message in messages)
            {
                player.ShowHint(message, interval);
                yield return Timing.WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// Отправляет появляющийся/исчезающий хинт (симуляция через прозрачность).
        /// </summary>
        public static CoroutineHandle SendFade(Player player, string message, float fadeInTime = 0.5f, float displayTime = 2f, float fadeOutTime = 0.5f)
        {
            return FermixCore.RunCoroutine(FadeCoroutine(player, message, fadeInTime, displayTime, fadeOutTime));
        }

        private static IEnumerator<float> FadeCoroutine(Player player, string message, float fadeIn, float display, float fadeOut)
        {
            // Fade in (имитация через размер)
            for (float t = 0; t < fadeIn; t += 0.1f)
            {
                int size = (int)(20 + (t / fadeIn) * 10);
                player.ShowHint(Size(message, size), 0.15f);
                yield return Timing.WaitForSeconds(0.1f);
            }
            
            // Display
            player.ShowHint(Size(message, 30), display);
            yield return Timing.WaitForSeconds(display);
            
            // Fade out
            for (float t = 0; t < fadeOut; t += 0.1f)
            {
                int size = (int)(30 - (t / fadeOut) * 10);
                player.ShowHint(Size(message, size), 0.15f);
                yield return Timing.WaitForSeconds(0.1f);
            }
        }

        #endregion

        #region Text Formatting - Форматирование Текста

        /// <summary>
        /// Применяет цвет к тексту.
        /// </summary>
        public static string Color(string text, string color)
        {
            return $"<color={color}>{text}</color>";
        }

        /// <summary>
        /// Делает текст жирным.
        /// </summary>
        public static string Bold(string text)
        {
            return $"<b>{text}</b>";
        }

        /// <summary>
        /// Делает текст курсивом.
        /// </summary>
        public static string Italic(string text)
        {
            return $"<i>{text}</i>";
        }

        /// <summary>
        /// Подчёркивает текст.
        /// </summary>
        public static string Underline(string text)
        {
            return $"<u>{text}</u>";
        }

        /// <summary>
        /// Перечёркивает текст.
        /// </summary>
        public static string Strikethrough(string text)
        {
            return $"<s>{text}</s>";
        }

        /// <summary>
        /// Устанавливает размер текста.
        /// </summary>
        public static string Size(string text, int size)
        {
            return $"<size={size}>{text}</size>";
        }

        /// <summary>
        /// Выравнивает текст.
        /// </summary>
        public static string Align(string text, string alignment)
        {
            return $"<align={alignment}>{text}</align>";
        }

        /// <summary>
        /// Центрирует текст.
        /// </summary>
        public static string Center(string text)
        {
            return Align(text, "center");
        }

        /// <summary>
        /// Добавляет отступ сверху (через переносы строк).
        /// </summary>
        public static string TopMargin(string text, int lines = 1)
        {
            return new string('\n', lines) + text;
        }

        /// <summary>
        /// Создаёт разделитель.
        /// </summary>
        public static string Separator(int length = 30, char c = '─')
        {
            return new string(c, length);
        }

        #endregion

        #region Builder - Строитель Хинтов

        /// <summary>
        /// Создаёт новый построитель хинтов.
        /// </summary>
        public static HintBuilder Builder()
        {
            return new HintBuilder();
        }

        /// <summary>
        /// Построитель для создания сложных хинтов.
        /// </summary>
        public class HintBuilder
        {
            private readonly StringBuilder _sb = new StringBuilder();
            private float _duration = 5f;

            public HintBuilder Line(string text)
            {
                _sb.AppendLine(text);
                return this;
            }

            public HintBuilder ColorLine(string text, string color)
            {
                _sb.AppendLine(Color(text, color));
                return this;
            }

            public HintBuilder Title(string text, string color = White)
            {
                _sb.AppendLine(Bold(Size(Color(text, color), 35)));
                return this;
            }

            public HintBuilder Subtitle(string text, string color = Gray)
            {
                _sb.AppendLine(Italic(Color(text, color)));
                return this;
            }

            public HintBuilder Empty()
            {
                _sb.AppendLine();
                return this;
            }

            public HintBuilder Divider(int length = 30)
            {
                _sb.AppendLine(Color(Separator(length), Gray));
                return this;
            }

            public HintBuilder Bullet(string text)
            {
                _sb.AppendLine($"• {text}");
                return this;
            }

            public HintBuilder Number(int num, string text)
            {
                _sb.AppendLine($"{num}. {text}");
                return this;
            }

            public HintBuilder WithDuration(float duration)
            {
                _duration = duration;
                return this;
            }

            public string Build()
            {
                return _sb.ToString().TrimEnd();
            }

            public void SendTo(Player player)
            {
                FermixHintStack.ShowHint(player, Build(), _duration, category: HintCategory.Custom, showBullet: false);
            }

            public void SendToAll()
            {
                var hint = Build();
                foreach (var player in Player.List)
                {
                    FermixHintStack.ShowHint(player, hint, _duration, category: HintCategory.Custom, showBullet: false);
                }
            }
        }

        #endregion
    }
}
