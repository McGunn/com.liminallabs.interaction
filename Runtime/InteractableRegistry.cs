using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// Live roster of enabled interactables plus the collider→interactable cache
    /// detectors resolve hits through (misses cached too, so scenery colliders cost
    /// one lookup, not a GetComponentInParent per ray). The cache is capped, dropped
    /// when an interactable appears (a cached miss must not outlive its reason), and
    /// cleared on scene unload; everything resets for domain-reload-off.
    /// </summary>
    public static class InteractableRegistry
    {
        private const int CacheCap = 512;

        private static readonly HashSet<Interactable> all = new HashSet<Interactable>();
        private static readonly Dictionary<Collider, Interactable> byCollider = new Dictionary<Collider, Interactable>();

        // One delegate, held, so the scene-unload hook is added once per play session and
        // removed at the next - a fresh lambda per session would pile up under
        // domain-reload-off, one more Clear per session forever.
        private static readonly UnityAction<Scene> onSceneUnloaded = _ => byCollider.Clear();
        private static bool hooked;

        /// <summary>All enabled interactables (for tooling and diagnostics).</summary>
        public static IReadOnlyCollection<Interactable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
            byCollider.Clear();
            if (hooked) SceneManager.sceneUnloaded -= onSceneUnloaded;
            hooked = false;
        }

        internal static void Register(Interactable interactable)
        {
            all.Add(interactable);

            // Anything that resolved to "not interactable" before this existed may now be
            // wrong: a prop that just gained an Interactable, a child collider cached as a
            // miss before its parent was enabled. How many colliders the newcomer has is not
            // known here, so the whole cache goes; it refills at one GetComponentInParent per
            // collider actually hit, and at scene load - where most registering happens - it
            // is empty already.
            if (byCollider.Count > 0) byCollider.Clear();

            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneUnloaded += onSceneUnloaded;
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
