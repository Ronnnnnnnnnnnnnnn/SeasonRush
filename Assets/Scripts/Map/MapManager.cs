using UnityEngine;

namespace SeasonRush
{
   
    public class MapManager : MonoBehaviour
    {
        public static MapManager Instance { get; private set; }

        [Tooltip("Spring, Fall, Winter - in that order.")]
        public SeasonTheme[] themes;

        [Tooltip("The three maps as objects in the scene (Environment > Maps), in season order. Made by Season Rush > Update Maps In Scene. Empty = built when the game starts.")]
        public GameObject[] maps;
        [Tooltip("The invisible walls around the island (Environment > ArenaWalls). Empty = built when the game starts.")]
        public GameObject arenaWalls;

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
            if (arenaWalls == null) CreateWalls(transform);

            _roots = new GameObject[themes.Length];
            for (int i = 0; i < themes.Length; i++)
            {
                GameObject baked = maps != null && i < maps.Length ? maps[i] : null;
                var marker = baked != null ? baked.GetComponent<BakedMap>() : null;
                if (marker != null && marker.themeHash == themes[i].ContentHash())
                {
                    _roots[i] = baked;
                }
                else
                {
                    if (baked != null)
                    {
                        Debug.LogWarning($"[Map] The baked {themes[i].displayName} map in the scene is out of date: building it from the theme instead. Run Season Rush > Update Maps In Scene.");
                        baked.SetActive(false);
                    }
                    _roots[i] = IsoMapBuilder.Build(themes[i], transform);
                }
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

        static void ApplyLayerRules()
        {
            int player = LayerMask.NameToLayer("Player");
            int enemy = LayerMask.NameToLayer("Enemy");
            if (player < 0 || enemy < 0) return;
            Physics2D.IgnoreLayerCollision(player, player, true);
            Physics2D.IgnoreLayerCollision(player, enemy, true);
            Physics2D.IgnoreLayerCollision(enemy, enemy, true);
        }

        public static GameObject CreateWalls(Transform parent)
        {
            float n = GameConfig.ArenaSize;
            var walls = new GameObject("ArenaWalls");
            walls.transform.SetParent(parent, false);
            int layer = LayerMask.NameToLayer(IsoMapBuilder.ObstacleLayerName);
            if (layer >= 0) walls.layer = layer;

            AddWall(walls, new Vector2(n * 0.5f, -1f), new Vector2(n + 4f, 2f), layer);
            AddWall(walls, new Vector2(n * 0.5f, n + 1f), new Vector2(n + 4f, 2f), layer);
            AddWall(walls, new Vector2(-1f, n * 0.5f), new Vector2(2f, n + 4f), layer);
            AddWall(walls, new Vector2(n + 1f, n * 0.5f), new Vector2(2f, n + 4f), layer);
            return walls;
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

        public Vector2 PickEnemySpawn(int season, System.Collections.Generic.IList<Vector2> playerPositions)
        {
            Vector2[] spawns = Theme(season).enemySpawns;
            if (spawns == null || spawns.Length == 0) return new Vector2(GameConfig.ArenaSize * 0.5f, GameConfig.ArenaSize * 0.5f);

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

        void FrameCamera()
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            float n = GameConfig.ArenaSize;
            float halfWidth = n * Iso.HalfW + 1.0f;
            float top = n * 2f * Iso.HalfH + 2.1f;
            float bottom = -2.3f;
            float size = Mathf.Max((top - bottom) * 0.5f, halfWidth / Mathf.Max(0.5f, _cam.aspect));
            _cam.orthographic = true;
            _cam.orthographicSize = size;
            _cam.transform.position = new Vector3(0f, (top + bottom) * 0.5f, -10f);
        }
    }
}
