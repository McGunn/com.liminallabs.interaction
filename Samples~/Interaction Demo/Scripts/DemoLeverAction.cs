using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Flips the lever visual. The interesting part isn't here: the lever's
    /// Interactable broadcasts a GameEvent on interact, and the lamp and the locked
    /// chest react to it with zero references to this lever — interactions produce,
    /// events broadcast.</summary>
    public class DemoLeverAction : InteractAction
    {
        [SerializeField] private Transform handle;
        [SerializeField] private float flipAngle = 55f;

        private bool flipped;

        protected override void OnInteracted(in InteractionContext context)
        {
            flipped = !flipped;
            if (handle != null) handle.localRotation = Quaternion.Euler(flipped ? flipAngle : -flipAngle, 0f, 0f);
        }
    }
}
