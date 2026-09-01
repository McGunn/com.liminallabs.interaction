using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if LIMINAL_UGUI
using UnityEngine.EventSystems;
#endif

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// Pointer detection for point-and-click and CRPG mouse targeting: a ray through
    /// the cursor position (Input System or legacy input, automatically), with
    /// occlusion, and optional blocking while the pointer is over uGUI so clicks
    /// never fall through menus. Hover enter/exit comes for free via the
    /// interactor's focus events.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Interaction/Pointer Detector")]
    public class PointerDetector : InteractionDetector
    {
        [SerializeField, Tooltip("Camera the cursor ray goes through. Empty = Camera.main.")]
        private Camera cameraOverride;

        [SerializeField, Min(1f)] private float maxDistance = 200f;
        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField, Tooltip("Hits on or under this transform are treated as transparent (e.g. the controlled character, so clicking through them still targets the world).")]
        private Transform ignoreRoot;

        /// <summary>Colliders under this root never block or receive the pointer ray.</summary>
        public Transform IgnoreRoot { get => ignoreRoot; set => ignoreRoot = value; }

#if LIMINAL_UGUI
        [SerializeField, Tooltip("Ignore the pointer while it is over uGUI, so clicks never fall through menus.")]
        private bool blockedByUI = true;
#endif

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Camera cachedMain;

        private Camera ResolvedCamera
        {
            get
            {
                if (cameraOverride != null) return cameraOverride;
                // Re-asked when the remembered camera is gone or switched off, so a camera
                // swap is followed rather than clicked through a camera that no longer renders.
                if (cachedMain == null || !cachedMain.isActiveAndEnabled) cachedMain = Camera.main;
                return cachedMain;
            }
        }

        /// <summary>The current pointer position in screen pixels (input-backend agnostic).</summary>
        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public override void GatherCandidates(Interactor interactor, List<InteractionCandidate> results)
        {
            Camera cam = ResolvedCamera;
            if (cam == null) return;

#if LIMINAL_UGUI
            if (blockedByUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
#endif

            Ray ray = cam.ScreenPointToRay(PointerPosition);
            int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, layerMask, triggerInteraction);
            if (count == 0) return;

            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (ignoreRoot != null && hits[i].collider.transform.IsChildOf(ignoreRoot)) continue;
                if (nearest < 0 || hits[i].distance < hits[nearest].distance) nearest = i;
            }
            if (nearest < 0) return;
            Interactable interactable = InteractableRegistry.Resolve(hits[nearest].collider);
            if (interactable == null) return;

            results.Add(new InteractionCandidate
            {
                interactable = interactable,
                score = 1f,
                distance = hits[nearest].distance,
                point = hits[nearest].point,
            });
        }
    }
}
