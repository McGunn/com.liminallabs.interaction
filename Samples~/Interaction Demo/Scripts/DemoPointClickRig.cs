using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Point-and-click rig: fixed overview camera, PointerDetector under
    /// the cursor (hover = focus, so prompts and cursor swaps come free), click to
    /// interact — hold-verbs hold the button.</summary>
    public class DemoPointClickRig : MonoBehaviour
    {
        [SerializeField] private Camera sharedCamera;
        [SerializeField] private Interactor interactor;
        [SerializeField] private Vector3 cameraPosition = new Vector3(0f, 11f, -9f);
        [SerializeField] private Vector3 cameraEuler = new Vector3(52f, 0f, 0f);

        void OnEnable()
        {
            if (sharedCamera != null)
            {
                sharedCamera.transform.SetParent(null);
                sharedCamera.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraEuler));
            }
        }

        void Update()
        {
            if (interactor == null) return;
            if (DemoRigInput.ClickPressed) interactor.StartInteraction();
            if (DemoRigInput.ClickReleased) interactor.CancelInteraction();
        }
    }
}
