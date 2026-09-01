using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// The pure math behind detection — separated from the MonoBehaviours so the
    /// semantics are pinned by EditMode tests: proximity scoring (closer and more
    /// centered wins, current focus gets a stickiness bonus so near-ties don't
    /// flicker), the fat-cursor ray ring, candidate ranking, and the hold-to-interact
    /// timer.
    /// </summary>
    public static class InteractionScoring
    {
        /// <summary>
        /// Proximity score in [0..1]: 1 at zero distance dead ahead, 0 at max range
        /// or the angle limit. <paramref name="facingWeight"/> blends how much facing
        /// matters vs pure distance (0 = distance only, 1 = facing only).
        /// </summary>
        public static float ProximityScore(float distance, float maxDistance, float angleDegrees, float maxAngleDegrees, float facingWeight)
        {
            if (maxDistance <= 0f || distance > maxDistance) return 0f;
            if (angleDegrees > maxAngleDegrees) return 0f;

            float distanceScore = 1f - Mathf.Clamp01(distance / maxDistance);
            float angleScore = maxAngleDegrees > 0f ? 1f - Mathf.Clamp01(angleDegrees / maxAngleDegrees) : 1f;
            return Mathf.Lerp(distanceScore, angleScore, Mathf.Clamp01(facingWeight));
        }

        /// <summary>Evenly spaced screen-space offsets for fat-cursor fallback rays
        /// (deterministic ring, first offset pointing up).</summary>
        public static void BuildRingOffsets(int count, float radiusPixels, Vector2[] results)
        {
            for (int i = 0; i < count && i < results.Length; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count + Mathf.PI * 0.5f;
                results[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radiusPixels;
            }
        }

        /// <summary>
        /// Orders candidates best-first, keeping the detector's order among equal scores.
        ///
        /// Stability is the point. <c>List.Sort</c> is not stable, so two candidates with the
        /// same score could swap places from one detection to the next — which a player sees
        /// as focus flickering between two identical items on a shelf. Insertion sort is
        /// stable, ideal for the handful of nearly sorted entries a detector produces, and
        /// allocates nothing, where <c>List.Sort(Comparison)</c> allocates a comparer per call.
        /// </summary>
        public static void SortByScoreDescending(List<InteractionCandidate> candidates)
        {
            for (int i = 1; i < candidates.Count; i++)
            {
                InteractionCandidate moving = candidates[i];
                int j = i - 1;
                while (j >= 0 && candidates[j].score < moving.score)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }
                candidates[j + 1] = moving;
            }
        }
    }

    /// <summary>Hold-to-interact timer: pure state, no Unity lifecycle, test-pinned.
    /// Zero-duration holds complete on the first tick (instant verbs).</summary>
    public struct HoldTimer
    {
        private float duration;
        private float elapsed;
        private bool active;

        public bool IsActive => active;
        public float Progress01 => !active ? 0f : duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

        public void Begin(float holdSeconds)
        {
            duration = Mathf.Max(0f, holdSeconds);
            elapsed = 0f;
            active = true;
        }

        public void Cancel()
        {
            active = false;
            elapsed = 0f;
        }

        /// <summary>Advances the hold; true exactly once when it completes (and deactivates).</summary>
        public bool Tick(float deltaTime)
        {
            if (!active) return false;
            elapsed += deltaTime;
            if (elapsed >= duration)
            {
                active = false;
                return true;
            }
            return false;
        }
    }
}
