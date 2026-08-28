using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// Third-person detection: everything within a radius, ranked by distance and
    /// facing (see <see cref="InteractionScoring.ProximityScore"/>), with a
    /// stickiness bonus for the current focus so the target never flickers between
    /// two near-equal candidates. No aiming required — the classic
    /// walk-up-and-press-a-button model.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Interaction/Proximity Detector")]
    public class ProximityDetector : InteractionDetector
    {
        [SerializeField, Min(0.1f)] private float radius = 2.5f;

        [SerializeField, Range(0f, 1f), Tooltip("How much facing matters vs distance (0 = closest wins, 1 = most-centered wins).")]
        private float facingWeight = 0.5f;

        [SerializeField, Range(10f, 180f), Tooltip("Candidates beyond this angle from forward are ignored (stops interacting with things behind you).")]
        private float maxAngle = 110f;

        [SerializeField, Tooltip("Forward direction for facing checks. Empty = this transform (use the camera for camera-relative facing).")]
        private Transform facingSource;

        [SerializeField, Range(0f, 0.5f), Tooltip("Score bonus for the currently focused target — hysteresis against focus flicker on near-ties.")]
        private float stickiness = 0.12f;

        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private readonly Collider[] overlaps = new Collider[32];
        private readonly HashSet<Interactable> seen = new HashSet<Interactable>();

        public float Radius => radius;

        public override void GatherCandidates(Interactor interactor, List<InteractionCandidate> results)
        {
            Vector3 position = transform.position;
            Transform facing = facingSource != null ? facingSource : transform;

            int count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, layerMask, triggerInteraction);
            seen.Clear();
            for (int i = 0; i < count; i++)
            {
                Interactable interactable = InteractableRegistry.Resolve(overlaps[i]);
                if (interactable == null || !seen.Add(interactable)) continue;

                Vector3 point = overlaps[i].ClosestPoint(position);
                float distance = Vector3.Distance(position, point);
                Vector3 direction = point - position;
                float angle = direction.sqrMagnitude > 0.0001f ? Vector3.Angle(facing.forward, direction) : 0f;

                float score = InteractionScoring.ProximityScore(distance, radius, angle, maxAngle, facingWeight);
                if (score <= 0f) continue;
                if (interactor.Focused == interactable) score += stickiness;

                results.Add(new InteractionCandidate
                {
                    interactable = interactable,
                    score = score,
                    distance = distance,
                    point = point,
                });
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.5f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
