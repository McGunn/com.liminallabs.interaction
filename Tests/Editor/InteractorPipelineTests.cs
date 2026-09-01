using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LiminalLabs.Interaction.Tests
{
    /// <summary>
    /// The interactor driven a frame at a time, without play mode: detection on demand,
    /// stable ranking, holds on explicit targets, which condition refused, listeners that
    /// cannot stop one another, and a registry that forgets a miss when it stops being one.
    /// </summary>
    public class InteractorPipelineTests
    {
        private readonly List<Object> created = new List<Object>();

        /// <summary>A detector that reports exactly what the test told it to.</summary>
        private class ScriptedDetector : InteractionDetector
        {
            public readonly List<InteractionCandidate> report = new List<InteractionCandidate>();

            public override void GatherCandidates(Interactor interactor, List<InteractionCandidate> results) =>
                results.AddRange(report);

            public void Offer(Interactable interactable, float score) => report.Add(new InteractionCandidate
            {
                interactable = interactable,
                score = score,
                distance = 1f,
                point = interactable.InteractionPoint,
            });
        }

        private class BlockingCondition : MonoBehaviour, IInteractionCondition
        {
            public bool available;
            public bool IsAvailable(in InteractionContext context) => available;
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo info = null;
            for (var type = target.GetType(); type != null && info == null; type = type.BaseType)
            {
                info = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            }
            Assert.IsNotNull(info, $"field '{field}' not found on {target.GetType().Name}");
            info.SetValue(target, value);
        }

        private Interaction Verb(string name, float holdSeconds = 0f)
        {
            var verb = ScriptableObject.CreateInstance<Interaction>();
            verb.name = name;
            SetField(verb, "holdSeconds", holdSeconds);
            created.Add(verb);
            return verb;
        }

        private GameObject Go(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private Interactable Target(string name, params Interaction[] verbs)
        {
            Interactable interactable = Go(name).AddComponent<Interactable>();
            SetField(interactable, "verbs", new List<Interaction>(verbs));
            return interactable;
        }

        private Interactor Agent(out ScriptedDetector detector)
        {
            GameObject go = Go("Agent");
            detector = go.AddComponent<ScriptedDetector>();
            return go.AddComponent<Interactor>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        // ---- detection --------------------------------------------------------------

        [Test]
        public void DetectNow_DetectsNow()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Agent(out ScriptedDetector detector);
            detector.Offer(door, 1f);

            Assert.IsNull(interactor.Focused, "nothing has looked yet");
            interactor.DetectNow();
            Assert.AreEqual(door, interactor.Focused, "now means now, not at the next scheduled pass");
            Assert.AreEqual(1, interactor.Candidates.Count);
        }

        [Test]
        public void Ranking_IsBestFirst_AndStableAmongEqualScores()
        {
            Interaction open = Verb("Open");
            Interactable a = Target("A", open), b = Target("B", open), c = Target("C", open);
            Interactor interactor = Agent(out ScriptedDetector detector);
            detector.Offer(a, 0.5f);
            detector.Offer(b, 0.9f);
            detector.Offer(c, 0.5f);

            interactor.DetectNow();

            Assert.AreEqual(b, interactor.Candidates[0].interactable, "best first");
            Assert.AreEqual(a, interactor.Candidates[1].interactable, "ties keep the detector's order");
            Assert.AreEqual(c, interactor.Candidates[2].interactable);
            Assert.AreEqual(b, interactor.Focused);
        }

        [Test]
        public void Sort_IsStableAndDescending()
        {
            var list = new List<InteractionCandidate>
            {
                Candidate(0.5f, 1), Candidate(0.9f, 2), Candidate(0.5f, 3), Candidate(0.1f, 4), Candidate(0.9f, 5),
            };

            InteractionScoring.SortByScoreDescending(list);

            var order = new int[list.Count];
            for (int i = 0; i < list.Count; i++) order[i] = (int)list[i].distance;
            CollectionAssert.AreEqual(new[] { 2, 5, 1, 3, 4 }, order, "descending by score, equal scores in original order");
        }

        private static InteractionCandidate Candidate(float score, int tag) =>
            new InteractionCandidate { score = score, distance = tag };

        // ---- holds ------------------------------------------------------------------

        [Test]
        public void Hold_OnAnExplicitTarget_DoesNotNeedFocus()
        {
            Interaction pickUp = Verb("Pick Up", holdSeconds: 1f);
            Interactable gem = Target("Gem", pickUp);
            Interactor interactor = Agent(out _);
            int interacted = 0;
            gem.Interacted += _ => interacted++;

            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction(gem, pickUp));
            Assert.IsTrue(interactor.IsHolding);
            Assert.IsNull(interactor.Focused, "the gem was chosen by name, not by looking at it");

            interactor.Tick(0f, 0.5f);
            Assert.IsTrue(interactor.IsHolding, "no focus is no reason to drop a hold on a chosen target");
            Assert.AreEqual(0.5f, interactor.HoldProgress01, 0.001f);

            interactor.Tick(0f, 0.5f);
            Assert.AreEqual(1, interacted, "the hold completed");
            Assert.IsFalse(interactor.IsHolding);
            Assert.AreEqual(InteractionRejection.None, interactor.LastRejection);
        }

        [Test]
        public void Hold_OnTheFocus_BreaksWhenFocusMoves_AndSaysSo()
        {
            Interaction pickUp = Verb("Pick Up", holdSeconds: 1f);
            Interactable gem = Target("Gem", pickUp);
            Interactor interactor = Agent(out ScriptedDetector detector);
            detector.Offer(gem, 1f);
            interactor.DetectNow();
            Assert.AreEqual(gem, interactor.Focused);

            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction());
            interactor.Tick(0f, 0.3f);   // before the next scheduled detection: focus stands, the hold runs
            Assert.IsTrue(interactor.IsHolding);

            detector.report.Clear();     // the player looked away
            interactor.Tick(float.MaxValue, 0.3f);   // a detection pass is due: nothing in view
            Assert.IsNull(interactor.Focused);
            Assert.IsFalse(interactor.IsHolding, "a hold on the focus goes with the focus");
            Assert.AreEqual(InteractionRejection.FocusLost, interactor.LastRejection, "and the reason is recorded");
        }

        [Test]
        public void Hold_BreaksWhenValidationFails_AndNamesTheCondition()
        {
            Interaction pickUp = Verb("Pick Up", holdSeconds: 1f);
            Interactable gem = Target("Gem", pickUp);
            BlockingCondition rule = gem.gameObject.AddComponent<BlockingCondition>();
            rule.available = true;
            gem.RefreshConditions();
            Interactor interactor = Agent(out _);

            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction(gem, pickUp));
            rule.available = false;      // it locked while the player was holding
            interactor.Tick(0f, 0.2f);

            Assert.IsFalse(interactor.IsHolding);
            Assert.AreEqual(InteractionRejection.VerbUnavailable, interactor.LastRejection);
            Assert.AreSame(rule, interactor.LastBlocker);
        }

        // ---- which condition ----------------------------------------------------------

        [Test]
        public void Rejection_NamesTheConditionThatRefused()
        {
            Interaction open = Verb("Open");
            Interactable chest = Target("Chest", open);
            BlockingCondition first = chest.gameObject.AddComponent<BlockingCondition>();
            BlockingCondition second = chest.gameObject.AddComponent<BlockingCondition>();
            first.available = true;
            second.available = false;
            chest.RefreshConditions();
            Interactor interactor = Agent(out _);

            var context = new InteractionContext(interactor, chest, open, chest.InteractionPoint);
            Assert.AreEqual(InteractionRejection.VerbUnavailable, chest.Evaluate(context, out IInteractionCondition blocker));
            Assert.AreSame(second, blocker, "the one that said no, not the first one asked");

            Assert.AreEqual(InteractionRejection.VerbUnavailable, interactor.StartInteraction(chest, open));
            Assert.AreSame(second, interactor.LastBlocker);

            second.available = true;
            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction(chest, open));
            Assert.IsNull(interactor.LastBlocker, "cleared with the rejection");
        }

        [Test]
        public void Blocker_IsOnlyEverAConditionRefusal()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            BlockingCondition rule = door.gameObject.AddComponent<BlockingCondition>();
            rule.available = false;
            door.RefreshConditions();
            Interactor interactor = Agent(out _);

            door.gameObject.SetActive(false);
            Assert.AreEqual(InteractionRejection.TargetDisabled, interactor.StartInteraction(door, open),
                "disabled outranks the condition");
            Assert.IsNull(interactor.LastBlocker, "so no condition is blamed");
        }

        [Test]
        public void DisabledConditions_DoNotGate()
        {
            Interaction open = Verb("Open");
            Interactable chest = Target("Chest", open);
            BlockingCondition rule = chest.gameObject.AddComponent<BlockingCondition>();
            rule.available = false;
            chest.RefreshConditions();
            var context = new InteractionContext(null, chest, open, default);

            Assert.AreEqual(InteractionRejection.VerbUnavailable, chest.Evaluate(context));

            rule.enabled = false;
            Assert.AreEqual(InteractionRejection.None, chest.Evaluate(context),
                "a switched-off rule is a rule that is not there, with no RefreshConditions needed");

            rule.enabled = true;
            Assert.AreEqual(InteractionRejection.VerbUnavailable, chest.Evaluate(context));
        }

        [Test]
        public void Describe_NamesTheComponentAndItsObject()
        {
            Interactable chest = Target("Chest");
            BlockingCondition rule = chest.gameObject.AddComponent<BlockingCondition>();

            Assert.AreEqual("BlockingCondition on Chest", Interactor.Describe(rule));
            Assert.AreEqual("none", Interactor.Describe(null));
        }

        // ---- listeners --------------------------------------------------------------

        [Test]
        public void FocusListeners_AreExceptionIsolated()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Agent(out ScriptedDetector detector);

            bool gainedRan = false, changedRan = false;
            door.FocusGained += _ => throw new System.InvalidOperationException("boom");
            door.FocusGained += _ => gainedRan = true;
            interactor.FocusChanged += (previous, next) => throw new System.InvalidOperationException("boom");
            interactor.FocusChanged += (previous, next) => changedRan = true;

            detector.Offer(door, 1f);
            LogAssert.Expect(LogType.Error, new Regex("FocusGained listener threw"));
            LogAssert.Expect(LogType.Error, new Regex("FocusChanged listener threw"));
            interactor.DetectNow();

            Assert.IsTrue(gainedRan, "the interactable's second listener still ran");
            Assert.IsTrue(changedRan, "and so did the interactor's");
        }

        [Test]
        public void AListenerThatUnsubscribesWhileBeingInvoked_IsSafe()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Agent(out _);

            int firstCalls = 0, secondCalls = 0;
            System.Action<InteractionContext> first = null;
            first = _ => { firstCalls++; door.Interacted -= first; };
            door.Interacted += first;
            door.Interacted += _ => secondCalls++;

            interactor.StartInteraction(door, open);
            interactor.StartInteraction(door, open);

            Assert.AreEqual(1, firstCalls, "gone after removing itself");
            Assert.AreEqual(2, secondCalls, "the listener after it was not skipped by the removal");
        }

        // ---- registry ---------------------------------------------------------------

        [Test]
        public void Registry_FindsAnInteractableAddedAfterAMiss()
        {
            GameObject prop = Go("Prop");
            var collider = prop.AddComponent<BoxCollider>();

            Assert.IsNull(InteractableRegistry.Resolve(collider), "a plain prop is not interactable");

            Interactable added = prop.AddComponent<Interactable>();
            InteractableRegistry.Register(added);   // what OnEnable does in play mode
            try
            {
                Assert.AreEqual(added, InteractableRegistry.Resolve(collider),
                    "the cached miss must not outlive the reason for it");
            }
            finally
            {
                InteractableRegistry.Unregister(added);
            }
        }
    }
}
