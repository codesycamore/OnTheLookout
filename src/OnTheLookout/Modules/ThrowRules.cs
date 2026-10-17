using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.Modules;

/// <summary>
/// Thrown snowballs and stones during a round:
/// - a runner's snowball or stone goes straight through every other runner (no push, no hit), so runners can't
///   affect each other with them;
/// - a runner's stone that hits a chaser does StoneChaserDamage injury to them (0.1 = 10% of the bar, exact: the
///   chasers' half-damage toughness doesn't apply to it).
/// </summary>
internal static class ThrowRules
{
    /// <summary>How long after a throw the item ignores other runners and can still hurt a chaser (s).</summary>
    private const float ThrowWindowSeconds = 4f;

    /// <summary>A stone has to hit at least this fast (m/s) to hurt; a stone just rolling against a chaser doesn't.</summary>
    private const float StoneMinSpeed = 3f;

    public static bool Install(Harmony harmony)
    {
        const string m = "ThrowRules";
        // Item.RPC_SetThrownData(int characterID, float thrownAmount) (postfix). Why: runs on every client when an
        // item is thrown, after lastThrownCharacter is set (it raises GlobalEvents.OnItemThrown).
        return SafePatch.Postfix(harmony, typeof(Item), nameof(Item.RPC_SetThrownData), typeof(ThrowRules), nameof(ThrownPostfix), m);
    }

    public static bool IsStone(Item item) => item is Stone || ItemCatalog.Effective(item) is Stone;

    public static bool IsSnowball(Item item) => item.GetComponent<Snowball>() != null;

    public static void ThrownPostfix(Item __instance)
    {
        if (!RoundManager.IsActive || __instance == null) return;
        Character thrower = __instance.lastThrownCharacter;
        if (thrower == null || !RoleManager.IsRunner(thrower)) return;
        bool stone = IsStone(__instance);
        if (!stone && !IsSnowball(__instance)) return;

        ModNetwork.Instance?.StartCoroutine(PassThroughRunners(__instance, thrower));
        if (stone)
        {
            StoneHit hit = __instance.GetComponent<StoneHit>() ?? __instance.gameObject.AddComponent<StoneHit>();
            hit.Arm(thrower);
        }
    }

    /// <summary>Every client: the thrown item's colliders ignore every other runner's body for ThrowWindowSeconds.</summary>
    private static IEnumerator PassThroughRunners(Item item, Character thrower)
    {
        var ignored = new List<(Collider, Collider)>();
        Collider[] own = item.colliders ?? item.GetComponentsInChildren<Collider>();
        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c == thrower || c.isBot || !RoleManager.IsRunner(c) || c.refs?.ragdoll == null) continue;
            foreach (Collider body in c.refs.ragdoll.colliderList)
            {
                foreach (Collider part in own)
                {
                    if (body == null || part == null) continue;
                    Physics.IgnoreCollision(part, body, true);
                    ignored.Add((part, body));
                }
            }
        }

        yield return new WaitForSeconds(ThrowWindowSeconds);
        foreach ((Collider part, Collider body) in ignored)
        {
            if (part != null && body != null) Physics.IgnoreCollision(part, body, false);
        }
    }

    /// <summary>The chaser's own client: the stone's injury, exactly StoneChaserDamage.</summary>
    public static void ApplyStoneHit(int throwerActor)
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || !RoundManager.IsActive || !RoleManager.IsChaser(local)) return;
        float damage = Mathf.Clamp01(Plugin.ModConfig.StoneChaserDamage.Synced());
        if (damage <= 0f) return;
        ChaserResilience.AddExact(local, CharacterAfflictions.STATUSTYPE.Injury, damage);
        Plugin.Log.LogInfo($"[OTL][Throw] hit by {Net.NameOf(throwerActor)}'s stone: +{damage:0.##} injury.");
    }

    /// <summary>On a stone a runner threw: the first fast hit on a living chaser (by the stone's owner) hurts them.</summary>
    internal sealed class StoneHit : MonoBehaviour
    {
        private Item? _item;
        private Character? _thrower;
        private float _until;

        public void Arm(Character thrower)
        {
            _item ??= GetComponent<Item>();
            _thrower = thrower;
            _until = Time.time + ThrowWindowSeconds;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (_thrower == null || _item == null || Time.time > _until || other?.collider == null) return;
            if (_item.view == null || !_item.view.IsMine || !RoundManager.IsActive) return; // the stone's owner decides, once
            if (other.relativeVelocity.magnitude < StoneMinSpeed) return;
            if (!CharacterRagdoll.TryGetCharacterFromCollider(other.collider, out Character hit)) hit = other.collider.GetComponentInParent<Character>();
            if (hit == null || hit == _thrower || hit.data.dead || hit.isBot || !RoleManager.IsChaser(hit)) return;

            _until = 0f; // one hit per throw
            int thrower = Net.Actor(_thrower);
            Plugin.Log.LogInfo($"[OTL][Throw] stone thrown by {_thrower.characterName} hit chaser {hit.characterName} at {other.relativeVelocity.magnitude:0.#} m/s.");
            if (hit.IsLocal) ApplyStoneHit(thrower);
            else Net.SendToActor(Net.Actor(hit), Msg.StoneHit, thrower);
        }
    }
}
