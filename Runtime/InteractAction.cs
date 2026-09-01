using System;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// A composable response to being interacted with: inherit, implement
    /// <see cref="OnInteracted"/>, and stack as many actions on one interactable as
    /// it needs — one behavior per component, no god objects. The base owns target
    /// resolution (this object or a parent, or an explicit reference), the
    /// subscription lifecycle, and an optional verb filter; it validates itself on
    /// Awake and disables loudly when miswired — never a silent no-op.
    /// </summary>
    public abstract class InteractAction : MonoBehaviour
    {
        [SerializeField, Tooltip("Interactable to respond to. Empty = this GameObject or its parents.")]
        private Interactable target;

        [SerializeField, Tooltip("Only respond to this verb. Empty = respond to every verb.")]
        private Interaction verbFilter;

        // Built once: a method group makes a new delegate each time it is written, and an
        // action on a pooled object enables and disables on every spawn.
        private Action<InteractionContext> handler;

        /// <summary>The resolved interactable this action responds to.</summary>
        protected Interactable Target => target;

        protected virtual void Awake()
        {
            if (target == null) target = GetComponentInParent<Interactable>();
            if (target == null)
            {
                Debug.LogError($"[Interaction] {GetType().Name} on '{gameObject.name}' has no Interactable (here, on a parent, or assigned) — disabling so the miss is loud.", this);
                enabled = false;
            }
        }

        protected virtual void OnEnable()
        {
            if (target != null) target.Interacted += handler ?? (handler = HandleInteracted);
        }

        protected virtual void OnDisable()
        {
            if (target != null && handler != null) target.Interacted -= handler;
        }

        private void HandleInteracted(InteractionContext context)
        {
            if (verbFilter != null && context.verb != verbFilter) return;
            OnInteracted(context);
        }

        /// <summary>The action. Runs once per successful interaction (after the verb filter).</summary>
        protected abstract void OnInteracted(in InteractionContext context);
    }
}
