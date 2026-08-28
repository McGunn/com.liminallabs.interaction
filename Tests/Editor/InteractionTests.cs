using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LiminalLabs.Interaction.Tests
{
    /// <summary>
    /// Pins the decided semantics: primary-verb selection, proximity scoring shape,
    /// hold-timer behavior, validation rejection order, the request-handler seam,
    /// and listener exception isolation. A red test is a bug or a breaking change.
    /// </summary>
    public class InteractionTests
    {
        private readonly List<Object> created = new List<Object>();

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

        private Interaction Verb(string name, int sortOrder = 0, float holdSeconds = 0f)
        {
            var verb = ScriptableObject.CreateInstance<Interaction>();
            verb.name = name;
            SetField(verb, "sortOrder", sortOrder);
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

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        // ---- verbs ------------------------------------------------------------------

        [Test]
        public void PrimaryVerb_LowestSortOrder_TiesByListOrder()
        {
            Interaction open = Verb("Open", 0), talk = Verb("Talk", 0), examine = Verb("Examine", 5);
            Assert.AreEqual(open, Interaction.SelectPrimary(new[] { examine, open, talk }),
                "lowest sort order wins; among ties, earliest in the list");
            Assert.AreEqual(talk, Interaction.SelectPrimary(new[] { examine, talk, open }));
            Assert.IsNull(Interaction.SelectPrimary(new Interaction[] { null, null }));
        }

        // ---- proximity scoring ------------------------------------------------------

        [Test]
        public void ProximityScore_CloserAndMoreCenteredWins()
        {
            float near = InteractionScoring.ProximityScore(1f, 5f, 0f, 90f, 0.5f);
            float far = InteractionScoring.ProximityScore(4f, 5f, 0f, 90f, 0.5f);
            Assert.Greater(near, far, "closer scores higher");

            float centered = InteractionScoring.ProximityScore(2f, 5f, 5f, 90f, 0.5f);
            float offAngle = InteractionScoring.ProximityScore(2f, 5f, 80f, 90f, 0.5f);
            Assert.Greater(centered, offAngle, "more centered scores higher");
        }

        [Test]
        public void ProximityScore_Limits()
        {
            Assert.AreEqual(0f, InteractionScoring.ProximityScore(6f, 5f, 0f, 90f, 0.5f), "beyond max distance");
            Assert.AreEqual(0f, InteractionScoring.ProximityScore(1f, 5f, 120f, 90f, 0.5f), "beyond angle limit");
            Assert.AreEqual(
                InteractionScoring.ProximityScore(2f, 5f, 10f, 90f, 0f),
                InteractionScoring.ProximityScore(2f, 5f, 80f, 90f, 0f),
                "facingWeight 0 ignores angle within the limit");
        }

        [Test]
        public void RingOffsets_AreDeterministicOnTheRadius()
        {
            var a = new Vector2[8];
            var b = new Vector2[8];
            InteractionScoring.BuildRingOffsets(8, 25f, a);
            InteractionScoring.BuildRingOffsets(8, 25f, b);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(a[i], b[i], "deterministic");
                Assert.AreEqual(25f, a[i].magnitude, 0.001f, "on the radius");
            }
        }

        // ---- hold timer -------------------------------------------------------------

        [Test]
        public void HoldTimer_ProgressesAndCompletesOnce()
        {
            var timer = new HoldTimer();
            timer.Begin(1f);
            Assert.IsFalse(timer.Tick(0.5f));
            Assert.AreEqual(0.5f, timer.Progress01, 0.001f);
            Assert.IsTrue(timer.Tick(0.5f), "completes exactly at the duration");
            Assert.IsFalse(timer.Tick(0.5f), "completes only once");
            Assert.IsFalse(timer.IsActive);
        }

        [Test]
        public void HoldTimer_CancelAndInstant()
        {
            var timer = new HoldTimer();
            timer.Begin(1f);
            timer.Cancel();
            Assert.IsFalse(timer.Tick(2f), "cancelled holds never complete");

            timer.Begin(0f);
            Assert.IsTrue(timer.Tick(0f), "zero-duration completes on the first tick");
        }

        // ---- validation -------------------------------------------------------------

        [Test]
        public void Evaluate_RejectsWithTheRightReason()
        {
            Interaction open = Verb("Open");
            Interaction talk = Verb("Talk");
            Interactable door = Target("Door", open);

            Assert.AreEqual(InteractionRejection.None, door.Evaluate(new InteractionContext(null, door, open, default)));
            Assert.AreEqual(InteractionRejection.VerbNotOffered, door.Evaluate(new InteractionContext(null, door, talk, default)));
            Assert.AreEqual(InteractionRejection.NoVerb, door.Evaluate(new InteractionContext(null, door, null, default)));

            door.gameObject.SetActive(false);
            Assert.AreEqual(InteractionRejection.TargetDisabled, door.Evaluate(new InteractionContext(null, door, open, default)));
        }

        [Test]
        public void Evaluate_RangeOverride_ChecksInteractorDistance()
        {
            Interaction open = Verb("Open");
            Interactable keypad = Target("Keypad", open);
            SetField(keypad, "rangeOverride", 1.5f);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();

            interactor.transform.position = new Vector3(0, 0, 1f);
            Assert.AreEqual(InteractionRejection.None, keypad.Evaluate(new InteractionContext(interactor, keypad, open, default)));

            interactor.transform.position = new Vector3(0, 0, 3f);
            Assert.AreEqual(InteractionRejection.OutOfRange, keypad.Evaluate(new InteractionContext(interactor, keypad, open, default)));
        }

        private class BlockingCondition : MonoBehaviour, IInteractionCondition
        {
            public bool available;
            public bool IsAvailable(in InteractionContext context) => available;
        }

        [Test]
        public void Evaluate_ConditionsGateAvailability()
        {
            Interaction open = Verb("Open");
            Interactable chest = Target("Chest", open);
            BlockingCondition condition = chest.gameObject.AddComponent<BlockingCondition>();
            chest.RefreshConditions();

            condition.available = false;
            Assert.AreEqual(InteractionRejection.VerbUnavailable, chest.Evaluate(new InteractionContext(null, chest, open, default)));
            condition.available = true;
            Assert.AreEqual(InteractionRejection.None, chest.Evaluate(new InteractionContext(null, chest, open, default)));
        }

        // ---- the pipeline -----------------------------------------------------------

        private class CapturingHandler : IInteractionRequestHandler
        {
            public InteractionContext captured;
            public int calls;
            public void HandleRequest(in InteractionContext context) { captured = context; calls++; }
        }

        [Test]
        public void StartInteraction_ExecutesImmediately_WithoutHandler()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();

            int interacted = 0;
            door.Interacted += _ => interacted++;

            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction(door, null));
            Assert.AreEqual(1, interacted, "instant verb executes on the spot; null verb resolves to primary");
            Assert.AreEqual(InteractionRejection.None, interactor.LastRejection);
        }

        [Test]
        public void RequestHandler_InterceptsExecution_AndCompletesViaExecute()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();
            var handler = new CapturingHandler();
            interactor.RequestHandler = handler;

            int interacted = 0;
            door.Interacted += _ => interacted++;

            interactor.StartInteraction(door, open);
            Assert.AreEqual(1, handler.calls, "the handler received the validated request");
            Assert.AreEqual(0, interacted, "nothing executes until the handler completes");

            Assert.AreEqual(InteractionRejection.None, interactor.Execute(handler.captured));
            Assert.AreEqual(1, interacted, "Execute is the handler's completion call");
        }

        [Test]
        public void Execute_Revalidates_SoStaleRequestsReject()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();
            var handler = new CapturingHandler();
            interactor.RequestHandler = handler;

            interactor.StartInteraction(door, open);
            door.gameObject.SetActive(false);   // the world changed during the walk-to
            Assert.AreEqual(InteractionRejection.TargetDisabled, interactor.Execute(handler.captured));
            Assert.AreEqual(InteractionRejection.TargetDisabled, interactor.LastRejection);
        }

        [Test]
        public void OutOfRange_WithHandler_DefersInsteadOfRejecting()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            door.transform.position = new Vector3(0f, 0f, 20f);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();
            SetField(interactor, "maxInteractDistance", 2f);
            var handler = new CapturingHandler();
            interactor.RequestHandler = handler;

            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction(door, open),
                "too far + handler = the handler's job, not a rejection");
            Assert.AreEqual(1, handler.calls);

            Assert.AreEqual(InteractionRejection.OutOfRange, interactor.Execute(handler.captured),
                "Execute still enforces range — the handler must actually arrive first");

            interactor.transform.position = new Vector3(0f, 0f, 19f);
            Assert.AreEqual(InteractionRejection.None, interactor.Execute(handler.captured));
        }

        [Test]
        public void OutOfRange_WithoutHandler_Rejects()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            door.transform.position = new Vector3(0f, 0f, 20f);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();
            SetField(interactor, "maxInteractDistance", 2f);

            Assert.AreEqual(InteractionRejection.OutOfRange, interactor.StartInteraction(door, open));
        }

        [Test]
        public void Rejections_AreRecorded()
        {
            Interactor interactor = Go("Agent").AddComponent<Interactor>();
            Assert.AreEqual(InteractionRejection.NoTarget, interactor.StartInteraction(null, null));
            Assert.AreEqual(InteractionRejection.NoTarget, interactor.LastRejection);

            Interactable empty = Target("Empty");   // no verbs
            Assert.AreEqual(InteractionRejection.NoVerb, interactor.StartInteraction(empty, null));
        }

        [Test]
        public void InteractedListeners_AreExceptionIsolated()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            Interactor interactor = Go("Agent").AddComponent<Interactor>();

            bool second = false;
            door.Interacted += _ => throw new System.InvalidOperationException("boom");
            door.Interacted += _ => second = true;

            LogAssert.Expect(LogType.Error, new Regex("threw"));
            interactor.StartInteraction(door, open);
            Assert.IsTrue(second, "the listener after the throwing one still runs");
        }
    }
}
