# Acceptance checklist

Do not mark device checks passed from an Editor run. Record tester, date, headset OS and APK hash.

## Quest hardware (pending: no headset connected during implementation)

- [ ] Install APK; launch from Unknown Sources; no startup errors in logcat.
- [ ] Floor height and head movement are correct; no duplicate camera or rig.
- [ ] Both hands can pinch-grab, operate UI, release and recover dropped atoms.
- [ ] Both Touch Plus controllers can grab and operate every control.
- [ ] Switching hands/controllers leaves no stuck selection or duplicate input.
- [ ] Tracking loss, opening system UI, sleep and resume release interactions safely.
- [ ] Adjust bench height seated and standing. All required controls remain reachable.
- [ ] Build H2O, CH4, C2H6, H2 and O2. Wrong atoms and occupied sites are rejected.
- [ ] Grab a completed molecule with two hands; rotate and scale to both limits.
- [ ] Ethane twist preserves bond lengths/local angles; deformation returns on release.
- [ ] Charge the energy tool, break ethane into two CH3 radicals, then rejoin.
- [ ] React tray rejects an incomplete set; 2 H2 + O2 creates exactly 2 H2O.
- [ ] Products remain grabbable and reusable; repeated activation does not duplicate them.
- [ ] Reset while grabbing or reacting produces a clean bench without exceptions.
- [ ] Complete guided lesson without restarting.
- [ ] Run 20 minutes including repeated construction/reactions. Sustain 72 FPS and record CPU/GPU frame time, memory growth and thermal behavior.

## Scientific review

- [ ] Water: bent geometry, 104.5 degree reference angle.
- [ ] Methane: tetrahedral geometry, approximately 109.5 degrees.
- [ ] Ethane: tetrahedral carbons; only the central single bond rotates in torsion mode.
- [ ] Hydrogen single bond; oxygen double bond.
- [ ] Atom counts and bond orders match displayed formulas.
- [ ] Radical labels, reaction activation and energy demonstration are explicitly simplified educational representations.

## Build evidence

See LastBuild.txt, QuestValidation.txt and automated test results. A successful build does not certify the pending headset checks above.

## Academic redesign (device checks pending)

- [ ] Identify interactive atoms/tools before instruction; record attempted decoration grabs.
- [ ] See and reach all 48 atoms, seated and standing, including lower-tier baskets.
- [ ] Labels, hover rings and stock counts remain readable from the working position.
- [ ] Ready ring predicts attachment reliably at valid distance AND angle, using either hand/input method.
- [ ] Wrong-element and wrong-angle attempts do not consume stock or alter a molecule.
- [ ] Exhaust the carbon stock, recycle a molecule, and verify the exact returned count.
- [ ] Switch an unfinished recipe; all its atoms return without duplication.
- [ ] Break/rejoin, react/recycle and reset during reaction; total stock remains C8/O8/H32.
- [ ] Sound mute and reduced-effects options retain complete visual feedback.
- [ ] Fixed decorations neither highlight nor grab; all required controls work via hands/controllers.


## 28 September interaction/chemistry acceptance
Use INTERACTION_AND_CHEMISTRY.md for the current procedure. Record 20 direct atom grabs and 20 ray-menu clicks per hand, aiming for >=18 first-attempt successes per set, zero duplicate actions and no tracking-loss bonds. Test the new eleven-molecule catalogue and four reactions, catalyst/ignition selection, no remote hand grabbing, no poke menu clicks, and suppression of menu rays while holding. None of these physical interaction acceptance targets has been recorded for this update.


## Version 1.1.1 hand repair
Use HAND_INTERACTION_REPAIR.md for the current three-part, 20-attempt-per-hand worksheet: direct acquisition, distance acquisition and menu clicks. Prior menu-only hand-ray expectations are superseded. Free build remains deferred. Hardware results have not yet been collected.
