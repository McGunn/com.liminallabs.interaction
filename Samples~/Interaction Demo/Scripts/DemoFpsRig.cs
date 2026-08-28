using UnityEngine;
using LiminalLabs.Core;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>FPS rig: WASD + mouse look, RayDetector through the camera center
    /// with fat-cursor forgiveness, E to interact (hold verbs hold E). The camera is
    /// borrowed while this rig is active.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class DemoFpsRig : MonoBehaviour
    {
        [SerializeField] private Camera sharedCamera;
        [SerializeField] private Interactor interactor;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float lookSensitivity = 2.2f;
        [SerializeField] private float eyeHeight = 1.6f;

        private CharacterController body;
        private float yaw, pitch;

        void Awake() => body = GetComponent<CharacterController>();

        void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (sharedCamera != null)
            {
                sharedCamera.transform.SetParent(transform);
                sharedCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
                sharedCamera.transform.localRotation = Quaternion.identity;
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (sharedCamera != null) sharedCamera.transform.SetParent(null);
        }

        void Update()
        {
            Vector2 look = DemoInput.Look * lookSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (sharedCamera != null) sharedCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            Vector2 move = DemoInput.Move;
            Vector3 velocity = (transform.forward * move.y + transform.right * move.x) * moveSpeed;
            velocity.y = -9.81f;
            body.Move(velocity * Time.deltaTime);

            if (interactor == null) return;
            if (DemoRigInput.InteractPressed) interactor.StartInteraction();
            if (DemoRigInput.InteractReleased) interactor.CancelInteraction();
        }
    }
}
