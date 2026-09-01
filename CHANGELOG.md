# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-09-01

### Added

- **Which condition refused.** `Interactable.Evaluate` and `Interactor.Validate` have
  overloads with `out IInteractionCondition blocker`, and the interactor keeps `LastBlocker`
  beside `LastRejection`. `Interactor.Describe(condition)` names it the way a designer would
  find it ("KeyLock on Chest"). The F3 overlay, the Interactor inspector (with a button that
  selects the condition) and `interact.state` / `interact.info` all show it. "VerbUnavailable"
  alone was the difference between a prompt that can say *locked* and one that cannot, and
  between a designer told which of three conditions on a chest said no and one guessing.
- **`InteractionRejection.FocusLost`.** A hold-to-interact abandoned because focus moved off
  its target now records why, like every other refusal; a hold broken by validation records
  that validation's rejection and blocker. Before, a broken hold left `LastRejection` at None.
- **`ProximityDetector.maxOverlaps`** (8-256, default 32). The overlap buffer was a constant,
  and a shelf with more colliders than it silently never offered the rest. It is a setting,
  and a full buffer logs once.
- **`InteractionScoring.SortByScoreDescending`**: the ranking, pure and test-pinned.
- This changelog. Earlier: 0.1.0 the package; 0.1.1 `IgnoreRoot` on the ray and pointer
  detectors; 0.1.2 the console addon.

### Changed

- **Firing a hook allocates nothing.** `Interacted`, `FocusGained`, `FocusLost` and the
  interactor's `FocusChanged` isolated listeners through `GetInvocationList()`, which
  allocates an array per call - on every focus change, several times a second per interactor.
  They run on an internal copy-on-write listener array now: isolation kept, allocation gone,
  and `FocusChanged`, which was not isolated at all, is. `InteractAction` builds its handler
  delegate once. A throwing `On Interacted` or focus UnityEvent is logged and no longer stops
  the reactions after it - the global broadcast in particular.
- **`DetectNow()` detects now.** It only scheduled a pass for the next Update, so its own doc
  was wrong and the console's `interact.detect` compared focus before and after nothing.
- **Ranking is stable.** `List.Sort` is not, so two candidates with equal scores could swap
  from one detection to the next - focus flickering between two identical items on a shelf.
  Insertion sort keeps the detector's order among ties and allocates nothing, where
  `List.Sort(Comparison)` allocates a comparer per call.
- **A hold on an explicit target does not need focus.** `StartInteraction(target, verb)`
  with a hold verb was cancelled on the next frame unless the target happened to be focused -
  which a verb menu's or a CRPG context menu's target rarely is. Holds started from
  `StartInteraction()` still follow focus; holds started on a named target follow only that
  target's validity.
- **A disabled condition component does not gate**, the way a disabled collider is not
  there. The enabled checkbox is a designer's way to lift a lock, with no `RefreshConditions`
  call. A condition destroyed since the cache was built is skipped rather than called.
- **The collider cache forgets its misses when an interactable appears.** A prop hit before
  it gained an `Interactable` stayed cached as "not interactable" until a scene unload or the
  cache cap - silently, against the package's one rule. `Register` drops the cache; it refills
  at one lookup per collider actually hit.
- The registry's scene-unload hook is one held delegate, added once per play session and
  removed at reset, instead of a new lambda per session under domain-reload-off.
- Ray and pointer detectors re-resolve `Camera.main` when the remembered camera is destroyed
  or disabled, so a camera swap is followed.

### Tests

- 31 (was 18). `InteractorPipelineTests` drives the interactor a frame at a time through an
  internal `Tick(now, deltaTime)`, without play mode.
