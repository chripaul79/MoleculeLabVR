Current hand controls and test worksheet: [Hand interaction repair](Documentation/HAND_INTERACTION_REPAIR.md).

# Molecule Lab VR

**27 October (2):** endless glass beakers with smaller atoms replace the baskets; finished molecules go in the **GALLERY** or **TRASH** bin; a red **RESET** button on the bench replaces the menu reset; controller teleport; a simpler FREE BUILD panel (Fill hydrogens / Undo / Clear); unused atoms vanish after 10 s; menu tabs always switch. Only the gallery is remembered between sessions. See [Free build and PubChem](Documentation/FREE_BUILD.md#bins-reset-button-and-moving-around).

**27 October:** six more elements (N, S, P, F, Cl, Br) on a second shelf to the right of the bench; baskets are unlimited by default (Settings > Atoms: unlimited/finite); finished molecules fall under gravity; and a **molecule gallery** behind you (the old front wall is now an opening) shows a copy of every molecule you complete, saved between sessions. See [Free build and PubChem](Documentation/FREE_BUILD.md#gallery-elements-and-endless-atoms).

**26 October free build:** a new FREE BUILD tab lets you build any structure by hand (drop an atom on the ring, snap more on, push atoms together for double bonds or rings); when it is complete it is checked in PubChem automatically and locked with its PubChem name, CID and 3D shape. Quest builds now request internet access. See [Free build and PubChem](Documentation/FREE_BUILD.md). Headset acceptance pending.

**29 September hand repair (1.1.1):** easier direct grabs, restored distance grabs and repaired ray-menu clicks, paginated controls, eleven buildable molecules and four guided reactions. Read [the updated user guide](Documentation/INTERACTION_AND_CHEMISTRY.md). Headset acceptance of this update remains pending.

Standalone Quest 3 teaching prototype, Unity **6000.6.2f1**. Open `Assets/Scenes/MoleculeLab.unity` and press Play. The academic room, table, shelf, baskets and background furnishings are editable in the scene; runtime creates the session atoms and control UI. Use a Quest for tracked interaction; no desktop XR simulator is enabled.

## Academic lab redesign

- Modern teaching room with a central bench, reachable side shelf, fixed reagent archive, element poster and instrument workstation.
- Nine elements in endless beakers on the side shelves; every atom in play still has a tracked identity.
- Choose a recipe to transfer its seed from the shelf. Take atoms, follow directional guides, and release when the ring says **Release to bond**.
- Initial bonding thresholds: 18 cm preview, 5 cm snap radius and 15-degree direction tolerance, exposed on LabController. The electrical effect is a teaching cue, not simulated chemistry.
- Keep a finished molecule by dropping it in the gallery bin, or discard it in the trash bin. The red button on the bench resets the lab (the gallery is kept).
- Mute, reduced effects and optional lesson hints are available on the display.

Hands and Touch Plus controllers share the XRI input modality manager. Use nearby pinch/grip to grab and ray/pinch or controller ray/trigger to press controls. Hand rays operate menus and grab objects at a distance; poke activation is disabled. Green direction guides indicate available authored sites; wrong element, direction, distance and occupied sites cannot create bonds.

The catalogue includes water, methane, ethane, H2, O2, ethene, ethyne, methanol, ethanol, CO2 and benzene; one-/two-hand manipulation (0.25x–6x); ethane torsion and reversible deformation; illustrative bond breaking/rejoining; and four guided reactions: water formation, methane combustion, ethene hydrogenation and ethene hydration. In Inspect, put ethane down before using its handles or energy wand. Choose a reaction, dock its listed inputs in Build or React, select the required conditions, then activate in React.

## Editing and delivery

The environment prefab is `Assets/MoleculeLab/Environment/AcademicLab.prefab`. Edit its contents or scene overrides normally. **Create chemistry content** updates definitions while preserving an existing scene. **Install academic environment** only installs when an environment is absent; it does not erase decoration.

- **Molecule Lab > Build Quest APK** produces `Builds/MoleculeLab-Quest3.apk`.
- Run **MoleculeLab.Tests** from Unity's EditMode Test Runner. The playthrough test enters Play mode.
- [Quest deployment](Documentation/QUEST_BUILD.md)
- [Authoring and architecture](Documentation/ADDING_MOLECULES.md)
- [Free build and PubChem identification](Documentation/FREE_BUILD.md)
- [Asset provenance](Documentation/ASSET_PROVENANCE.md)
- [Treatment report source](Documentation/TreatmentReport/README.md)
- [Evaluation worksheet](Documentation/EVALUATION_WORKSHEET.md)
- [Acceptance checklist](Documentation/ACCEPTANCE.md)
- [Verified build status](Documentation/IMPLEMENTATION_STATUS.md)

Physical reach, readability, comfort and sustained performance require Quest testing. Existing template scenes and Unity Version Control metadata are preserved. Source backups are under `Backups/`.
