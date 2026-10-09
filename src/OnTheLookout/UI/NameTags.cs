using HarmonyLib;
using OnTheLookout.Core;

namespace OnTheLookout.UI;

/// <summary>
/// Chasers don't see the name tags above runners (ChasersSeeRunnerNames = false), so they have to find
/// runners by sight. Runners still see everyone's name, and chasers still see each other's.
/// </summary>
internal static class NameTags
{
    public static bool Install(Harmony harmony)
    {
        // UIPlayerNames.UpdateName(int index, Vector3 position, bool visible, int speakingAmplitude) (prefix).
        // Why: called every frame per player name tag with whether it should show; forcing visible = false
        // lets the tag fade out and hide the vanilla way.
        return SafePatch.Prefix(harmony, typeof(UIPlayerNames), nameof(UIPlayerNames.UpdateName), typeof(NameTags), nameof(UpdateNamePrefix), "NameTags");
    }

    public static void UpdateNamePrefix(UIPlayerNames __instance, int index, ref bool visible)
    {
        if (!visible || Plugin.ModConfig.ChasersSeeRunnerNames.Synced() || !RoundManager.IsActive) return;
        Character local = Character.localCharacter;
        if (local == null || !RoleManager.IsChaser(local) || __instance.playerNameText == null || index < 0 || index >= __instance.playerNameText.Length) return;
        Character? other = __instance.playerNameText[index]?.characterInteractable?.character;
        if (other != null && RoleManager.IsRunner(other)) visible = false;
    }
}
