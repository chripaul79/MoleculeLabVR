# Hand interaction repair — version 1.1.1

## Controls
- Pick up nearby atoms by bringing the thumb–index pinch point close to the atom and pinching. The acquisition region extends 3 cm beyond the visible atom surface; the atom itself is unchanged.
- Alternatively, point and pinch an atom or completed molecule. It remains at the ray's selection distance, as in the original prototype; it is not automatically pulled into the hand.
- Point at a menu button, pinch, and release to activate it. Fingertip menu presses are disabled.
- A pinch owns one target until release. Nearby object acquisition takes priority. The holding hand cannot operate a menu at the same time.
- Let go before changing targets. A pinch started within 150 ms of approaching an atom can acquire it; a longer-held pinch cannot silently pick up a new atom.
- The initial pinch thresholds are 0.8 to activate and 0.6 to release. These use the existing normalized Meta Aim index-pinch input, not a wrist angle.
- Binding still requires release within 5 cm and 15 degrees of a valid authored site; preview starts at 18 cm. Wrist rotation is not required.
- Tracking loss cancels the gesture; reacquiring tracking requires an open hand before another pinch.
- Controllers remain supported. Free build is deferred; choose a molecule recipe before assembly.

## Developer tuning and diagnosis
Each hand's `HandMenuRay` component in the project-owned rig contains the acquisition margin, candidate switch margin, pinch thresholds and early-pinch time. The historical component name is retained for serialization; it no longer creates a separate menu ray. Enable **Show Diagnostics** for the per-hand tracking, pinch, ownership and UI event display. This is off by default and available only in development/editor runs.

The previous UI path repeatedly queued next-frame input from an early Update. That could postpone consumption indefinitely. The repaired path uses immediate reader state and the Near–Far interactor's UI support. An automated test reproduces the queue failure, and an integration test exercises pointer-down, pointer-up and click on a real world-space canvas.

## Hardware acceptance worksheet — not yet performed
Record build, runtime, headset, posture and date. Repeat separately on Windows Quest Link and standalone Quest.

| Per-hand task | Left successes / 20 | Right successes / 20 | Target |
| --- | --- | --- | --- |
| Direct atom acquisition | — | — | >=18 first-attempt successes |
| Distance atom/molecule acquisition | — | — | >=18 first-attempt successes |
| Menu pinch-and-release | — | — | >=18 first-attempt successes |

Also record: incorrect shelf target; duplicate click; menu activation while holding; bond on cancellation; hand/controller switching; both-hand scaling; reset during manipulation; tracking loss and recovery. Duplicate clicks and unintended bonds must be zero. Confirm ray selections do not pass through the menu. Test all basket corners in both seated and standing use.

Run the full lesson and retain the standalone 72 FPS / 20-minute performance target. Do not infer physical reliability or performance from editor tests.
