# Liminal Labs Interaction

One interaction system for FPS, third person, point-and-click, and CRPG. The
genre differences live at the edges — how you *find* the target and how you
*present* the choices — so both edges are pluggable and the core never
assumes a genre, a device, or "the player".

Requires `com.liminallabs.core` and `com.liminallabs.gameevents`.

## Five-minute quickstart

1. Make something interactable: add an **Interactable** to any object with a
   collider, and give it a verb — **Assets > Create > Liminal Labs >
   Interaction > Interaction Verb** (e.g. "Open"; verbs are reusable across
   every object that opens).
2. Make an agent: add an **Interactor** to your character plus one detector —
   **Ray Detector** (FPS aim), **Proximity Detector** (third-person walk-up),
   or **Pointer Detector** (mouse cursor).
3. Wire input (the system reads none — that's what keeps it genre-agnostic):

   ```csharp
   if (interactPressed)  interactor.StartInteraction();   // uses current focus
   if (interactReleased) interactor.CancelInteraction();  // for hold verbs
   ```

4. React: stack `InteractAction` subclasses on the interactable (one behavior
   per component — the base owns wiring and self-validates), use the
   UnityEvents, subscribe `Interacted` in code, or set the optional GameEvent
   to broadcast globally with zero scene references.

## The model

| Piece | Role |
| --- | --- |
| `Interaction` (asset) | A verb: localized name, icon, cursor id, sort order, hold seconds. Reusable everywhere |
| `Interactable` | What can be interacted with: verbs, optional tighter range, focus + interacted events |
| `IInteractionCondition` | Availability rules as sibling components (locks, power, quest state) |
| `Interactor` | The agent (one per party member, not "the player"): focus tracking, candidates, the pipeline |
| `InteractionDetector` | Pluggable finding: Ray (with fat-cursor forgiveness rays), Proximity (distance+facing scoring with anti-flicker stickiness), Pointer (uGUI-aware). Or write your own |
| `IInteractionRequestHandler` | The CRPG seam: intercept validated requests, walk to the target, complete with `interactor.Execute(context)` — which re-validates |
| `InteractAction` | Composable responses with wiring and verb filtering inherited |

Hold-to-interact is first-class: give a verb `holdSeconds` and
`StartInteraction`/`CancelInteraction` from your input does the rest;
`HoldProgress01` drives your prompt's fill ring.

## Presentation is yours

The core exposes hooks — `FocusChanged` on the interactor, focus events and
UnityEvents on interactables, `Candidates` for verb menus — and ships no UI.
Prompts, outlines, cursors, and radial menus are listeners; reference
presenters come with the demo sample rather than the runtime.

## Multiplayer

The pipeline maps onto client/server/remote-client cleanly, with no netcode
dependency:

- **Client (predict):** the request-handler seam is your network hook — a
  handler that sends the request to the server instead of executing. Local
  `Evaluate` gives instant client-side denial UX for free.
- **Server (validate + execute):** give the server's player objects
  Interactors and call `StartInteraction(target, verb)` — the server runs
  the identical validation the client predicted with.
- **Remote clients (perform):** on receiving the replicated result, call
  `interactable.PerformInteraction(context)` — the authority-already-decided
  path that fires all reactions **without** validation, so unsynced local
  condition state can never veto what the server ruled.
- **On the wire:** the interactable is your netcode's object reference;
  the verb travels as one byte via `IndexOfVerb` / `GetVerb(index)` (verb
  lists are identical asset data on every machine).

## Nothing fails silently

Every refused attempt records why (`Interactor.LastRejection`): out of range,
verb not offered, condition failed, target disabled. **F3** (with an
Interaction Debug Overlay in the scene) shows every interactor's focus,
ranked candidates with scores, hold progress, and last rejection, live. The
Interactor inspector shows the same in play mode, and Setup & Validation
flags detector-less interactors and collider-less or verb-less interactables.
