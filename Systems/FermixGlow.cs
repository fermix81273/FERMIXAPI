using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.Features;
using FermixAPI.Core;
using MEC;
using UnityEngine;
using Handlers = Exiled.Events.Handlers;
using Light = Exiled.API.Features.Toys.Light;

namespace FermixAPI.Systems
{
    /// <summary>
    /// Кастомное свечение предметов (pickups и предметов в руках) через
    /// EXILED <see cref="Light"/> (LightSourceToy). Поддерживает HEX/Color,
    /// pulse, rainbow и точечную настройку интенсивности/радиуса.
    ///
    /// Конфигурации регистрируются по ID; светящиеся объекты пересоздаются
    /// автоматически на старте раунда и при подборе/выбросе предметов.
    /// </summary>
    public static class FermixGlow
    {
        /// <summary>Конфигурация одной "подсветки".</summary>
        public sealed class GlowConfig
        {
            /// <summary>Какие сериалы предметов подсвечивать.</summary>
            public Func<ushort, bool> ItemCheck { get; set; }

            /// <summary>Цвет.</summary>
            public Color Color { get; set; } = Color.white;

            /// <summary>Базовая интенсивность.</summary>
            public float Intensity { get; set; } = 1f;

            /// <summary>Радиус.</summary>
            public float Range { get; set; } = 5f;

            /// <summary>Период тика обновления (сек).</summary>
            public float UpdateInterval { get; set; } = 0.1f;

            /// <summary>Подсвечивать, когда предмет в руке.</summary>
            public bool GlowInHands { get; set; } = true;

            /// <summary>Включить пульсацию интенсивности.</summary>
            public bool PulseEffect { get; set; }

            /// <summary>Скорость пульсации (1 = sin от Time.time).</summary>
            public float PulseSpeed { get; set; } = 1f;
        }

        // Внутренний "тег" каждого активного источника света — какой config'у он принадлежит.
        private sealed class GlowTag
        {
            public Light Light;
            public string ConfigId;
        }

        private static readonly Dictionary<string, GlowConfig> _configs = new(StringComparer.Ordinal);

        // GameObject -> активный источник света (один свет на игрока/pickup).
        private static readonly Dictionary<GameObject, GlowTag> _active = new();

        private static readonly Dictionary<string, CoroutineHandle> _coroutines = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, CoroutineHandle> _rainbowCoroutines = new(StringComparer.Ordinal);

        private static bool _initialized;

        // Кэш делегатов для отписки.
        private static CustomEventHandler _onRoundStarted;
        private static CustomEventHandler<RoundEndedEventArgs> _onRoundEnded;
        private static CustomEventHandler<ChangingItemEventArgs> _onChangingItem;
        private static CustomEventHandler<DroppingItemEventArgs> _onDroppingItem;
        private static CustomEventHandler<PickupAddedEventArgs> _onPickupAdded;
        private static CustomEventHandler<PickupDestroyedEventArgs> _onPickupDestroyed;

        #region Lifecycle

        /// <summary>Подписывается на события EXILED. Идемпотентно.</summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _onRoundStarted = OnRoundStarted;
            _onRoundEnded = OnRoundEnded;
            _onChangingItem = OnChangingItem;
            _onDroppingItem = OnDroppingItem;
            _onPickupAdded = OnPickupAdded;
            _onPickupDestroyed = OnPickupDestroyed;

            Handlers.Server.RoundStarted.Subscribe(_onRoundStarted);
            Handlers.Server.RoundEnded.Subscribe(_onRoundEnded);
            Handlers.Player.ChangingItem.Subscribe(_onChangingItem);
            Handlers.Player.DroppingItem.Subscribe(_onDroppingItem);
            Handlers.Map.PickupAdded.Subscribe(_onPickupAdded);
            Handlers.Map.PickupDestroyed.Subscribe(_onPickupDestroyed);

            _initialized = true;
            FermixLog.Info("FermixGlow инициализирован.");
        }

        /// <summary>Отписывается от событий, гасит все источники света.</summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            try { Handlers.Server.RoundStarted.Unsubscribe(_onRoundStarted); } catch { /* ignore */ }
            try { Handlers.Server.RoundEnded.Unsubscribe(_onRoundEnded); } catch { /* ignore */ }
            try { Handlers.Player.ChangingItem.Unsubscribe(_onChangingItem); } catch { /* ignore */ }
            try { Handlers.Player.DroppingItem.Unsubscribe(_onDroppingItem); } catch { /* ignore */ }
            try { Handlers.Map.PickupAdded.Unsubscribe(_onPickupAdded); } catch { /* ignore */ }
            try { Handlers.Map.PickupDestroyed.Unsubscribe(_onPickupDestroyed); } catch { /* ignore */ }

            CleanupAllGlows();
            _configs.Clear();
            _initialized = false;
        }

        #endregion

        #region Public API — простые helper'ы (как в sosal.dll)

        /// <summary>
        /// Добавить статическую подсветку с HEX-цветом. Возвращает уникальный id.
        /// </summary>
        public static string AddGlowHex(
            Func<ushort, bool> itemCheck,
            string hexColor,
            float intensity = 1f,
            float range = 5f,
            float updateInterval = 0.1f,
            bool glowInHands = true)
        {
            var id = $"Glow_{hexColor}_{DateTime.Now.Ticks}";
            AddGlowHex(id, itemCheck, hexColor, intensity, range, updateInterval, glowInHands);
            return id;
        }

        /// <summary>
        /// Добавить пульсирующую подсветку с HEX-цветом. Возвращает уникальный id.
        /// </summary>
        public static string AddPulsingGlow(
            Func<ushort, bool> itemCheck,
            string hexColor,
            float intensity = 1f,
            float range = 5f,
            float pulseSpeed = 1f,
            bool glowInHands = true)
        {
            var id = $"PulseGlow_{hexColor}_{DateTime.Now.Ticks}";
            AddGlowHex(id, itemCheck, hexColor, intensity, range, 0.02f, glowInHands, pulseEffect: true, pulseSpeed: pulseSpeed);
            return id;
        }

        /// <summary>
        /// Добавить "радужную" подсветку (HSV-вращение). Возвращает уникальный id.
        /// </summary>
        public static string AddRainbowGlow(
            Func<ushort, bool> itemCheck,
            float intensity = 1f,
            float range = 5f,
            bool glowInHands = true)
        {
            var id = $"RainbowGlow_{DateTime.Now.Ticks}";
            AddGlow(id, itemCheck, Color.red, intensity, range, 0.05f, glowInHands);
            _rainbowCoroutines[id] = Timing.RunCoroutine(RainbowCoroutine(id));
            return id;
        }

        #endregion

        #region Public API — низкоуровневые методы

        /// <summary>Добавить/перезаписать конфигурацию подсветки с HEX-цветом.</summary>
        public static void AddGlowHex(
            string id,
            Func<ushort, bool> itemCheck,
            string hexColor,
            float intensity = 1f,
            float range = 5f,
            float updateInterval = 0.1f,
            bool glowInHands = true,
            bool pulseEffect = false,
            float pulseSpeed = 1f)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));
            if (itemCheck == null)
                throw new ArgumentNullException(nameof(itemCheck));

            if (!ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                FermixLog.Error($"FermixGlow: неверный HEX-цвет '{hexColor}' (id={id}).");
                return;
            }

            AddGlow(id, itemCheck, color, intensity, range, updateInterval, glowInHands, pulseEffect, pulseSpeed);
        }

        /// <summary>Добавить/перезаписать конфигурацию подсветки с готовым <see cref="Color"/>.</summary>
        public static void AddGlow(
            string id,
            Func<ushort, bool> itemCheck,
            Color color,
            float intensity = 1f,
            float range = 5f,
            float updateInterval = 0.1f,
            bool glowInHands = true,
            bool pulseEffect = false,
            float pulseSpeed = 1f)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id));
            if (itemCheck == null)
                throw new ArgumentNullException(nameof(itemCheck));

            // Чем чаще тик — тем плавнее обновление, но дороже. Защитимся от 0/отрицательных.
            var safeInterval = updateInterval <= 0f ? 0.05f : updateInterval;

            if (_configs.ContainsKey(id))
                FermixLog.Warn($"FermixGlow: подсветка '{id}' уже существует — обновляю конфигурацию.");

            _configs[id] = new GlowConfig
            {
                ItemCheck = itemCheck,
                Color = color,
                Intensity = intensity,
                Range = range,
                UpdateInterval = safeInterval,
                GlowInHands = glowInHands,
                PulseEffect = pulseEffect,
                PulseSpeed = pulseSpeed,
            };

            // Перезапускаем корутину с актуальной конфигурацией.
            StopCoroutine(_coroutines, id);
            if (Round.IsStarted)
                _coroutines[id] = Timing.RunCoroutine(UpdateGlowCoroutine(id));
        }

        /// <summary>Удалить подсветку по id (включая все активные источники света).</summary>
        public static void RemoveGlow(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (!_configs.Remove(id))
                return;

            StopCoroutine(_coroutines, id);
            StopCoroutine(_rainbowCoroutines, id);

            // Убираем все активные источники света этого конфига.
            var toRemove = _active
                .Where(kvp => kvp.Value.ConfigId == id)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var go in toRemove)
                DestroyGlow(go);
        }

        /// <summary>Есть ли зарегистрированная подсветка с данным id.</summary>
        public static bool HasGlow(string id) =>
            !string.IsNullOrEmpty(id) && _configs.ContainsKey(id);

        /// <summary>Снимки всех зарегистрированных id'ов.</summary>
        public static IReadOnlyCollection<string> ActiveGlowIds() => _configs.Keys.ToArray();

        #endregion

        #region Event handlers

        private static void OnRoundStarted()
        {
            CleanupAllGlows();
            foreach (var key in _configs.Keys.ToList())
            {
                StopCoroutine(_coroutines, key);
                _coroutines[key] = Timing.RunCoroutine(UpdateGlowCoroutine(key));
            }
        }

        private static void OnRoundEnded(RoundEndedEventArgs ev) => CleanupAllGlows();

        private static void OnChangingItem(ChangingItemEventArgs ev)
        {
            if (ev?.Player?.GameObject == null)
                return;

            var go = ev.Player.GameObject;
            var player = ev.Player;

            // Убираем подсветку с прошлого предмета в руке.
            DestroyGlow(go);

            if (ev.Item == null) return;

            Timing.CallDelayed(0.1f, () =>
            {
                var current = player?.CurrentItem;
                if (current != null)
                    CheckAndApplyHandGlow(current, go);
            });
        }

        private static void OnDroppingItem(DroppingItemEventArgs ev)
        {
            var go = ev?.Player?.GameObject;
            if (go != null)
                DestroyGlow(go);
        }

        private static void OnPickupAdded(PickupAddedEventArgs ev)
        {
            var pickup = ev?.Pickup;
            if (pickup == null) return;
            Timing.CallDelayed(0.1f, () => CheckAndApplyPickupGlow(pickup));
        }

        private static void OnPickupDestroyed(PickupDestroyedEventArgs ev)
        {
            var go = ev?.Pickup?.GameObject;
            if (go != null)
                DestroyGlow(go);
        }

        #endregion

        #region Coroutines

        private static IEnumerator<float> UpdateGlowCoroutine(string configId)
        {
            while (Round.IsStarted && _configs.TryGetValue(configId, out var config))
            {
                float interval;
                try
                {
                    foreach (var pickup in Pickup.List)
                    {
                        if (pickup?.GameObject == null) continue;
                        if (config.ItemCheck(pickup.Serial))
                            UpdatePickupGlow(pickup.GameObject, configId, config);
                    }

                    if (config.GlowInHands)
                    {
                        foreach (var player in Player.List)
                        {
                            if (player == null || !player.IsAlive) continue;
                            var item = player.CurrentItem;
                            if (item != null && config.ItemCheck(item.Serial))
                                UpdateHandGlow(player.GameObject, configId, config);
                        }
                    }

                    if (config.PulseEffect)
                    {
                        var pulse = Mathf.Sin(Time.time * config.PulseSpeed) * 0.5f + 0.5f;
                        var pulsedIntensity = config.Intensity * (0.5f + pulse * 0.5f);
                        foreach (var kvp in _active)
                        {
                            if (kvp.Value.ConfigId == configId && kvp.Value.Light != null)
                                kvp.Value.Light.Intensity = pulsedIntensity;
                        }
                    }
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"FermixGlow.UpdateGlowCoroutine: {ex}");
                }
                finally
                {
                    interval = config.UpdateInterval;
                }

                yield return Timing.WaitForSeconds(interval);
            }
        }

        private static IEnumerator<float> RainbowCoroutine(string configId)
        {
            float hue = 0f;
            while (_configs.ContainsKey(configId))
            {
                try
                {
                    hue = (hue + 0.01f) % 1f;
                    if (_configs.TryGetValue(configId, out var cfg))
                        cfg.Color = Color.HSVToRGB(hue, 1f, 1f);
                }
                catch (Exception ex)
                {
                    FermixLog.Error($"FermixGlow.RainbowCoroutine: {ex}");
                }

                yield return Timing.WaitForSeconds(0.05f);
            }
        }

        #endregion

        #region Light handling

        private static void CheckAndApplyPickupGlow(Pickup pickup)
        {
            if (pickup?.GameObject == null) return;

            foreach (var pair in _configs)
            {
                if (!pair.Value.ItemCheck(pickup.Serial)) continue;
                UpdatePickupGlow(pickup.GameObject, pair.Key, pair.Value);
                break;
            }
        }

        private static void CheckAndApplyHandGlow(Item item, GameObject target)
        {
            if (item == null || target == null) return;

            foreach (var pair in _configs)
            {
                if (!pair.Value.ItemCheck(item.Serial)) continue;
                if (!pair.Value.GlowInHands) break;
                UpdateHandGlow(target, pair.Key, pair.Value);
                break;
            }
        }

        private static void UpdatePickupGlow(GameObject target, string configId, GlowConfig config)
        {
            if (target == null) return;
            var tag = GetOrCreateGlow(target, configId);
            if (tag?.Light == null) return;

            tag.Light.Color = config.Color;
            tag.Light.Intensity = config.Intensity;
            tag.Light.Range = config.Range;
            tag.Light.Position = target.transform.position + Vector3.up * 0.1f;
        }

        private static void UpdateHandGlow(GameObject target, string configId, GlowConfig config)
        {
            if (target == null) return;
            var tag = GetOrCreateGlow(target, configId);
            if (tag?.Light == null) return;

            tag.Light.Color = config.Color;
            tag.Light.Intensity = config.Intensity * 0.7f;
            tag.Light.Range = config.Range * 0.8f;
            tag.Light.Position = target.transform.position + target.transform.forward * 0.5f + Vector3.up * 0.8f;
        }

        private static GlowTag GetOrCreateGlow(GameObject target, string configId)
        {
            if (target == null) return null;

            if (_active.TryGetValue(target, out var existing))
            {
                if (existing.ConfigId != configId)
                {
                    DestroyGlow(target);
                }
                else
                {
                    return existing;
                }
            }

            Light light;
            try
            {
                light = Light.Create(target.transform.position, Vector3.zero, Vector3.one, true);
            }
            catch (Exception ex)
            {
                FermixLog.Error($"FermixGlow: не удалось создать LightSourceToy: {ex.Message}");
                return null;
            }

            // EXILED 9.13.3 не позволяет напрямую парентить LightSourceToy через wrapper —
            // делаем это вручную через GameObject, чтобы свет двигался вместе с целью.
            if (light?.AdminToyBase != null)
                light.AdminToyBase.transform.SetParent(target.transform, worldPositionStays: true);

            var tag = new GlowTag
            {
                Light = light,
                ConfigId = configId,
            };
            _active[target] = tag;
            return tag;
        }

        private static void DestroyGlow(GameObject target)
        {
            if (target == null) return;
            if (!_active.TryGetValue(target, out var tag)) return;

            try { tag.Light?.Destroy(); } catch { /* ignore */ }
            _active.Remove(target);
        }

        private static void CleanupAllGlows()
        {
            foreach (var handle in _coroutines.Values.ToList())
            {
                if (handle.IsRunning) Timing.KillCoroutines(handle);
            }
            _coroutines.Clear();

            foreach (var handle in _rainbowCoroutines.Values.ToList())
            {
                if (handle.IsRunning) Timing.KillCoroutines(handle);
            }
            _rainbowCoroutines.Clear();

            foreach (var go in _active.Keys.ToList())
                DestroyGlow(go);
        }

        private static void StopCoroutine(Dictionary<string, CoroutineHandle> dict, string id)
        {
            if (!dict.TryGetValue(id, out var handle)) return;
            try { if (handle.IsRunning) Timing.KillCoroutines(handle); } catch { /* ignore */ }
            dict.Remove(id);
        }

        #endregion
    }
}
