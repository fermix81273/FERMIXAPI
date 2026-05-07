using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using FermixAPI.Core;
using FermixAPI.Hints.Core.Enum;
using FermixAPI.Hints.Core.Utilities;
using MEC;
using UnityEngine;
using HsmHint = FermixAPI.Hints.Core.Models.Hints.Hint;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Голосования игроков (инициируются админом): kick / restart / freeform.
    /// Голосование рисуется в правом нижнем углу через собственный
    /// <see cref="HsmHint"/> (отдельная группа PlayerDisplay), чтобы не
    /// перекрывать центральный hint-стек других подсистем.
    /// </summary>
    public static class FermixCallvote
    {
        public enum VoteKind { Kick, Restart, Custom }

        private const string HsmGroupName = "FermixAPI.Callvote";
        private const float HudYCoordinate = 200f;   // от нижней кромки экрана
        private const float HudXCoordinate = -40f;   // от правой кромки экрана
        private const int HudFontSize = 18;

        private static readonly object _lock = new();
        private static readonly Dictionary<Player, HsmHint> _hudHints = new();
        private static ActiveVote _vote;
        private static CoroutineHandle _ticker;
        private static bool _initialized;

        // Сохранённая ссылка на handler — иначе анонимный лямбда никогда
        // не отписался бы (Action equality не работает на разных делегатах
        // одной и той же лямбды), и при reload плагина мы тащили бы за
        // собой висящие подписки.
        private static Action<Exiled.Events.EventArgs.Server.RoundEndedEventArgs> _onRoundEnd;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.CallvoteEnabled != true) return;
            _onRoundEnd = _ => Cancel("раунд завершён");
            FermixEvents.OnRoundEnd += _onRoundEnd;
            FermixEvents.OnPlayerJoin += OnPlayerJoinedDeferred;
            FermixEvents.OnPlayerLeave += OnPlayerLeft;
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            if (_onRoundEnd != null)
            {
                FermixEvents.OnRoundEnd -= _onRoundEnd;
                _onRoundEnd = null;
            }
            FermixEvents.OnPlayerJoin -= OnPlayerJoinedDeferred;
            FermixEvents.OnPlayerLeave -= OnPlayerLeft;
            Cancel(null);
            _initialized = false;
        }

        // Привязываем HUD новому игроку, если голосование уже идёт. Делаем
        // это с тем же 5-секундным defer'ом, что и FermixChat (чтобы не
        // сломать join: Mirror NetworkBehaviour ещё инициализируется).
        private static void OnPlayerJoinedDeferred(Exiled.Events.EventArgs.Player.JoinedEventArgs ev)
        {
            if (ev?.Player == null) return;
            var player = ev.Player;
            FermixScheduler.Delay(5f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;
                    bool active;
                    lock (_lock) active = _vote != null;
                    if (!active) return;
                    AttachHudFor(player);
                    PushHudText();
                }
                catch (Exception ex) { FermixLog.Warn($"FermixCallvote.OnPlayerJoinedDeferred: {ex.Message}"); }
            });
        }

        private static void OnPlayerLeft(Exiled.Events.EventArgs.Player.LeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            HsmHint hint = null;
            lock (_lock)
            {
                if (_hudHints.TryGetValue(ev.Player, out hint)) _hudHints.Remove(ev.Player);
            }
            try
            {
                if (hint != null && ev.Player.ReferenceHub != null)
                    PlayerDisplay.Get(ev.Player.ReferenceHub).RemoveHint(hint, HsmGroupName);
            }
            catch (Exception ex) { FermixLog.Warn($"FermixCallvote.OnPlayerLeft: {ex.Message}"); }
        }

        public static bool TryStart(Player author, VoteKind kind, string targetArg, string reason, out string error)
        {
            error = null;
            if (!_initialized) { error = "Голосования выключены."; return false; }

            float cd = FermixCore.Config?.CallvoteCooldown ?? 60f;
            lock (_lock)
            {
                if (_vote != null) { error = "Уже идёт голосование."; return false; }
                if (_lastEnded != DateTime.MinValue && (DateTime.UtcNow - _lastEnded).TotalSeconds < cd)
                {
                    int left = (int)Math.Ceiling(cd - (DateTime.UtcNow - _lastEnded).TotalSeconds);
                    error = $"Подожди ещё {left} сек до следующего голосования.";
                    return false;
                }

                Player target = null;
                if (kind == VoteKind.Kick)
                {
                    target = ResolveTarget(targetArg);
                    if (target == null) { error = $"Игрок '{targetArg}' не найден."; return false; }
                    if (target == author) { error = "Себя нельзя."; return false; }
                    if (target.RemoteAdminAccess) { error = "Нельзя голосовать против админа."; return false; }
                }

                _vote = new ActiveVote
                {
                    Kind = kind,
                    Author = author,
                    Target = target,
                    Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                    EndsAt = DateTime.UtcNow.AddSeconds(FermixCore.Config?.CallvoteDuration ?? 30f),
                    Threshold = kind == VoteKind.Kick ? 0.6f : 0.5f,
                };
            }

            FermixLog.Info($"[VOTE] {author?.Nickname} начал {kind} {targetArg ?? reason}");
            AttachHudToAll();
            _ticker = FermixScheduler.Repeat("fermix_callvote_tick", 1f, Tick);
            return true;
        }

        public static bool TryCast(Player voter, bool yes, out string error)
        {
            error = null;
            lock (_lock)
            {
                if (_vote == null) { error = "Сейчас нет активного голосования."; return false; }
                string id = voter.UserId ?? voter.Nickname;
                if (_vote.Cast.ContainsKey(id)) { error = "Ты уже голосовал."; return false; }
                _vote.Cast[id] = yes;
            }
            try { PushHudText(); }
            catch (Exception ex) { FermixLog.Warn($"FermixCallvote.TryCast PushHudText: {ex.Message}"); }
            return true;
        }

        public static void Cancel(string reason)
        {
            lock (_lock)
            {
                if (_vote == null) return;
                _vote = null;
                _lastEnded = DateTime.UtcNow;
            }
            if (_ticker.IsValid) Timing.KillCoroutines(_ticker);
            DetachHudFromAll();
            if (!string.IsNullOrEmpty(reason))
                BroadcastAll($"<color=#888888>Голосование отменено: {reason}.</color>");
        }

        private static DateTime _lastEnded = DateTime.MinValue;

        private static void Tick()
        {
            ActiveVote v;
            lock (_lock) v = _vote;
            if (v == null) { if (_ticker.IsValid) Timing.KillCoroutines(_ticker); return; }

            // Каждую секунду перерисовываем HUD (таймер обратного отсчёта,
            // обновлённое количество за/против).
            try { PushHudText(); }
            catch (Exception ex) { FermixLog.Warn($"FermixCallvote.Tick PushHudText: {ex.Message}"); }

            if (DateTime.UtcNow >= v.EndsAt) Resolve();
        }

        private static void Resolve()
        {
            ActiveVote v;
            lock (_lock) { v = _vote; _vote = null; _lastEnded = DateTime.UtcNow; }
            if (_ticker.IsValid) Timing.KillCoroutines(_ticker);
            DetachHudFromAll();
            if (v == null) return;

            int yes = v.Cast.Count(kv => kv.Value);
            int no = v.Cast.Count - yes;
            int total = Math.Max(1, Player.List.Count(p => !p.IsHost));
            float ratio = yes / (float)total;
            bool passed = ratio >= v.Threshold;

            string title = DescribeVote(v);
            string verdict = passed ? "<color=#5cd45c>принято</color>" : "<color=#ff4444>отклонено</color>";
            BroadcastAll($"Голосование {verdict}: {title} ({yes}/{no}, нужно {Mathf.CeilToInt(v.Threshold * total)})");

            if (passed) Execute(v);
        }

        private static void Execute(ActiveVote v)
        {
            try
            {
                switch (v.Kind)
                {
                    case VoteKind.Kick:
                        v.Target?.Kick(v.Reason ?? "Кикнут голосованием");
                        break;
                    case VoteKind.Restart:
                        FermixScheduler.Delay(2f, () => Round.Restart());
                        break;
                    case VoteKind.Custom:
                        // Просто оглашение результата.
                        break;
                }
            }
            catch (Exception e) { FermixLog.Error($"[VOTE] Execute: {e.Message}"); }
        }

        private static void AttachHudToAll()
        {
            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || player.ReferenceHub == null) continue;
                AttachHudFor(player);
            }
            PushHudText();
        }

        private static void AttachHudFor(Player player)
        {
            HsmHint hint;
            lock (_lock)
            {
                if (_hudHints.ContainsKey(player)) return;
                hint = new HsmHint
                {
                    YCoordinate = HudYCoordinate,
                    XCoordinate = HudXCoordinate,
                    Alignment = HintAlignment.Right,
                    YCoordinateAlign = HintVerticalAlign.Bottom,
                    SyncSpeed = HintSyncSpeed.Normal,
                    FontSize = HudFontSize,
                    Text = string.Empty,
                };
                _hudHints[player] = hint;
            }

            try
            {
                PlayerDisplay.Get(player.ReferenceHub).AddHint(hint, HsmGroupName);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixCallvote.AttachHudFor: {ex.Message}");
                lock (_lock) _hudHints.Remove(player);
            }
        }

        private static void DetachHudFromAll()
        {
            List<KeyValuePair<Player, HsmHint>> snapshot;
            lock (_lock)
            {
                snapshot = new List<KeyValuePair<Player, HsmHint>>(_hudHints);
                _hudHints.Clear();
            }

            foreach (var kv in snapshot)
            {
                try
                {
                    if (kv.Key?.ReferenceHub != null)
                        PlayerDisplay.Get(kv.Key.ReferenceHub).RemoveHint(kv.Value, HsmGroupName);
                }
                catch (Exception ex) { FermixLog.Warn($"FermixCallvote.DetachHud: {ex.Message}"); }
            }
        }

        // Принудительный апдейт текста HUD для всех зарегистрированных хинтов.
        // Вызывается из Tick() каждую секунду и сразу при cast/cancel.
        private static void PushHudText()
        {
            string text = RenderHud();

            List<KeyValuePair<Player, HsmHint>> snapshot;
            lock (_lock)
            {
                snapshot = new List<KeyValuePair<Player, HsmHint>>(_hudHints);
            }

            foreach (var kv in snapshot)
            {
                if (kv.Value == null) continue;
                try
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        kv.Value.Text = string.Empty;
                        if (!kv.Value.Hide) kv.Value.Hide = true;
                    }
                    else
                    {
                        kv.Value.Text = text;
                        if (kv.Value.Hide) kv.Value.Hide = false;
                    }
                }
                catch (Exception ex) { FermixLog.Warn($"FermixCallvote.PushHudText: {ex.Message}"); }
            }
        }

        private static string RenderHud()
        {
            ActiveVote v;
            lock (_lock) v = _vote;
            if (v == null) return string.Empty;

            int yes = v.Cast.Count(kv => kv.Value);
            int no = v.Cast.Count - yes;
            int left = Math.Max(0, (int)Math.Ceiling((v.EndsAt - DateTime.UtcNow).TotalSeconds));
            return $"<color=#ffd24a>Голосование</color>: {DescribeVote(v)}\n" +
                   $"<color=#5cd45c>За {yes}</color> | <color=#ff4444>Против {no}</color> | {left}с " +
                   $"<color=#888888>(.vote y/n)</color>";
        }

        private static string DescribeVote(ActiveVote v) => v.Kind switch
        {
            VoteKind.Kick => $"кикнуть {v.Target?.Nickname ?? "?"}" + (v.Reason != null ? $" ({v.Reason})" : ""),
            VoteKind.Restart => "перезапустить раунд",
            VoteKind.Custom => v.Reason ?? "произвольный вопрос",
            _ => "?",
        };

        private static Player ResolveTarget(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return null;
            arg = arg.Trim();
            return Player.Get(arg)
                ?? Player.List.FirstOrDefault(p => p.Nickname?.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void BroadcastAll(string text)
        {
            foreach (var p in Player.List) FermixHint.Send(p, text, 5f);
        }

        private sealed class ActiveVote
        {
            public VoteKind Kind;
            public Player Author;
            public Player Target;
            public string Reason;
            public DateTime EndsAt;
            public float Threshold;
            public Dictionary<string, bool> Cast = new(StringComparer.Ordinal);
        }
    }
}
