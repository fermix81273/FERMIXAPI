using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Enums;
using Exiled.API.Features;
using FermixAPI.Core;
using FermixAPI.Hints.Core.Enum;
using FermixAPI.Hints.Core.Utilities;
using HsmHint = FermixAPI.Hints.Core.Models.Hints.Hint;

namespace FermixAPI.Systems
{
    /// <summary>
    /// HUD для SCP-команды: отображает в ЛЕВОМ НИЖНЕМ углу экрана список
    /// активирующихся генераторов и оставшееся время до их окончательного
    /// запуска. Использует собственный <see cref="HsmHint"/> в отдельной группе
    /// PlayerDisplay ("FermixAPI.GenHud"), чтобы не наслаиваться на центральный
    /// hint-стек.
    /// </summary>
    public static class FermixGeneratorHud
    {
        private const string HsmGroupName = "FermixAPI.GenHud";

        // Координаты — нижний левый угол, чуть выше дна экрана.
        //   YCoordinate=200 от низа (с YCoordinateAlign.Bottom).
        //   XCoordinate=40 от левого края (с Alignment.Left).
        private const float HudYCoordinate = 200f;
        private const float HudXCoordinate = 40f;
        private const int HudFontSize = 18;

        private static readonly object _lock = new();
        private static readonly Dictionary<Player, HsmHint> _hudHints = new();
        private static bool _initialized;

        /// <summary>Подписаться на события и развесить персистентный HUD.</summary>
        public static void Initialize()
        {
            if (_initialized) return;
            if (FermixCore.Config == null || !FermixCore.Config.GeneratorHudEnabled) return;

            FermixEvents.OnRoundStart += OnRoundStart;
            FermixEvents.OnRoundEnd += OnRoundEnd;

            _initialized = true;
        }

        /// <summary>Отписаться и снять HUD у всех игроков.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            FermixEvents.OnRoundStart -= OnRoundStart;
            FermixEvents.OnRoundEnd -= OnRoundEnd;

            ClearHud();

            _initialized = false;
        }

        private static void OnRoundStart()
        {
            ClearHud();
            float interval = Math.Max(0.5f, FermixCore.Config?.GeneratorHudUpdateInterval ?? 1f);
            foreach (Player p in Player.List)
                AttachHud(p);
            FermixScheduler.Repeat("fermix_generator_hud_attach", 5f, ReattachAll);
            FermixScheduler.Repeat("fermix_generator_hud_text", interval, PushTextToAllHints);
        }

        private static void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            ClearHud();
        }

        private static void ReattachAll()
        {
            foreach (Player p in Player.List)
                AttachHud(p);
        }

        private static void AttachHud(Player player)
        {
            if (player == null || !player.IsConnected || player.ReferenceHub == null) return;

            HsmHint hint;
            lock (_lock)
            {
                if (_hudHints.ContainsKey(player)) return;
                hint = new HsmHint
                {
                    YCoordinate = HudYCoordinate,
                    XCoordinate = HudXCoordinate,
                    Alignment = HintAlignment.Left,
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
                FermixLog.Warn($"FermixGeneratorHud.AttachHud: {ex.Message}");
                lock (_lock) _hudHints.Remove(player);
            }
        }

        private static void ClearHud()
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
                catch (Exception ex) { FermixLog.Warn($"FermixGeneratorHud.ClearHud: {ex.Message}"); }
            }
        }

        private static void PushTextToAllHints()
        {
            List<KeyValuePair<Player, HsmHint>> snapshot;
            lock (_lock)
            {
                snapshot = new List<KeyValuePair<Player, HsmHint>>(_hudHints);
            }

            foreach (var kv in snapshot)
            {
                if (kv.Value == null) continue;
                if (kv.Key == null || !kv.Key.IsConnected) continue;

                try
                {
                    string text = Render(kv.Key);
                    if (string.IsNullOrEmpty(text))
                    {
                        if (!kv.Value.Hide) kv.Value.Hide = true;
                        kv.Value.Text = string.Empty;
                    }
                    else
                    {
                        kv.Value.Text = text;
                        if (kv.Value.Hide) kv.Value.Hide = false;
                    }
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixGeneratorHud.PushText for {kv.Key?.Nickname}: {ex.Message}");
                }
            }
        }

        private static string Render(Player viewer)
        {
            if (viewer == null || viewer.Role?.Side != Side.Scp) return string.Empty;

            var lines = new List<string>(8);
            foreach (var gen in Generator.List)
            {
                if (gen == null) continue;
                if (!gen.IsActivating || gen.IsEngaged) continue;

                int remaining = Math.Max(0, (int)Math.Round((double)gen.CurrentTime));
                string color = ColorForRemaining(remaining);
                string roomName = LocalizeRoom(gen.Room?.Type ?? RoomType.Unknown);
                lines.Add($"<size=20><color={color}>GEN {roomName}: {remaining}с</color></size>");
            }

            int engaged = Generator.List.Count(g => g != null && g.IsEngaged);
            if (engaged > 0)
                lines.Add($"<size=18><color=#888888>Запущено: {engaged}</color></size>");

            if (lines.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<size=18><color=#cccccc>Генераторы</color></size>\n");
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Count - 1) sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string ColorForRemaining(int remaining)
        {
            if (remaining <= 15) return "#ff4444";
            if (remaining <= 30) return "#f0c44a";
            return "#5cd45c";
        }

        private static string LocalizeRoom(RoomType type) => type switch
        {
            RoomType.Hcz079 => "079",
            RoomType.HczArmory => "Армори",
            RoomType.HczHid => "MicroHID",
            RoomType.Hcz106 => "106",
            RoomType.Hcz049 => "049",
            RoomType.Hcz939 => "939",
            RoomType.Hcz096 => "096",
            RoomType.HczNuke => "Ядерка",
            RoomType.HczTestRoom => "Тестовая",
            RoomType.HczCrossing => "Крест",
            RoomType.HczCurve => "Изгиб",
            RoomType.HczStraight => "Коридор",
            RoomType.HczTesla => "Тесла",
            RoomType.HczEzCheckpointA => "ЧП-EZ-A",
            RoomType.HczEzCheckpointB => "ЧП-EZ-B",
            _ => type.ToString(),
        };
    }
}
