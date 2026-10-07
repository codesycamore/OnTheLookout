using System;
using System.Reflection;
using HarmonyLib;

namespace OnTheLookout.Core;

/// <summary>
/// Applies a single Harmony patch, logging and returning false instead of throwing
/// when the target can't be found (e.g. after a game update), so only the owning module is disabled.
/// </summary>
internal static class SafePatch
{
    public static bool Postfix(Harmony harmony, Type type, string method, Type patchClass, string patchMethod, string module) =>
        Apply(harmony, type, method, patchClass, patchMethod, module, prefix: false);

    public static bool Prefix(Harmony harmony, Type type, string method, Type patchClass, string patchMethod, string module) =>
        Apply(harmony, type, method, patchClass, patchMethod, module, prefix: true);

    private static bool Apply(Harmony harmony, Type type, string method, Type patchClass, string patchMethod, string module, bool prefix)
    {
        MethodInfo? target = AccessTools.Method(type, method);
        if (target is null)
        {
            Plugin.Log.LogError($"[OTL][{module}] hook {type.Name}.{method} not found - module disabled.");
            return false;
        }

        try
        {
            var patch = new HarmonyMethod(AccessTools.Method(patchClass, patchMethod));
            if (prefix)
            {
                harmony.Patch(target, prefix: patch);
            }
            else
            {
                harmony.Patch(target, postfix: patch);
            }

            Plugin.Log.LogInfo($"[OTL][{module}] patched {type.Name}.{method} ({(prefix ? "prefix" : "postfix")}).");
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OTL][{module}] failed to patch {type.Name}.{method} - module disabled.\n{e}");
            return false;
        }
    }
}
