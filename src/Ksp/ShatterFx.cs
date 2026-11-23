using System.Collections.Generic;
using FarFutureWeaponry.Core;
using UnityEngine;

namespace FarFutureWeaponry
{
    /// <summary>
    /// The tachyon bomb's shatter and reassembly, all visual: a copy of the bomb's model (taken as it detonates) is cut
    /// into fragments that fly apart, hang frozen in the bomb's own field, fly back together as the field collapses, and
    /// become the whole bomb again, which then drops to the ground as a harmless dud (a prop: not a part, and it can't go
    /// off) and is cleared away after a while.
    /// </summary>
    internal sealed class ShatterFx
    {
        const int FragmentCount = 10;
        const float DudLifetime = 120;

        sealed class Fragment
        {
            public Transform T;
            public Vector3 Home;          // local position in the model's frame (its pivot is the fragment's centre)
            public Vector3 Fly;           // local offset when fully flung
            public Vector3 SpinAxis;
            public float SpinDegrees;
        }

        readonly GameObject whole, pieces;
        readonly List<Fragment> fragments = new List<Fragment>();
        readonly List<Mesh> meshes = new List<Mesh>();
        Vector3d anchor;                  // the model's position in the main body's frame
        readonly Quaternion rotation;
        float halfHeight;
        bool reassembled, landed;
        Vector3 velocity;
        float reassembledAt, age;

        public bool Done { get; private set; }

        public static ShatterFx Capture(Part part)
        {
            Transform model = part.partTransform != null ? part.partTransform.Find("model") : null;
            if (model == null) model = part.transform;
            try { return new ShatterFx(model, (int)(part.flightID & 0x7fffffff)); }
            catch (System.Exception e)
            {
                Log.Warn("Couldn't copy the bomb's model for the shatter effect: " + e.Message);
                return null;
            }
        }

        ShatterFx(Transform model, int seed)
        {
            rotation = model.rotation;
            anchor = Visuals.ToFrame(model.position);

            // The whole bomb: a stripped copy of the model (renderers only).
            whole = Object.Instantiate(model.gameObject, model.position, model.rotation);
            whole.name = "FFW tachyon bomb (copy)";
            whole.transform.localScale = model.lossyScale;
            Strip(whole);
            var bounds = new Bounds(model.position, Vector3.zero);
            foreach (Renderer r in whole.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            halfHeight = Mathf.Max(0.2f, bounds.extents.magnitude * 0.5f);

            pieces = new GameObject("FFW tachyon bomb fragments");
            pieces.transform.SetPositionAndRotation(model.position, model.rotation);
            BuildFragments(seed, bounds);
            SetWhole(fragments.Count == 0);   // no readable meshes: the bomb just vanishes and comes back
            pieces.SetActive(fragments.Count > 0);
        }

        /// <summary>Keep only meshes: renderers of other kinds first (a particle renderer needs its particle system).</summary>
        static void Strip(GameObject go)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                if (!(r is MeshRenderer)) Object.DestroyImmediate(r);
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer) continue;
                Object.DestroyImmediate(c);
            }
        }

        void SetWhole(bool on)
        {
            foreach (Renderer r in whole.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        void BuildFragments(int seed, Bounds worldBounds)
        {
            var filters = whole.GetComponentsInChildren<MeshFilter>();
            Matrix4x4 toRoot = pieces.transform.worldToLocalMatrix;
            // All triangles, their renderer and submesh, and centroids in the model's frame.
            var tris = new List<(int f, int sub, int a, int b, int c)>();
            var centroids = new List<float>();
            var verts = new List<Vector3[]>();
            var norms = new List<Vector3[]>();
            var uvs = new List<Vector2[]>();
            for (int f = 0; f < filters.Length; f++)
            {
                MeshFilter mf = filters[f];
                var mr = mf.GetComponent<MeshRenderer>();
                Mesh m = mf.sharedMesh;
                if (mr == null || !mr.enabled || m == null || !m.isReadable) { verts.Add(null); norms.Add(null); uvs.Add(null); continue; }
                Matrix4x4 local = toRoot * mf.transform.localToWorldMatrix;
                Vector3[] v = m.vertices, n = m.normals;
                var vl = new Vector3[v.Length];
                var nl = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    vl[i] = local.MultiplyPoint3x4(v[i]);
                    nl[i] = n.Length == v.Length ? local.MultiplyVector(n[i]).normalized : Vector3.up;
                }
                verts.Add(vl); norms.Add(nl); uvs.Add(m.uv.Length == v.Length ? m.uv : new Vector2[v.Length]);
                for (int sub = 0; sub < m.subMeshCount; sub++)
                {
                    int[] t = m.GetTriangles(sub);
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        tris.Add((f, sub, t[i], t[i + 1], t[i + 2]));
                        Vector3 c = (vl[t[i]] + vl[t[i + 1]] + vl[t[i + 2]]) / 3;
                        centroids.Add(c.x); centroids.Add(c.y); centroids.Add(c.z);
                    }
                }
            }
            if (tris.Count == 0) return;
            int[] group = Shatter.Assign(centroids.ToArray(), FragmentCount, seed);
            var rng = new System.Random(seed);
            float size = Mathf.Max(0.5f, worldBounds.extents.magnitude);
            Vector3 middle = Vector3.zero;
            for (int i = 0; i < tris.Count; i++) middle += new Vector3(centroids[i * 3], centroids[i * 3 + 1], centroids[i * 3 + 2]);
            middle /= tris.Count;

            for (int g = 0; g < FragmentCount; g++)
            {
                // This fragment's triangles, by renderer and submesh.
                var mine = new Dictionary<(int f, int sub), List<int>>();
                Vector3 centre = Vector3.zero; int count = 0;
                for (int i = 0; i < tris.Count; i++)
                {
                    if (group[i] != g) continue;
                    var key = (tris[i].f, tris[i].sub);
                    if (!mine.TryGetValue(key, out var list)) mine[key] = list = new List<int>();
                    list.Add(i);
                    centre += new Vector3(centroids[i * 3], centroids[i * 3 + 1], centroids[i * 3 + 2]); count++;
                }
                if (count == 0) continue;
                centre /= count;
                var go = new GameObject("fragment " + g);
                go.transform.SetParent(pieces.transform, false);
                go.transform.localPosition = centre;
                foreach (var kv in mine)
                {
                    int f = kv.Key.f, sub = kv.Key.sub;
                    var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>();
                    foreach (int i in kv.Value)
                    {
                        foreach (int vi in new[] { tris[i].a, tris[i].b, tris[i].c })
                        {
                            idx.Add(v.Count);
                            v.Add(verts[f][vi] - centre); n.Add(norms[f][vi]); uv.Add(uvs[f][vi]);
                        }
                    }
                    var mesh = new Mesh();
                    if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(idx, 0);
                    mesh.RecalculateBounds();
                    meshes.Add(mesh);
                    var child = new GameObject("piece");
                    child.transform.SetParent(go.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var src = filters[f].GetComponent<MeshRenderer>();
                    var mr = child.AddComponent<MeshRenderer>();
                    Material[] mats = src.sharedMaterials;
                    mr.sharedMaterial = mats.Length > sub ? mats[sub] : mats.Length > 0 ? mats[0] : null;
                }
                Vector3 outward = centre - middle;
                if (outward.sqrMagnitude < 1e-4f) outward = Random(rng);
                Vector3 dir = (outward.normalized + Random(rng) * 0.5f).normalized;
                fragments.Add(new Fragment
                {
                    T = go.transform, Home = centre,
                    Fly = dir * size * (1.5f + 1.5f * (float)rng.NextDouble()),
                    SpinAxis = Random(rng).normalized,
                    SpinDegrees = 90 + 240 * (float)rng.NextDouble()
                });
            }
        }

        static Vector3 Random(System.Random rng) =>
            new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);

        /// <summary>
        /// Per frame. spread: how far out the pieces are (0 together, 1 fully flung; the field's clock decides, so they
        /// slow to a stop, hang, and fly back as time returns); together: they are back in one piece, for good.
        /// </summary>
        public void Tick(float spread, bool together, float dt)
        {
            if (Done || whole == null) { Done = true; return; }
            float t = (age += dt);
            if (!reassembled)
            {
                Vector3 at = Visuals.FromFrame(anchor);
                pieces.transform.SetPositionAndRotation(at, rotation);
                whole.transform.SetPositionAndRotation(at, rotation);
                float s = spread;
                foreach (Fragment f in fragments)
                {
                    f.T.localPosition = f.Home + f.Fly * s;
                    f.T.localRotation = Quaternion.AngleAxis(f.SpinDegrees * s, f.SpinAxis);
                }
                if (together)
                {
                    reassembled = true; reassembledAt = t;
                    pieces.SetActive(false);
                    SetWhole(true);
                }
                return;
            }
            // The dud: falls under gravity until it hits the ground or the sea, then lies there.
            if (!landed && dt > 0)
            {
                Vector3 pos = Visuals.FromFrame(anchor);
                Vector3 g = FlightGlobals.getGeeForceAtPosition(pos);
                velocity += g * dt;
                Vector3 step = velocity * dt;
                Vector3 up = -g.normalized;
                int mask = LayerMask.GetMask("Local Scenery", "TerrainColliders");
                if (Physics.Raycast(pos + up * halfHeight, step.normalized, out RaycastHit hit, step.magnitude + halfHeight, mask, QueryTriggerInteraction.Ignore))
                {
                    pos = hit.point + up * halfHeight;
                    landed = true;
                }
                else
                {
                    pos += step;
                    CelestialBody body = FlightGlobals.currentMainBody;
                    if (body != null && body.ocean && body.GetAltitude(pos) < 0) landed = true;
                }
                anchor = Visuals.ToFrame(pos);
            }
            whole.transform.position = Visuals.FromFrame(anchor);
            if (t - reassembledAt > DudLifetime) Clear();
        }

        public void Clear()
        {
            Done = true;
            if (whole != null) Object.Destroy(whole);
            if (pieces != null) Object.Destroy(pieces);
            foreach (Mesh m in meshes) if (m != null) Object.Destroy(m);
            meshes.Clear();
        }
    }
}
