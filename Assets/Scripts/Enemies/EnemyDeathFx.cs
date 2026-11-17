using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// The death animation of an enemy. An enemy is removed from the game the moment it is killed, so the animation plays on a
    /// harmless copy of its visual prefab (no network, no collider) that removes itself after a moment.
    /// Only enemies whose Animator has a Trigger called "Die" get this (the Snowman does).
    /// </summary>
    public static class EnemyDeathFx
    {
        const float Lifetime = 0.6f;
        static readonly int DieHash = Animator.StringToHash("Die");

        public static void Play(GameObject visualPrefab, Vector3 position, Vector3 scale, bool flipX, int sortingOrder, Color tint)
        {
            if (visualPrefab == null) return;
            GameObject copy = Object.Instantiate(visualPrefab, position, Quaternion.identity);
            copy.name = visualPrefab.name + "_Death";
            copy.transform.localScale = scale;

            var animator = copy.GetComponentInChildren<Animator>();
            bool hasDie = false;
            if (animator != null)
                foreach (AnimatorControllerParameter p in animator.parameters)
                    if (p.nameHash == DieHash && p.type == AnimatorControllerParameterType.Trigger) hasDie = true;
            if (!hasDie) { Object.Destroy(copy); return; }

            var renderer = copy.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = flipX;
                renderer.sortingOrder = sortingOrder;
                renderer.color = tint;
            }
            animator.SetTrigger(DieHash);
            Object.Destroy(copy, Lifetime);
        }
    }
}
