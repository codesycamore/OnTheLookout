using System.Collections;
using System.Collections.Generic;
using OnTheLookout.Core;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.Modules;

/// <summary>
/// What happens to a player's inventory when their role changes during a round (the role draw at a campfire,
/// or a debug swap):
/// - runner -> chaser: they drop everything, backpack included (their own client, PEAK's DropAllItems, the
///   same as on death); then <see cref="LegLoadout.HostEnsureChaserKits"/> gives them the blowgun and napberry;
/// - chaser -> runner: they drop the blowgun and napberry, get a backpack if they have none, and get this leg's
///   runner items if they haven't had them yet.
/// The host compares every role update with the previous one; the first draw of a round (from no roles) is
/// not a swap.
/// </summary>
internal static class RoleSwap
{
    private const float DropSettleSeconds = 2.5f;

    private static readonly Dictionary<int, Role> s_LastRoles = new();

    public static void Install() => RoleManager.RolesChanged += OnRolesChanged;

    private static void OnRolesChanged()
    {
        if (!Net.IsHost)
        {
            s_LastRoles.Clear();
            return;
        }

        bool swapsCount = RoundManager.IsActive; // outside a round (first draw, airport reset) only take a snapshot
        foreach (Photon.Realtime.Player p in PhotonNetwork.PlayerList)
        {
            int actor = p.ActorNumber;
            Role role = RoleManager.RoleOf(actor);
            if (swapsCount && s_LastRoles.TryGetValue(actor, out Role previous) && previous != role)
            {
                if (role == Role.Chaser) HostBecameChaser(actor);
                else HostBecameRunner(actor);
            }

            s_LastRoles[actor] = role;
        }
    }

    private static void HostBecameChaser(int actor)
    {
        Plugin.Log.LogInfo($"[OTL][Swap] HOST: {Net.NameOf(actor)} is now a chaser: dropping everything, then the chaser kit.");
        LegLoadout.HostDelayChaserKit(actor, DropSettleSeconds); // the kit comes after the drop has landed
        if (Net.CharacterOf(actor) is { IsLocal: true }) DropAllLocal();
        else Net.SendToActor(actor, Msg.DropAllItems);
    }

    private static void HostBecameRunner(int actor)
    {
        Plugin.Log.LogInfo($"[OTL][Swap] HOST: {Net.NameOf(actor)} is now a runner: dropping the chaser kit, backpack if needed.");
        if (Net.CharacterOf(actor) is { IsLocal: true }) DropChaserKitLocal();
        else Net.SendToActor(actor, Msg.DropChaserKit);
        ModNetwork.Instance?.StartCoroutine(GiveBackpackLater(actor));
    }

    private static IEnumerator GiveBackpackLater(int actor)
    {
        yield return new WaitForSeconds(DropSettleSeconds);
        Character? c = Net.CharacterOf(actor);
        if (!Net.IsHost || c == null || c.data.dead || c.player == null || !RoleManager.IsRunner(c)) yield break;
        Backpack? backpack = ItemCatalog.PlainBackpack;
        if (backpack != null && c.player.backpackSlot.IsEmpty()) LegLoadout.Give(c, backpack);

        // A runner item for this leg too, unless they already got this leg's items as a runner.
        // (At a campfire role draw the leg-start hand-out covers them; this catches swaps during a leg.)
        yield return new WaitForSeconds(1f);
        if (Net.IsHost && c != null && !c.data.dead && RoleManager.IsRunner(c))
        {
            LegLoadout.HostGiveRunnerLegItems(c);
        }
    }

    /// <summary>The local player's client: drop every item and the backpack (PEAK's own drop-everything, as on death).</summary>
    public static void DropAllLocal()
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null) return;
        local.refs.items.DropAllItems(includeBackpack: true);
        local.refs.items.EquipSlot(Optionable<byte>.None);
        Plugin.Log.LogInfo("[OTL][Swap] now a chaser: dropped all items and the backpack.");
    }

    /// <summary>The local player's client: drop the blowgun and the napberry (no longer a chaser).</summary>
    public static void DropChaserKitLocal() => ModNetwork.Instance?.StartCoroutine(DropChaserKitRoutine());

    private static IEnumerator DropChaserKitRoutine()
    {
        Character local = Character.localCharacter;
        if (local == null || local.data.dead || local.player == null) yield break;

        Item? held = local.data.currentItem;
        if (held != null && (ItemCatalog.IsBlowgun(held) || ChaserKit.IsNapberry(held)))
        {
            local.refs.items.EquipSlot(Optionable<byte>.None); // put it away first, then drop it from its slot
            yield return new WaitForSeconds(0.3f);
        }

        if (local == null || local.player == null) yield break;
        Vector3 spot = local.Center + local.data.lookDirection_Flat * 0.8f + Vector3.up * 0.3f;
        for (byte i = 0; i < local.player.itemSlots.Length; i++)
        {
            ItemSlot slot = local.player.itemSlots[i];
            if (slot == null || slot.IsEmpty() || slot.prefab == null) continue;
            if (!ItemCatalog.IsBlowgun(slot.prefab) && !ChaserKit.IsNapberry(slot.prefab)) continue;
            local.refs.items.photonView.RPC("DropItemFromSlotRPC", RpcTarget.All, i, spot);
            spot += Vector3.up * 0.4f;
            Plugin.Log.LogInfo($"[OTL][Swap] now a runner: dropped {ItemCatalog.NameOf(slot.prefab)} from slot {i}.");
        }
    }
}
