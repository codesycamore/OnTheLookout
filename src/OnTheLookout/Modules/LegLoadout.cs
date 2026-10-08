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
        RoundManager.LegStarted += _ => Schedule(GiveLegItems());
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
