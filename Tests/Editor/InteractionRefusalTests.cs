using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using LiminalLabs.Core.Localization;

namespace LiminalLabs.Interaction.Tests
{
    /// <summary>
    /// A condition that says why. The reason reaches the interactor with the refusal, is
    /// cleared with it, and can be asked for before any attempt - which is how a prompt
    /// shows "Locked — needs a key" while the player is still deciding.
    /// </summary>
    public class InteractionRefusalTests
    {
        private readonly List<Object> created = new List<Object>();

        private class ReasonedCondition : MonoBehaviour, IInteractionCondition, IInteractionRefusal
        {
            public bool available;
            public LocalizedText reason = new LocalizedText("Needs a key");

            public bool IsAvailable(in InteractionContext context) => available;
            public LocalizedText Reason => reason;
        }

        private class MuteCondition : MonoBehaviour, IInteractionCondition
        {
            public bool IsAvailable(in InteractionContext context) => false;
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

        private Interaction Verb(string name)
        {
            var verb = ScriptableObject.CreateInstance<Interaction>();
            verb.name = name;
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

        [Test]
        public void ARefusalWithAReason_IsRecordedWithTheRejection_AndClearedWithIt()
        {
            Interaction open = Verb("Open");
            Interactable chest = Target("Chest", open);
            ReasonedCondition lockRule = chest.gameObject.AddComponent<ReasonedCondition>();
            lockRule.available = false;
            chest.RefreshConditions();
            Interactor interactor = Go("Agent").AddComponent<Interactor>();

            Assert.AreEqual(InteractionRejection.VerbUnavailable, interactor.StartInteraction(chest, open));
            Assert.AreSame(lockRule, interactor.LastBlocker);
            Assert.IsNotNull(interactor.LastReason);
            Assert.AreEqual("Needs a key", interactor.LastReason.GetLocalized());

            lockRule.available = true;
            Assert.AreEqual(InteractionRejection.None, interactor.StartInteraction(chest, open));
            Assert.IsNull(interactor.LastReason, "cleared with the rejection, like the blocker");
        }

        [Test]
        public void AConditionWithoutAReason_LeavesTheReasonNull()
        {
            Interaction open = Verb("Open");
            Interactable door = Target("Door", open);
            MuteCondition rule = door.gameObject.AddComponent<MuteCondition>();
            door.RefreshConditions();
            Interactor interactor = Go("Agent").AddComponent<Interactor>();

            Assert.AreEqual(InteractionRejection.VerbUnavailable, interactor.StartInteraction(door, open));
            Assert.AreSame(rule, interactor.LastBlocker, "the condition is still named");
            Assert.IsNull(interactor.LastReason, "it just has nothing to tell the player");
        }

        [Test]
        public void ReasonOf_AnswersBeforeAnyAttempt()
        {
            Interaction open = Verb("Open");
            Interactable chest = Target("Chest", open);
            ReasonedCondition lockRule = chest.gameObject.AddComponent<ReasonedCondition>();
            lockRule.available = false;
            chest.RefreshConditions();
            Interactor interactor = Go("Agent").AddComponent<Interactor>();

            var context = new InteractionContext(interactor, chest, open, chest.InteractionPoint);
            Assert.AreEqual(InteractionRejection.VerbUnavailable,
                            interactor.Validate(context, out IInteractionCondition blocker));
            Assert.AreEqual("Needs a key", Interactor.ReasonOf(blocker).GetLocalized());
            Assert.AreEqual(InteractionRejection.None, interactor.LastRejection, "asking is not attempting");

            Assert.IsNull(Interactor.ReasonOf(null));
            Assert.IsNull(Interactor.ReasonOf(chest.gameObject.AddComponent<MuteCondition>()));
        }
    }
}
