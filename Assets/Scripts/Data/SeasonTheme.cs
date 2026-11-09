using System;
using UnityEngine;

namespace SeasonRush
{
    /// <summary>A decoration or obstacle that can be placed on a season map.</summary>
    [Serializable]
    public class PropDef
    {
        public string name;
        public Sprite sprite;
        [Tooltip("Multiplier applied on top of the sprite's imported size.")]
        public float scale = 1f;
        [Tooltip("Radius (plane units) of the blocking collider at the prop's base. 0 = decoration only.")]
        public float colliderRadius = 0f;
        public Color tint = Color.white;
    }

    /// <summary>Where a prop is placed. Position is in arena plane units (0..ArenaSize).</summary>
    [Serializable]
    public struct PropPlacement
    {
        public int prop;
        public Vector2 position;
        public bool flipX;
    }

    /// <summary>One enemy type for a season (a season has a weak, a medium and a strong one).</summary>
    [Serializable]
    public class EnemyDef
    {
        public string displayName;
        public EnemyTier tier;
        public Sprite sprite;
        [Tooltip("Optional animation: these pictures are shown one after the other (for example a cloud floating up and down). Needs 2 or more. Empty = just the Sprite above.")]
        public Sprite[] frames;
        [Tooltip("Pictures per second for Frames.")]
        public float frameRate = 2.5f;
        [Tooltip("How high the top of the picture is above the ground point (map units at scale 1). The health bar floats above it. 0 = use a default height.")]
        public float headHeight = 0f;
        public float scale = 1f;
        public Color tint = Color.white;
        public int maxHealth = 20;
        public float speed = 2.2f;
        public int contactDamage = 6;
        public int points = 10;
        [Tooltip("Relative chance of being picked when the spawner creates an enemy.")]
        public int spawnWeight = 6;
        [Tooltip("Body radius in plane units (collision with props and reach of its attack).")]
        public float radius = 0.4f;
    }

    /// <summary>
    /// Everything that makes one season different: look of the island, obstacle layout, spawn
    /// points and the three enemy types. The Season Rush setup tool generates one of these per
    /// season, and artists can tweak them directly in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Season Rush/Season Theme", fileName = "SeasonTheme")]
    public class SeasonTheme : ScriptableObject
    {
        public Season season;
        public string displayName = "Spring";
        [TextArea] public string mood;
        public Color skyColor = new Color(0.8f, 0.86f, 1f);

        [Header("Island materials (sprite-unlit materials using the tile textures)")]
        public Material groundMaterial;
        public Material sideMaterial;      // grass tile with the ragged fringe on its right edge
        public Material cliffMaterial;     // the dirt face shown on the two front sides of the island
        public Material patchMaterial;     // sand / ice patch in the middle of the map
        public bool hasPatch = true;
        public Vector2 patchCenter = new Vector2(6f, 6f);
        public float patchRadius = 2.3f;
        public float cliffHeight = 2.0f;

        [Header("Layout (arena plane units, symmetric so both players have a fair map)")]
        public PropDef[] props;
        public PropPlacement[] placements;
        public Vector2[] playerSpawns = { new Vector2(1.8f, 10.2f), new Vector2(10.2f, 1.8f) };
        public Vector2[] enemySpawns;

        [Header("Enemies (index = EnemyTier: weak, medium, strong)")]
        public EnemyDef[] enemies;

        public EnemyDef GetEnemy(int tier)
        {
            if (enemies == null || enemies.Length == 0) return null;
            return enemies[Mathf.Clamp(tier, 0, enemies.Length - 1)];
        }
    }
}
