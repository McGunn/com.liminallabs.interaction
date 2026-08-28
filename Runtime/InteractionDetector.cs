using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// How an <see cref="Interactor"/> finds candidates — the pluggable edge that
    /// makes the system genre-agnostic. Shipped detectors: <see cref="RayDetector"/>
    /// (FPS / first person), <see cref="ProximityDetector"/> (third person),
    /// <see cref="PointerDetector"/> (point-and-click / CRPG). Implement your own
    /// for anything else; append candidates with detector-meaningful scores
    /// (higher = better) and the interactor ranks them.
    /// </summary>
    public abstract class InteractionDetector : MonoBehaviour
    {
        /// <summary>Appends this frame's candidates. Called at the interactor's
        /// detection rate; must not allocate.</summary>
        public abstract void GatherCandidates(Interactor interactor, List<InteractionCandidate> results);
    }
}
