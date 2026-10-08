using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OnTheLookout.UI;

/// <summary>
/// PEAK's "the lava rises" alarm, without its banner: the sounds are taken from GUIManager.lavaRises (the
/// popup TheLavaRises() shows), both SFX_Instance assets referenced by its components and its AudioSource
/// clips, and played at the camera together with PEAK's own camera shake (GamefeelHandler.AddPerlinShake).
/// </summary>
internal static class LavaAlert
{
    private static List<SFX_Instance>? s_Sfx;
    private static List<AudioClip>? s_Clips;
    private static float s_LastPlayed = -10f;

    public static void Play()
    {
        if (Time.time - s_LastPlayed < 2f) return; // several zombies in one wave: one alarm
        s_LastPlayed = Time.time;

        if (s_Sfx == null) Load();
        Vector3 at = MainCamera.instance != null ? MainCamera.instance.transform.position : Vector3.zero;
        if (s_Sfx is { Count: > 0 } && SFX_Player.instance != null)
        {
            foreach (SFX_Instance sfx in s_Sfx) sfx.Play(at);
        }
        else if (s_Clips is { Count: > 0 })
        {
            foreach (AudioClip clip in s_Clips) AudioSource.PlayClipAtPoint(clip, at, 1f);
        }

        if (GamefeelHandler.instance != null) GamefeelHandler.instance.AddPerlinShake(5f, 1.2f, 20f);
    }

    private static void Load()
    {
        s_Sfx = new List<SFX_Instance>();
        s_Clips = new List<AudioClip>();
        GameObject? popup = GUIManager.instance != null ? GUIManager.instance.lavaRises : null;
        if (popup == null)
        {
            Plugin.Log.LogWarning("[OTL][UI] lava-rises popup not found; zombie alert has no sound.");
            return;
        }

        s_Clips.AddRange(popup.GetComponentsInChildren<AudioSource>(true).Select(a => a.clip).Where(c => c != null));
        foreach (MonoBehaviour component in popup.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            foreach (FieldInfo field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object? value;
                try { value = field.GetValue(component); }
                catch { continue; }
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

        s_Sfx = s_Sfx.Distinct().ToList();
        Plugin.Log.LogInfo($"[OTL][UI] zombie alert sound: {s_Sfx.Count} SFX asset(s), {s_Clips.Count} clip(s) from {popup.name}.");
    }
}
