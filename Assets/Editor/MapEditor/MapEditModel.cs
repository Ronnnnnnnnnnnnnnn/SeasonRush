using System.Collections.Generic;
using UnityEngine;

namespace SeasonRush.EditorTools
{
    /// <summary>
    /// The rules of map editing, without any window or mouse code (so they can be tested in batch mode).
    ///
    /// Positions are in "plane" units (0..12), see <see cref="Iso"/>. The two players should have the same cover, so a
    /// prop at (x, y) normally has a <b>mirror twin</b> of the same type at (y, x) drawn flipped. Moving, adding and
    /// deleting can keep the pair together.
    /// </summary>
    public static class MapEditModel
    {
        /// <summary>How close the twin must be to the exact mirror position to count as the twin.</summary>
        public const float TwinTolerance = 0.06f;
        /// <summary>Blocking props closer than this to a player start are reported by <see cref="Check"/>.</summary>
        public const float MinSpawnClearance = 1.5f;

        public static float Limit => GameConfig.ArenaSize;

        public static Vector2 Swap(Vector2 v) => new Vector2(v.y, v.x);

        /// <summary>True on the diagonal x = y: such a prop is its own mirror image and has no twin.</summary>
        public static bool OnMirrorAxis(Vector2 p) => Mathf.Abs(p.x - p.y) <= 0.01f;

        /// <summary>Snaps to the grid (0 = no snapping) and keeps the point inside the island.</summary>
        public static Vector2 Constrain(Vector2 p, float snap)
        {
            if (snap > 0.0001f)
            {
                p.x = Mathf.Round(p.x / snap) * snap;
                p.y = Mathf.Round(p.y / snap) * snap;
            }
            p.x = Mathf.Round(Mathf.Clamp(p.x, 0f, Limit) * 1000f) / 1000f;
            p.y = Mathf.Round(Mathf.Clamp(p.y, 0f, Limit) * 1000f) / 1000f;
            return p;
        }

        public static int PlacementCount(SeasonTheme t) => t != null && t.placements != null ? t.placements.Length : 0;

        public static bool IsValid(SeasonTheme t, int index) => index >= 0 && index < PlacementCount(t);

        public static string PropName(SeasonTheme t, int propIndex)
        {
            if (t != null && t.props != null && propIndex >= 0 && propIndex < t.props.Length) return t.props[propIndex].name;
            return "?";
        }

        // ------------------------------------------------------------------ props

        /// <summary>Index of the mirror twin of placement <paramref name="i"/>, or -1 if it has none.</summary>
        public static int FindTwin(SeasonTheme t, int i)
        {
            if (!IsValid(t, i)) return -1;
            PropPlacement a = t.placements[i];
            if (OnMirrorAxis(a.position)) return -1;
            Vector2 want = Swap(a.position);
            int best = -1;
            float bestDistance = TwinTolerance;
            for (int j = 0; j < t.placements.Length; j++)
            {
                if (j == i || t.placements[j].prop != a.prop) continue;
                float d = Vector2.Distance(t.placements[j].position, want);
                if (d <= bestDistance) { bestDistance = d; best = j; }
            }
            return best;
        }

        /// <summary>Moves placement <paramref name="i"/> and, if <paramref name="twin"/> is a valid index, its twin to the mirror position.</summary>
        public static void MovePlacement(SeasonTheme t, int i, int twin, Vector2 position)
        {
            if (!IsValid(t, i)) return;
            PropPlacement a = t.placements[i];
            a.position = position;
            t.placements[i] = a;
            if (IsValid(t, twin) && twin != i)
            {
                PropPlacement b = t.placements[twin];
                b.position = Swap(position);
                t.placements[twin] = b;
            }
        }

        /// <summary>Adds a prop. With <paramref name="mirror"/> and a position off the diagonal the twin is added right after it.</summary>
        /// <returns>The index of the new prop; the twin (if any) is returned in <paramref name="twinIndex"/>, otherwise -1.</returns>
        public static int AddPlacement(SeasonTheme t, int prop, Vector2 position, bool mirror, out int twinIndex)
        {
            var list = new List<PropPlacement>(t.placements ?? new PropPlacement[0]);
            list.Add(new PropPlacement { prop = prop, position = position, flipX = false });
            int index = list.Count - 1;
            twinIndex = -1;
            if (mirror && !OnMirrorAxis(position))
            {
                list.Add(new PropPlacement { prop = prop, position = Swap(position), flipX = true });
                twinIndex = list.Count - 1;
            }
            t.placements = list.ToArray();
            return index;
        }

        /// <summary>Removes the given placements (indices may be in any order; invalid ones are ignored).</summary>
        public static void RemovePlacements(SeasonTheme t, params int[] indices)
        {
            if (t.placements == null) return;
            var remove = new HashSet<int>();
            foreach (int i in indices) if (IsValid(t, i)) remove.Add(i);
            var keep = new List<PropPlacement>();
            for (int i = 0; i < t.placements.Length; i++) if (!remove.Contains(i)) keep.Add(t.placements[i]);
            t.placements = keep.ToArray();
        }

        /// <summary>Changes the type of a prop (and of its twin).</summary>
        public static void SetType(SeasonTheme t, int i, int twin, int prop)
        {
            if (!IsValid(t, i)) return;
            PropPlacement a = t.placements[i];
            a.prop = prop;
            t.placements[i] = a;
            if (IsValid(t, twin) && twin != i)
            {
                PropPlacement b = t.placements[twin];
                b.prop = prop;
                t.placements[twin] = b;
            }
        }

        public static void SetFlip(SeasonTheme t, int i, bool flip)
        {
            if (!IsValid(t, i)) return;
            PropPlacement a = t.placements[i];
            a.flipX = flip;
            t.placements[i] = a;
        }

        // ------------------------------------------------------------------ spawn points

        public static int FindSpawnTwin(Vector2[] list, int i)
        {
            if (list == null || i < 0 || i >= list.Length || OnMirrorAxis(list[i])) return -1;
            Vector2 want = Swap(list[i]);
            int best = -1;
            float bestDistance = TwinTolerance;
            for (int j = 0; j < list.Length; j++)
            {
                if (j == i) continue;
                float d = Vector2.Distance(list[j], want);
                if (d <= bestDistance) { bestDistance = d; best = j; }
            }
            return best;
        }

        public static void MoveSpawn(Vector2[] list, int i, int twin, Vector2 position)
        {
            if (list == null || i < 0 || i >= list.Length) return;
            list[i] = position;
            if (twin >= 0 && twin < list.Length && twin != i) list[twin] = Swap(position);
        }

        // ------------------------------------------------------------------ checks

        /// <summary>Plain-language problems with the layout (empty list = nothing wrong).</summary>
        public static List<string> Check(SeasonTheme t)
        {
            var warnings = new List<string>();
            if (t == null) return warnings;
            int n = PlacementCount(t);
            for (int i = 0; i < n; i++)
            {
                PropPlacement pl = t.placements[i];
                if (t.props == null || pl.prop < 0 || pl.prop >= t.props.Length)
                {
                    warnings.Add($"#{i}: prop type {pl.prop} does not exist (it is not drawn).");
                    continue;
                }
                PropDef def = t.props[pl.prop];
                string label = $"{def.name} #{i} at ({pl.position.x:0.0}, {pl.position.y:0.0})";
                if (pl.position.x < 0f || pl.position.y < 0f || pl.position.x > Limit || pl.position.y > Limit)
                    warnings.Add($"{label} is outside the island.");
                if (def.colliderRadius <= 0f) continue;                                    // decoration only: cosmetic

                if (t.playerSpawns != null)
                    for (int s = 0; s < t.playerSpawns.Length; s++)
                        if (Vector2.Distance(pl.position, t.playerSpawns[s]) < MinSpawnClearance)
                            warnings.Add($"{label} is too close to the {(s == 0 ? "blue" : "red")} player's start.");
                if (!OnMirrorAxis(pl.position) && FindTwin(t, i) < 0)
                    warnings.Add($"{label} blocks movement but has no mirror twin, so one player gets more cover.");
            }
            if (t.playerSpawns != null && t.playerSpawns.Length >= 2 && Vector2.Distance(Swap(t.playerSpawns[0]), t.playerSpawns[1]) > TwinTolerance)
                warnings.Add("The two player start points are not mirror images of each other.");
            return warnings;
        }
    }
}
