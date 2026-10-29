# Molecule Lab VR – project summary

Molecule Lab VR is a chemistry classroom for the Meta Quest 3. You build molecules from atoms with your hands or
controllers, look at their shapes, react them, and collect the results in a gallery. It is made with Unity 6000.6.2f1
(URP, XR Interaction Toolkit 3.6, XR Hands and OpenXR for Meta Quest) and aims for 72 frames per second on the headset.

How to open and run the project is in the [README](../README.md). Screenshots are in [Classroom](Classroom).

## A session in the lab

You start at a workbench with a menu screen in front of you. Beakers of atoms stand on cupboards either side, a reaction
table is in the front-right corner, and the gallery is through the opening behind you.

1. **Build.** Pick a goal on the menu, or just drop any atom on the paper build mat. With *Guide spots* every atom of a
   recipe has a teal target; with *Formula only* you get the formula and work out the bonds yourself. Atoms snap on at the
   guide dots, and teal rays show which way each free bond points (Settings can turn the rays off). Fill hydrogens, Undo
   and Clear are on the menu. Everything has gravity: build on the table, and pick the molecule up to reach underneath.
2. **Inspect.** Holding a finished molecule shows its name and geometry on a small hologram. Ethane can be twisted and
   bent with two orbs, and its C–C bond broken with the energy wand.
3. **React.** Put molecules in the glass vessel. The tablet beside it says what each tool would do. Hold the flame, the
   palladium catalyst or the acid over the vessel to run the reaction; bromine reacts with an alkene on its own.
4. **Keep or throw away.** The hole in the bench sends a molecule to the gallery; the trash can discards it. Gallery
   exhibits can be picked up to read about them, dropped over the vessel to react a copy, or dropped in the trash to
   remove them. The gallery is saved between sessions; the red button on the bench resets everything else.

## Chemistry

- **Elements:** H, C, O, N, S, P, F, Cl and Br, each with its normal number of bonds. The beakers never run out, but every
  atom in play is tracked, so reactions always conserve atoms.
- **Recipes:** water, methane, ethane, hydrogen, oxygen, ethene, ethyne, methanol, ethanol, carbon dioxide and benzene,
  plus formula-only goals such as ammonia, acetic acid and ethyl acetate.
- **Free building:** any structure that satisfies the valence rules counts as finished. It is then named: by matching a
  recipe, or by asking [PubChem](https://pubchem.ncbi.nlm.nih.gov/), which also supplies a 3D shape and a description.
  Answers are cached on the device, so a molecule only needs the internet the first time.
- **Reactions:** four hand-made recipes (water formation, methane combustion, ethene hydrogenation and hydration) and a set
  of general rules that work on any molecule's bonding: combustion of C/H/O fuels, H2 + halogen, alkane + halogen,
  hydrogenation of double and triple C–C bonds, hydration of alkenes, esterification, dehydration of alcohols and
  halogen addition to alkenes. The rules run offline. They are teaching simplifications, not a simulation: there are no
  energies, rates or equilibria, and anything outside the rules simply does not react.

## How the code is organised

All project code is under `Assets/MoleculeLab`.

| File(s) | Role |
|---|---|
| `Runtime/LabController*.cs` | The lab itself: modes, menu, building, reactions, gallery, bins and teleporting (one class split over several files). |
| `Runtime/MoleculeState.cs`, `ReactionDefinition.cs`, `ReactionRules.cs` | Chemistry rules, kept separate from input and physics. |
| `Runtime/FreeStructure.cs`, `FreeBuildView.cs`, `PubChemClient.cs` | Free building and naming through PubChem. |
| `Runtime/MoleculeView.cs`, `LooseAtom.cs`, `BondPreview.cs` | Molecules and atoms in the world, and the guides shown while building. |
| `Runtime/ClassroomBuilder.cs`, `BeakerShelf.cs`, `GalleryRoom.cs`, `ReactionStation.cs` | Build the room, beakers, gallery and reaction table when the scene starts. |
| `Runtime/LabelCraft.cs`, `HoloTag.cs`, `MoleculeHologram.cs` | Labels, signs and holograms. |
| `Editor/` | The **Molecule Lab** menu: Quest set-up and build, screenshot capture and the edit-mode preview of the classroom. |
| `Tests/Editor/` | Automated tests. |

The classroom is assembled by code when the scene starts, on top of the academic-lab environment saved in
`Assets/Scenes/MoleculeLab.unity`. In the editor, a preview of the finished room is shown outside Play mode; it is never
saved into the scene.

## Building for the Quest

1. Open `Assets/Scenes/MoleculeLab.unity`.
2. Run **Molecule Lab > Validate Quest** and fix any errors it reports (details are written to `Logs/QuestValidation.txt`).
3. Run **Molecule Lab > Build Quest APK**. The development build is written to `Builds/MoleculeLab-Quest3.apk`.
4. With developer mode enabled on the headset, install it with Meta Quest Developer Hub or `adb install -r Builds/MoleculeLab-Quest3.apk`,
   and start it from *Unknown sources*. Turn on hand tracking in the headset settings to use hands.

For quick testing, Quest Link or Air Link with Play in the editor also works.

## Tests

Run **MoleculeLab.Tests** from *Window > General > Test Runner > EditMode*. Most tests check chemistry, reactions and
free-build rules and take seconds; the playthrough tests start the scene in Play mode and take a few minutes in total.
Headset comfort, readability and frame rate still have to be judged on the device.

## Assets and credits

- Furniture, glassware and room modules: *3D Laboratory Environment with Apparatus* from the Unity Asset Store.
- A few office models: Kenney *Furniture Kit* (CC0), licence in `Assets/MoleculeLab/Art/KenneyFurniture`.
- Handwriting font: *Patrick Hand* (SIL Open Font License), in `Assets/MoleculeLab/Resources/Fonts`.
- Molecule names, shapes and descriptions: PubChem, U.S. National Library of Medicine.
- All sounds are generated in code.
