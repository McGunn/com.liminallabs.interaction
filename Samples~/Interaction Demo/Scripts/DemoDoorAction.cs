using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Swings a door open/closed on interact — an <see cref="InteractAction"/>,
    /// so wiring, validation, and verb filtering are inherited; this is just the swing.</summary>
    public class DemoDoorAction : InteractAction
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 105f;
        [SerializeField] private float swingSpeed = 4f;

        private bool open;
        private float angle;

        protected override void OnInteracted(in InteractionContext context)
        {
            open = !open;
        }

        void Update()
        {
            float target = open ? openAngle : 0f;
            if (Mathf.Approximately(angle, target)) return;
            angle = Mathf.MoveTowards(angle, target, Time.deltaTime * swingSpeed * openAngle);
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}
