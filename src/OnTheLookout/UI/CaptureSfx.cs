using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zorro.Core;

namespace OnTheLookout.UI;

/// <summary>
/// Plays PEAK's dynamite explosion sound at a position. The sound assets are pulled out of the
/// dynamite item's explosion prefab (Dynamite.explosionPrefab) by reflection, without spawning the
/// explosion itself, so there is no blast or damage. Falls back to raw AudioSource clips.
/// </summary>
internal static class CaptureSfx
{
    private static List<SFX_Instance>? s_Sfx;
    private static List<AudioClip>? s_Clips;

    public static void Play(Vector3 position)
    {
        if (s_Sfx == null) Load();

        if (s_Sfx is { Count: > 0 } && SFX_Player.instance != null)
        {
            foreach (SFX_Instance sfx in s_Sfx) sfx.Play(position);
        }
        else if (s_Clips is { Count: > 0 })
        {
            foreach (AudioClip clip in s_Clips) AudioSource.PlayClipAtPoint(clip, position, 1f);
        }
    }

    private static void Load()
    {
        s_Sfx = new List<SFX_Instance>();
        s_Clips = new List<AudioClip>();
        try
        {
            ItemDatabase db = SingletonAsset<ItemDatabase>.Instance;
            Dynamite? dynamite = db?.itemLookup.Values.Select(i => i != null ? i.GetComponentInChildren<Dynamite>(true) : null).FirstOrDefault(d => d != null);
            GameObject? prefab = dynamite != null ? dynamite.explosionPrefab : null;
            if (prefab == null)
            {
                Plugin.Log.LogWarning("[OTL][UI] dynamite explosion prefab not found; capture sound disabled.");
                return;
            }

            foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object? value = field.GetValue(component);
                    if (value is SFX_Instance one && one != null) s_Sfx.Add(one);
                    else if (value is IEnumerable many and not string)
                    {
                        foreach (object? element in many)
                        {
                            if (element is SFX_Instance s && s != null) s_Sfx.Add(s);
                        }
                    }
                }
            }

            s_Clips.AddRange(prefab.GetComponentsInChildren<AudioSource>(true).Select(a => a.clip).Where(c => c != null));
            s_Sfx = s_Sfx.Distinct().ToList();
            Plugin.Log.LogInfo($"[OTL][UI] capture sound: {s_Sfx.Count} SFX asset(s), {s_Clips.Count} clip(s) from {prefab.name}.");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[OTL][UI] couldn't load capture sound: {e.Message}");
        }
    }
}
