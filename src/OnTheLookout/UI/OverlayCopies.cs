using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace OnTheLookout.UI;

/// <summary>
/// See-through copies of an object's renderers, for highlights drawn through walls and terrain (the chaser gem's
/// x-ray, the runners' luggage glow). Each copy shares the source renderer's mesh (and bones, for a skinned mesh, so
/// it follows every animation) and is parented to it, with an unlit material that ignores depth: UI/Default with
/// unity_GUIZTestMode = Always (the shader the safe zone wall falls back to, there with LessEqual).
/// </summary>
internal static class OverlayCopies
{
    public const string CopyName = "OTL_Overlay";

    /// <summary>An unlit, transparent, never depth-tested material; null if PEAK has no UI/Default shader.</summary>
    public static Material? CreateMaterial(string name, Color color, int renderQueue)
    {
        Shader? shader = Shader.Find("UI/Default");
        if (shader == null)
        {
            Plugin.Log.LogWarning($"[OTL][Overlay] shader UI/Default not found; {name} is off.");
            return null;
        }

        var material = new Material(shader) { name = name, renderQueue = renderQueue, color = color };
        material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        Plugin.Log.LogInfo($"[OTL][Overlay] {name} material ready ({shader.name}).");
        return material;
    }

    /// <summary>
    /// Stencil settings for a CreateMaterial material (UI/Default's _Stencil* properties): with <paramref name="write"/>
    /// it marks every pixel it draws with <paramref name="bit"/>; otherwise it only draws where that bit is NOT set.
    /// </summary>
    public static void SetStencil(Material material, int bit, bool write)
    {
        material.SetFloat("_Stencil", bit);
        material.SetFloat("_StencilComp", (float)(write ? CompareFunction.Always : CompareFunction.NotEqual));
        material.SetFloat("_StencilOp", (float)(write ? StencilOp.Replace : StencilOp.Keep));
        material.SetFloat("_StencilReadMask", bit);
        material.SetFloat("_StencilWriteMask", bit);
    }

    /// <summary>The active, enabled mesh renderers under <paramref name="root"/>: not held items, not world text, not our own copies.</summary>
    public static IEnumerable<Renderer> Sources(Component root) =>
        root.GetComponentsInChildren<Renderer>(false).Where(r =>
            r != null && r.enabled && r.gameObject.name != CopyName && r.GetComponentInParent<Item>() == null
            && r.GetComponent<TMPro.TMP_Text>() == null
            && (r is SkinnedMeshRenderer { sharedMesh: not null } || (r is MeshRenderer && r.GetComponent<MeshFilter>() is { sharedMesh: not null })));

    /// <summary>Copies of <see cref="Sources"/>, drawn with <paramref name="material"/>.</summary>
    public static List<Renderer> Build(Component root, Material material)
    {
        var copies = new List<Renderer>();
        foreach (Renderer r in Sources(root))
        {
            if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                var copy = NewCopy(r).AddComponent<SkinnedMeshRenderer>();
                copy.sharedMesh = skinned.sharedMesh;
                copy.bones = skinned.bones;
                copy.rootBone = skinned.rootBone;
                copy.localBounds = skinned.localBounds;
                copy.updateWhenOffscreen = true;
                Setup(copy, material, skinned.sharedMesh.subMeshCount);
                copies.Add(copy);
            }
            else if (r is MeshRenderer && r.GetComponent<MeshFilter>() is { sharedMesh: { } mesh })
            {
                GameObject go = NewCopy(r);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var copy = go.AddComponent<MeshRenderer>();
                Setup(copy, material, mesh.subMeshCount);
                copies.Add(copy);
            }
        }

        return copies;
    }

    public static void Destroy(List<Renderer>? copies)
    {
        if (copies == null) return;
        foreach (Renderer r in copies)
        {
            if (r != null) Object.Destroy(r.gameObject);
        }

        copies.Clear();
    }

    private static GameObject NewCopy(Renderer source)
    {
        var go = new GameObject(CopyName) { layer = source.gameObject.layer };
        go.transform.SetParent(source.transform, false);
        return go;
    }

    private static void Setup(Renderer copy, Material material, int subMeshes)
    {
        copy.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, subMeshes)).ToArray();
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }
}
