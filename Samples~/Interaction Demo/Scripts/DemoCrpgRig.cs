using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>
    /// CRPG rig: fixed camera, cursor targeting, and the request-handler seam doing
    /// its job — clicking a target doesn't execute; this component (the interactor's
    /// IInteractionRequestHandler) walks the character over first and completes with
    /// Execute on arrival, which re-validates. That deferred flow is what most
    /// interaction systems can't express.
    /// </summary>
    public class DemoCrpgRig : MonoBehaviour, IInteractionRequestHandler
    {
        [SerializeField] private Camera sharedCamera;
        [SerializeField] private Interactor interactor;
        [SerializeField] private Transform character;
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float arriveWithin = 1.6f;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 12f, -8f);
        [SerializeField] private Vector3 cameraEuler = new Vector3(56f, 0f, 0f);

        private InteractionContext pending;
        private bool walking;

        void OnEnable()
        {
            if (sharedCamera != null)
            {
                sharedCamera.transform.SetParent(null);
                sharedCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraEuler));
            }
            if (interactor != null) interactor.RequestHandler = this;
        }

        void OnDisable()
        {
            walking = false;
            pending = default;
        }

        public void HandleRequest(in InteractionContext context)
        {
            pending = context;
            walking = true;
        }

        void Update()
        {
            if (interactor != null && DemoRigInput.ClickPressed)
            {
                walking = false;                    // a new click replaces the current trip
                interactor.StartInteraction();      // valid target -> HandleRequest fires
            }

            if (!walking || character == null) return;
            if (pending.interactable == null) { walking = false; return; }

            Vector3 destination = pending.interactable.InteractionPoint;
            Vector3 flat = destination - character.position;
            flat.y = 0f;

            if (flat.magnitude <= arriveWithin)
            {
                walking = false;
                interactor.Execute(pending);        // re-validates on arrival
                pending = default;
                return;
            }

            Vector3 step = flat.normalized * moveSpeed * Time.deltaTime;
            character.position += step;
            if (flat.sqrMagnitude > 0.01f)
            {
                character.rotation = Quaternion.Slerp(character.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 10f);
            }
        }
    }
}
