using System.Collections.Generic;
using System.Linq;
using OnTheLookout.Core;
using OnTheLookout.Modules;
using UnityEngine;

namespace OnTheLookout.UI;

/// <summary>
/// A glowing yellow wall around every campfire safe zone, shown on every client for the whole round (from the first
/// leg's role reveal until the run is back in the airport). With SafeZoneRequiresLit only lit campfires get one.
/// The safe zone is a sphere of CampfireSafeRadius around the campfire (SafeZoneSystem.SafeZoneAt), so the wall
/// stands where that sphere meets the ground: for each direction the ground is found with downward raycasts and the
/// horizontal radius is shrunk until the ground point is CampfireSafeRadius from the campfire.
/// A wall built before the ground around its campfire was there (far-away terrain) is rebuilt a few seconds later.
/// The peak (PeakSafeZone) gets one too: everything past PEAK's last progress point is the peak, so its wall runs
/// straight across the mountain along that line, following the ground.
/// Purely local visuals (a generated mesh, no colliders): nobody is blocked by it.
/// </summary>
internal sealed class SafeZoneWall : MonoBehaviour
{
    private const int Segments = 96;
    private const float WallHeight = 3.5f;
    private const float RimHeight = 0.12f;
    private const float RetrySeconds = 5f;
    private static readonly Color Yellow = new(1f, 0.84f, 0.2f);

    private sealed class Wall
    {
        public GameObject Go = null!;
        public Vector3 Centre;
        public float Radius;
        public bool Complete; // every direction found the ground
        public float RetryAt;
    }

    private readonly Dictionary<Campfire, Wall> _walls = new();
    private readonly Dictionary<Campfire, int> _attempts = new(); // walls built per campfire; a real drop never finds ground
    private const int MaxAttempts = 3;
    private readonly List<Campfire> _gone = new();
    private Material? _material;
    private bool _failed;
    private float _nextScan;

    private void Update()
    {
        bool show = RoundManager.IsActive && Plugin.ModConfig.EnableSafeZones.Value && !_failed;
        if (!show)
        {
            HideAll();
            return;
        }

        if (Time.time >= _nextScan)
        {
            _nextScan = Time.time + 1f;
            Refresh();
            RefreshPeak();
        }

        if (_material != null)
        {
            // A slow glow pulse.
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f);
            _material.color = new Color(pulse, pulse, pulse, 1f);
        }
    }

    /// <summary>Once a second: a wall for each campfire that is a safe zone, none for the others.</summary>
    private void Refresh()
    {
        float radius = Mathf.Max(1f, Plugin.ModConfig.CampfireSafeRadius.Synced());
        bool requireLit = Plugin.ModConfig.SafeZoneRequiresLit.Synced();
        var wanted = new HashSet<Campfire>(SafeZoneSystem.Campfires.Where(f =>
            f != null && f.isActiveAndEnabled && (!requireLit || f.state == Campfire.FireState.Lit)));

        _gone.Clear();
        foreach (KeyValuePair<Campfire, Wall> pair in _walls)
        {
            Wall wall = pair.Value;
            bool stale = pair.Key == null || !wanted.Contains(pair.Key) || wall.Go == null
                || !Mathf.Approximately(wall.Radius, radius) || (pair.Key.transform.position - wall.Centre).sqrMagnitude > 0.01f
                || (!wall.Complete && Time.time >= wall.RetryAt);
            if (stale) _gone.Add(pair.Key!); // a destroyed campfire is still the dictionary key
        }

        foreach (Campfire fire in _gone) Remove(fire);
        foreach (Campfire fire in wanted)
        {
            if (!_walls.ContainsKey(fire)) Build(fire, radius);
        }
    }

    private void Build(Campfire fire, float radius)
    {
        _material ??= CreateMaterial();
        if (_material == null)
        {
            _failed = true;
            return;
        }

        Vector3 centre = fire.transform.position;
        var go = new GameObject("OTL_SafeZoneWall");
        go.transform.position = centre; // the mesh is built around the campfire
        go.AddComponent<MeshFilter>().sharedMesh = BuildMesh(centre, radius, out int misses);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        int attempt = _attempts.TryGetValue(fire, out int a) ? a + 1 : 1;
        _attempts[fire] = attempt;
        bool complete = misses == 0 || attempt >= MaxAttempts;
        _walls[fire] = new Wall { Go = go, Centre = centre, Radius = radius, Complete = complete, RetryAt = Time.time + RetrySeconds };
        if (attempt == 1 || complete) Plugin.Log.LogInfo($"[OTL][SafeZone] wall around the campfire at {centre} ({radius:0.#} m){(misses > 0 ? $", {misses} direction(s) without ground yet" : "")}.");
    }

    private void Remove(Campfire fire)
    {
        if (_walls.TryGetValue(fire, out Wall wall) && wall.Go != null)
        {
            Destroy(wall.Go.GetComponent<MeshFilter>().sharedMesh);
            Destroy(wall.Go);
        }

        _walls.Remove(fire);
    }

    // ---------- The peak ----------

    private const float PeakHalfWidth = 90f;
    private const float PeakStep = 1f;
    private const int PeakMinSamples = 12; // fewer ground points than this: the peak's terrain isn't there yet
    private GameObject? _peakWall;
    private Vector3 _peakAt;
    private float _peakRetryAt;

    /// <summary>Once a second: the peak's wall while the peak is a safe zone (rebuilt every 5 s until its ground is there).</summary>
    private void RefreshPeak()
    {
        Transform? point = SafeZoneSystem.PeakIsSafeZone ? SafeZoneSystem.PeakPoint : null;
        if (point == null)
        {
            RemovePeak();
            return;
        }

        bool moved = (point.position - _peakAt).sqrMagnitude > 0.01f;
        if (_peakWall != null && !moved && _peakRetryAt <= 0f) return; // built and complete
        if (_peakWall != null && !moved && Time.time < _peakRetryAt) return;

        RemovePeak();
        _material ??= CreateMaterial();
        if (_material == null)
        {
            _failed = true;
            return;
        }

        _peakAt = point.position;
        Mesh mesh = BuildPeakMesh(_peakAt, out int samples);
        _peakWall = new GameObject("OTL_PeakWall");
        _peakWall.transform.position = _peakAt;
        _peakWall.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = _peakWall.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        _peakRetryAt = samples >= PeakMinSamples ? 0f : Time.time + RetrySeconds;
        if (samples >= PeakMinSamples) Plugin.Log.LogInfo($"[OTL][SafeZone] wall at the edge of the peak (z {_peakAt.z:0.#}, {samples} ground points).");
    }

    private void RemovePeak()
    {
        if (_peakWall == null) return;
        Destroy(_peakWall.GetComponent<MeshFilter>().sharedMesh);
        Destroy(_peakWall);
        _peakWall = null;
    }

    /// <summary>
    /// The peak's edge: the line z = the last progress point's z, sampled every PeakStep metres across (x), dropped onto
    /// the ground. Neighbouring points are joined into a wall unless the ground jumps between them (a cliff).
    /// </summary>
    private static Mesh BuildPeakMesh(Vector3 origin, out int samples)
    {
        int count = Mathf.RoundToInt(PeakHalfWidth * 2f / PeakStep) + 1;
        var points = new Vector3?[count];
        samples = 0;
        int mask = HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap);
        for (int i = 0; i < count; i++)
        {
            Vector3 top = new(origin.x - PeakHalfWidth + i * PeakStep, origin.y + 120f, origin.z + 0.25f);
            if (Physics.Raycast(top, Vector3.down, out RaycastHit hit, 300f, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - origin.y) < 80f)
            {
                points[i] = hit.point - origin;
                samples++;
            }
        }

        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        Color rim = new(Yellow.r, Yellow.g, Yellow.b, 0.95f);
        Color wallBottom = new(Yellow.r, Yellow.g, Yellow.b, 0.45f);
        Color wallTop = new(Yellow.r, Yellow.g, Yellow.b, 0f);
        for (int i = 0; i + 1 < count; i++)
        {
            if (points[i] is not { } p || points[i + 1] is not { } q || Mathf.Abs(q.y - p.y) > 4f) continue;
            int a = vertices.Count, b = a + 3;
            foreach (Vector3 g in new[] { p, q })
            {
                vertices.Add(g + Vector3.up * 0.05f);
                vertices.Add(g + Vector3.up * RimHeight);
                vertices.Add(g + Vector3.up * WallHeight);
                colors.Add(rim);
                colors.Add(wallBottom);
                colors.Add(wallTop);
            }

            triangles.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1, a + 1, a + 2, b + 1, b + 1, a + 2, b + 2 });
        }

        var mesh = new Mesh { name = "OTL_PeakWall" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void HideAll()
    {
        RemovePeak();
        _attempts.Clear();
        if (_walls.Count == 0) return;
        foreach (Campfire fire in _walls.Keys.ToList()) Remove(fire);
    }

    /// <summary>
    /// A transparent, unlit, double-sided material that uses vertex colours. Sprites/Default does all of that and
    /// depth-tests against the world (ZWrite off, ZTest LEqual); UI/Default is the fallback.
    /// </summary>
    private static Material? CreateMaterial()
    {
        Shader? shader = Shader.Find("Sprites/Default");
        bool ui = false;
        if (shader == null)
        {
            shader = Shader.Find("UI/Default");
            ui = true;
        }

        if (shader == null)
        {
            Plugin.Log.LogWarning("[OTL][SafeZone] no usable shader found; the safe zone wall is off.");
            return null;
        }

        var material = new Material(shader) { name = "OTL_SafeZoneWall", renderQueue = 3100 };
        if (ui) material.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual); // else it draws through terrain
        Plugin.Log.LogInfo($"[OTL][SafeZone] wall shader: {shader.name}.");
        return material;
    }

    /// <summary>
    /// A ring of quads: a bright rim at the ground (the outline), and above it the wall, fading out towards the top.
    /// </summary>
    private static Mesh BuildMesh(Vector3 centre, float radius, out int misses)
    {
        misses = 0;
        var vertices = new Vector3[(Segments + 1) * 3];
        var colors = new Color[vertices.Length];
        var triangles = new int[Segments * 12];
        Color rim = new(Yellow.r, Yellow.g, Yellow.b, 0.95f);
        Color wallBottom = new(Yellow.r, Yellow.g, Yellow.b, 0.45f);
        Color wallTop = new(Yellow.r, Yellow.g, Yellow.b, 0f);

        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 dir = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (!GroundOnSphere(centre, dir, radius, out Vector3 point)) misses++;
            Vector3 ground = point - centre; // mesh is built around the campfire
            int v = i * 3;
            vertices[v] = ground + Vector3.up * 0.05f;
            vertices[v + 1] = ground + Vector3.up * RimHeight;
            vertices[v + 2] = ground + Vector3.up * WallHeight;
            colors[v] = rim;
            colors[v + 1] = wallBottom;
            colors[v + 2] = wallTop;
        }

        for (int i = 0; i < Segments; i++)
        {
            int a = i * 3, b = (i + 1) * 3, t = i * 12;
            // rim: a -> a+1 / b -> b+1
            triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = b;
            triangles[t + 3] = b; triangles[t + 4] = a + 1; triangles[t + 5] = b + 1;
            // wall: a+1 -> a+2 / b+1 -> b+2
            triangles[t + 6] = a + 1; triangles[t + 7] = a + 2; triangles[t + 8] = b + 1;
            triangles[t + 9] = b + 1; triangles[t + 10] = a + 2; triangles[t + 11] = b + 2;
        }

        var mesh = new Mesh { name = "OTL_SafeZoneWall", vertices = vertices, colors = colors, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// The ground point in direction <paramref name="dir"/> that is <paramref name="radius"/> from the campfire
    /// (3D distance, like the safe zone itself). Starts at the full radius and shrinks it by the height difference.
    /// False if no ground was found below (the point is then at the campfire's height).
    /// </summary>
    private static bool GroundOnSphere(Vector3 centre, Vector3 dir, float radius, out Vector3 point)
    {
        float r = radius;
        point = centre + dir * r;
        for (int i = 0; i < 4; i++)
        {
            Vector3 top = centre + dir * r + Vector3.up * radius;
            if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, radius * 2f, HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainMap), QueryTriggerInteraction.Ignore))
            {
                point = centre + dir * r; // no ground (a drop, or terrain not there yet): the wall at the campfire's height
                return false;
            }

            point = hit.point;
            float dy = point.y - centre.y;
            float next = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
            if (Mathf.Abs(next - r) < 0.1f) break;
            r = next;
        }

        return true;
    }
}
