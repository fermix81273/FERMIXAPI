using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Core.UserSettings;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using FermixAPI.Core;
using PlayerRoles;
using UnityEngine;

namespace FermixAPI.Systems
{
    /// <summary>
    /// SSS-биндинги SCP-106 («усиленный 106»):
    /// • <b>F</b> — телепорт через портал к ближайшему живому человеку, дистанция ≤ 100 м.
    ///
    /// Stalk-режим вырезан полностью: отдельного бинда и субкоманды
    /// больше нет — оставлена только ванильная игровая механика.
    ///
    /// SSS keybind'ы с пометкой [SCP-106] в названии появляются у игрока в меню
    /// Server Specific Settings и не пересекаются с обычными биндами
    /// FermixInput. Срабатывает только если игрок реально SCP-106.
    ///
    /// При спавне за SCP-106 игроку шлётся расширенный инфо-хинт (на ~37 сек),
    /// чтобы объяснить особенности роли и бинд.
    /// </summary>
    public static class FermixScp106Bindings
    {
        // Уникальные SSS-id для биндов 106. Не пересекаются ни с FermixInput
        // дефолтами (300-306), ни с пользовательскими (307+).
        private const int Scp106HeaderId = 308;
        private const int Scp106PortalKeyId = 320;

        // Радиус поиска цели для F-телепорта (запрос пользователя).
        private const float TeleportMaxRangeMeters = 100f;

        private const float CooldownSeconds = 1.5f;

        private static readonly Dictionary<string, DateTime> _lastUse = new(StringComparer.Ordinal);
        private static readonly List<SettingBase> _ownSettings = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized || FermixCore.Config?.Scp106PlusEnabled != true) return;
            if (FermixCore.Config?.Scp106BindingsEnabled != true) return;
            if (!FermixInput.IsInitialized)
            {
                FermixLog.Warn("FermixScp106Bindings: FermixInput не инициализирован, биндинги отключены.");
                return;
            }

            // Регистрируем СВОИ keybind'ы (не дефолтные), чтобы они появились
            // в SSS меню как отдельная секция [SCP-106]. На дефолтные F/Q
            // обработчики не вешаем — раньше это конфликтовало с voicechat
            // (Q) и другими подсистемами, использующими default F.
            try
            {
                var header = new HeaderSetting(Scp106HeaderId, "FermixAPI: SCP-106", string.Empty, false);
                _ownSettings.Add(header);

                _ownSettings.Add(MakeKeybind(
                    Scp106PortalKeyId,
                    "[SCP-106] Портал к цели (≤100м)",
                    KeyCode.F,
                    header,
                    "Открыть портал к ближайшему живому игроку-человеку в радиусе 100 метров."));

                FermixInput.DropExistingByIds(new[] { Scp106HeaderId, Scp106PortalKeyId });
                SettingBase.Register(_ownSettings);
            }
            catch (Exception ex)
            {
                FermixLog.Error($"FermixScp106Bindings: не удалось зарегистрировать SSS keybinds: {ex.Message}");
                _ownSettings.Clear();
                return;
            }

            // Бинды роутятся через onChanged-лямбду в MakeKeybind() — отдельный
            // FermixInput.RegisterPressedHandler не нужен (наши id вне его
            // дефолтного набора, а HandleKeybind им не управляет).

            // Личный бродкаст игроку, который только что заспавнился за 106.
            Exiled.Events.Handlers.Player.Spawned += OnSpawned;

            _initialized = true;
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            Exiled.Events.Handlers.Player.Spawned -= OnSpawned;

            try
            {
                if (_ownSettings.Count > 0) SettingBase.Unregister(settings: _ownSettings);
            }
            catch (Exception ex)
            {
                FermixLog.Warn($"FermixScp106Bindings.Shutdown unregister: {ex.Message}");
            }
            _ownSettings.Clear();

            _lastUse.Clear();
            _initialized = false;
        }

        private static KeybindSetting MakeKeybind(int id, string label, KeyCode defaultKey, HeaderSetting header, string description)
        {
            return new KeybindSetting(
                id: id,
                label: label,
                suggested: defaultKey,
                preventInteractionOnGUI: false,
                allowSpectatorTrigger: false,
                hintDescription: description,
                collectionId: byte.MaxValue,
                header: header,
                onChanged: (player, setting) =>
                {
                    if (setting is KeybindSetting kb)
                        OnKeybindChanged(player, kb);
                });
        }

        // SSS keybind onChanged шлёт нажатие/отпускание. Транслируем в
        // FermixInput-handlers (зарегистрированные через RegisterPressedHandler),
        // чтобы переиспользовать общий механизм.
        private static void OnKeybindChanged(Player player, KeybindSetting kb)
        {
            if (player == null) return;
            // FermixInput не делал вшитый routing для произвольных id, но мы
            // заранее зарегистрировали наши id через FermixInput.RegisterPressedHandler.
            // Поэтому просто диспатчим pressed-edge сами. Released/Held нам не нужны.
            if (kb.IsPressed)
            {
                if (kb.Id == Scp106PortalKeyId) OnPortalKey(player);
            }
        }

        private static bool Allowed(Player p)
        {
            if (p == null) return false;
            if (p.Role?.Type != RoleTypeId.Scp106) return false;
            string id = p.UserId ?? p.Nickname;
            if (_lastUse.TryGetValue(id, out var t) && (DateTime.UtcNow - t).TotalSeconds < CooldownSeconds)
                return false;
            _lastUse[id] = DateTime.UtcNow;
            return true;
        }

        private static void OnPortalKey(Player p)
        {
            if (!Allowed(p)) return;

            // Поиск ближайшего живого человека в радиусе 100 м.
            // SCP/Tutorial/None исключаем — телепорт работает только к врагам.
            Player target = null;
            float bestDist = TeleportMaxRangeMeters;
            foreach (var o in Player.List)
            {
                if (o == null || o == p || !o.IsAlive || !o.IsConnected) continue;
                var side = o.Role?.Side;
                if (side == null || side == Side.Scp || side == Side.None) continue;

                float d = Vector3.Distance(o.Position, p.Position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    target = o;
                }
            }

            if (target == null)
            {
                FermixHint.SendColored(p,
                    $"Нет живых людей в радиусе {TeleportMaxRangeMeters:0} м.",
                    FermixHint.Yellow,
                    2f);
                return;
            }

            if (p.Role?.As<Scp106Role>() is not Scp106Role role)
            {
                FermixHint.SendColored(p, "Не удалось получить роль 106.", FermixHint.Yellow, 2f);
                return;
            }

            float cost = FermixCore.Config?.Scp106PlusVigorCost ?? 0.3f;
            if (role.Vigor < cost)
            {
                FermixHint.SendColored(p,
                    $"Не хватает Vigor (нужно {cost:F2}).",
                    FermixHint.Yellow,
                    2f);
                return;
            }

            // Телепортируемся НА позицию цели (а не в её комнату), чтобы
            // материализоваться прямо в лицо. +1.0 по Y чтобы 106 не
            // оказался под полом.
            var targetPos = target.Position + Vector3.up * 1.0f;
            if (!role.UsePortal(targetPos, cost))
            {
                FermixHint.SendColored(p, "Портал не сработал.", FermixHint.Yellow, 2f);
                return;
            }

            FermixHint.SendColored(p,
                $"Портал к {target.Nickname} ({bestDist:0}м)",
                FermixHint.Magenta,
                2f);
        }

        // ── Personal spawn broadcast ─────────────────────────────────

        private static void OnSpawned(SpawnedEventArgs ev)
        {
            if (ev?.Player == null) return;
            if (ev.Player.Role?.Type != RoleTypeId.Scp106) return;

            // Делаем 1.5-секундную задержку, чтобы игроку успели прогрузиться
            // SCP UI / sounds — иначе hint иногда «съедается» сменой роли.
            var player = ev.Player;
            FermixScheduler.Delay(1.5f, () =>
            {
                try
                {
                    if (player == null || !player.IsConnected) return;
                    if (player.Role?.Type != RoleTypeId.Scp106) return;

                    FermixHint.SendColored(
                        player,
                        "<b>Ты — УСИЛЕННЫЙ SCP-106.</b>\n\n" +
                        "Основные возможности этой версии:\n" +
                        "• <color=#ff8b8b>F</color> — открыть портал к ближайшему живому врагу (радиус ≤100м).\n" +
                        "  Портал тратит Vigor — используй, когда жертва уже ослаблена.\n" +
                        "• <color=#ff8b8b>.106 tp &lt;комната&gt;</color> — портал в конкретную комнату.\n\n" +
                        "<color=#ffd24a>Подсказка:</color> отдельного Stalk-бинда больше нет —\n" +
                        "работает только ванильная игровая механика SCP-106.\n\n" +
                        "<size=80%><color=#aaaaaa>Биндинги настраиваются в Server Specific Settings.</color></size>",
                        FermixHint.Magenta,
                        37f);
                }
                catch (Exception ex)
                {
                    FermixLog.Warn($"FermixScp106Bindings.OnSpawned hint: {ex.Message}");
                }
            });
        }
    }
}
