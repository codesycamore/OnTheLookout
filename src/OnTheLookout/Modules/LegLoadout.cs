using System.Collections;
using System.Collections.Generic;
using System.Linq;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Host: hands out items at the start of every leg (round start and each lit campfire):
/// chasers get their blowgun if they don't have one, and every living runner gets one random
/// item from <c>RunnerLegItems</c> (snowball, banana or fortified milk by default).
/// New chasers from a statue conversion get their blowgun right away.
/// Before a chaser gets an item their hands are cleared: whatever they hold is dropped in front of
/// them (by their own client, the vanilla way), then the item is given.
/// </summary>
internal static class LegLoadout
{
    private const float ClearHandsDelay = 0.75f;

    public static void Install()
    {
        RoundManager.LegStarted += newRound =>
        {
            Schedule(GiveLegItems());
            if (newRound) Schedule(GiveRunnerBackpacks());
        };
        RoundManager.LegCompleted += () => Schedule(StockCampfireFood());
    }

    /// <summary>Host, after roles are assigned at round start: every runner without a backpack gets one.</summary>
    private static IEnumerator GiveRunnerBackpacks()
    {
        yield return new WaitForSeconds(1.5f);
        if (!Net.IsHost || !RoundManager.IsActive || !Plugin.ModConfig.RunnerBackpacks.Synced()) yield break;

        Backpack? backpack = ItemCatalog.PlainBackpack;
        if (backpack == null)
        {
            Plugin.Log.LogWarning("[OTL][Loadout] no backpack item found.");
            yield break;
        }

        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (!RoleManager.IsRunner(c) || c.data.dead || c.player == null || !c.player.backpackSlot.IsEmpty()) continue;
            Give(c, backpack);
        }
    }

    /// <summary>
    /// Host, when the chase of a leg ends (every living runner at the campfire, chasers brought there):
    /// make sure there is one campfire food item (random pick from CampfireFoodItems, e.g. marshmallow or
    /// hot dog) per living player lying near the fire, spawning only the shortfall.
    /// </summary>
    private static IEnumerator StockCampfireFood()
    {
        yield return new WaitForSeconds(2f); // after the chasers have been brought over
        if (!Net.IsHost || !RoundManager.IsActive) yield break;

        List<Item> foods = ItemCatalog.FindByNames(Plugin.ModConfig.CampfireFoodItems.Synced());
        if (foods.Count == 0) yield break;

        Campfire? fire = SafeZoneSystem.Campfires
            .Where(f => f.isActiveAndEnabled && f.state == Campfire.FireState.Off)
            .OrderBy(f => Character.AllCharacters.Where(c => RoleManager.IsRunner(c) && !c.data.dead)
                .Select(c => Vector3.Distance(c.Center, f.transform.position)).DefaultIfEmpty(float.MaxValue).Max())
            .FirstOrDefault();
        if (fire == null) yield break;

        Vector3 center = fire.transform.position;
        var foodIds = new HashSet<ushort>(foods.Select(f => f.itemID));
        int players = Character.AllCharacters.Count(c => c != null && !c.isBot && !c.data.dead);
        int present = Object.FindObjectsByType<Item>(FindObjectsSortMode.None)
            .Count(i => i != null && i.itemState == ItemState.Ground && foodIds.Contains(i.itemID)
                && Vector3.Distance(i.transform.position, center) <= 15f);
        int missing = players - present;

        for (int i = 0; i < missing; i++)
        {
            Item food = foods[Random.Range(0, foods.Count)];
            float angle = (i * 360f / Mathf.Max(1, missing) + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            Vector3 spot = center + new Vector3(Mathf.Cos(angle) * 2.5f, 1f, Mathf.Sin(angle) * 2.5f);
            PhotonNetwork.Instantiate("0_Items/" + food.gameObject.name, spot, Quaternion.identity, 0);
        }

        Plugin.Log.LogInfo($"[OTL][Loadout] HOST campfire food: {players} player(s), {present} already there, spawned {Mathf.Max(0, missing)}.");
    }

    private static void Schedule(IEnumerator routine)
    {
        if (Net.IsHost) ModNetwork.Instance?.StartCoroutine(routine);
    }

    public static void GiveBlowgunLater(int actor) => Schedule(GiveBlowgunRoutine(actor, 1.5f)); // after the revive has landed

    private static IEnumerator GiveLegItems()
    {
        yield return new WaitForSeconds(1f);
        if (!Net.IsHost || !RoundManager.IsActive) yield break;

        foreach (int actor in RoleManager.Chasers.ToArray()) Schedule(GiveBlowgunRoutine(actor, 0f));

        List<Item> pool = ItemCatalog.FindByNames(Plugin.ModConfig.RunnerLegItems.Synced());
        if (pool.Count == 0) yield break;
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (!RoleManager.IsRunner(c) || c.data.dead) continue;
            Give(c, pool[Random.Range(0, pool.Count)]);
        }
    }

    private static IEnumerator GiveBlowgunRoutine(int actor, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (!Net.IsHost || !Plugin.ModConfig.ChaserBlowgun.Synced() || !RoleManager.IsChaser(actor)) yield break;
        Character? c = Net.CharacterOf(actor);
        Item? blowgun = ItemCatalog.Blowgun;
        if (c == null || c.data.dead || blowgun == null || c.player == null || c.player.HasInAnySlot(blowgun.itemID)) yield break;

        // Empty hands first, then give.
        if (c.IsLocal) ClearLocalHands();
        else Net.SendToActor(actor, Msg.ClearHands);
        yield return new WaitForSeconds(ClearHandsDelay);

        if (c != null && !c.data.dead && !c.player.HasInAnySlot(blowgun.itemID)) Give(c, blowgun);
    }

    /// <summary>
    /// The local player's client: drop the held item just in front of them (as if they pressed drop).
    /// If nothing is held but every slot is full, drop the first slot's item to make room.
    /// </summary>
    public static void ClearLocalHands()
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null) return;
        CharacterItems items = local.refs.items;
        Vector3 inFront = local.Center + local.data.lookDirection_Flat * 1.2f + Vector3.up * 0.3f;

        if (local.data.currentItem != null && items.currentSelectedSlot.IsSome)
        {
            byte slot = items.currentSelectedSlot.Value;
            ItemSlot itemSlot = local.player.GetItemSlot(slot);
            if (itemSlot != null && !itemSlot.IsEmpty())
            {
                items.photonView.RPC("DropItemRpc", RpcTarget.All, 0f, slot, inFront, Vector3.zero,
                    local.data.currentItem.transform.rotation, itemSlot.data, false);
                items.EquipSlot(Optionable<byte>.None);
                Plugin.Log.LogInfo($"[OTL][Loadout] dropped held {ItemCatalog.NameOf(itemSlot.prefab)} to make room.");
                return;
            }
        }

        if (local.player.itemSlots.All(s => s != null && !s.IsEmpty()))
        {
            items.photonView.RPC("DropItemFromSlotRPC", RpcTarget.All, (byte)0, inFront);
            Plugin.Log.LogInfo("[OTL][Loadout] inventory full; dropped slot 1 to make room.");
        }
    }

    /// <summary>Into the inventory if there is room, otherwise dropped at their feet.</summary>
    public static void Give(Character c, Item prefab)
    {
        if (c.player != null && c.player.AddItem(prefab.itemID, null, out _))
        {
            Plugin.Log.LogInfo($"[OTL][Loadout] HOST gave {c.characterName} a {ItemCatalog.NameOf(prefab)}.");
            return;
        }

        PhotonNetwork.Instantiate("0_Items/" + prefab.gameObject.name, c.Center + Vector3.up, Quaternion.identity, 0);
        Plugin.Log.LogInfo($"[OTL][Loadout] HOST dropped a {ItemCatalog.NameOf(prefab)} at {c.characterName}'s feet (inventory full).");
    }
}
