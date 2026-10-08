using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// The chasers' blowgun: unlimited uses, a cooldown between shots, and instead of putting a runner
/// to sleep a dart marks them with flare smoke that follows them for a few seconds.
/// </summary>
internal static class BlowgunSystem
{
    private static float s_CooldownUntil;

    public static bool OnCooldown => Time.time < s_CooldownUntil;

    public static float CooldownSecondsLeft => Mathf.Max(0f, s_CooldownUntil - Time.time);

    public static bool Install(Harmony harmony)
    {
        const string m = "Blowgun";
        bool ok = true;

        // Action_RaycastDart.RPC_DartImpact(int characterID, ...) (prefix, [PunRPC] on all clients).
        // Why: on hit, the target's own client applies the sleep afflictions here. We mark the target
        // with smoke instead and pass characterID = -1 so the dart's visuals still play but no sleep.
        ok &= SafePatch.Prefix(harmony, typeof(Action_RaycastDart), "RPC_DartImpact", typeof(BlowgunSystem), nameof(DartImpactPrefix), m);

        // Action_RaycastDart.FireDart() (postfix, shooter only). Why: starts the local cooldown.
        ok &= SafePatch.Postfix(harmony, typeof(Action_RaycastDart), "FireDart", typeof(BlowgunSystem), nameof(FireDartPostfix), m);

        // Action_ReduceUses.RunAction() (prefix). Why: this is what spends a use; chasers' blowguns never run out.
        ok &= SafePatch.Prefix(harmony, typeof(Action_ReduceUses), nameof(Action_ReduceUses.RunAction), typeof(BlowgunSystem), nameof(ReduceUsesPrefix), m);
        return ok;
    }

    private static bool ChasersBlowgun(Item item) =>
        RoundManager.IsActive && Plugin.ModConfig.ChaserBlowgun.Synced() && RoleManager.IsChaser(item.holderCharacter);

    public static void DartImpactPrefix(Action_RaycastDart __instance, ref int characterID)
    {
        if (characterID == -1 || !ChasersBlowgun(__instance.item)) return;

        if (PhotonNetwork.GetPhotonView(characterID) is { } view && view.GetComponent<Character>() is { } target && RoleManager.IsRunner(target))
        {
            TrackingSmoke.Attach(target, Plugin.ModConfig.TrackingSmokeSeconds.Synced());

            // A small dose of sleep instead of the vanilla knock-out; statuses belong to the runner's own client.
            float drowsy = Plugin.ModConfig.BlowdartDrowsy.Synced();
            if (target.IsLocal && drowsy > 0f) target.refs.afflictions.AddStatus(CharacterAfflictions.STATUSTYPE.Drowsy, drowsy);
        }

        characterID = -1;
    }

    public static void FireDartPostfix(Action_RaycastDart __instance)
    {
        if (ChasersBlowgun(__instance.item))
        {
            s_CooldownUntil = Time.time + Plugin.ModConfig.BlowgunCooldownSeconds.Synced();
        }
    }

    public static bool ReduceUsesPrefix(Action_ReduceUses __instance) =>
        !(ItemCatalog.IsBlowgun(__instance.item) && ChasersBlowgun(__instance.item));
}

/// <summary>
/// Flare smoke that follows a character. Uses the flare item's own smoke particles
/// (Flare.flareVFXPrefab) spawned locally on every client, with its network tracker removed.
/// </summary>
internal sealed class TrackingSmoke : MonoBehaviour
{
    private static readonly Color FallbackColor = new(1f, 0.3f, 0.2f);
    private static readonly Dictionary<Character, TrackingSmoke> s_Active = new();

    private Character _target = null!;
    private float _until;
    private ParticleSystem[] _systems = System.Array.Empty<ParticleSystem>();

    public static void Attach(Character target, float seconds)
    {
        if (s_Active.TryGetValue(target, out TrackingSmoke existing) && existing != null)
        {
            existing._until = Time.time + seconds;
            return;
        }

        Flare? flare = ItemCatalog.FlarePrefab;
        if (flare == null || flare.flareVFXPrefab == null)
        {
            Plugin.Log.LogWarning("[OTL][Blowgun] flare smoke prefab not found; no tracking smoke.");
            return;
        }

        GameObject go = Instantiate(flare.flareVFXPrefab.gameObject, target.Center, Quaternion.identity);
        go.name = "OTL_TrackingSmoke";
        if (go.GetComponent<TrackNetworkedObject>() is { } tracker) DestroyImmediate(tracker); // we follow the runner ourselves

        var smoke = go.AddComponent<TrackingSmoke>();
        smoke._target = target;
        smoke._until = Time.time + seconds;
        smoke._systems = go.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in smoke._systems)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = SmokeColorFor(target);
            ps.Play(true);
        }

        s_Active[target] = smoke;
        Plugin.Log.LogInfo($"[OTL][Blowgun] {target.characterName} is marked for {seconds:0.#}s.");
    }

    /// <summary>The runner's skin colour (CharacterCustomization.PlayerColor, the same colour PEAK uses for their warp poof).</summary>
    private static Color SmokeColorFor(Character target)
    {
        if (target.refs.customization == null) return FallbackColor;
        Color c = target.refs.customization.PlayerColor;
        return new Color(c.r, c.g, c.b, 1f);
    }

    private void LateUpdate()
    {
        if (_target == null || Time.time >= _until)
        {
            // Stop emitting and let the existing smoke drift away before cleaning up.
            foreach (ParticleSystem ps in _systems)
            {
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (_target != null) s_Active.Remove(_target);
            Destroy(gameObject, 8f);
            enabled = false;
            return;
        }

        transform.position = _target.Center;
    }
}
