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

        /// <summary>Code-side interaction notification (exception-isolated per listener).</summary>
        public event Action<InteractionContext> Interacted;

        public event Action<Interactor> FocusGained;
        public event Action<Interactor> FocusLost;

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

        /// <summary>Call after adding/removing IInteractionCondition components at runtime.</summary>
        public void RefreshConditions()
        {
            conditionsCached = false;
        }

        /// <summary>
        /// Whether the attempt in <paramref name="context"/> would be allowed, and if
        /// not, exactly why — the answer the debug overlay and inspectors surface.
        /// </summary>
        public InteractionRejection Evaluate(in InteractionContext context)
        {
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
            foreach (IInteractionCondition condition in conditions)
            {
                if (!condition.IsAvailable(context)) return InteractionRejection.VerbUnavailable;
            }
            return InteractionRejection.None;
        }

        internal void NotifyFocus(Interactor interactor, bool gained)
        {
            if (gained)
            {
                Isolated(FocusGained, interactor);
                onFocusGained?.Invoke();
            }
            else
            {
                Isolated(FocusLost, interactor);
                onFocusLost?.Invoke();
            }
        }

        internal void HandleInteracted(in InteractionContext context)
        {
            if (Interacted != null)
            {
                foreach (Delegate listener in Interacted.GetInvocationList())
                {
                    try
                    {
                        ((Action<InteractionContext>)listener)(context);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[Interaction] '{name}': Interacted listener threw — remaining listeners still run.\n{exception}", this);
                    }
                }
            }
            onInteracted?.Invoke();
            if (raiseOnInteracted != null) raiseOnInteracted.Raise();
        }

        private void Isolated(Action<Interactor> handlers, Interactor interactor)
        {
            if (handlers == null) return;
            foreach (Delegate listener in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<Interactor>)listener)(interactor);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Interaction] '{name}': focus listener threw — remaining listeners still run.\n{exception}", this);
                }
            }
        }
    }
}
