# Interaction and chemistry update — 28 September 2026

See **HAND_INTERACTION_REPAIR.md** for the version 1.1.1 hand repair and its hardware worksheet.

## Start and controls
Open `Assets/Scenes/MoleculeLab.unity`. Use the project's exact Unity version, 6000.6.2f1.

- Hands: reach close to an atom and pinch thumb/index to grab, or point and pinch for distance grabbing. Point at a menu button, pinch and release to click. Release the pinch before changing interaction targets.
- Direct object selection takes precedence over UI. The holding hand's menu ray is suppressed. Menus do not respond to fingertip pokes; hand rays can grab atoms and completed molecules at a distance.
- Controllers retain grip grabbing and ray/trigger menus. Test both sides separately.
- Settings provides bench lower/higher and nearer/farther, sound, reduced effects and hints. Recovery controls stay visible.
- In Build, choose Water first. A seed automatically moves from stock to the assembly area. Add atoms at authored sites; wait for “Release to bond,” then release. Targets require 5 cm / 15 degrees; the preview starts at 18 cm. Wrist rotation is irrelevant.
- The shelf moves 20 cm left and forward enough to clear the bench by 5 cm. The adjustment is part of the editable prefab, not runtime decoration generation.

## Catalogue
Water, methane, ethane, hydrogen, oxygen, ethene, ethyne, methanol, ethanol, carbon dioxide and benzene are buildable. Four choices appear per page. All use the same finite C8/O8/H32 stock.

Benzene is planar with equivalent visible C–C bonds and a delocalization circle representing six pi electrons. Internal Kekule bond orders are bookkeeping, not a claim that aromatic bond lengths alternate. Benzene does not use the ethane twist or break tool. The ring closes when the last carbon is placed; both existing-neighbour connections are created once.

Geometry is idealized and enlarged, not molecular dynamics. Oxygen's triplet ground state is explained alongside the simple O=O structural model. The methyl radical has one unpaired electron; ethane homolysis and the energy wand remain explicitly simplified.

## Reactions
Select a reaction in React, return to Build for each required molecule, then release completed inputs into the tray. Docking works in Build and React. The input checklist persists. Return to React, select conditions, then Activate. Wrong conditions or quantities do not consume anything.

| Recipe | Exact inputs | Products | Condition |
| --- | --- | --- | --- |
| Water formation | 2 H2 + O2 | 2 H2O | Ignition |
| Complete methane combustion | CH4 + 2 O2 | CO2 + 2 H2O | Ignition; sufficient oxygen |
| Ethene hydrogenation | C2H4 + H2 | C2H6 | Pd catalyst |
| Ethene hydration | C2H4 + H2O | C2H5OH | Acid catalyst |

Hydration is reversible. Only its forward net transformation is animated; the model does not predict equilibrium conversion, reaction rates, mechanisms or heat. Catalysts are condition controls and are not consumed from stock. All product atoms retain their inventory identities. Switching reactions undocks inputs without consuming them. Reset cancels an active reaction and restores exactly 48 atoms.

Benzene, methanol and ethyne have no reaction recipes in this update. Arbitrary mixtures are unsupported, even if a balanced equation could be written.

## Chemistry references
- https://openstax.org/books/organic-chemistry/pages/15-2-structure-and-stability-of-benzene
- https://openstax.org/books/organic-chemistry/pages/8-6-reduction-of-alkenes-hydrogenation
- https://openstax.org/books/organic-chemistry/pages/17-3-preparation-of-alcohols-a-review
- https://openstax.org/books/chemistry-2e/pages/4-1-writing-and-balancing-chemical-equations

## Windows transfer
Extract the new project ZIP to a new folder, add it in Unity Hub, and open with 6000.6.2f1. Do not merge it into an older Windows copy. The Mac project is the source of truth for this update.

For Meta Horizon Link: set Meta as the active OpenXR runtime, enable developer runtime features and hand tracking as required by Meta's current Link setup, then restart Unity. Use the project's desktop OpenXR configuration. Hand tracking over Link must be tested separately from standalone Quest tracking. See https://developers.meta.com/horizon/documentation/unity/unity-link/ .

## Required hardware evaluation (unperformed for this update)
Record date, headset/runtime, seated/standing posture and whether running Link or standalone.

1. Per hand: 20 direct atom grabs, 20 distance grabs and 20 ray-menu clicks. Target >=18 first-attempt successes in each set, zero duplicate actions.
2. Hold an atom and point at controls: no accidental clicks. Keep pinching while crossing targets: no switch until release.
3. Tracking loss while holding or clicking must cancel safely, never bond. Test sleep/resume, controller switching, both hands and reset.
4. Reach every basket socket without walking or leaning through the bench. Check shelf clearance and menu readability.
5. Build all eleven molecules, execute all four reactions, try invalid releases and wrong conditions, recycle and repeat.
6. Standalone: record CPU/GPU frame timing for a 20-minute session targeting stable 72 FPS. Editor speed and Link frame rate are not standalone performance evidence.

Participant/evaluator: ______  Runtime: ______  Results: ______
Issue / heuristic / severity / proposed fix: ____________________
