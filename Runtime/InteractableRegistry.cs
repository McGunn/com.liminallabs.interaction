using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// Live roster of enabled interactables plus the collider→interactable cache
    /// detectors resolve hits through (misses cached too, so scenery colliders cost
    /// one lookup, not a GetComponentInParent per ray). Cache is capped and cleared
    /// on scene unload; everything resets for domain-reload-off.
    /// </summary>
    public static class InteractableRegistry
    {
        private const int CacheCap = 512;

        private static readonly HashSet<Interactable> all = new HashSet<Interactable>();
        private static readonly Dictionary<Collider, Interactable> byCollider = new Dictionary<Collider, Interactable>();
        private static bool hooked;

        /// <summary>All enabled interactables (for tooling and diagnostics).</summary>
        public static IReadOnlyCollection<Interactable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            byCollider.Clear();
            hooked = false;
        }

        internal static void Register(Interactable interactable)
        {
            all.Add(interactable);
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneUnloaded += _ => byCollider.Clear();
            }
        }

        internal static void Unregister(Interactable interactable)
        {
            all.Remove(interactable);
            // Cache entries for it go stale; they resolve to a disabled component and
            // are filtered by callers, then swept on scene unload.
        }

        /// <summary>The enabled interactable a collider belongs to (itself or a parent), or null.</summary>
        public static Interactable Resolve(Collider collider)
        {
            if (collider == null) return null;
            if (!byCollider.TryGetValue(collider, out Interactable interactable))
            {
                if (byCollider.Count >= CacheCap) byCollider.Clear();
                interactable = collider.GetComponentInParent<Interactable>();
                byCollider[collider] = interactable;   // misses cached as null
            }
            return interactable != null && interactable.isActiveAndEnabled ? interactable : null;
        }
    }
}
