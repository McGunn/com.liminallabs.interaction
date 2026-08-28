using UnityEngine;
using LiminalLabs.Core;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Third-person rig: WASD moves the character, the camera follows from
    /// behind-above, ProximityDetector picks the best nearby target (no aiming),
    /// E to interact. Watch the focus hop between targets as you walk.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class DemoThirdPersonRig : MonoBehaviour
    {
        [SerializeField] private Camera sharedCamera;
        [SerializeField] private Interactor interactor;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 6f, -6.5f);

        private CharacterController body;

        void Awake() => body = GetComponent<CharacterController>();

        void OnEnable()
        {
            if (sharedCamera != null) sharedCamera.transform.SetParent(null);
        }

        void Update()
        {
            Vector2 move = DemoInput.Move;
            var velocity = new Vector3(move.x, 0f, move.y) * moveSpeed;
            if (velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(new Vector3(velocity.x, 0f, velocity.z)), Time.deltaTime * 10f);
            }
            velocity.y = -9.81f;
            body.Move(velocity * Time.deltaTime);

            if (sharedCamera != null)
            {
                Vector3 target = transform.position + cameraOffset;
                sharedCamera.transform.position = Vector3.Lerp(sharedCamera.transform.position, target, Time.deltaTime * 6f);
                sharedCamera.transform.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 1.2f - sharedCamera.transform.position);
            }

            if (interactor == null) return;
            if (DemoRigInput.InteractPressed) interactor.StartInteraction();
            if (DemoRigInput.InteractReleased) interactor.CancelInteraction();
        }
    }
}
