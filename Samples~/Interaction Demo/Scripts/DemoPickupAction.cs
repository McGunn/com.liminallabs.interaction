using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Collects the object (its verb is hold-to-interact — watch the prompt
    /// fill while E or the mouse button is held), then respawns it after a delay so
    /// the demo never runs out.</summary>
    public class DemoPickupAction : InteractAction
    {
        [SerializeField] private float respawnSeconds = 4f;
        [SerializeField] private Renderer visual;
        [SerializeField] private Collider pickupCollider;

        private float respawnAt = float.PositiveInfinity;

        protected override void OnInteracted(in InteractionContext context)
        {
            if (visual != null) visual.enabled = false;
            if (pickupCollider != null) pickupCollider.enabled = false;
            respawnAt = Time.time + respawnSeconds;
        }

        void Update()
        {
            if (Time.time < respawnAt) return;
            respawnAt = float.PositiveInfinity;
            if (visual != null) visual.enabled = true;
            if (pickupCollider != null) pickupCollider.enabled = true;
        }
    }
}
