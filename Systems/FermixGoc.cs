using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using FermixAPI.Core;
using PlayerRoles;

namespace FermixAPI.Systems
{
    /// <summary>
    /// G.O.C. (Global Occult Coalition) — отдельный отряд, спаунящийся
    /// волной MTF. Внешне — стандартный NTF, но во внутренней логике
    /// он враждебен и MTF, и Chaos, и SCP. Друг к другу — союзники
    /// (на одной фракции NtfSergeant, friendly fire выключен по дефолту).
    /// Реализовано без новой роли: трекаем игроков по UserId и
    /// ловим Hurting между ними и обычными MTF.
    /// </summary>
    public static class FermixGoc
    {
        private static readonly HashSet<string> _members = new(StringComparer.Ordinal);
        private static readonly object _lock = new();
        private static bool _initialized;

        public static IReadOnlyCollection<string> Members
        {
            get { lock (_lock) return _members.ToArray(); }
        }

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.GocEnabled != true) return;
            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;
            FermixEvents.OnPlayerLeave += OnPlayerLeave;
            FermixEvents.OnPlayerHurt += OnPlayerHurt;
            Exiled.Events.Handlers.Server.RespawnedTeam += OnRespawnedTeam;
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;
            FermixEvents.OnPlayerLeave -= OnPlayerLeave;
            FermixEvents.OnPlayerHurt -= OnPlayerHurt;
            Exiled.Events.Handlers.Server.RespawnedTeam -= OnRespawnedTeam;
            ClearAll();
            _initialized = false;
        }

        public static bool IsMember(Player p) =>
            p?.UserId != null && _members.Contains(p.UserId);

        public static void Mark(Player p, bool announce = true)
        {
            if (p?.UserId == null) return;
            lock (_lock) _members.Add(p.UserId);
            try
            {
                p.CustomInfo = "<color=#ffd24a>G.O.C.</color>";
                ApplyLoadout(p);
            }
            catch (Exception e) { FermixLog.Error($"[GOC] Mark: {e.Message}"); }
            if (announce) FermixHint.SendColored(p, "Ты — оперативник G.O.C.\nВраги: SCP, Chaos и MTF.", FermixHint.Gold, 6f);
        }

        public static void Unmark(Player p)
        {
            if (p?.UserId == null) return;
            lock (_lock) _members.Remove(p.UserId);
            try { if (p.CustomInfo != null && p.CustomInfo.Contains("G.O.C.")) p.CustomInfo = string.Empty; }
            catch { }
        }

        private static void ClearAll()
        {
            string[] ids;
            lock (_lock) { ids = _members.ToArray(); _members.Clear(); }
            foreach (var id in ids)
            {
                var p = Player.Get(id);
                if (p != null) try { if (p.CustomInfo?.Contains("G.O.C.") == true) p.CustomInfo = string.Empty; } catch { }
            }
        }

        private static void OnRoundStart() { ClearAll(); }
        private static void OnRoundEnd(RoundEndedEventArgs _) => ClearAll();

        private static void OnPlayerLeave(LeftEventArgs ev)
        {
            if (ev?.Player?.UserId == null) return;
            lock (_lock) _members.Remove(ev.Player.UserId);
        }

        private static void OnRespawnedTeam(RespawnedTeamEventArgs ev)
        {
            if (ev?.Players == null) return;
            // Считаем, что это «MTF-волна», если хоть один заспавнился в Team.FoundationForces.
            var spawned = ev.Players.Where(p => p?.Role?.Team == Team.FoundationForces).ToList();
            if (spawned.Count == 0) return;

            float chance = Math.Max(0f, Math.Min(1f, FermixCore.Config?.GocWaveChance ?? 0.1f));
            if (UnityEngine.Random.value > chance) return;

            FermixScheduler.Delay(0.5f, () =>
            {
                foreach (var p in spawned)
                    if (p != null && p.IsAlive) Mark(p, announce: true);

                BroadcastAnnouncement(spawned.Count);
            });
        }

        private static void BroadcastAnnouncement(int count)
        {
            string msg = $"<color=#ffd24a>Прибыли оперативники G.O.C.</color> ({count}). Они враждебны всем.";
            foreach (var p in Player.List) FermixHint.Send(p, msg, 5f);
        }

        private static void ApplyLoadout(Player p)
        {
            try
            {
                p.ClearInventory();
                p.AddItem(ItemType.GunE11SR);
                p.AddItem(ItemType.GunCOM18);
                p.AddItem(ItemType.Medkit);
                p.AddItem(ItemType.Adrenaline);
                p.AddItem(ItemType.KeycardMTFCaptain);
                p.AddItem(ItemType.GrenadeFlash);
            }
            catch (Exception e) { FermixLog.Warn($"[GOC] ApplyLoadout: {e.Message}"); }
        }

        private static void OnPlayerHurt(HurtingEventArgs ev)
        {
            if (ev == null || ev.Player == null || ev.Attacker == null) return;
            if (ev.Player == ev.Attacker) return;

            bool atkGoc = IsMember(ev.Attacker);
            bool tgtGoc = IsMember(ev.Player);
            if (!atkGoc && !tgtGoc) return;

            // GOC vs GOC — friendly fire выключен (стандартный teamkill prevention NTF).
            if (atkGoc && tgtGoc) return;

            // GOC vs MTF (или MTF vs GOC) — стандартно блокируется как тимкилл, форсим разрешение.
            bool atkMtf = ev.Attacker.Role?.Team == Team.FoundationForces;
            bool tgtMtf = ev.Player.Role?.Team == Team.FoundationForces;
            if ((atkGoc && tgtMtf && !tgtGoc) || (atkMtf && !atkGoc && tgtGoc))
                ev.IsAllowed = true;
        }
    }
}
