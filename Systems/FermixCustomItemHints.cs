using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Показывает игроку короткий хинт-описание при подборе кастомного предмета
    /// FermixAPI (NVG / SCRAMBLE / любой будущий, зарегистрированный через
    /// <see cref="Register"/>).
    ///
    /// Хук — <see cref="FermixEvents.OnItemPickup"/>. Срабатывает сразу после
    /// успешного PickingUp (с задержкой 0.05 с, чтобы предмет уехал в инвентарь
    /// и серийник был стабилен).
    /// </summary>
    public static class FermixCustomItemHints
    {
        /// <summary>
        /// Возвращает описание (строку с цветом и поясняющим текстом) для
        /// предмета с указанным serial, если предмет — кастомный.
        /// Если null/empty — предмет не наш, хинт не показываем.
        /// </summary>
        public delegate string DescribeBySerial(ushort serial);

        private static readonly object _lock = new();
        private static readonly List<DescribeBySerial> _resolvers = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;

            // Дефолтные резолверы — наши встроенные кастомные предметы.
            Register(serial => FermixNvg.IsNvgSerial(serial)
                ? "<size=110%><b><color=#33ff66>Прибор ночного видения (NVG)</color></b></size>\n" +
                  "Активация — бинд из меню SSS «FermixAPI: NVG» (по умолчанию <b>B</b>).\n" +
                  "Окружающие никакого свечения не видят."
                : null);

            Register(serial => FermixScramble.IsScrambleSerial(serial)
                ? "<size=110%><b><color=#ff66cc>SCRAMBLE — очки SCP-1344</color></b></size>\n" +
                  "Активируется ванильным биндом использования предмета.\n" +
                  "В Active-режиме игнорируете цели SCP-096."
                : null);

            FermixEvents.OnItemPickup += OnItemPickup;
            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            FermixEvents.OnItemPickup -= OnItemPickup;
            lock (_lock) _resolvers.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Зарегистрировать кастомный резолвер описания предмета.
        /// </summary>
        public static void Register(DescribeBySerial resolver)
        {
            if (resolver == null) return;
            lock (_lock) _resolvers.Add(resolver);
        }

        private static void OnItemPickup(PickingUpItemEventArgs ev)
        {
            if (ev?.Player == null || ev.Pickup == null) return;
            if (!ev.IsAllowed) return;

            ushort serial = ev.Pickup.Serial;
            var captured = ev.Player;

            // Хинт показываем С небольшим лагом, чтобы:
            //  1) ивент успел отработать (не отменился позже какой-то другой
            //     подсистемой),
            //  2) серийник стабилизировался в инвентаре игрока.
            FermixScheduler.Delay(0.10f, () =>
            {
                try
                {
                    string description = ResolveDescription(serial);
                    if (string.IsNullOrEmpty(description)) return;
                    if (captured == null || !captured.IsConnected || !captured.IsAlive) return;
                    FermixHint.Send(captured, description, 4f);
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixCustomItemHints.OnItemPickup: {ex.Message}");
                }
            });
        }

        private static string ResolveDescription(ushort serial)
        {
            DescribeBySerial[] snapshot;
            lock (_lock) snapshot = _resolvers.ToArray();
            foreach (var r in snapshot)
            {
                try
                {
                    var d = r(serial);
                    if (!string.IsNullOrEmpty(d)) return d;
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixCustomItemHints resolver: {ex.Message}");
                }
            }
            return null;
        }
    }
}
