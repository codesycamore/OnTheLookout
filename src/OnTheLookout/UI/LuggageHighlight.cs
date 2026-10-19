using System.Collections.Generic;
using System.Linq;
using OnTheLookout.Core;
using OnTheLookout.Modules;
using UnityEngine;

namespace OnTheLookout.UI;

/// <summary>
/// On a living runner's screen during a round: every unopened runner luggage on the map (PEAK's Luggage.ALL_LUGGAGE;
/// not clown luggage, which only chasers can open, and not scout statues) glows a subtle, faint green through walls and
/// terrain, at any distance (<see cref="OverlayCopies"/>). The glow fades out as the runner comes close and is gone
/// within reach (<see cref="HiddenWithin"/> m), so it never covers what they are climbing; it's removed for good once
/// the luggage is open. Purely local and visual.
/// </summary>
internal sealed class LuggageHighlight : MonoBehaviour
{
    /// <summary>The glow is gone this close (m); PEAK's interact reach is 2 m from the camera (Interaction.distance).</summary>
    private const float HiddenWithin = 3.5f;

    /// <summary>From this distance (m) down to HiddenWithin the glow fades out.</summary>
    private const float FadeFrom = 15f;

    private const float Alpha = 0.22f;
    private static readonly Color Green = new(0.35f, 1f, 0.5f, Alpha);
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Dictionary<Luggage, List<Renderer>> _copies = new();
    private readonly MaterialPropertyBlock _block = new();
    private Material? _material;
    private bool _materialTried;
    private float _nextScan;

    private void LateUpdate()
    {
        Character local = Character.localCharacter;
        bool on = Plugin.ModConfig.RunnersSeeLuggage.Synced() && RoundManager.IsActive && local != null && !local.data.dead && RoleManager.IsRunner(local);
        if (!on)
        {
            if (_copies.Count > 0) ClearAll();
            return;
        }

        if (!_materialTried)
        {
            _materialTried = true;
            _material = OverlayCopies.CreateMaterial("OTL_LuggageGlow", Green, 3990);
        }

        if (_material == null) return;

        // New, opened or unloaded luggage: re-checked twice a second.
        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + 0.5f;
            foreach (Luggage luggage in Luggage.ALL_LUGGAGE)
            {
                if (luggage == null || _copies.ContainsKey(luggage) || !Wanted(luggage)) continue;
                _copies[luggage] = OverlayCopies.Build(luggage, _material);
            }

            foreach (Luggage luggage in _copies.Keys.Where(l => l == null || !Wanted(l)).ToList()) Remove(luggage);
        }

        // Fade by distance: faint at any range, fading out close up so it never covers the climb.
        Vector3 eye = MainCamera.instance != null ? MainCamera.instance.transform.position : local!.Center;
        foreach ((Luggage luggage, List<Renderer> copies) in _copies)
        {
            if (luggage == null) continue;
            float distance = Vector3.Distance(eye, luggage.transform.position);
            float alpha = Alpha * Mathf.InverseLerp(HiddenWithin, FadeFrom, distance);
            _block.SetColor(ColorId, new Color(Green.r, Green.g, Green.b, alpha));
            foreach (Renderer r in copies)
            {
                if (r == null) continue;
                r.enabled = alpha > 0.005f;
                r.SetPropertyBlock(_block);
            }
        }
    }

    private static bool Wanted(Luggage luggage) =>
        luggage.isActiveAndEnabled && !luggage.IsOpen && luggage is not RespawnChest && !ItemRules.IsClownLuggage(luggage);

    private void Remove(Luggage luggage)
    {
        if (_copies.TryGetValue(luggage, out List<Renderer> copies)) OverlayCopies.Destroy(copies);
        _copies.Remove(luggage);
    }

    private void ClearAll()
    {
        foreach (Luggage luggage in _copies.Keys.ToList()) Remove(luggage);
        _copies.Clear();
    }
}
