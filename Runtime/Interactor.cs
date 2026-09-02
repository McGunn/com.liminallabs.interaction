using System;
using System.Collections.Generic;
using UnityEngine;
using LiminalLabs.Core.Localization;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// An agent that can interact — not "the player": a CRPG puts one on each party
    /// member. Pairs with a pluggable <see cref="InteractionDetector"/>, tracks
    /// focus (best candidate) with change events for prompts and outlines, and owns
    /// the execution pipeline:
    ///
    ///   StartInteraction → validate → hold (if the verb wants one) → request →
    ///   handler seam (<see cref="IInteractionRequestHandler"/>, e.g. walk there
    ///   first) or immediate → Execute (re-validates) → interactable reacts.
    ///
    /// This component reads NO input — the game calls <see cref="StartInteraction()"/>
    /// and <see cref="CancelInteraction"/> from whatever input it owns, which is what
    /// keeps it genre- and device-agnostic. Every failed attempt records WHY in
    /// <see cref="LastRejection"/>, which condition in <see cref="LastBlocker"/>, and the
    /// player-facing reason in <see cref="LastReason"/> when the condition offers one;
    /// nothing fails silently.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Interaction/Interactor")]
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField, Tooltip("How this interactor finds candidates. Empty = the detector on this GameObject.")]
        private InteractionDetector detector;

        [SerializeField, Range(1f, 60f), Tooltip("Detection updates per second. Focus feel stays crisp at 15; crowds of NPCs can go much lower.")]
        private float detectionsPerSecond = 15f;

        [SerializeField, Min(0f), Tooltip("Interactions must happen within this distance of the target (checked at execute time — catches walking away during a handler). 0 = trust the detector's reach.")]
        private float maxInteractDistance = 0f;

        [SerializeField, Tooltip("Position used for range checks. Empty = this transform (a character's feet/center, not the camera).")]
        private Transform rangeOrigin;

        private static readonly List<Interactor> active = new List<Interactor>();

        private readonly List<InteractionCandidate> candidates = new List<InteractionCandidate>(8);
        private float nextDetection;
        private HoldTimer holdTimer;
        private InteractionContext pendingHold;
        private bool holdFollowsFocus;
        private IInteractionRequestHandler requestHandler;
        private bool handlerSearched;
        private IsolatedEvent<Interactable, Interactable> focusChanged;

        /// <summary>All enabled interactors (for the debug overlay and tooling).</summary>
        public static IReadOnlyList<Interactor> Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        /// <summary>This frame's ranked candidates (best first; equal scores keep the
        /// detector's order). Presenters read this for verb menus and multi-target UI.</summary>
        public IReadOnlyList<InteractionCandidate> Candidates => candidates;

        /// <summary>The best current candidate, or null.</summary>
        public Interactable Focused { get; private set; }

        /// <summary>Details of the focused candidate (valid while Focused != null).</summary>
        public InteractionCandidate FocusedCandidate { get; private set; }

        /// <summary>Why the most recent attempt didn't happen (None after a success).</summary>
        public InteractionRejection LastRejection { get; private set; }

        /// <summary>
        /// The condition that refused the most recent attempt, when <see cref="LastRejection"/>
        /// is <see cref="InteractionRejection.VerbUnavailable"/>; null otherwise. The thing a
        /// prompt names, a designer selects, and a game casts to ask for a reason.
        /// </summary>
        public IInteractionCondition LastBlocker { get; private set; }

        /// <summary>
        /// Why the most recent attempt was refused, in the player's language, when the
        /// condition that refused it offers one through <see cref="IInteractionRefusal"/>;
        /// null otherwise. What a prompt shows under "Locked". For the reason of a focus the
        /// player has not pressed on yet, see <see cref="ReasonOf"/>.
        /// </summary>
        public LocalizedText LastReason { get; private set; }

        /// <summary><see cref="LastReason"/> resolved for the current locale, or null. For a
        /// presenter that only wants the words.</summary>
        public string LastReasonText
        {
            get
            {
                string text = LastReason != null ? LastReason.GetLocalized() : null;
                return string.IsNullOrEmpty(text) ? null : text;
            }
        }

        /// <summary>Hold progress 0–1 while a hold-to-interact is running, else 0.</summary>
        public float HoldProgress01 => holdTimer.Progress01;

        public bool IsHolding => holdTimer.IsActive;

        /// <summary>Raised when focus changes: (previous, next); either may be null. Listeners
        /// are called one at a time; one that throws is logged and the rest still run.</summary>
        public event Action<Interactable, Interactable> FocusChanged
        {
            add => focusChanged.Add(value);
            remove => focusChanged.Remove(value);
        }

        public Vector3 RangePosition => rangeOrigin != null ? rangeOrigin.position : transform.position;

        public InteractionDetector Detector
        {
            get
            {
                if (detector == null) detector = GetComponent<InteractionDetector>();
                return detector;
            }
            set => detector = value;
        }

        /// <summary>The execution seam (see <see cref="IInteractionRequestHandler"/>).
        /// Auto-found on this GameObject; settable from code.</summary>
        public IInteractionRequestHandler RequestHandler
        {
            get
            {
                if (!handlerSearched && requestHandler == null)
                {
                    handlerSearched = true;
                    requestHandler = GetComponent<IInteractionRequestHandler>();
                }
                return requestHandler;
            }
            set
            {
                requestHandler = value;
                handlerSearched = true;
            }
        }

        void OnEnable()
        {
            active.Add(this);
            nextDetection = 0f;   // detect immediately
        }

        void OnDisable()
        {
            active.Remove(this);
            CancelInteraction();
            SetFocus(null, default);
        }

        void Update() => Tick(Time.unscaledTime, Time.deltaTime);

        /// <summary>
        /// One frame of the interactor, with time passed in rather than read - so the pipeline
        /// can be driven without a frame. The tests do; so can a game that wants interaction
        /// to stand still while a menu is up.
        /// </summary>
        internal void Tick(float now, float deltaTime)
        {
            if (now >= nextDetection)
            {
                nextDetection = now + DetectionInterval;
                Detect();
            }

            if (!holdTimer.IsActive) return;

            // A hold breaks when its target stops being valid, and - when it was started on
            // whatever was focused - when focus moves off it. A hold started on an explicit
            // target (a verb menu, a CRPG context menu) does not depend on focus at all: the
            // player chose that target by name, and looking elsewhere while holding is not a
            // change of mind. Either way the reason is recorded, like any other refusal.
            if (holdFollowsFocus && pendingHold.interactable != Focused)
            {
                CancelInteraction();
                Record(InteractionRejection.FocusLost, null);
                return;
            }

            InteractionRejection rejection = Validate(pendingHold, out IInteractionCondition blocker);
            if (rejection != InteractionRejection.None)
            {
                CancelInteraction();
                Record(rejection, blocker);
                return;
            }

            if (holdTimer.Tick(deltaTime))
            {
                InteractionContext context = pendingHold;
                pendingHold = default;
                holdFollowsFocus = false;
                Dispatch(context);
            }
        }

        private float DetectionInterval => 1f / Mathf.Max(1f, detectionsPerSecond);

        /// <summary>Runs a detection pass now rather than at the next scheduled one — right
        /// after teleporting, or when something just appeared and a prompt should not wait a
        /// tick for it. The regular cadence resumes from here.</summary>
        public void DetectNow()
        {
            nextDetection = Time.unscaledTime + DetectionInterval;
            Detect();
        }

        private void Detect()
        {
            candidates.Clear();
            InteractionDetector activeDetector = Detector;
            if (activeDetector != null && activeDetector.isActiveAndEnabled)
            {
                activeDetector.GatherCandidates(this, candidates);
                if (candidates.Count > 1) InteractionScoring.SortByScoreDescending(candidates);
            }

            if (candidates.Count > 0) SetFocus(candidates[0].interactable, candidates[0]);
            else SetFocus(null, default);
        }

        private void SetFocus(Interactable next, InteractionCandidate candidate)
        {
            Interactable previous = Focused;
            FocusedCandidate = candidate;
            if (previous == next) return;

            Focused = next;
            if (previous != null) previous.NotifyFocus(this, gained: false);
            if (next != null) next.NotifyFocus(this, gained: true);
            focusChanged.Invoke(previous, next, "FocusChanged", this);
        }

        // ---- the pipeline -----------------------------------------------------------

        /// <summary>Begin interacting with the current focus using its primary verb.
        /// Call from your input's press; pair with <see cref="CancelInteraction"/> on
        /// release for hold-to-interact verbs. A hold started this way breaks if focus
        /// moves off the target.</summary>
        public InteractionRejection StartInteraction() => Begin(Focused, null, holdFollowsFocus: true);

        /// <summary>Begin interacting with a specific target/verb (verb menus, CRPG
        /// context menus). Null verb = the target's primary verb. A hold started this way
        /// does not care what is focused: the target was chosen explicitly.</summary>
        public InteractionRejection StartInteraction(Interactable target, Interaction verb) =>
            Begin(target, verb, holdFollowsFocus: false);

        private InteractionRejection Begin(Interactable target, Interaction verb, bool holdFollowsFocus)
        {
            CancelInteraction();

            InteractionContext context = BuildContext(target, verb);
            InteractionRejection rejection = Validate(context, out IInteractionCondition blocker);

            // Out of range is special: with a request handler present, "valid but too
            // far" is exactly the handler's job (walk there, then Execute) — so it
            // dispatches instead of rejecting. Every other rejection stands.
            if (rejection == InteractionRejection.OutOfRange && RequestHandler != null)
            {
                return Dispatch(context);
            }
            if (rejection != InteractionRejection.None)
            {
                return Record(rejection, blocker);
            }

            if (context.verb.HoldSeconds > 0f)
            {
                pendingHold = context;
                this.holdFollowsFocus = holdFollowsFocus;
                holdTimer.Begin(context.verb.HoldSeconds);
                return Record(InteractionRejection.None, null);
            }
            return Dispatch(context);
        }

        /// <summary>Cancels an in-progress hold (call from your input's release).</summary>
        public void CancelInteraction()
        {
            holdTimer.Cancel();
            pendingHold = default;
            holdFollowsFocus = false;
        }

        /// <summary>Executes a validated context — the completion call for request
        /// handlers (re-validates first, so walking away during the walk-to still
        /// rejects properly).</summary>
        public InteractionRejection Execute(in InteractionContext context)
        {
            InteractionRejection rejection = Validate(context, out IInteractionCondition blocker);
            Record(rejection, blocker);
            if (rejection != InteractionRejection.None) return rejection;

            context.interactable.HandleInteracted(context);
            return InteractionRejection.None;
        }

        private InteractionRejection Dispatch(in InteractionContext context)
        {
            IInteractionRequestHandler handler = RequestHandler;
            if (handler != null)
            {
                Record(InteractionRejection.None, null);
                handler.HandleRequest(context);   // the game completes via Execute
                return InteractionRejection.None;
            }
            return Execute(context);
        }

        private InteractionRejection Record(InteractionRejection rejection, IInteractionCondition blocker)
        {
            LastRejection = rejection;
            LastBlocker = rejection == InteractionRejection.VerbUnavailable ? blocker : null;
            LastReason = ReasonOf(LastBlocker);
            return rejection;
        }

        private InteractionContext BuildContext(Interactable target, Interaction verb)
        {
            if (target == null) return default;
            if (verb == null) verb = target.PrimaryVerb;
            Vector3 point = target == Focused && FocusedCandidate.interactable == target
                ? FocusedCandidate.point
                : target.InteractionPoint;
            return new InteractionContext(this, target, verb, point);
        }

        /// <summary>Full validation for a context, in rejection-priority order.</summary>
        public InteractionRejection Validate(in InteractionContext context) => Validate(context, out _);

        /// <summary><see cref="Validate(in InteractionContext)"/>, also naming the condition
        /// that refused when the answer is <see cref="InteractionRejection.VerbUnavailable"/>.</summary>
        public InteractionRejection Validate(in InteractionContext context, out IInteractionCondition blocker)
        {
            blocker = null;
            if (context.interactable == null) return InteractionRejection.NoTarget;
            if (context.verb == null) return InteractionRejection.NoVerb;

            if (maxInteractDistance > 0f)
            {
                float distance = Vector3.Distance(RangePosition, context.interactable.InteractionPoint);
                if (distance > maxInteractDistance) return InteractionRejection.OutOfRange;
            }
            return context.interactable.Evaluate(context, out blocker);
        }

        /// <summary>A condition, named the way a designer would find it: the component type
        /// and the object it sits on.</summary>
        public static string Describe(IInteractionCondition condition)
        {
            if (condition == null) return "none";
            if (condition is Component component && component != null)
                return component.GetType().Name + " on " + component.name;
            return condition.GetType().Name;
        }

        /// <summary>The reason a condition gives for refusing, or null when it gives none.
        /// For a prompt that validates the focus before the player presses anything:
        /// <c>Validate(context, out var blocker)</c>, then this.</summary>
        public static LocalizedText ReasonOf(IInteractionCondition condition) =>
            condition is IInteractionRefusal refusal ? refusal.Reason : null;
    }
}
