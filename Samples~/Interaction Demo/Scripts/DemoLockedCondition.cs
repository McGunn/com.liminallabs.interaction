using UnityEngine;
using LiminalLabs.Core.Localization;
using LiminalLabs.GameEvents;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>
    /// An <see cref="IInteractionCondition"/> that starts locked and unlocks when its
    /// game event is raised (the lever, across the room). Until then, interacting
    /// with the chest rejects VerbUnavailable — visible in the prompt, the overlay,
    /// and the Interactor inspector. Conditions + events composing: no code links.
    ///
    /// It also says why, through <see cref="IInteractionRefusal"/>: the prompt shows the
    /// reason under the verb without knowing what a lever is, and the text is a
    /// <see cref="LocalizedText"/> like every other player-facing string.
    /// </summary>
    public class DemoLockedCondition : GameEventReceiver, IInteractionCondition, IInteractionRefusal
    {
        [SerializeField] private bool startLocked = true;

        [SerializeField, Tooltip("What the player is told while this is locked.")]
        private LocalizedText lockedReason = new LocalizedText("Locked — pull the lever");

        private bool locked;

        void Awake() => locked = startLocked;

        protected override void OnEventRaised()
        {
            locked = !locked;
        }

        public bool IsAvailable(in InteractionContext context) => !locked;

        public LocalizedText Reason => locked ? lockedReason : null;
    }
}
