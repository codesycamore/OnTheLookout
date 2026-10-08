using System.Collections.Generic;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.UI;

/// <summary>
/// Chasers can't see ghosts (spectators). A ghost floats around the player it spectates, so for a
/// chaser it would point straight at a runner. PEAK already hides your own ghost from you by switching
/// off its renderers on your client (PlayerGhost.RPCA_InitGhost); this does the same on a chaser's
/// client for every other ghost. Purely local and visual; voices are untouched.
/// Re-evaluated continuously, so it follows role changes, death and round end.
/// </summary>
internal sealed class GhostVisibility : MonoBehaviour
{
    private const float Interval = 0.25f;

    // What we switched off, so we only switch back on what was on before.
    private readonly Dictionary<PlayerGhost, List<Renderer>> _hiddenRenderers = new();
    private readonly Dictionary<PlayerGhost, bool> _hiddenThirdEye = new();
    private float _next;

    private static bool ShouldHide()
    {
        Character local = Character.localCharacter;
        return !Plugin.ModConfig.ChasersSeeGhosts.Synced() && local != null && !local.data.dead && RoleManager.IsChaser(local);
    }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + Interval;

        bool hide = ShouldHide();
        if (!hide && _hiddenRenderers.Count == 0) return;

        foreach (PlayerGhost ghost in FindObjectsByType<PlayerGhost>(FindObjectsSortMode.None))
        {
            if (ghost == null || (ghost.m_owner != null && ghost.m_owner.IsLocal)) continue; // our own ghost: PEAK handles it
            if (hide) Hide(ghost);
            else Show(ghost);
        }

        // Forget ghosts that no longer exist.
        foreach (PlayerGhost gone in new List<PlayerGhost>(_hiddenRenderers.Keys))
        {
            if (gone == null)
            {
                _hiddenRenderers.Remove(gone!);
                _hiddenThirdEye.Remove(gone!);
            }
        }
    }

    private void Hide(PlayerGhost ghost)
    {
        if (_hiddenRenderers.ContainsKey(ghost)) return;
        var turnedOff = new List<Renderer>();
        foreach (Renderer r in AllRenderers(ghost))
        {
            if (r != null && r.enabled)
            {
                r.enabled = false;
                turnedOff.Add(r);
            }
        }

        _hiddenRenderers[ghost] = turnedOff;
        bool eye = ghost.thirdEye != null && ghost.thirdEye.activeSelf;
        if (eye) ghost.thirdEye!.SetActive(false);
        _hiddenThirdEye[ghost] = eye;
    }

    private void Show(PlayerGhost ghost)
    {
        if (!_hiddenRenderers.TryGetValue(ghost, out List<Renderer> turnedOff)) return;
        foreach (Renderer r in turnedOff)
        {
            if (r != null) r.enabled = true;
        }

        if (_hiddenThirdEye.TryGetValue(ghost, out bool eye) && eye && ghost.thirdEye != null) ghost.thirdEye.SetActive(true);
        _hiddenRenderers.Remove(ghost);
        _hiddenThirdEye.Remove(ghost);
    }

    private static IEnumerable<Renderer> AllRenderers(PlayerGhost ghost)
    {
        if (ghost.PlayerRenderers != null) foreach (Renderer r in ghost.PlayerRenderers) yield return r;
        if (ghost.EyeRenderers != null) foreach (Renderer r in ghost.EyeRenderers) yield return r;
        yield return ghost.mouthRenderer;
        yield return ghost.accessoryRenderer;
    }
}
