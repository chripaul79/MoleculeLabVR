# Authoring molecules

Molecule assets live in Assets/MoleculeLab/Data. Gameplay code lives in Assets/MoleculeLab/Runtime; generation and build utilities live in Assets/MoleculeLab/Editor.

## Add a recipe

1. Create an Element asset using Assets > Create > Molecule Lab > Element, or reuse Element_Hydrogen, Element_Oxygen and Element_Carbon. Supply a unique chemical symbol, display color, display radius and maximum bond-order sum.
2. Create a Molecule asset with a unique ID, display name and formula. Add atom sites with element references and local positions in Unity metres. These are enlarged teaching models, not physical molecular length units.
3. List each bond once using zero-based atom indices and bond order. Every atom must connect to the seed through this graph. Set Seed Index to the initial atom. Set Rotatable Bond to -1 when unsupported.
4. Add the molecule to the LabController's Molecules list in MoleculeLab.unity. The selector is populated from that list. The selector paginates automatically, four molecule recipes per page.
5. Supply the geometry explanation and verify the positions independently. IsValid checks connectivity, endpoint indices, duplicate bonds and maximum valence; it does not prove scientific correctness.
6. Run the EditMode tests, build, and check every site in the headset.

For a single rotatable bond, provide the bond index and all sites on the rotating substituent (excluding its pivot atom). The prototype's break/rejoin tool is specifically for ethane and its CH3 fragments. Supporting other fragment types requires extending the runtime fragment creation before enabling breaking for those molecules.

## Reactions

A Reaction asset contains input and output molecule/count pairs. ReactionEngine checks full element conservation, exact reactant multiplicities, unique object identities, completion and prior consumption. It consumes inputs only after every check succeeds. Molecules cannot be consumed twice.

The tray derives its accepted species, quotas and equation from the selected reaction definition. Add the definition to LabController.reactions; include its conditions and chemistry references. Activation is explicit; proximity alone does not run a reaction. Products are ordinary complete molecule states and remain grabbable.

## Interaction architecture

- MoleculeState and ReactionEngine enforce chemistry rules independently of devices.
- LooseAtom passes released atoms to LabController.TryAttach. A nearest authored site within 13 cm is considered; wrong or occupied sites are rejected without mutation.
- MoleculeView renders sites and bonds; a complete structure becomes one multi-select XRGrabInteractable.
- MoleculeScaleTransformer scales between 0.25x and 6x about the two-hand midpoint.
- InspectionHandle applies constrained torsion or temporary deformation; release restores deformation.
- EnergyTool requires a held wand near the selected ethane central bond for two seconds. The threshold is illustrative, not a measurement of physical bond energy.
- LabController owns modes, the guided lesson, tray, reset, and bounded scene population (12 molecule roots; loose atoms come from the finite 48-token inventory).

The generated rig uses the imported XRI 3.5.1 Hands sample against installed XRI 3.6.0 and XR Hands 1.9.0. Compilation and automated checks cover compatibility, but actual hand/controller switching requires hardware acceptance testing.

## Regeneration

Molecule Lab > Create chemistry content updates the built-in recipe values and preserves an existing scene. Save custom recipes separately. Normal development and APK builds do not require definition generation.

## Finite inventory and editable room (academic redesign)

The environment prefab holds a `LabEnvironment` component with workstation, bench, tray, panel mount, 48 socket references and four basket-label references. These are the stable layout interface for runtime sessions. Keep socket indices C0–7, O8–15 and H16–47. The scene keeps the authored room between sessions; runtime reset only recreates session objects.

`AtomInventory` tokens have an element symbol and one owner string. All transfers require the expected owner. `MoleculeView.TokenIds` maps occupied sites to inventory tokens. Seed allocation, attachment, recycling, fragments and reactions must maintain a one-to-one mapping. Never introduce a molecule by calling Spawn without also allocating/transferring matching tokens. The test playthrough checks this invariant across the lesson.

`BondPlacement.Evaluate` is the pure positional/directional evaluator. `LabController.FindPlacement` combines chemical validity with those thresholds and stable target selection; both preview and release use it. Keep guide effects cosmetic: they must not bypass validation.

Definition generation now preserves the scene. It still intentionally updates the built-in definitions; do not use it as a routine scene rebuild. The academic builder installs only into scenes without an existing LabEnvironment. Third-party mesh provenance is in ASSET_PROVENANCE.md.


## Expanded definitions (28 September update)
`molecularFormula` is the unambiguous atom-count formula, while `formula` may show a condensed label such as C2H5OH. `radicalSites` explicitly names atoms with one unsatisfied valence/unpaired electron. `electronicState` supplies the explanatory annotation. Closed-shell structures must satisfy supported element valences exactly.

`aromaticRing` is the ordered six-carbon ring for this prototype's supported benzene model. Its Kekule bond orders are used only for valence bookkeeping; render all aromatic C-C bonds equally and add the delocalization circle. Do not treat an aromatic bond as a rotatable single bond. Ring closure must create all occupied-neighbour edges once.

Reaction definitions have identity, title, required condition, conditions text, reversibility and references. Register them in `LabController.reactions`; register molecules in `LabController.molecules`. The tray and paginated menu use these arrays. `ReactionEngine.CanReact` and `TryReact` take the selected `ReactionCondition`; omitted conditions default to None and cannot authorize the curated reactions. Inventory feasibility and whole-scene tests are required before adding a recipe.

`LabUpgrade.Apply` is an explicit migration tool that updates the curated catalogue, rig and shelf layout. It is not run automatically at startup. Do not invoke it to preserve custom scene adjustments; edit the project-owned prefabs instead.
