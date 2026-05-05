using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using FermixAPI.Core;
using MEC;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Голосования игроков: kick / restart / freeform. Один активный слот;
    /// HUD с таймером и счётом «за/против» отображается у всех живых.
    /// </summary>
    public static class FermixCallvote
    {
        public enum VoteKind { Kick, Restart, Custom }

        private const string HudId = "fermix_callvote";
        private static readonly object _lock = new();
        private static ActiveVote _vote;
        private static CoroutineHandle _ticker;
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.CallvoteEnabled != true) return;
            FermixEvents.OnRoundEnd += _ => Cancel("раунд завершён");
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            Cancel(null);
            _initialized = false;
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
            foreach (var p in Player.List) FermixHintStack.RemoveHint(p, HudId);
            if (!string.IsNullOrEmpty(reason))
                BroadcastAll($"<color=#888888>Голосование отменено: {reason}.</color>");
        }

        private static DateTime _lastEnded = DateTime.MinValue;

        private static void Tick()
        {
            ActiveVote v;
            lock (_lock) v = _vote;
            if (v == null) { if (_ticker.IsValid) Timing.KillCoroutines(_ticker); return; }
            if (DateTime.UtcNow >= v.EndsAt) Resolve();
        }

        private static void Resolve()
        {
            ActiveVote v;
            lock (_lock) { v = _vote; _vote = null; _lastEnded = DateTime.UtcNow; }
            if (_ticker.IsValid) Timing.KillCoroutines(_ticker);
            foreach (var p in Player.List) FermixHintStack.RemoveHint(p, HudId);
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
            foreach (var p in Player.List)
                FermixHintStack.ShowPersistentDynamicHint(p, _ => RenderHud(), HudId,
                    updateInterval: 1f, priority: -10, color: FermixHint.White, showBullet: false);
        }

        private static string RenderHud()
        {
            ActiveVote v;
            lock (_lock) v = _vote;
            if (v == null) return string.Empty;

            int yes = v.Cast.Count(kv => kv.Value);
            int no = v.Cast.Count - yes;
            int left = Math.Max(0, (int)Math.Ceiling((v.EndsAt - DateTime.UtcNow).TotalSeconds));
            return $"<size=20><color=#ffd24a>Голосование</color>: {DescribeVote(v)}\n" +
                   $"<color=#5cd45c>За {yes}</color> | <color=#ff4444>Против {no}</color> | {left}с " +
                   $"<color=#888888>(.vote y / .vote n)</color></size>";
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
