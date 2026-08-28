using UnityEngine;
using LiminalLabs.GameEvents;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>
    /// An <see cref="IInteractionCondition"/> that starts locked and unlocks when its
    /// game event is raised (the lever, across the room). Until then, interacting
    /// with the chest rejects VerbUnavailable — visible in the prompt, the overlay,
    /// and the Interactor inspector. Conditions + events composing: no code links.
    /// </summary>
    public class DemoLockedCondition : GameEventReceiver, IInteractionCondition
    {
        [SerializeField] private bool startLocked = true;

        private bool locked;

        void Awake() => locked = startLocked;

        protected override void OnEventRaised()
        {
            locked = !locked;
        }

        public bool IsAvailable(in InteractionContext context) => !locked;
    }
}
