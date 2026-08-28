using System;
using System.Collections.Generic;
using UnityEngine;

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
    /// <see cref="LastRejection"/>; nothing fails silently.
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
        private static readonly Comparison<InteractionCandidate> ByScoreDescending =
            (a, b) => b.score.CompareTo(a.score);

        private readonly List<InteractionCandidate> candidates = new List<InteractionCandidate>(8);
        private float nextDetection;
        private HoldTimer holdTimer;
        private InteractionContext pendingHold;
        private IInteractionRequestHandler requestHandler;
        private bool handlerSearched;

        /// <summary>All enabled interactors (for the debug overlay and tooling).</summary>
        public static IReadOnlyList<Interactor> Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        /// <summary>This frame's ranked candidates (best first). Presenters read this
        /// for verb menus and multi-target UI.</summary>
        public IReadOnlyList<InteractionCandidate> Candidates => candidates;

        /// <summary>The best current candidate, or null.</summary>
        public Interactable Focused { get; private set; }

        /// <summary>Details of the focused candidate (valid while Focused != null).</summary>
        public InteractionCandidate FocusedCandidate { get; private set; }

        /// <summary>Why the most recent attempt didn't happen (None after a success).</summary>
        public InteractionRejection LastRejection { get; private set; }

        /// <summary>Hold progress 0–1 while a hold-to-interact is running, else 0.</summary>
        public float HoldProgress01 => holdTimer.Progress01;

        public bool IsHolding => holdTimer.IsActive;

        /// <summary>Raised when focus changes: (previous, next); either may be null.</summary>
        public event Action<Interactable, Interactable> FocusChanged;

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

        void Update()
        {
            if (Time.unscaledTime >= nextDetection)
            {
                nextDetection = Time.unscaledTime + 1f / Mathf.Max(1f, detectionsPerSecond);
                Detect();
            }

            if (holdTimer.IsActive)
            {
                // A hold breaks if the target stops being focused or becomes invalid.
                if (pendingHold.interactable != Focused || Validate(pendingHold) != InteractionRejection.None)
                {
                    CancelInteraction();
                }
                else if (holdTimer.Tick(Time.deltaTime))
                {
                    InteractionContext context = pendingHold;
                    pendingHold = default;
                    Dispatch(context);
                }
            }
        }

        /// <summary>Forces a detection pass now (e.g. right after teleporting).</summary>
        public void DetectNow()
        {
            nextDetection = 0f;
        }

        private void Detect()
        {
            candidates.Clear();
            InteractionDetector activeDetector = Detector;
            if (activeDetector != null && activeDetector.isActiveAndEnabled)
            {
                activeDetector.GatherCandidates(this, candidates);
                if (candidates.Count > 1) candidates.Sort(ByScoreDescending);
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
            try
            {
                FocusChanged?.Invoke(previous, next);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Interaction] '{name}': FocusChanged listener threw.\n{exception}", this);
            }
        }

        // ---- the pipeline -----------------------------------------------------------

        /// <summary>Begin interacting with the current focus using its primary verb.
        /// Call from your input's press; pair with <see cref="CancelInteraction"/> on
        /// release for hold-to-interact verbs.</summary>
        public InteractionRejection StartInteraction() => StartInteraction(Focused, null);

        /// <summary>Begin interacting with a specific target/verb (verb menus, CRPG
        /// context menus). Null verb = the target's primary verb.</summary>
        public InteractionRejection StartInteraction(Interactable target, Interaction verb)
        {
            CancelInteraction();

            InteractionContext context = BuildContext(target, verb);
            InteractionRejection rejection = Validate(context);

            // Out of range is special: with a request handler present, "valid but too
            // far" is exactly the handler's job (walk there, then Execute) — so it
            // dispatches instead of rejecting. Every other rejection stands.
            if (rejection == InteractionRejection.OutOfRange && RequestHandler != null)
            {
                return Dispatch(context);
            }
            if (rejection != InteractionRejection.None)
            {
                LastRejection = rejection;
                return rejection;
            }

            if (context.verb.HoldSeconds > 0f)
            {
                pendingHold = context;
                holdTimer.Begin(context.verb.HoldSeconds);
                LastRejection = InteractionRejection.None;
                return InteractionRejection.None;
            }
            return Dispatch(context);
        }

        /// <summary>Cancels an in-progress hold (call from your input's release).</summary>
        public void CancelInteraction()
        {
            holdTimer.Cancel();
            pendingHold = default;
        }

        /// <summary>Executes a validated context — the completion call for request
        /// handlers (re-validates first, so walking away during the walk-to still
        /// rejects properly).</summary>
        public InteractionRejection Execute(in InteractionContext context)
        {
            InteractionRejection rejection = Validate(context);
            LastRejection = rejection;
            if (rejection != InteractionRejection.None) return rejection;

            context.interactable.HandleInteracted(context);
            return InteractionRejection.None;
        }

        private InteractionRejection Dispatch(in InteractionContext context)
        {
            IInteractionRequestHandler handler = RequestHandler;
            if (handler != null)
            {
                LastRejection = InteractionRejection.None;
                handler.HandleRequest(context);   // the game completes via Execute
                return InteractionRejection.None;
            }
            return Execute(context);
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
        public InteractionRejection Validate(in InteractionContext context)
        {
            if (context.interactable == null) return InteractionRejection.NoTarget;
            if (context.verb == null) return InteractionRejection.NoVerb;

            if (maxInteractDistance > 0f)
            {
                float distance = Vector3.Distance(RangePosition, context.interactable.InteractionPoint);
                if (distance > maxInteractDistance) return InteractionRejection.OutOfRange;
            }
            return context.interactable.Evaluate(context);
        }
    }
}
