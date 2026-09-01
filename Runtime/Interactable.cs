using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using LiminalLabs.Core.Localization;
using LiminalLabs.GameEvents;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// Something that can be interacted with. Deliberately lean: an identity
    /// (localized display name), the verbs it offers, an optional tighter range, and
    /// reaction surfaces — C# events for code, UnityEvents for designers, an optional
    /// GameEvent for global broadcast, and <see cref="InteractAction"/> components
    /// for composable behaviors. Availability rules live in sibling
    /// <see cref="IInteractionCondition"/> components. Needs a collider (any child
    /// collider works) for detectors to find it.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Interaction/Interactable")]
    public class Interactable : MonoBehaviour
    {
        [SerializeField, Tooltip("Player-facing name for prompts (\"Wooden Door\"). Falls back to the GameObject name when empty.")]
        private LocalizedText displayName;

        [SerializeField, Tooltip("Verbs this object offers. Lowest sort order is the primary verb.")]
        private List<Interaction> verbs = new List<Interaction>();

        [SerializeField, Min(0f), Tooltip("Interactions must happen within this distance of the interaction point (a keypad wants you close). 0 = no extra limit beyond the detector's reach.")]
        private float rangeOverride = 0f;

        [SerializeField, Tooltip("Point used for range checks, prompts, and indicators. Empty = this transform.")]
        private Transform interactionPoint;

        [Header("Feedback")]
        [SerializeField] private UnityEvent onFocusGained;
        [SerializeField] private UnityEvent onFocusLost;
        [SerializeField] private UnityEvent onInteracted;

        [Header("Global Broadcast (optional)")]
        [SerializeField, Tooltip("Raised on every successful interaction, so distant systems react with no scene links.")]
        private GameEvent raiseOnInteracted;

        private IsolatedEvent<InteractionContext> interacted;
        private IsolatedEvent<Interactor> focusGained;
        private IsolatedEvent<Interactor> focusLost;

        /// <summary>Code-side interaction notification. Listeners are called one at a time;
        /// one that throws is logged and the rest still run, and firing allocates nothing.</summary>
        public event Action<InteractionContext> Interacted
        {
            add => interacted.Add(value);
            remove => interacted.Remove(value);
        }

        /// <summary>An interactor's focus arrived here. Same guarantees as <see cref="Interacted"/>.</summary>
        public event Action<Interactor> FocusGained
        {
            add => focusGained.Add(value);
            remove => focusGained.Remove(value);
        }

        /// <summary>An interactor's focus left. Same guarantees as <see cref="Interacted"/>.</summary>
        public event Action<Interactor> FocusLost
        {
            add => focusLost.Add(value);
            remove => focusLost.Remove(value);
        }

        private IInteractionCondition[] conditions;
        private bool conditionsCached;

        public IReadOnlyList<Interaction> Verbs => verbs;
        public Interaction PrimaryVerb => Interaction.SelectPrimary(verbs);
        public float RangeOverride => rangeOverride;
        public Vector3 InteractionPoint => interactionPoint != null ? interactionPoint.position : transform.position;

        /// <summary>Prompt-facing name: localized display name, or the GameObject name.</summary>
        public string DisplayName
        {
            get
            {
                string resolved = displayName;
                return string.IsNullOrEmpty(resolved) ? gameObject.name : resolved;
            }
        }

        void OnEnable() => InteractableRegistry.Register(this);
        void OnDisable() => InteractableRegistry.Unregister(this);

        /// <summary>Call after adding or removing <see cref="IInteractionCondition"/> components
        /// at runtime. Toggling an existing one's <c>enabled</c> needs no call: a disabled
        /// condition is skipped when evaluated.</summary>
        public void RefreshConditions()
        {
            conditionsCached = false;
        }

        /// <summary>
        /// Whether the attempt in <paramref name="context"/> would be allowed, and if
        /// not, exactly why — the answer the debug overlay and inspectors surface.
        /// </summary>
        public InteractionRejection Evaluate(in InteractionContext context) => Evaluate(context, out _);

        /// <summary>
        /// <see cref="Evaluate(in InteractionContext)"/>, also naming the condition that refused
        /// when the answer is <see cref="InteractionRejection.VerbUnavailable"/>.
        ///
        /// "Unavailable" alone is the difference between a prompt that says <i>locked</i> and
        /// one that says nothing, and between a designer told which of the three conditions on
        /// a chest is the one saying no and a designer guessing. Null for every other answer.
        /// </summary>
        public InteractionRejection Evaluate(in InteractionContext context, out IInteractionCondition blocker)
        {
            blocker = null;

            if (!isActiveAndEnabled) return InteractionRejection.TargetDisabled;
            if (context.verb == null) return InteractionRejection.NoVerb;
            if (!verbs.Contains(context.verb)) return InteractionRejection.VerbNotOffered;

            if (rangeOverride > 0f && context.interactor != null)
            {
                float distance = Vector3.Distance(context.interactor.RangePosition, InteractionPoint);
                if (distance > rangeOverride) return InteractionRejection.OutOfRange;
            }

            if (!conditionsCached)
            {
                conditions = GetComponents<IInteractionCondition>();
                conditionsCached = true;
            }

            for (int i = 0; i < conditions.Length; i++)
            {
                IInteractionCondition condition = conditions[i];

                // A disabled condition component is a rule switched off, the way a disabled
                // collider is a collider that is not there - which makes the enabled checkbox
                // a designer's way to lift a lock without a script. One that was destroyed
                // since the cache was built is a rule that is gone.
                if (condition is Behaviour behaviour && (behaviour == null || !behaviour.enabled)) continue;

                if (!condition.IsAvailable(context))
                {
                    blocker = condition;
                    return InteractionRejection.VerbUnavailable;
                }
            }

            return InteractionRejection.None;
        }

        /// <summary>
        /// Index of a verb in this interactable's list, or -1 — the compact way to
        /// say "which verb" over a network or in a save (send the index, resolve
        /// with <see cref="GetVerb"/> on the other side; verb lists are identical
        /// asset data on every machine).
        /// </summary>
        public int IndexOfVerb(Interaction verb) => verbs.IndexOf(verb);

        /// <summary>The verb at an index, or null when out of range.</summary>
        public Interaction GetVerb(int index) => index >= 0 && index < verbs.Count ? verbs[index] : null;

        /// <summary>
        /// Fires this interactable's reactions WITHOUT validation — the
        /// authority-already-decided path. Use it on remote clients when a
        /// replicated interaction arrives: the server validated and executed, so
        /// local condition state (possibly not yet synced) must not veto the
        /// result. All local flows go through <see cref="Interactor"/>, which
        /// validates.
        /// </summary>
        public void PerformInteraction(in InteractionContext context)
        {
            HandleInteracted(context);
        }

        internal void NotifyFocus(Interactor interactor, bool gained)
        {
            if (gained)
            {
                focusGained.Invoke(interactor, "FocusGained", this);
                Safely(onFocusGained, "On Focus Gained");
            }
            else
            {
                focusLost.Invoke(interactor, "FocusLost", this);
                Safely(onFocusLost, "On Focus Lost");
            }
        }

        internal void HandleInteracted(in InteractionContext context)
        {
            interacted.Invoke(context, "Interacted", this);
            Safely(onInteracted, "On Interacted");
            if (raiseOnInteracted != null) raiseOnInteracted.Raise();
        }

        /// <summary>A UnityEvent is one reaction among several here, and a mistake wired into
        /// one must not cost the reactions after it - the global broadcast in particular.</summary>
        private void Safely(UnityEvent reaction, string what)
        {
            if (reaction == null) return;

            try
            {
                reaction.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Interaction] '{name}': {what} UnityEvent threw — the remaining reactions still run.\n{exception}", this);
            }
        }
    }
}
