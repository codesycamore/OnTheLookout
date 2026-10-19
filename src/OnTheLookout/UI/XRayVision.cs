using System.Collections.Generic;
using System.Linq;
using OnTheLookout.Core;
using UnityEngine;

namespace OnTheLookout.UI;

/// <summary>
/// The chaser gem's x-ray vision, on the chaser's own screen only: for GemXRaySeconds every living runner within
/// GemXRayRange is drawn through walls, terrain and every other object as a blinking red fill with a strong, solid red
/// outline.
/// - Fill: <see cref="OverlayCopies"/> of the runner's renderers (they follow every animation), which also mark their
///   pixels in the stencil buffer.
/// - Outline: the runner's meshes (skinned ones baked each frame) drawn again 8 times, shifted a few pixels in each
///   screen direction, solid red and only where the fill didn't draw (stencil), so just a rim around the silhouette
///   is left. The shift is scaled by distance, so the outline keeps the same thickness on screen.
/// Everything is removed when the x-ray ends or a runner leaves its range. Purely local and visual.
/// </summary>
internal sealed class XRayVision : MonoBehaviour
{
    private const float BlinkHz = 3f;
    private const int StencilBit = 64;

    /// <summary>Outline thickness on screen (pixels).</summary>
    private const float OutlinePixels = 3f;

    private static readonly Color Red = new(1f, 0.06f, 0.06f, 1f);
    private static readonly Vector2[] Directions =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        new(0.707f, 0.707f), new(-0.707f, 0.707f), new(0.707f, -0.707f), new(-0.707f, -0.707f),
    };

    private static float s_Until;
    private static float s_Range;

    private sealed class Shown
    {
        public List<Renderer> Copies = new();
        public readonly List<(Renderer Source, Mesh Mesh, bool Baked)> Outline = new();
    }

    private readonly Dictionary<Character, Shown> _shown = new();
    private Material? _fill, _outline;
    private bool _materialTried;

    public static bool Active => Time.time < s_Until;

    /// <summary>Local chaser: x-ray vision for <paramref name="seconds"/>, on runners within <paramref name="range"/> m.</summary>
    public static void Begin(float seconds, float range)
    {
        if (seconds <= 0f || range <= 0f) return;
        s_Until = Time.time + seconds;
        s_Range = range;
        Plugin.Log.LogInfo($"[OTL][XRay] x-ray vision for {seconds:0.#}s, runners within {range:0}m.");
    }

    public static void Stop() => s_Until = 0f;

    private void LateUpdate()
    {
        Character local = Character.localCharacter;
        bool on = Active && local != null && !local.data.dead && RoundManager.IsChasing && RoleManager.IsChaser(local);
        if (!on)
        {
            if (_shown.Count > 0) ClearAll();
            return;
        }

        if (!_materialTried)
        {
            _materialTried = true;
            _fill = OverlayCopies.CreateMaterial("OTL_XRay", Red, 4000);
            _outline = OverlayCopies.CreateMaterial("OTL_XRayOutline", Red, 4001);
            if (_fill != null) OverlayCopies.SetStencil(_fill, StencilBit, write: true);
            if (_outline != null) OverlayCopies.SetStencil(_outline, StencilBit, write: false);
        }

        if (_fill == null) return;
        float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * BlinkHz);
        _fill.color = new Color(Red.r, Red.g, Red.b, Mathf.Lerp(0.2f, 0.7f, blink));

        foreach (Character c in Character.AllCharacters)
        {
            if (c == null || c == local) continue;
            bool wanted = !c.data.dead && RoleManager.IsRunner(c) && Vector3.Distance(local!.Center, c.Center) <= s_Range;
            bool shown = _shown.ContainsKey(c);
            if (wanted && !shown) _shown[c] = Show(c);
            else if (!wanted && shown) Remove(c);
        }

        foreach (Character gone in _shown.Keys.Where(k => k == null).ToList()) Remove(gone);
        DrawOutlines();
    }

    private Shown Show(Character c)
    {
        var shown = new Shown { Copies = OverlayCopies.Build(c, _fill!) };
        foreach (Renderer r in OverlayCopies.Sources(c))
        {
            if (r is SkinnedMeshRenderer) shown.Outline.Add((r, new Mesh { name = "OTL_XRayBake" }, true));
            else if (r.GetComponent<MeshFilter>() is { sharedMesh: { } mesh }) shown.Outline.Add((r, mesh, false));
        }

        return shown;
    }

    /// <summary>Each frame: every shown runner's meshes, shifted OutlinePixels in 8 screen directions, solid red.</summary>
    private void DrawOutlines()
    {
        if (_outline == null) return;
        Camera? cam = MainCamera.instance != null ? MainCamera.instance.GetComponent<Camera>() : Camera.main;
        if (cam == null) return;
        Transform view = cam.transform;
        float pixelAngle = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, Screen.height);

        foreach (Shown shown in _shown.Values)
        {
            foreach ((Renderer source, Mesh mesh, bool baked) in shown.Outline)
            {
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy) continue;
                Matrix4x4 matrix;
                if (baked && source is SkinnedMeshRenderer skinned)
                {
                    skinned.BakeMesh(mesh, true);
                    matrix = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                }
                else
                {
                    matrix = source.transform.localToWorldMatrix;
                }

                float distance = Vector3.Distance(view.position, source.bounds.center);
                float shift = OutlinePixels * pixelAngle * distance;
                foreach (Vector2 d in Directions)
                {
                    Vector3 offset = (view.right * d.x + view.up * d.y) * shift;
                    Matrix4x4 shifted = Matrix4x4.Translate(offset) * matrix;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        Graphics.DrawMesh(mesh, shifted, _outline, source.gameObject.layer, cam, sub, null, false, false);
                    }
                }
            }
        }
    }

    private void Remove(Character c)
    {
        if (_shown.TryGetValue(c, out Shown shown))
        {
            OverlayCopies.Destroy(shown.Copies);
            foreach ((_, Mesh mesh, bool baked) in shown.Outline)
            {
                if (baked && mesh != null) Destroy(mesh);
            }
        }

        _shown.Remove(c);
    }

    private void ClearAll()
    {
        foreach (Character c in _shown.Keys.ToList()) Remove(c);
        _shown.Clear();
    }
}
