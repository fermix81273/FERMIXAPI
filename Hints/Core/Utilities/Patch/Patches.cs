namespace FermixAPI.Hints.Core.Utilities.Patch
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using global::Hints;
    using FermixAPI.Hints.Core.Extension;
    using FermixAPI.Hints.Plugin;
    using LabApi.Features.Wrappers;
    using Logger = FermixAPI.Hints.Core.Utilities.Tools.Logger;

    internal static class Patches
    {
        private static readonly Func<TextHint, string> TextGetter = (Func<TextHint, string>)GetTextGetter();

#pragma warning disable SA1313
        public static bool HintDisplayPatch(ref Hint hint, ref HintDisplay __instance)
        {
            try
            {
                if (!Plugin.Instance.Config.UseHintCompatibilityAdapter)
                    return false;

                if (hint is not TextHint textHint)
                    return false;

                // На раннем этапе подключения у HintDisplay может ещё не быть
                // привязанного NetworkConnection (или identity на нём), а
                // ReferenceHub может ещё не быть зарегистрирован в HubsByNetID.
                // В таком случае молча роняем хинт, но НЕ роняем подключение.
                var conn = __instance != null ? __instance.connectionToClient : null;
                var identity = conn != null ? conn.identity : null;
                if (identity == null)
                    return false;

                if (!ReferenceHub.TryGetHubNetID(identity.netId, out ReferenceHub referenceHub) || referenceHub == null)
                    return false;

                string assemblyName = Assembly.GetCallingAssembly().FullName;
                string content = TextGetter(textHint);
                float duration = textHint.DurationScalar;
                PlayerDisplay.Get(referenceHub).ShowCompatibilityHint(assemblyName, content, duration);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }

            return false;
        }

        public static bool SendHintPatch1(ref string text, ref float duration, ref Player __instance)
        {
            try
            {
                if (!Plugin.Instance.Config.UseHintCompatibilityAdapter)
                    return false;

                if (__instance == null || __instance.ReferenceHub == null)
                    return false;

                string assemblyName = Assembly.GetCallingAssembly().FullName;
                __instance.GetPlayerDisplay().ShowCompatibilityHint(assemblyName, text, duration);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }

            return false;
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public static bool SendHintPatch2(ref string text, ref HintEffect[] effects, ref float duration, ref Player __instance)
        {
            try
            {
                if (!Plugin.Instance.Config.UseHintCompatibilityAdapter)
                    return false;

                if (__instance == null || __instance.ReferenceHub == null)
                    return false;

                string assemblyName = Assembly.GetCallingAssembly().FullName;
                __instance.GetPlayerDisplay().ShowCompatibilityHint(assemblyName, text, duration);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }

            return false;
        }
#pragma warning restore IDE0060 // Remove unused parameter

#if EXILED
        public static bool ExiledHintPatch1(ref string message, ref float duration, ref Exiled.API.Features.Player __instance)
        {
            try
            {
                if (!Plugin.Instance.Config.UseHintCompatibilityAdapter)
                    return false;

                if (__instance == null || __instance.ReferenceHub == null)
                    return false;

                string assemblyName = Assembly.GetCallingAssembly().FullName;
                __instance.GetPlayerDisplay().ShowCompatibilityHint(assemblyName, message, duration);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }

            return false;
        }

        public static bool ExiledHintPatch2(ref Exiled.API.Features.Hint hint, ref Exiled.API.Features.Player __instance)
#pragma warning restore SA1313
        {
            try
            {
                if (!Plugin.Instance.Config.UseHintCompatibilityAdapter)
                    return false;

                if (hint == null || !hint.Show)
                    return false;

                if (__instance == null || __instance.ReferenceHub == null)
                    return false;

                string assemblyName = Assembly.GetCallingAssembly().FullName;
                __instance.GetPlayerDisplay().ShowCompatibilityHint(assemblyName, hint.Content, hint.Duration);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(ex);
            }

            return false;
        }
#endif

        private static Delegate GetTextGetter()
        {
            var prop = typeof(TextHint).GetProperty(
                        "Text",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (prop == null)
                throw new MissingMemberException(typeof(TextHint).FullName, "Text");

            var getMethod = prop.GetGetMethod(nonPublic: true);
            if (getMethod == null)
                throw new InvalidOperationException($"Property 'Text' has no getter.");

            var objParam = Expression.Parameter(typeof(TextHint), "obj");
            var call = Expression.Call(objParam, getMethod);
            var body = Expression.Convert(call, typeof(string));

            return Expression.Lambda<Func<TextHint, string>>(body, objParam).Compile();
        }
    }
}
