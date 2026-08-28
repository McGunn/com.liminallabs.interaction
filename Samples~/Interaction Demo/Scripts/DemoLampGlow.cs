using UnityEngine;
using LiminalLabs.GameEvents;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Toggles between two colors every time its game event is raised — a
    /// GameEventReceiver, reacting to the lever across the room with no reference
    /// to it.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DemoLampGlow : GameEventReceiver
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color offColor = new Color(0.15f, 0.15f, 0.18f);
        [SerializeField] private Color onColor = new Color(0.35f, 1f, 0.55f);

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private bool lit;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
            Apply();
        }

        protected override void OnEventRaised()
        {
            lit = !lit;
            Apply();
        }

        private void Apply()
        {
            block.SetColor(BaseColor, lit ? onColor : offColor);
            meshRenderer.SetPropertyBlock(block);
        }
    }
}
