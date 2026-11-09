using System.Collections.Generic;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Builds the isometric floating island for one season from its <see cref="SeasonTheme"/>.
    /// The ground, the ragged grass edge and the two dirt cliff faces are real meshes that are
    /// projected into isometric space from the flat tile textures, so the art pack's square
    /// tiles can be used as they are. Props are sprites placed at their projected position,
    /// with a blocking collider (on the flat plane) where the theme asks for one.
    /// </summary>
    public static class IsoMapBuilder
    {
        public const string ObstacleLayerName = "Obstacle";

        class MeshBatch
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color32> C = new List<Color32>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                UV.Add(ua); UV.Add(ub); UV.Add(uc); UV.Add(ud);
                for (int k = 0; k < 4; k++) C.Add(new Color32(255, 255, 255, 255));
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }

            public void Tri(Vector2 a, Vector2 b, Vector2 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                UV.Add(ua); UV.Add(ub); UV.Add(uc);
                for (int k = 0; k < 3; k++) C.Add(new Color32(255, 255, 255, 255));
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V);
                m.SetUVs(0, UV);
                m.SetColors(C);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <param name="withColliders">False for the editor preview: the blocking circles are then only drawn as guides.</param>
        public static GameObject Build(SeasonTheme theme, Transform parent, bool withColliders = true)
        {
            var root = new GameObject("Map_" + theme.displayName);
            root.transform.SetParent(parent, false);

            float n = GameConfig.ArenaSize;
            int cells = Mathf.RoundToInt(n);

            // ---------- top surface ----------
            var ground = new MeshBatch();
            var side = new MeshBatch();
            for (int i = 0; i < cells; i++)
            {
                for (int j = 0; j < cells; j++)
                {
                    Vector2 p00 = Iso.ToScreen(new Vector2(i, j));
                    Vector2 p10 = Iso.ToScreen(new Vector2(i + 1, j));
                    Vector2 p11 = Iso.ToScreen(new Vector2(i + 1, j + 1));
                    Vector2 p01 = Iso.ToScreen(new Vector2(i, j + 1));

                    bool outerY = j == cells - 1;   // back-left edge of the diamond
                    bool outerX = i == cells - 1;   // back-right edge of the diamond

                    if (outerY && theme.sideMaterial != null)
                    {
                        // texture "right" edge (the ragged fringe) must face plane +y
                        side.Quad(p00, p10, p11, p01,
                            new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
                    }
                    else if (outerX && theme.sideMaterial != null)
                    {
                        // texture "right" edge must face plane +x
                        side.Quad(p00, p10, p11, p01,
                            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                    }
                    else
                    {
                        // alternate the orientation a little so the grass tufts do not form a visible grid
                        bool flip = ((i * 7 + j * 13) & 1) == 1;
                        if (flip)
                            ground.Quad(p00, p10, p11, p01,
                                new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0));
                        else
                            ground.Quad(p00, p10, p11, p01,
                                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                    }
                }
            }
            AddMesh(root.transform, "Ground", ground, theme.groundMaterial, Iso.GroundSortOrder);
            AddMesh(root.transform, "GroundFringe", side, theme.sideMaterial, Iso.GroundSortOrder + 1);

            // ---------- patch (sand / ice) ----------
            if (theme.hasPatch && theme.patchMaterial != null)
            {
                var patch = new MeshBatch();
                const int segs = 28;
                Vector2 centerScreen = Iso.ToScreen(theme.patchCenter);
                Vector2 centerUv = new Vector2(0.5f, 0.5f);
                for (int s = 0; s < segs; s++)
                {
                    float a0 = (s / (float)segs) * Mathf.PI * 2f;
                    float a1 = ((s + 1) / (float)segs) * Mathf.PI * 2f;
                    Vector2 q0 = PatchPoint(theme, a0);
                    Vector2 q1 = PatchPoint(theme, a1);
                    patch.Tri(centerScreen, Iso.ToScreen(q0), Iso.ToScreen(q1),
                        centerUv,
                        centerUv + (q0 - theme.patchCenter) / (theme.patchRadius * 2.4f),
                        centerUv + (q1 - theme.patchCenter) / (theme.patchRadius * 2.4f));
                }
                AddMesh(root.transform, "Patch", patch, theme.patchMaterial, Iso.GroundSortOrder + 2);
            }

            // ---------- cliff faces on the two front sides ----------
            if (theme.cliffMaterial != null)
            {
                var cliff = new MeshBatch();
                Vector2 south = Iso.ToScreen(new Vector2(0, 0));
                Vector2 east = Iso.ToScreen(new Vector2(n, 0));
                Vector2 west = Iso.ToScreen(new Vector2(0, n));
                AddCliff(cliff, west, south, theme.cliffHeight);   // front-left face
                AddCliff(cliff, south, east, theme.cliffHeight);   // front-right face
                AddMesh(root.transform, "Cliff", cliff, theme.cliffMaterial, Iso.GroundSortOrder + 3);
            }

            BuildProps(theme, root.transform, withColliders);
            return root;
        }

        /// <summary>
        /// Builds the trees, rocks and bushes of a theme as a child called "Props" of <paramref name="mapRoot"/>.
        /// Separate from <see cref="Build"/> so the map editor can rebuild only the props while you drag them.
        /// </summary>
        public static Transform BuildProps(SeasonTheme theme, Transform mapRoot, bool withColliders = true)
        {
            // ---------- props ----------
            var propsRoot = new GameObject("Props").transform;
            propsRoot.SetParent(mapRoot, false);
            int obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
            if (theme.props != null && theme.placements != null)
            {
                for (int i = 0; i < theme.placements.Length; i++)
                {
                    PropPlacement pl = theme.placements[i];
                    if (pl.prop < 0 || pl.prop >= theme.props.Length) continue;
                    PropDef def = theme.props[pl.prop];

                    var go = new GameObject($"{def.name}_{i}");
                    go.transform.SetParent(propsRoot, false);
                    go.transform.position = Iso.ToScreen3(pl.position);
                    go.transform.localScale = new Vector3(pl.flipX ? -def.scale : def.scale, def.scale, 1f);

                    if (def.sprite != null)
                    {
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = def.sprite;
                        sr.color = def.tint;
                        sr.sortingOrder = Iso.SortOrder(pl.position);
                    }

                    if (withColliders && def.colliderRadius > 0f)
                    {
                        // The blocking collider lives on the flat gameplay plane, not at the drawn position.
                        var col = new GameObject("Collider");
                        col.transform.SetParent(propsRoot, false);
                        col.transform.position = new Vector3(pl.position.x, pl.position.y, 0f);
                        if (obstacleLayer >= 0) col.layer = obstacleLayer;
                        var cc = col.AddComponent<CircleCollider2D>();
                        cc.radius = def.colliderRadius;
                    }
                }
            }

            return propsRoot;
        }

        static Vector2 PatchPoint(SeasonTheme theme, float angle)
        {
            // slightly irregular outline so the patch looks organic, like the concept art
            float wobble = 1f + 0.13f * Mathf.Sin(angle * 3f + 1f) + 0.07f * Mathf.Sin(angle * 5f + 2f);
            return theme.patchCenter + new Vector2(Mathf.Cos(angle) * 1.15f, Mathf.Sin(angle)) * (theme.patchRadius * wobble);
        }

        static void AddCliff(MeshBatch batch, Vector2 left, Vector2 right, float height)
        {
            float length = Vector2.Distance(left, right);
            int segments = Mathf.Max(1, Mathf.RoundToInt(length / height));
            Vector2 down = new Vector2(0f, -height);
            for (int s = 0; s < segments; s++)
            {
                Vector2 a = Vector2.Lerp(left, right, s / (float)segments);
                Vector2 b = Vector2.Lerp(left, right, (s + 1) / (float)segments);
                batch.Quad(a, b, b + down, a + down,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0));
            }
        }

        static void AddMesh(Transform parent, string name, MeshBatch batch, Material material, int sortingOrder)
        {
            if (batch.V.Count == 0 || material == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = batch.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
