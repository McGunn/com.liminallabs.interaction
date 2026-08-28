using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// FPS-style detection: one ray from the camera center (or a transform's
    /// forward), with optional fat-cursor forgiveness — when the central ray misses,
    /// a ring of fallback rays within a screen-pixel tolerance tries again, so
    /// small or thin interactables don't demand pixel-perfect aim. Occlusion is
    /// inherent: whatever the ray hits first wins, interactable or not.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Interaction/Ray Detector")]
    public class RayDetector : InteractionDetector
    {
        public enum RaySource { CameraCenter, TransformForward }

        [SerializeField, Tooltip("CameraCenter: through the screen center (classic FPS). TransformForward: along a transform's forward (VR hands, turrets).")]
        private RaySource source = RaySource.CameraCenter;

        [SerializeField, Tooltip("Camera for CameraCenter mode. Empty = Camera.main.")]
        private Camera cameraOverride;

        [SerializeField, Tooltip("Origin for TransformForward mode. Empty = this transform.")]
        private Transform originOverride;

        [SerializeField, Min(0.1f)] private float maxDistance = 3.5f;
        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Fat Cursor (CameraCenter only)")]
        [SerializeField, Min(0f), Tooltip("When the central ray misses, retry through a ring of points within this many screen pixels. 0 = off.")]
        private float screenTolerancePixels = 22f;

        [SerializeField, Range(4, 16)] private int fallbackRays = 8;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Vector2[] ringOffsets;
        private int ringOffsetsFor = -1;
        private Camera cachedMain;

        private Camera ResolvedCamera
        {
            get
            {
                if (cameraOverride != null) return cameraOverride;
                if (cachedMain == null) cachedMain = Camera.main;
                return cachedMain;
            }
        }

        public override void GatherCandidates(Interactor interactor, List<InteractionCandidate> results)
        {
            if (source == RaySource.TransformForward)
            {
                Transform origin = originOverride != null ? originOverride : transform;
                TryRay(new Ray(origin.position, origin.forward), 1f, results);
                return;
            }

            Camera cam = ResolvedCamera;
            if (cam == null) return;

            var center = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);
            if (TryRay(cam.ScreenPointToRay(center), 1f, results)) return;
            if (screenTolerancePixels <= 0f) return;

            if (ringOffsetsFor != fallbackRays)
            {
                ringOffsets = new Vector2[fallbackRays];
                ringOffsetsFor = fallbackRays;
                InteractionScoring.BuildRingOffsets(fallbackRays, 1f, ringOffsets);
            }
            for (int i = 0; i < fallbackRays; i++)
            {
                Vector2 point = center + ringOffsets[i] * screenTolerancePixels;
                // Slightly lower score than a central hit, so games can tell.
                if (TryRay(cam.ScreenPointToRay(point), 0.8f, results)) return;
            }
        }

        private bool TryRay(Ray ray, float score, List<InteractionCandidate> results)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, layerMask, triggerInteraction);
            if (count == 0) return false;

            // Nearest hit decides: if it isn't (part of) an interactable, the view is blocked.
            int nearest = 0;
            for (int i = 1; i < count; i++)
            {
                if (hits[i].distance < hits[nearest].distance) nearest = i;
            }
            Interactable interactable = InteractableRegistry.Resolve(hits[nearest].collider);
            if (interactable == null) return false;

            results.Add(new InteractionCandidate
            {
                interactable = interactable,
                score = score,
                distance = hits[nearest].distance,
                point = hits[nearest].point,
            });
            return true;
        }
    }
}
