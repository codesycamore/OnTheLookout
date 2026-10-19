using System.Collections;
using System.Collections.Generic;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// Inventory changes when a player's role changes between legs.
/// The host saves everyone's role when a leg ends, before the role window opens (<see cref="HostSnapshot"/>),
/// and compares it with the new roles once the next leg has started and roles are assigned
/// (<see cref="HostApplySnapshot"/>):
/// - runner -> chaser: they drop everything, backpack included (their own client, PEAK's DropAllItems, the
///   same as on death); then <see cref="LegLoadout.HostEnsureChaserKits"/> gives them the blowgun and gem;
/// - chaser -> runner: they drop the blowgun and chaser gem, get a backpack if they have none, and get this
///   leg's runner items if they haven't had them yet.
/// A debug role swap during a leg (<see cref="RoleManager.SetRole"/>) is handled the same way right away.
/// As a safety net every runner's own client drops any blowgun or chaser gem it still carries at each leg start.
/// </summary>
internal static class RoleSwap
{
    private const float DropSettleSeconds = 2.5f;

    /// <summary>Host: everyone's role at the end of the last leg; null when there is nothing to compare.</summary>
    private static Dictionary<int, Role>? s_Snapshot;

    public static void Install()
    {
        RoundManager.LegStarted += _ => ModNetwork.Instance?.StartCoroutine(LocalSafetyCheck());
        RoundManager.FreshStart += () => s_Snapshot = null;
    }

    /// <summary>Host, when a leg ends (before the role window): remember who was a chaser and who a runner.</summary>
    public static void HostSnapshot()
    {
        if (!Net.IsHost) return;
        s_Snapshot = new Dictionary<int, Role>();
        foreach (Photon.Realtime.Player p in PhotonNetwork.PlayerList)
        {
            s_Snapshot[p.ActorNumber] = RoleManager.RoleOf(p.ActorNumber);
        }

        Plugin.Log.LogInfo($"[OTL][Swap] HOST: saved {s_Snapshot.Count} role(s) before the role window.");
    }

    /// <summary>Host, at the start of a leg after the new roles are assigned: handle everyone whose role changed.</summary>
    public static void HostApplySnapshot()
    {
        if (!Net.IsHost || s_Snapshot == null) return;
        Dictionary<int, Role> before = s_Snapshot;
        s_Snapshot = null;
        int changed = 0;
        foreach (Photon.Realtime.Player p in PhotonNetwork.PlayerList)
        {
            if (!before.TryGetValue(p.ActorNumber, out Role old)) continue; // joined during the window: nothing to swap
            Role now = RoleManager.RoleOf(p.ActorNumber);
            if (old == now) continue;
            HostOnSwap(p.ActorNumber, now, legStart: true);
            changed++;
        }

        Plugin.Log.LogInfo($"[OTL][Swap] HOST: compared roles with the end of the last leg: {changed} change(s).");
    }

    /// <summary>Host: <paramref name="actor"/>'s role just changed to <paramref name="now"/>.</summary>
    /// <summary>
    /// <paramref name="legStart"/>: the swap was found when a leg started. With ResetItemsEachLeg everyone's items
    /// are already being removed then (<see cref="LegLoadout"/>), so there is nothing to drop.
    /// </summary>
    public static void HostOnSwap(int actor, Role now, bool legStart = false)
    {
        if (!Net.IsHost || !RoundManager.IsActive) return;
        bool hotbarReset = legStart && Plugin.ModConfig.ResetItemsEachLeg.Synced();
        if (now == Role.Chaser) HostBecameChaser(actor, hotbarReset);
        else HostBecameRunner(actor, hotbarReset);
    }

    private static void HostBecameChaser(int actor, bool hotbarReset)
    {
        Plugin.Log.LogInfo($"[OTL][Swap] HOST: {Net.NameOf(actor)} is now a chaser: {(hotbarReset ? "items removed by the leg reset" : "dropping everything")}, then the chaser kit.");
        LegLoadout.HostDelayChaserKit(actor, DropSettleSeconds); // the kit comes after the drop has landed
        if (hotbarReset) return;
        if (Net.CharacterOf(actor) is { IsLocal: true }) DropAllLocal();
        else Net.SendToActor(actor, Msg.DropAllItems);
    }

    private static void HostBecameRunner(int actor, bool hotbarReset)
    {
        Plugin.Log.LogInfo($"[OTL][Swap] HOST: {Net.NameOf(actor)} is now a runner: dropping the chaser kit, backpack if needed.");
        if (!hotbarReset)
        {
            if (Net.CharacterOf(actor) is { IsLocal: true }) DropChaserKitLocal();
            else Net.SendToActor(actor, Msg.DropChaserKit);
        }

        ModNetwork.Instance?.StartCoroutine(RunnerKitLater(actor));
    }

    private static IEnumerator RunnerKitLater(int actor)
    {
        yield return new WaitForSeconds(DropSettleSeconds);
        Character? c = Net.CharacterOf(actor);
        if (!Net.IsHost || c == null || c.data.dead || c.player == null || !RoleManager.IsRunner(c)) yield break;
        Backpack? backpack = ItemCatalog.PlainBackpack;
        if (backpack != null && c.player.backpackSlot.IsEmpty()) LegLoadout.Give(c, backpack);

        // The runner gem (dash), and a runner item for this leg unless they already got this leg's items as a runner.
        yield return new WaitForSeconds(1f);
        if (Net.IsHost && c != null && !c.data.dead && RoleManager.IsRunner(c))
        {
            LegLoadout.HostGiveRunnerGem(c);
            LegLoadout.HostGiveRunnerLegItems(c);
        }
    }

    /// <summary>
    /// Every client, shortly after each leg starts: a runner never keeps a blowgun or the chaser gem, and a chaser
    /// never keeps the runner gem.
    /// </summary>
    private static IEnumerator LocalSafetyCheck()
    {
        yield return new WaitForSeconds(DropSettleSeconds + 1f);
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null) yield break;
        System.Func<Item, bool>? wrong = RoleManager.IsRunner(local) ? IsChaserKit : RoleManager.IsChaser(local) ? RunnerDash.IsRunnerGem : null;
        if (wrong == null) yield break;
        foreach (ItemSlot slot in local.player.itemSlots)
        {
            if (slot == null || slot.IsEmpty() || slot.prefab == null || !wrong(slot.prefab)) continue;
            Plugin.Log.LogInfo("[OTL][Swap] carrying the other role's items; dropping them.");
            ModNetwork.Instance?.StartCoroutine(DropItemsRoutine(wrong));
            yield break;
        }
    }

    public static bool IsChaserKit(Item item) => ItemCatalog.IsBlowgun(item) || ChaserKit.IsGem(item);

    /// <summary>
    /// The local player's client: drop every item and the backpack (PEAK's own drop-everything, as on death), or with
    /// <paramref name="backpackOnly"/> just the backpack (the leg-start item reset empties the hotbar).
    /// </summary>
    public static void DropAllLocal(bool backpackOnly = false)
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null) return;
        if (backpackOnly)
        {
            if (local.player.backpackSlot.IsEmpty()) return;
            Vector3 spot = local.Center + local.data.lookDirection_Flat * 0.8f + Vector3.up * 0.3f;
            local.refs.items.photonView.RPC("DropItemFromSlotRPC", RpcTarget.All, (byte)3, spot);
            Plugin.Log.LogInfo("[OTL][Swap] now a chaser: dropped the backpack.");
            return;
        }

        local.refs.items.DropAllItems(includeBackpack: true);
        local.refs.items.EquipSlot(Optionable<byte>.None);
        Plugin.Log.LogInfo("[OTL][Swap] now a chaser: dropped all items and the backpack.");
    }

    /// <summary>The local player's client: drop the blowgun and the chaser gem (no longer a chaser).</summary>
    public static void DropChaserKitLocal() => ModNetwork.Instance?.StartCoroutine(DropItemsRoutine(IsChaserKit));

    private static IEnumerator DropItemsRoutine(System.Func<Item, bool> which)
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null) yield break;

        Item? held = local.data.currentItem;
        if (held != null && which(held))
        {
            local.refs.items.EquipSlot(Optionable<byte>.None); // put it away first, then drop it from its slot
            yield return new WaitForSeconds(0.3f);
        }

        if (local == null || local.player == null) yield break;
        Vector3 spot = local.Center + local.data.lookDirection_Flat * 0.8f + Vector3.up * 0.3f;
        for (byte i = 0; i < local.player.itemSlots.Length; i++)
        {
            ItemSlot slot = local.player.itemSlots[i];
            if (slot == null || slot.IsEmpty() || slot.prefab == null || !which(slot.prefab)) continue;
            local.refs.items.photonView.RPC("DropItemFromSlotRPC", RpcTarget.All, i, spot);
            spot += Vector3.up * 0.4f;
            Plugin.Log.LogInfo($"[OTL][Swap] dropped {ItemCatalog.NameOf(slot.prefab)} from slot {i} (role items).");
        }
    }
}
