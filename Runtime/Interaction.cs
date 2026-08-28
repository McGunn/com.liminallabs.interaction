using System.Collections.Generic;
using UnityEngine;
using LiminalLabs.Core.Localization;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// A verb: "Open", "Talk", "Pick Up" — the reusable definition of one way to
    /// interact, shared by every object that offers it. Carries only what every
    /// genre needs (localized name, icon, cursor id, ordering, hold time); gameplay
    /// rules live on the interactable side as <see cref="IInteractionCondition"/>
    /// components, never here.
    /// </summary>
    [CreateAssetMenu(fileName = "Interaction", menuName = "Liminal Labs/Interaction/Interaction Verb")]
    public class Interaction : ScriptableObject
    {
        [SerializeField, Tooltip("Player-facing name (\"Open\", \"Talk\"). Localized via the Core bridge; the fallback text works with no localization package.")]
        private LocalizedText displayName;

        [SerializeField, TextArea(2, 3), Tooltip("What this verb means and when to offer it. Shown in tooling.")]
        private string description;

        [SerializeField, Tooltip("Optional icon for prompts, verb menus, radial wheels.")]
        private Sprite icon;

        [SerializeField, Tooltip("Optional cursor identifier for pointer-driven games. Your cursor system maps it; this package doesn't own cursors.")]
        private string cursorId;

        [SerializeField, Tooltip("Ordering among verbs on the same interactable; lowest is the primary verb.")]
        private int sortOrder;

        [SerializeField, Min(0f), Tooltip("Seconds the interact input must be held to execute. 0 = instant press.")]
        private float holdSeconds;

        public string Description => description;
        public Sprite Icon => icon;
        public string CursorId => cursorId;
        public int SortOrder => sortOrder;
        public float HoldSeconds => holdSeconds;

        /// <summary>The localized player-facing name (falls back to the asset name).</summary>
        public string DisplayName
        {
            get
            {
                string resolved = displayName;
                return string.IsNullOrEmpty(resolved) ? name : resolved;
            }
        }

        /// <summary>The primary verb of a list: lowest sort order, ties by list order.
        /// Pure and pinned by tests.</summary>
        public static Interaction SelectPrimary(IReadOnlyList<Interaction> verbs)
        {
            Interaction best = null;
            for (int i = 0; i < (verbs?.Count ?? 0); i++)
            {
                Interaction verb = verbs[i];
                if (verb == null) continue;
                if (best == null || verb.sortOrder < best.sortOrder) best = verb;
            }
            return best;
        }
    }
}
