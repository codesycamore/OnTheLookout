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
        Item? biomeItem = BiomeItemForLeg();
        foreach (Character c in Character.AllCharacters.ToArray())
        {
            if (!RoleManager.IsRunner(c) || c.data.dead) continue;
            if (pool.Count > 0) Give(c, pool[Random.Range(0, pool.Count)]);
            if (biomeItem != null) Give(c, biomeItem);
        }
    }

    /// <summary>
    /// The biome this leg is played in: the segment the campfire just lit leads into (for the first leg,
    /// the current segment). Uses MapHandler.MapSegment.biome, which accounts for biome variants.
    /// </summary>
    private static Biome.BiomeType? LegBiome()
    {
        MapHandler map = Zorro.Core.Singleton<MapHandler>.Instance;
        if (map == null || map.segments == null) return null;
        int segment = SafeZoneSystem.LastLitSegment >= 0 ? SafeZoneSystem.LastLitSegment : (int)MapHandler.CurrentSegmentNumber;
        return segment >= 0 && segment < map.segments.Length ? map.segments[segment].biome : null;
    }

    /// <summary>"Biome:Item" pairs from RunnerBiomeItems, as (biome, item name).</summary>
    public static IEnumerable<(string Biome, string Item)> BiomeItemPairs() =>
        Plugin.ModConfig.RunnerBiomeItems.Synced().Split(',')
            .Select(p => p.Split(':'))
            .Where(p => p.Length == 2 && p[0].Trim().Length > 0 && p[1].Trim().Length > 0)
            .Select(p => (p[0].Trim(), p[1].Trim()));

    /// <summary>The extra item every runner gets for this leg's biome (RunnerBiomeItems), if any.</summary>
    private static Item? BiomeItemForLeg()
    {
        Biome.BiomeType? biome = LegBiome();
        if (biome == null) return null;
        foreach ((string key, string itemName) in BiomeItemPairs())
        {
            if (!System.Enum.TryParse(key, true, out Biome.BiomeType wanted) || wanted != biome.Value) continue;
            Item? item = ItemCatalog.FindByNames(itemName).FirstOrDefault();
            Plugin.Log.LogInfo($"[OTL][Loadout] leg biome {biome.Value}: runners get {(item != null ? ItemCatalog.NameOf(item) : $"nothing ('{itemName}' not found)")}.");
            return item;
        }

        Plugin.Log.LogInfo($"[OTL][Loadout] leg biome {biome.Value}: no biome item.");
        return null;
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

    /// <summary>
    /// The local player's client: the host just spawned an item at their feet for them. Pick it up the
    /// vanilla way (Item.Interact -> RequestPickup on the host -> OnPickupAccepted), which puts it in a
    /// free slot AND equips it properly, so it can't end up "stuck" in a slot without showing in their
    /// hands. If every slot is full the item simply stays on the ground in front of them.
    /// </summary>
    public static void PickUpLocal(int viewId) => ModNetwork.Instance?.StartCoroutine(PickUpLocalRoutine(viewId));

    private static IEnumerator PickUpLocalRoutine(int viewId)
    {
        float deadline = Time.time + 4f;
        while (Time.time < deadline)
        {
            yield return new WaitForSeconds(0.3f); // let the instantiation land; vanilla ignores pickups within 0.25 s of an equip
            Character local = Character.localCharacter;
            if (local == null || local.data.dead || local.player == null) yield break;
            PhotonView view = PhotonNetwork.GetPhotonView(viewId);
            if (view == null || !view.TryGetComponent(out Item item)) continue; // not here yet (or already picked up and destroyed)
            if (!item.gameObject.activeSelf) continue; // pickup requested; a denied pickup reactivates it and we try again
            if (!local.player.HasEmptySlot(item.itemID))
            {
                Plugin.Log.LogInfo($"[OTL][Loadout] inventory full; left the {ItemCatalog.NameOf(item)} at my feet.");
                yield break;
            }

            if (local.refs.items.lastEquippedSlotTime + 0.25f > Time.time) continue;
            item.Interact(local);
        }
    }

    /// <summary>
    /// Host: give <paramref name="c"/> an item. Backpacks go straight into the backpack slot. Every other
    /// item is spawned at the player's feet and their own client picks it up the vanilla way
    /// (<see cref="PickUpLocal"/>): into a free slot and into their hands, or left on the ground if
    /// their slots are full.
    /// </summary>
    public static void Give(Character c, Item prefab)
    {
        if (prefab is Backpack && c.player != null && c.player.AddItem(prefab.itemID, null, out ItemSlot slot))
        {
            Plugin.Log.LogInfo($"[OTL][Loadout] HOST gave {c.characterName} a {ItemCatalog.NameOf(prefab)} (slot {slot.itemSlotID}).");
            return;
        }

        Vector3 spot = c.Center + c.data.lookDirection_Flat * 0.6f + Vector3.up * 0.5f;
        GameObject go = PhotonNetwork.Instantiate("0_Items/" + prefab.gameObject.name, spot, Quaternion.identity, 0);
        int viewId = go.GetComponent<PhotonView>().ViewID;
        if (c.IsLocal) PickUpLocal(viewId);
        else Net.SendToActor(Net.Actor(c), Msg.PickUpItem, viewId);
        Plugin.Log.LogInfo($"[OTL][Loadout] HOST spawned a {ItemCatalog.NameOf(prefab)} for {c.characterName} to pick up.");
    }
}
