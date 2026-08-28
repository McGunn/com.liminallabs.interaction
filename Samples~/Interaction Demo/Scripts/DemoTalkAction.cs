using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>Shows a speech line above the NPC for a few seconds, cycling
    /// through the lines per interaction.</summary>
    public class DemoTalkAction : InteractAction
    {
        [SerializeField] private TextMesh speechText;
        [SerializeField] private float showSeconds = 3f;
        [SerializeField]
        private string[] lines =
        {
            "Same door, four genres.",
            "I never met that lever,\nbut I heard the event.",
            "Try holding E on the gem.",
        };

        private int next;
        private float hideAt = float.PositiveInfinity;

        void Start()
        {
            if (speechText != null) speechText.text = "";
        }

        protected override void OnInteracted(in InteractionContext context)
        {
            if (speechText == null || lines.Length == 0) return;
            speechText.text = lines[next];
            next = (next + 1) % lines.Length;
            hideAt = Time.time + showSeconds;
        }

        void Update()
        {
            if (Time.time < hideAt) return;
            hideAt = float.PositiveInfinity;
            if (speechText != null) speechText.text = "";
        }
    }
}
