# Molecule Lab 1.1.1 — hand interaction repair

Implemented: a single Near–Far interactor per hand for nearby objects, distance objects and menus; immediate normalized pinch readers replace the custom queued menu ray. Distance selection retains the original Far attachment behavior. Direct atom acquisition uses a 3 cm surface margin, 1 cm candidate-switch margin, 0.8 / 0.6 pinch hysteresis and a 150 ms early-pinch window. Hover and selection have distinct visual feedback. Tracking loss cancels clicks and bonds. Controller fallback is retained.

Chemistry remains unchanged: eleven molecules, four reactions, C8/O8/H32 finite inventory and 18 cm / 5 cm / 15 degree bonding thresholds. Free build is deferred.

## Local verification
64 automated checks passed, zero failures. These include actual world-space UI pointer-down/up/click events and cancellation, the old queued-input starvation reproduction, distance grabbing without pull-to-hand, direct acquisition boundaries, stable shelf target choice, gesture ownership and chemistry/inventory regressions.

Result: Succeeded
Size: 1672406720
Duration: 00:02:54.7980300
Errors: 0
Warnings: 402

APK version 1.1.1, Android code 3. ARM64, IL2CPP, Vulkan. Development build.
APK bytes: 85471465
APK SHA-256: `8dea2f2f38a3d7cd123fb75e51555415d3a02fcc362b99c450c2967af54e8474`

## Hardware validation pending
No physical hand-success rates, Windows Link acceptance, standalone 72 FPS/20-minute performance results or participant findings are claimed. Use HAND_INTERACTION_REPAIR.md for the 20 direct grabs, 20 distance grabs and 20 menu selections per hand worksheet. Target >=18/20 first-attempt successes for each category with no duplicate clicks or unintended bonds.

The TreatmentReport folder records the preceding 1.1.0 design. Its menu-only hand-ray descriptions are superseded by HAND_INTERACTION_REPAIR.md and the updated control guide in this release.
