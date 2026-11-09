using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Owns the three season maps. Runs identically on host and clients: every peer builds the same
    /// maps locally from the theme assets (the maps are static scenery, so nothing about them needs
    /// to be sent over the network), and simply shows the one matching the networked season.
    /// The camera is fixed: it frames the whole island and never follows a player.
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        public static MapManager Instance { get; private set; }

        [Tooltip("Spring, Fall, Winter - in that order.")]
        public SeasonTheme[] themes;

        GameObject[] _roots;
        int _current = -1;
        Camera _cam;

        public int CurrentSeason => _current;

        void Awake()
        {
            Instance = this;
            GameConfig.Touch();
            ApplyLayerRules();

            _cam = Camera.main;
            BuildWalls();

            _roots = new GameObject[themes.Length];
            for (int i = 0; i < themes.Length; i++)
            {
                _roots[i] = IsoMapBuilder.Build(themes[i], transform);
                _roots[i].SetActive(false);
            }
            ShowSeason(0);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            FrameCamera();
        }

        /// <summary>
        /// Players, enemies and the rival never physically collide with each other (all fighting uses
        /// distance checks on the server); they only collide with the obstacle layer.
        /// </summary>
        static void ApplyLayerRules()
        {
            int player = LayerMask.NameToLayer("Player");
            int enemy = LayerMask.NameToLayer("Enemy");
            if (player < 0 || enemy < 0) return;
            Physics2D.IgnoreLayerCollision(player, player, true);
            Physics2D.IgnoreLayerCollision(player, enemy, true);
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
        }

        void BuildWalls()
        {
            float n = GameConfig.ArenaSize;
            var walls = new GameObject("ArenaWalls");
            walls.transform.SetParent(transform, false);
            int layer = LayerMask.NameToLayer(IsoMapBuilder.ObstacleLayerName);
            if (layer >= 0) walls.layer = layer;

            // four thick boxes just outside the arena square, on the gameplay plane
            AddWall(walls, new Vector2(n * 0.5f, -1f), new Vector2(n + 4f, 2f), layer);
            AddWall(walls, new Vector2(n * 0.5f, n + 1f), new Vector2(n + 4f, 2f), layer);
            AddWall(walls, new Vector2(-1f, n * 0.5f), new Vector2(2f, n + 4f), layer);
            AddWall(walls, new Vector2(n + 1f, n * 0.5f), new Vector2(2f, n + 4f), layer);
        }

        static void AddWall(GameObject parent, Vector2 center, Vector2 size, int layer)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = center;
            if (layer >= 0) go.layer = layer;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        public void ShowSeason(int season)
        {
            season = Mathf.Clamp(season, 0, themes.Length - 1);
            for (int i = 0; i < _roots.Length; i++) _roots[i].SetActive(i == season);
            _current = season;
            if (_cam != null) _cam.backgroundColor = themes[season].skyColor;
        }

        public SeasonTheme Theme(int season)
        {
            return themes[Mathf.Clamp(season, 0, themes.Length - 1)];
        }

        public Vector2 GetPlayerSpawn(int slot, int season)
        {
            Vector2[] spawns = Theme(season).playerSpawns;
            return spawns[Mathf.Clamp(slot, 0, spawns.Length - 1)];
        }

        /// <summary>Picks an enemy spawn point that is not too close to any living player.</summary>
        public Vector2 PickEnemySpawn(int season, System.Collections.Generic.IList<Vector2> playerPositions)
        {
            Vector2[] spawns = Theme(season).enemySpawns;
            if (spawns == null || spawns.Length == 0) return new Vector2(GameConfig.ArenaSize * 0.5f, GameConfig.ArenaSize * 0.5f);

            // try a few random points first so spawns are not always the same, then fall back to the farthest one
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 candidate = spawns[Random.Range(0, spawns.Length)];
                bool ok = true;
                for (int p = 0; p < playerPositions.Count; p++)
                    if (Vector2.Distance(candidate, playerPositions[p]) < GameConfig.MinSpawnDistanceFromPlayers) { ok = false; break; }
                if (ok) return candidate;
            }

            Vector2 best = spawns[0];
            float bestDist = -1f;
            foreach (Vector2 s in spawns)
            {
                float nearest = float.MaxValue;
                for (int p = 0; p < playerPositions.Count; p++) nearest = Mathf.Min(nearest, Vector2.Distance(s, playerPositions[p]));
                if (nearest > bestDist) { bestDist = nearest; best = s; }
            }
            return best;
        }

        /// <summary>The fixed camera: frames the whole island (plus the cliffs below it) for any aspect ratio.</summary>
        void FrameCamera()
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            float n = GameConfig.ArenaSize;
            float halfWidth = n * Iso.HalfW + 1.0f;             // diamond half width + margin
            float top = n * 2f * Iso.HalfH + 2.1f;              // north corner + tall trees
            float bottom = -2.3f;                               // south corner + cliff face
            float size = Mathf.Max((top - bottom) * 0.5f, halfWidth / Mathf.Max(0.5f, _cam.aspect));
            _cam.orthographic = true;
            _cam.orthographicSize = size;
            _cam.transform.position = new Vector3(0f, (top + bottom) * 0.5f, -10f);
        }
    }
}
