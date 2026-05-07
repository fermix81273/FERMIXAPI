namespace FermixAPI.Hints.Core.Utilities.Patch
{
    using System;
    using System.Reflection;
    using HarmonyLib;
    using Logger = FermixAPI.Hints.Core.Utilities.Tools.Logger;

    /// <summary>
    /// Provides methods to apply and remove Harmony patches used by FermixAPI.Hints.
    /// </summary>
    public static class Patcher
    {
        /// <summary>
        /// Gets the active <see cref="HarmonyLib.Harmony"/> instance used to manage patches, or <see langword="null"/> if patching has not been applied.
        /// </summary>
        public static Harmony? Harmony { get; private set; }

        /// <summary>
        /// Уникальный ID для нашего Harmony-инстанса. Сохраняем его явно,
        /// чтобы Unpatch() снимал ТОЛЬКО наши патчи (через
        /// <see cref="HarmonyLib.Harmony.UnpatchAll(string)"/>), а не все патчи
        /// всех плагинов сервера, как делает безпараметрический UnpatchAll().
        /// </summary>
        private static string? _harmonyId;

        /// <summary>
        /// Applies all Harmony patches required by FermixAPI.Hints, including patches for hint display and hint sending methods.
        /// </summary>
        /// <remarks>
        /// Каждый патч применяется независимо: если конкретный целевой метод
        /// исчез (например, после обновления SCP:SL / LabAPI / EXILED), мы
        /// логируем потерю и продолжаем без него, вместо того чтобы валить
        /// всю инициализацию hint-движка и ронять подключение игроков.
        /// </remarks>
        public static void Patch()
        {
            _harmonyId = "FermixAPI.HintsHarmony." + Guid.NewGuid();
            Harmony = new Harmony(_harmonyId);

            Type patchType = typeof(Patches);

            ApplyPatch(
                "HintDisplay.Show",
                ResolveMethod(typeof(global::Hints.HintDisplay), nameof(global::Hints.HintDisplay.Show)),
                patchType.GetMethod(nameof(Patches.HintDisplayPatch)));

            ApplyPatch(
                "LabApi.Player.SendHint(string, float)",
                ResolveMethod(typeof(LabApi.Features.Wrappers.Player), nameof(LabApi.Features.Wrappers.Player.SendHint), typeof(string), typeof(float)),
                patchType.GetMethod(nameof(Patches.SendHintPatch1)));

            ApplyPatch(
                "LabApi.Player.SendHint(string, HintEffect[], float)",
                ResolveMethod(typeof(LabApi.Features.Wrappers.Player), nameof(LabApi.Features.Wrappers.Player.SendHint), typeof(string), typeof(global::Hints.HintEffect[]), typeof(float)),
                patchType.GetMethod(nameof(Patches.SendHintPatch2)));

#if EXILED
            ApplyPatch(
                "Exiled.Player.ShowHint(string, float)",
                ResolveMethod(typeof(Exiled.API.Features.Player), nameof(Exiled.API.Features.Player.ShowHint), typeof(string), typeof(float)),
                patchType.GetMethod(nameof(Patches.ExiledHintPatch1)));

            ApplyPatch(
                "Exiled.Player.ShowHint(Hint)",
                ResolveMethod(typeof(Exiled.API.Features.Player), nameof(Exiled.API.Features.Player.ShowHint), typeof(Exiled.API.Features.Hint)),
                patchType.GetMethod(nameof(Patches.ExiledHintPatch2)));
#endif
        }

        /// <summary>
        /// Removes only Harmony patches applied by this patcher.
        /// Use the harmonyID overload — иначе UnpatchAll() безпараметрически
        /// сносит ВСЕ патчи (включая EXILED, LabAPI, прочих плагинов), что
        /// было бы катастрофой при reload'е плагина.
        /// </summary>
        public static void Unpatch()
        {
            try
            {
                if (Harmony != null && !string.IsNullOrEmpty(_harmonyId))
                {
                    Harmony.UnpatchAll(_harmonyId);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"FermixAPI.Hints: failed to unpatch Harmony id '{_harmonyId}': {ex.Message}");
            }
            finally
            {
                Harmony = null;
                _harmonyId = null;
            }
        }

        private static MethodInfo? ResolveMethod(Type owner, string name, params Type[] signature)
        {
            try
            {
                return signature.Length == 0
                    ? owner.GetMethod(name)
                    : owner.GetMethod(name, signature);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"FermixAPI.Hints: failed to resolve {owner.FullName}.{name}: {ex.Message}");
                return null;
            }
        }

        private static void ApplyPatch(string label, MethodInfo? target, MethodInfo? prefix)
        {
            if (Harmony == null) return;

            if (target == null)
            {
                Logger.Instance.Error($"FermixAPI.Hints: target method '{label}' not found, skipping patch.");
                return;
            }

            if (prefix == null)
            {
                Logger.Instance.Error($"FermixAPI.Hints: prefix for '{label}' not found, skipping patch.");
                return;
            }

            try
            {
                Harmony.Unpatch(target, HarmonyPatchType.All);
                Harmony.Patch(target, new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"FermixAPI.Hints: failed to (un)patch '{label}': {ex.Message}");
            }
        }
    }
}
