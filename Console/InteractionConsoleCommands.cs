using System;
using System.Collections.Generic;
using LiminalLabs.Core.Console;
using UnityEngine;

namespace LiminalLabs.Interaction.Console
{
    /// <summary>
    /// The interaction console addon.
    ///
    /// Interaction fails quietly by design - a verb whose condition is false simply does
    /// not offer itself, and from the player's side that is indistinguishable from a bug.
    /// The commands here exist to make the refusal legible: what is focused, what scored,
    /// and which rejection came back.
    /// </summary>
    internal static class InteractionConsoleCommands
    {
        private const string Category = "Interaction";

        [ConsoleCommand("interact.state", "What each interactor is focused on.", Category = Category,
            Aliases = new[] { "interact" })]
        public static void State(ConsoleContext context)
        {
            IReadOnlyList<Interactor> interactors = Interactor.Active;

            if (interactors.Count == 0)
            {
                context.Warn("No active Interactor. Nothing can interact with anything.");
                return;
            }

            foreach (Interactor interactor in interactors)
            {
                context.Heading(interactor.name.ToUpperInvariant());

                var rows = new List<KeyValuePair<string, string>>
                {
                    Row("focused", interactor.Focused != null
                        ? ConsoleMarkup.Accent(interactor.Focused.DisplayName) +
                          ConsoleMarkup.Dim($"  on {interactor.Focused.name}")
                        : ConsoleMarkup.Dim("nothing")),
                    Row("candidates", interactor.Candidates.Count.ToString()),
                    Row("detector", interactor.Detector != null
                        ? interactor.Detector.GetType().Name
                        : ConsoleMarkup.Bad("none - detection cannot run")),
                    Row("handler", interactor.RequestHandler != null
                        ? interactor.RequestHandler.GetType().Name
                        : ConsoleMarkup.Dim("none (direct execution)")),
                    Row("holding", interactor.IsHolding
                        ? ConsoleMarkup.Value($"{interactor.HoldProgress01:P0}")
                        : "no"),
                    Row("last rejection", interactor.LastRejection == InteractionRejection.None
                        ? ConsoleMarkup.Good("none")
                        : ConsoleMarkup.Warn(interactor.LastRejection.ToString())),
                    Row("refused by", interactor.LastBlocker != null
                        ? ConsoleMarkup.Warn(Interactor.Describe(interactor.LastBlocker))
                        : ConsoleMarkup.Dim("no condition")),
                };

                context.Table(rows, 16);
            }
        }

        [ConsoleCommand("interact.candidates", "What the interactor can currently see, and how it scored.",
            Category = Category,
            Description = "Sorted the way the interactor sorts them, so the top row is what it " +
                          "would focus. When the wrong thing is focused, the scores say why.")]
        public static void Candidates(ConsoleContext context)
        {
            Interactor interactor = RequireInteractor();
            interactor.DetectNow();

            IReadOnlyList<InteractionCandidate> candidates = interactor.Candidates;

            if (candidates.Count == 0)
            {
                context.Info("Nothing in range. `interact.list` shows every interactable in the scene.");
                return;
            }

            var rows = new List<KeyValuePair<string, string>>();
            foreach (InteractionCandidate candidate in candidates)
            {
                string name = candidate.interactable != null
                    ? candidate.interactable.DisplayName
                    : "<destroyed>";

                rows.Add(new KeyValuePair<string, string>(name,
                    ConsoleMarkup.Value($"score {candidate.score:0.###}") +
                    ConsoleMarkup.Dim($"  {candidate.distance:0.00} m")));
            }

            context.Heading($"{candidates.Count} candidate(s)");
            context.Table(rows, 26);
        }

        [ConsoleCommand("interact.list", "Every interactable in the scene.", Category = Category)]
        public static void List(
            ConsoleContext context,
            [ConsoleParam("Only names containing this.")] string filter = null)
        {
            IReadOnlyCollection<Interactable> all = InteractableRegistry.All;

            var rows = new List<KeyValuePair<string, string>>();
            foreach (Interactable interactable in all)
            {
                if (interactable == null) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    interactable.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    interactable.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var detail = new System.Text.StringBuilder();
                detail.Append(ConsoleMarkup.Dim($"{interactable.Verbs.Count} verb(s)"));

                if (interactable.PrimaryVerb != null)
                    detail.Append(ConsoleMarkup.Dim("  primary ") + interactable.PrimaryVerb.name);
                else
                    detail.Append(ConsoleMarkup.Warn("  no primary verb"));

                if (!interactable.gameObject.activeInHierarchy) detail.Append(ConsoleMarkup.Warn("  inactive"));

                rows.Add(new KeyValuePair<string, string>(interactable.DisplayName, detail.ToString()));
            }

            context.Heading($"{rows.Count} interactable(s) of {all.Count} registered");
            context.Table(rows, 26);
        }

        [ConsoleCommand("interact.info", "One interactable, its verbs, and why each would refuse.",
            Category = Category,
            Description = "Evaluates every verb against the current interactor right now, so a " +
                          "verb that is failing a condition names the rejection rather than " +
                          "simply not appearing.",
            RequiresSelection = true)]
        public static void Info(ConsoleContext context)
        {
            GameObject go = context.RequireSelection();

            var interactable = go.GetComponent<Interactable>();
            if (interactable == null) interactable = go.GetComponentInParent<Interactable>();

            if (interactable == null)
                throw new ConsoleException($"{go.name} has no Interactable, and neither does any parent.");

            context.Heading(interactable.DisplayName.ToUpperInvariant());
            context.Table(new List<KeyValuePair<string, string>>
            {
                Row("object", interactable.name),
                Row("verbs", interactable.Verbs.Count.ToString()),
                Row("primary", interactable.PrimaryVerb != null
                    ? interactable.PrimaryVerb.name : ConsoleMarkup.Warn("none")),
                Row("range override", interactable.RangeOverride > 0f
                    ? interactable.RangeOverride.ToString("0.##") + " m"
                    : ConsoleMarkup.Dim("uses the interactor's range")),
            }, 16);

            Interactor interactor = Interactor.Active.Count > 0 ? Interactor.Active[0] : null;
            if (interactor == null)
            {
                context.Info("No interactor is active, so the verbs cannot be evaluated.");
                return;
            }

            interactable.RefreshConditions();

            context.Print(string.Empty);
            context.Heading("Verbs, evaluated against " + interactor.name);

            var rows = new List<KeyValuePair<string, string>>();
            foreach (Interaction verb in interactable.Verbs)
            {
                if (verb == null) continue;

                var evaluation = new InteractionContext(
                    interactor, interactable, verb, interactable.InteractionPoint);

                InteractionRejection rejection = interactable.Evaluate(evaluation, out IInteractionCondition blocker);

                string answer = rejection == InteractionRejection.None
                    ? ConsoleMarkup.Good("available")
                    : ConsoleMarkup.Warn(rejection.ToString());
                if (blocker != null) answer += ConsoleMarkup.Dim("  by " + Interactor.Describe(blocker));

                rows.Add(new KeyValuePair<string, string>(verb.name, answer));
            }

            context.Table(rows, 22);
        }

        [ConsoleCommand("interact.do", "Interacts with what is focused, or with the selection.",
            Category = Category,
            Description = "Goes through the interactor, so conditions and the request handler " +
                          "still apply - this triggers an interaction, it does not bypass one.")]
        public static string Do(ConsoleContext context)
        {
            Interactor interactor = RequireInteractor();

            Interactable target = interactor.Focused;

            if (target == null && ConsoleSelection.Current != null)
            {
                target = ConsoleSelection.Current.GetComponent<Interactable>() ??
                         ConsoleSelection.Current.GetComponentInParent<Interactable>();
            }

            if (target == null)
                throw new ConsoleException("Nothing focused, and the selection is not interactable.");

            InteractionRejection rejection = interactor.StartInteraction(target, null);

            return rejection == InteractionRejection.None
                ? $"Interacted with {ConsoleMarkup.Accent(target.DisplayName)}."
                : ConsoleMarkup.Warn($"Refused: {rejection}");
        }

        [ConsoleCommand("interact.detect", "Runs detection now and reports what changed.",
            Category = Category,
            Description = "Detection normally runs on its own schedule. Forcing it is how you " +
                          "check whether a thing that just appeared is being seen.")]
        public static string Detect()
        {
            Interactor interactor = RequireInteractor();

            Interactable before = interactor.Focused;
            interactor.DetectNow();
            Interactable after = interactor.Focused;

            if (ReferenceEquals(before, after))
                return after != null
                    ? $"Still focused on {after.DisplayName}."
                    : "Still focused on nothing.";

            return $"Focus: {(before != null ? before.DisplayName : "nothing")} → " +
                   ConsoleMarkup.Accent(after != null ? after.DisplayName : "nothing");
        }

        private static Interactor RequireInteractor()
        {
            IReadOnlyList<Interactor> active = Interactor.Active;
            if (active.Count == 0) throw new ConsoleException("No active Interactor in the scene.");
            return active[0];
        }

        private static KeyValuePair<string, string> Row(string key, string value) =>
            new KeyValuePair<string, string>(key, value);
    }
}
