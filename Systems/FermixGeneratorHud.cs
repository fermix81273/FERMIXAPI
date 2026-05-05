using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exiled.API.Enums;
using Exiled.API.Features;
using FermixAPI.Core;

namespace FermixAPI.Systems
{
    /// <summary>
    /// HUD для SCP-команды: отображает в углу экрана список активирующихся
    /// генераторов и оставшееся время до их окончательного запуска. Аналог
    /// HyperBeastHUB/SCP-Generator-List, переписанный под наш стек хинтов.
    /// </summary>
    public static class FermixGeneratorHud
    {
        private const string HintId = "fermix_generator_hud";
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
                AttachHud(p, interval);
            FermixScheduler.Repeat("fermix_generator_hud_attach", 5f, ReattachAll);
        }

        private static void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            ClearHud();
        }

        private static void ReattachAll()
        {
            float interval = Math.Max(0.5f, FermixCore.Config?.GeneratorHudUpdateInterval ?? 1f);
            foreach (Player p in Player.List)
                AttachHud(p, interval);
        }

        private static void AttachHud(Player player, float interval)
        {
            if (player == null) return;
            FermixHintStack.ShowPersistentDynamicHint(
                player,
                Render,
                HintId,
                updateInterval: interval,
                priority: -40,
                category: HintCategory.Custom,
                color: FermixHint.White,
                showBullet: false);
        }

        private static void ClearHud()
        {
            foreach (Player p in Player.List)
                FermixHintStack.RemoveHint(p, HintId);
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
