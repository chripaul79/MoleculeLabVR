# Free build and PubChem identification (updated 27 October 2026)

Free build lets the player assemble any structure from nine elements (H, C, O, N, S, P, F, Cl, Br), without a recipe. Atoms come from endless glass beakers on the two side shelves. When the structure is finished, the lab asks PubChem whether that compound exists. If it does, PubChem's name and 3D shape are loaded.

## In the headset (hands only)

1. Open the **FREE BUILD** tab once. A pulsing teal ring, about 18 cm above the middle of the bench, marks the build spot. As the structure grows it lifts itself so no atom sinks into the bench.
2. Pick an atom out of a beaker and release it over the ring (within 30 cm). That atom starts the structure. A fresh atom appears in the beaker at once. An atom you take out and leave unused for 10 s disappears.
3. Teal dots show where open bonds point. These are VSEPR directions: tetrahedral, trigonal or linear, depending on the atom's bonds and lone pairs. Hold another atom over a dot until it says **Release to bond**, then let go.
4. After every snap, a floating label above the structure says what is missing, for example `Not complete: 3 open bonds / C1 needs 2, O3 needs 1`.
5. **Double or triple bond:** grab one of two bonded atoms (directly, or with a distance grab: the atom stays on the ray) and push it into the other. When the label says **Release to make a double bond**, let go. Do it again for a triple bond. Make multiple bonds *before* the last hydrogens: an atom with no spare valence can't take one, and the label says so ("C2 has no spare bond. Pull a hydrogen off it first"). Atoms of one structure pass through each other, so nothing blocks the squeeze; if you let go without forming a bond, the atom springs back and the rest of the molecule stays where it is.
6. **Ring:** grab an atom with a free bond and push it into another atom with a free bond. Let go at **Release to close a ring**.
7. **Remove:** grab an end atom and pull it about 35 cm away. At **Release to put … back**, let go and it leaves the structure. Atoms that still hold other atoms can't be pulled off.
8. **Automatic check:** the moment every valence is satisfied, the structure locks and is sent to PubChem (`Checking PubChem...`). If PubChem knows it, it becomes an ordinary molecule with its name label (e.g. *Acetic Acid C2H4O2*), using PubChem's 3D shape.
   - *Not in PubChem:* the structure unlocks and the label says so. Pull an atom off or push two together to change it; it is re-checked when complete again.
   - *Offline:* curated recipes (water, ethanol, benzene…) are still recognised. Anything else says `retrying` and is re-checked automatically up to three times, every 8 s.
9. Decide what happens to the finished molecule: carry it to the **GALLERY** bin (left of the bench) to keep it on the gallery table behind you, or to the **TRASH** bin (right) to discard it. Nothing is kept unless it goes in the gallery bin.
10. To build the next molecule, drop a new atom on the ring.

The FREE BUILD panel has three shortcuts: **Fill hydrogens**, **Undo last atom** and **Clear structure**. Switching tab, clearing or resetting during a PubChem check cancels the check.

## Bins, reset button and moving around

- **Gallery bin / trash bin:** floor bins either side of the bench. Release a finished molecule over one. A loose atom dropped in a bin just disappears.
- **Red RESET button** on the bench: push it down with a fingertip or controller. It resets everything (bench, free build, mode) except the gallery, which is saved between sessions. The gallery can be emptied in SETTINGS (press Clear gallery twice).
- **Teleport:** push the controller thumbstick forward, aim the arc at the floor and let go. Push it sideways to snap turn. Hands still grab and press as before.
- **Gallery hologram:** when PubChem has no text, the hologram says so plainly: offline, not in PubChem, or no description/uses recorded.

## Beakers and imported models

The beakers are drawn at runtime (`Runtime/BeakerShelf.cs`): left shelf H, C, O / N, S, P; right shelf F, Cl, Br; three atoms per beaker at 60 % of the old atom size (`LabVisuals.AtomScale`). To use a model from an imported asset pack (e.g. *Chemistry Lab Items Pack*), assign its beaker prefab to **LabController › Beaker Prefab**. It is scaled to 15 cm and its colliders are removed so atoms can be lifted out.

## What the check does

1. Local rules: the structure is connected, neutral and closed-shell (H 1, O 2, C 4 bonds). `FreeStructure.IsComplete` decides when a check starts, so incomplete structures are never sent; each snap only updates the label.
2. The structure is written as a SMILES string with explicit bracket atoms and Kekulé bond orders. Ethanol is `[CH3][CH2][OH]`; benzene is `[CH]1=[CH][CH]=[CH][CH]=[CH]1`.
3. `GET https://pubchem.ncbi.nlm.nih.gov/rest/pug/compound/smiles/property/Title,IUPACName,MolecularFormula/JSON?smiles=…`
   - `CID > 0`: found. `CID 0`: PubChem has no such compound. HTTP 400: PubChem could not read the structure.
4. If found: `GET …/compound/cid/{CID}/SDF?record_type=3d` returns the 3D conformer in Ångström, with hydrogens. A 404 means PubChem has no conformer, so the lab keeps the player's relaxed geometry.
5. The conformer is matched atom-for-atom to the player's graph (`StructureMatcher`: same atoms, connectivity and H counts; Kekulé forms may differ). It is converted to the lab's teaching scale: 0.13 m per Å, with z flipped for Unity's left-handed axes so chirality is preserved. The result is a normal `MoleculeDefinition` with the player's own bond orders.
6. If the structure matches a curated recipe (water, ethanol, benzene…), the curated definition is used instead, so the product works in the four guided reactions. A curated match is also recognised offline.

The result becomes an ordinary complete molecule: grab, scale, inspect (CID and IUPAC name appear in Inspect), recycle. Atom tokens transfer one-to-one, so the 48-atom invariant holds.

Not found does not mean impossible. PubChem lists compounds that have been reported, so a valence-valid structure can still be absent (unstable, exotic or simply undocumented). The panel says so and leaves the structure editable.

## Network, limits and caching

- Quest builds need internet access. `ProjectSettings > Player > Android > Internet Access` is now **Require** (`ForceInternetPermission: 1`). `QuestManifestValidation` records whether the INTERNET permission reached the manifest.
- Two requests per check, spaced at least 0.25 s apart. This stays well inside PubChem's limit of 5 requests/second and 400/minute.
- Definitive answers (found / not found) are cached as JSON in `Application.persistentDataPath/PubChemCache`, keyed by SHA-256 of the SMILES. Repeat checks work offline. Network errors are not cached.
- The service is behind `IMoleculeLookup`. Tests swap in a fake (`LabController.MoleculeLookup`), and a school without internet could supply a local lookup.

## Code map

| File | Role |
| --- | --- |
| `Runtime/FreeStructure.cs` | `FreeStructure` (valence rules, VSEPR guide directions, relaxation), `MolGraph`, `SmilesWriter`, `StructureMatcher`, Hill formulas |
| `Runtime/PubChemClient.cs` | `IMoleculeLookup`, `PubChemClient` (+ cache), `MolfileParser`, `DiscoveredMolecule` |
| `Runtime/FreeBuildView.cs` | Rendering, guide dots, floating status label, grab gestures (push together = bond / ring, pull away = remove) |
| `Runtime/LabController.FreeBuild.cs` | Drop-to-start ring, inventory transfers, `FreeStructureChanged` (status + automatic check), retries, `CancelFreeCheck`, the menu page |
| `Runtime/BeakerShelf.cs` | Endless beakers (procedural or `beakerPrefab`) and their atom spots |
| `Runtime/PhysicalControls.cs`, `Runtime/LabController.Physical.cs` | Red reset button, gallery and trash bins |
| `Runtime/LabController.Locomotion.cs` | Controller teleport and snap turn |
| `Tests/Editor/FreeBuildTests.cs` | Pure chemistry/parsing tests with captured PubChem replies (`PubChemFixtures.cs`) |
| `Tests/Editor/FreeBuildPlaythroughTests.cs` | Play-mode tests with a fake lookup: automatic check, curated match, not found, offline retry, ring and removal gestures, undo, tab switch / clear / reset during a check |
| `Tests/Editor/LabExtensionsPlaythroughTests.cs` | Beakers, bins, idle despawn, reset button, teleport, gravity, gallery and hologram |

Tuning on `LabController`: `freeSnapDistance` (7 cm to a guide dot), `ringSnapDistance` (13 cm between atom centres), `freeSeedDistance` (30 cm from the build spot), `freeRetrySeconds` (8 s); `FreeBuildView.PullOffDistance` (34 cm).

## Known limits

- Elements: H, C, O plus N, S, P, F, Cl, Br, each with its usual neutral valence (N/P 3, S 2, halogens 1). Hypervalent forms (SF6, PCl5, sulfates) and charged species are not modelled.
- Charged species, radicals and stereochemistry are not modelled. An unspecified stereocentre or E/Z double bond matches PubChem's stereo-undefined record, which may have no 3D conformer.
- Geometry before identification is an idealised teaching relaxation, not a force field.
- Headset acceptance (hand reach to guide dots, push/pull gestures, Wi-Fi behaviour on Quest) is still to be tested on hardware.

## Gallery, elements and endless atoms

**Gallery.** Turn around: the wall behind the bench is now an opening into a 2.8 m gallery (`GalleryRoom`, built at runtime so `AcademicLab.prefab` is untouched; it hides the prefab's `Front wall`). Every molecule you drop in the gallery bin, a recipe from BUILD or an identified structure from FREE BUILD, gets a slowly turning display copy on a stand, with its name, formula and how many times you have built it. Grab an exhibit to look closely; let go and it floats back to its stand. Two tables hold the latest 24 exhibits. The gallery is saved to `Application.persistentDataPath/gallery.json` and comes back next session. **Settings > Clear gallery** (press twice) empties it.

**More elements.** Beakers of N, S and P sit on the left shelf under H, C and O; F, Cl and Br are on the right shelf (`BeakerShelf`, runtime). Their `ElementDefinition`s are created at runtime (`ElementCatalog`) unless the scene's `LabController.elements` already contains an asset with that symbol, so you can replace any of them with an editable asset.

**Endless atoms.** Taking an atom puts a fresh one in its beaker at once; an atom put back in its beaker, dropped in a bin or left unused for `LabController.idleAtomSeconds` (10 s) leaves play. (`AtomInventory` still supports a finite mode for tests and future lessons.) `AtomInventory` keeps one owner per atom in both modes; `Inventory.Live` lists the atoms in play.

**Gravity.** A finished molecule is a dynamic rigidbody: it drops onto the bench, gallery table or floor, and you can stack or throw it gently. It is still while held or docked in the reaction tray. A molecule that lies on the floor for three seconds, or leaves the room, returns to the left of the bench (`LabController.BenchDropPosition`). Loose atoms and unfinished structures still float.

**Solid world.** Held atoms, structure atoms, finished molecules and gallery exhibits use XRI *velocity tracking*: physics pulls them towards your hand, so they stop against the bench, shelves, cabinets and walls instead of passing through. If your hand goes through the bench, the atom waits on top until your hand comes back. The room prefab was authored with most colliders disabled; `LabController.MakeEnvironmentSolid` switches them on at runtime (thin details such as socket markers, floor joints and the menu panel stay non-solid) and gives the imported furniture meshes box colliders. Loose atoms ignore each other, so a full beaker never jams.

**Gallery hologram.** Pick up any exhibit and a see-through screen opens beside it with the molecule's name, formula, IUPAC name and PubChem CID, *What it is* (PubChem's description, preferring the ChEBI definition), and *Commonly used for* (up to two sentences from the "Uses" section of the PubChem record, mostly HSDB; otherwise the use classification). It follows the exhibit while you hold it and fades 6 s after you let go (`LabController.hologramLinger`). Recipe molecules are first identified from their bonding (SMILES lookup), then described. Requests: `compound/cid/{CID}/description/JSON` and `pug_view/data/compound/{CID}/JSON?heading=Uses`, each cached per CID on the headset. When PubChem has no description or uses, or is offline, the screen says so. The service is `IMoleculeInfoSource` (`LabController.MoleculeInfoSource`), replaceable in tests.
