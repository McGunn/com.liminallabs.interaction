using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>Why an interaction attempt did not happen. Never silent — the
    /// Interactor keeps its last rejection and the debug overlay shows it live.</summary>
    public enum InteractionRejection
    {
        None = 0,
        NoTarget,
        TargetDisabled,
        OutOfRange,
        VerbNotOffered,
        VerbUnavailable,
        NoVerb,

        /// <summary>A hold-to-interact started on the focus was abandoned because focus moved
        /// off its target before it completed.</summary>
        FocusLost,
    }

    /// <summary>Everything about one interaction attempt: who, what, which verb, where.</summary>
    public readonly struct InteractionContext
    {
        public readonly Interactor interactor;
        public readonly Interactable interactable;
        public readonly Interaction verb;
        public readonly Vector3 point;

        public InteractionContext(Interactor interactor, Interactable interactable, Interaction verb, Vector3 point)
        {
            this.interactor = interactor;
            this.interactable = interactable;
            this.verb = verb;
            this.point = point;
        }
    }

    /// <summary>One detector result: a reachable interactable with its ranking score
    /// (higher is better; detector-specific meaning) and where it was found.</summary>
    public struct InteractionCandidate
    {
        public Interactable interactable;
        public float score;
        public float distance;
        public Vector3 point;
    }

    /// <summary>Gate an interactable's availability with game rules: put components
    /// implementing this beside the Interactable (a lock needing a key, a power
    /// requirement). All conditions must pass or the attempt rejects VerbUnavailable.</summary>
    public interface IInteractionCondition
    {
        bool IsAvailable(in InteractionContext context);
    }

    /// <summary>The execution seam that makes genre differences possible: when set on
    /// an Interactor, validated requests are handed here INSTEAD of executing
    /// immediately. A CRPG's handler pathfinds to the target, then calls
    /// <see cref="Interactor.Execute"/> on arrival (which re-validates). Without a
    /// handler, requests execute on the spot (FPS behavior).</summary>
    public interface IInteractionRequestHandler
    {
        void HandleRequest(in InteractionContext context);
    }
}
