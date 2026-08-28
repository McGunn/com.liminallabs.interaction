using UnityEngine;

namespace LiminalLabs.Interaction.Demo
{
    /// <summary>
    /// The genre switch: Tab cycles the rigs — FPS, third person, point-and-click,
    /// CRPG — enabling one at a time. The interactables never change; only the rig
    /// does. That's the whole demo's thesis.
    /// </summary>
    public class DemoRigSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public class Rig
        {
            public string label;
            public string controls;
            public GameObject root;
        }

        [SerializeField] private Rig[] rigs;
        [SerializeField] private TextMesh rigSign;
        [SerializeField] private int startIndex = 0;

        private int current = -1;

        void Start() => Activate(startIndex);

        void Update()
        {
            if (DemoRigInput.NextRigPressed) Activate((current + 1) % rigs.Length);
        }

        private void Activate(int index)
        {
            if (rigs == null || rigs.Length == 0) return;
            current = Mathf.Clamp(index, 0, rigs.Length - 1);
            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i].root != null) rigs[i].root.SetActive(i == current);
            }
            if (rigSign != null)
            {
                rigSign.text = $"{rigs[current].label}   ·   [Tab] next rig\n{rigs[current].controls}";
            }
        }
    }
}
