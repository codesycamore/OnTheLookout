using System;
using System.Reflection;
using HarmonyLib;

namespace OnTheLookout.Core;

/// <summary>
/// Applies Harmony patches one target at a time, logging and returning false instead of throwing
/// when a target can't be found or patched (e.g. after a game update), so only the owning module is disabled.
/// </summary>
internal static class SafePatch
{
    public static bool Postfix(Harmony harmony, Type type, string method, Type patchClass, string patchMethod, string module, Type[]? args = null) =>
        Apply(harmony, type, method, args, module, postfix: Hook(patchClass, patchMethod));

    public static bool Prefix(Harmony harmony, Type type, string method, Type patchClass, string patchMethod, string module, Type[]? args = null) =>
        Apply(harmony, type, method, args, module, prefix: Hook(patchClass, patchMethod));

    public static bool Transpiler(Harmony harmony, Type type, string method, Type patchClass, string patchMethod, string module, Type[]? args = null) =>
        Apply(harmony, type, method, args, module, transpiler: Hook(patchClass, patchMethod));

    private static HarmonyMethod Hook(Type patchClass, string patchMethod) =>
        new(AccessTools.Method(patchClass, patchMethod)
            ?? throw new MissingMethodException(patchClass.Name, patchMethod));

    private static bool Apply(Harmony harmony, Type type, string method, Type[]? args, string module,
        HarmonyMethod? prefix = null, HarmonyMethod? postfix = null, HarmonyMethod? transpiler = null)
    {
        string label = $"{type.Name}.{method}";
        try
        {
            MethodInfo? target = AccessTools.Method(type, method, args);
            if (target is null)
            {
                Plugin.Log.LogError($"[OTL][{module}] hook {label} not found - module disabled.");
                return false;
            }

            harmony.Patch(target, prefix, postfix, transpiler);
            string kind = prefix is not null ? "prefix" : postfix is not null ? "postfix" : "transpiler";
            Plugin.Log.LogInfo($"[OTL][{module}] patched {label} ({kind}).");
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[OTL][{module}] failed to patch {label} - module disabled.\n{e}");
            return false;
        }
    }
}
