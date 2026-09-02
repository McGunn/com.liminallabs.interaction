using UnityEngine;
using LiminalLabs.Core.Localization;

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

    /// <summary>
    /// A condition that can also say why, in the player's language.
    ///
    /// Optional, and separate from <see cref="IInteractionCondition"/> on purpose: a rule
    /// that only answers yes or no is still a complete rule, and a prompt shows nothing
    /// extra for it. One that implements this has its reason shown by the prompt, the F3
    /// overlay, the inspector and the console, and recorded on the interactor as
    /// <see cref="Interactor.LastReason"/> - so "Locked", "Needs power" and "Too heavy"
    /// reach the player without the prompt knowing what a lock is.
    /// </summary>
    public interface IInteractionRefusal
    {
        /// <summary>Why this rule refuses right now. Read after
        /// <see cref="IInteractionCondition.IsAvailable"/> answered false, and free to change
        /// with state ("Locked" today, "Needs power" tomorrow). Null means nothing to show.</summary>
        LocalizedText Reason { get; }
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
