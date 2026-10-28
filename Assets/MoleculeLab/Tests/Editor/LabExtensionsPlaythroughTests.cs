using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MoleculeLab.Tests
{
    // Endless beakers, extra elements, bins, reset button, teleport, gallery and gravity.
    public sealed class LabExtensionsPlaythroughTests
    {
        sealed class FakeLookup : IMoleculeLookup
        {
            public MoleculeLookupResult Next=new MoleculeLookupResult{status=LookupStatus.Error,message="offline"};
            public IEnumerator Lookup(string smiles,Action<MoleculeLookupResult> done)
            { yield return null; var copy=JsonUtility.FromJson<MoleculeLookupResult>(JsonUtility.ToJson(Next)); copy.smiles=smiles; done(copy); }
        }
        static void AssertInventory(LabController lab)
        {
            var views=Object.FindObjectsByType<MoleculeView>(); var atoms=Object.FindObjectsByType<LooseAtom>();
            var free=lab.FreeView!=null?lab.FreeView.Structure.TokenIds.ToArray():new int[0];
            var ids=views.SelectMany(v=>v.TokenIds.Where(id=>id>=0)).Concat(atoms.Select(a=>a.TokenId)).Concat(free).OrderBy(x=>x).ToArray();
            Assert.That(ids,Is.EqualTo(lab.Inventory.Live.Select(t=>t.Id).OrderBy(x=>x).ToArray()),"Every atom in play appears exactly once.");
            foreach(var atom in atoms) Assert.That(lab.Inventory.Tokens[atom.TokenId].Owner,Is.EqualTo(atom.OnShelf?AtomInventory.Shelf:atom.Owner));
        }
        static LooseAtom Shelf(string symbol) => Object.FindObjectsByType<LooseAtom>().First(a=>a.Element.symbol==symbol && a.OnShelf);
        static MoleculeView Assemble(LabController lab,string id)
        {
            var definition=lab.molecules.First(m=>m.id==id); lab.StartMolecule(definition); var view=lab.Selected;
            for(var i=0;i<definition.atoms.Length;i++)
            {
                if(view.State.IsOccupied(i)) continue;
                var atom=Object.FindObjectsByType<LooseAtom>().First(a=>a.Element==definition.atoms[i].element && a.OnShelf);
                atom.transform.position=view.SitePosition(i); Assert.That(lab.TryAttach(atom),Is.True);
            }
            Assert.That(view.State.Complete,Is.True); return view;
        }

        [UnityTest]
        public IEnumerator BeakersElementsBinsAndGravity()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.seedTransferDuration=0; lab.freeRetrySeconds=600;
            var fake=new FakeLookup(); lab.MoleculeLookup=fake;
            lab.UseGalleryFile(null); lab.ResetLab(); yield return null;

            // Nine elements, each in its own endless beaker of small atoms; the prefab's baskets are gone.
            foreach(var symbol in new[]{"H","C","O","N","S","P","F","Cl","Br"})
            {
                Assert.That(lab.elements.Any(e=>e!=null && e.symbol==symbol),Is.True,symbol);
                Assert.That(Object.FindObjectsByType<LooseAtom>().Count(a=>a.Element.symbol==symbol),Is.EqualTo(BeakerShelf.AtomsPerBeaker),symbol);
                Assert.That(GameObject.Find("Beaker "+symbol),Is.Not.Null,symbol);
            }
            Assert.That(lab.Inventory.SocketCount,Is.EqualTo(9*BeakerShelf.AtomsPerBeaker)); Assert.That(lab.Inventory.Unlimited,Is.True);
            var reachable=GameObject.Find("Reachable atom shelf"); Assert.That(reachable==null || !reachable.activeInHierarchy,Is.True);
            var carbonInBeaker=Shelf("C"); var beaker=GameObject.Find("Beaker C").transform;
            Assert.That(Vector2.Distance(new Vector2(carbonInBeaker.transform.position.x,carbonInBeaker.transform.position.z),new Vector2(beaker.position.x,beaker.position.z)),Is.LessThan(BeakerShelf.BeakerRadius),"Atoms sit inside the glass.");
            Assert.That(carbonInBeaker.transform.lossyScale.x,Is.EqualTo(.066f).Within(.006f),"Carbon is about tennis-ball size.");

            // Solid room: walls and furniture block held atoms (velocity tracking needs enabled colliders).
            Assert.That(GameObject.Find("Rear wall").GetComponent<Collider>().enabled,Is.True);
            Assert.That(Shelf("C").Grab.movementType,Is.EqualTo(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking));

            // Gallery room: the old front wall is open and a table waits behind the player.
            Assert.That(GameObject.Find("Front wall"),Is.Null); Assert.That(GameObject.Find("Gallery table 1"),Is.Not.Null);
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.Zero);

            // Finishing a molecule no longer adds it to the gallery; the player chooses with the bins.
            var water=Assemble(lab,"water");
            Assert.That(lab.Inventory.Available("O"),Is.EqualTo(lab.Inventory.Capacity("O"))); Assert.That(lab.Inventory.Available("H"),Is.EqualTo(lab.Inventory.Capacity("H")));
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.Zero,"Nothing is kept until it is put in the gallery bin.");
            AssertInventory(lab);

            // Gravity: a finished molecule is a dynamic body and falls onto the bench.
            Assert.That(water.Body.isKinematic,Is.False); Assert.That(water.Body.useGravity,Is.True);
            var start=water.transform.position.y;
            for(var deadline=EditorApplication.timeSinceStartup+1.5;EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(water.transform.position.y,Is.LessThan(start-.15f)); Assert.That(water.transform.position.y,Is.GreaterThan(lab.Environment.bench.position.y),"It lands on the bench, not through it.");

            // Gallery bin: dropped in, the molecule is recorded and leaves the bench.
            water.transform.position=lab.GalleryBin.transform.position+Vector3.up*1f; water.Body.position=water.transform.position; water.Body.linearVelocity=Vector3.zero;
            for(var deadline=EditorApplication.timeSinceStartup+3;water!=null && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(water==null,Is.True,water==null?"":"The molecule should have fallen into the gallery bin; it is at "+water.transform.position+", bin at "+lab.GalleryBin.transform.position+", kinematic="+water.Body.isKinematic+", docked="+water.Docked);
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.EqualTo(1)); Assert.That(lab.Gallery.Entries[0].id,Is.EqualTo("water"));
            Assert.That(lab.GalleryBin.LastFeedback,Does.Contain("Water")); AssertInventory(lab);

            // A second water into the gallery only raises its count.
            lab.MoleculeIntoBin(Assemble(lab,"water"),true); yield return null;
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.EqualTo(1)); Assert.That(lab.Gallery.Entries[0].timesBuilt,Is.EqualTo(2));

            // Trash bin: discarded, never recorded.
            var methane=Assemble(lab,"methane"); lab.MoleculeIntoBin(methane,false); yield return null;
            Assert.That(methane==null,Is.True); Assert.That(lab.Gallery.Entries.Count,Is.EqualTo(1)); Assert.That(lab.TrashBin.LastFeedback,Does.Contain("Discarded"));
            AssertInventory(lab);

            // An unfinished molecule cannot be binned.
            lab.StartMolecule(lab.molecules.First(m=>m.id=="water")); var unfinished=lab.Selected;
            lab.MoleculeIntoBin(unfinished,true); yield return null;
            Assert.That(unfinished!=null,Is.True); Assert.That(lab.Gallery.Entries.Count,Is.EqualTo(1));
            lab.ResetLab(); yield return null;

            // An atom put back in its beaker leaves play, because the beaker already refilled.
            var carbon=Shelf("C"); Assert.That(carbon.TakeFromShelf(),Is.True);
            Assert.That(lab.Inventory.Available("C"),Is.EqualTo(lab.Inventory.Capacity("C")));
            carbon.transform.position+=Vector3.up*.05f; lab.AtomReleased(carbon); yield return null;
            Assert.That(carbon==null,Is.True,"Returned atom retired.");
            Assert.That(Object.FindObjectsByType<LooseAtom>().Count(a=>a.Element.symbol=="C"),Is.EqualTo(BeakerShelf.AtomsPerBeaker));
            AssertInventory(lab);

            // An atom taken out and left unused despawns after idleAtomSeconds.
            lab.idleAtomSeconds=1; var idle=Shelf("O"); Assert.That(idle.TakeFromShelf(),Is.True);
            idle.transform.position=lab.BenchDropPosition; idle.MarkReleased();
            for(var deadline=EditorApplication.timeSinceStartup+.5;EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(idle!=null,Is.True,"Still there before the timeout.");
            for(var deadline=EditorApplication.timeSinceStartup+2;idle!=null && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(idle==null,Is.True,"Idle atom despawned."); AssertInventory(lab);
            lab.idleAtomSeconds=10;

            // Free build with a new element: ammonia is identified, then kept via the gallery bin.
            lab.SetMode(LabMode.Free);
            var nitrogen=Shelf("N"); nitrogen.transform.position=lab.FreeBuildPosition; Assert.That(lab.TryAttach(nitrogen),Is.True);
            Assert.That(lab.FreeView.StatusText,Does.Contain("3 open bonds"));
            Assert.That(lab.FreeView.transform.position.y-lab.BenchTop,Is.LessThan(.25f),"Free build starts just above the bench.");
            fake.Next=new MoleculeLookupResult{status=LookupStatus.Found,cid=222,title="Ammonia",iupacName="azane",molecularFormula="H3N"};
            for(var i=0;i<3;i++)
            {
                var hydrogen=Shelf("H"); hydrogen.transform.position=lab.FreeView.GuideWorldPositions(0).First();
                Assert.That(lab.TryAttach(hydrogen),Is.True);
            }
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            yield return null;
            Assert.That(lab.FreeView,Is.Null); Assert.That(lab.Selected.Definition.displayName,Is.EqualTo("Ammonia"));
            Assert.That(lab.Selected.GetComponentsInChildren<SphereCollider>().Min(c=>c.bounds.min.y),Is.GreaterThan(lab.BenchTop-.005f),"The structure was lifted clear of the bench.");
            Assert.That(lab.Gallery.Entries.Count,Is.EqualTo(1),"Not kept until binned.");
            lab.MoleculeIntoBin(lab.Selected,true); yield return null;
            Assert.That(lab.Gallery.Entries.Select(e=>e.id),Does.Contain("pubchem-222")); Assert.That(lab.GalleryRoom.ExhibitCount,Is.EqualTo(2));
            Assert.That(lab.Inventory.Available("N"),Is.EqualTo(BeakerShelf.AtomsPerBeaker));
            AssertInventory(lab);

            // The red reset button clears the bench and keeps the gallery.
            Assemble(lab,"water"); lab.SetMode(LabMode.Free);
            var presses=lab.ResetControl.Presses; lab.ResetControl.Fire();
            lab.ResetControl.Fire(); Assert.That(lab.ResetControl.Presses,Is.EqualTo(presses+1),"One press per push: it re-arms only when released.");
            yield return null;
            Assert.That(lab.Mode,Is.EqualTo(LabMode.Build));
            Assert.That(Object.FindObjectsByType<MoleculeView>().Length,Is.Zero); Assert.That(lab.Gallery.Entries.Count,Is.EqualTo(2),"The gallery survives a reset.");

            // Clearing the gallery (settings) needs two presses.
            lab.ClearGallery(); Assert.That(lab.Gallery.Entries.Count,Is.EqualTo(2));
            lab.ClearGallery(); yield return null;
            Assert.That(lab.Gallery.Entries.Count,Is.Zero); Assert.That(lab.GalleryRoom.ExhibitCount,Is.Zero);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ResetButtonAndTeleport()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.UseGalleryFile(null);
            var button=lab.ResetControl; Assert.That(button,Is.Not.Null);
            // Only a fingertip pressing down onto the cap counts; hovering above or beside it does not.
            Assert.That(button.PushDepth(button.CapTop+Vector3.up*.03f),Is.Zero);
            Assert.That(button.PushDepth(button.CapTop+Vector3.right*.2f),Is.Zero);
            Assert.That(button.PushDepth(button.CapTop+Vector3.down*.03f),Is.EqualTo(ResetButton.Travel).Within(1e-4f));
            Assert.That(button.transform.position.y,Is.GreaterThan(lab.BenchTop-.01f),"The button sits on the bench.");
            // Controller teleport onto the lab and gallery floors.
            Assert.That(lab.TeleportReady,Is.True);
            Assert.That(lab.TeleportAreas.Count,Is.GreaterThanOrEqualTo(2));
            Assert.That(Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>().isActiveAndEnabled,Is.True);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BrightClassroomIsDressedAndStillPlayable()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.UseGalleryFile(null);
            var room=lab.Classroom; Assert.That(room,Is.Not.Null);
            Assert.That(room.Windows,Is.EqualTo(2)); Assert.That(room.Whiteboard,Is.Not.Null);
            Assert.That(GameObject.Find("Opaque daylight glazing"),Is.Null,"The old small window is replaced.");
            // Visual dressing only: walls and floors keep their colliders, so physics and teleport still work.
            Assert.That(GameObject.Find("Rear wall").GetComponent<Collider>().enabled,Is.True);
            Assert.That(lab.TeleportReady,Is.True);
            if(lab.classroomKit!=null && lab.classroomKit.labCounter!=null)
            {
                Assert.That(room.PlacedModels,Is.GreaterThan(10),"Furniture and glassware from the lab pack.");
                Assert.That(GameObject.Find("Lab counter 1"),Is.Not.Null); Assert.That(GameObject.Find("Teacher desk"),Is.Not.Null);
                Assert.That(GameObject.Find("Rear counter"),Is.Null,"The old counters are replaced.");
                // Nothing placed in the play area around the bench.
                foreach(var collider in room.GetComponentsInChildren<BoxCollider>())
                {
                    var b=collider.bounds;
                    Assert.That(b.max.x<-1.6f || b.min.x>1.6f || b.min.z>1.35f || b.max.z<-1.6f,Is.True,collider.name+" intrudes on the play area: "+b);
                }
            }
            if(room.Skipped.Count>0) Debug.Log("Classroom skipped: "+string.Join(", ",room.Skipped));
            foreach(var renderer in room.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)
                    if(material!=null) Assert.That(material.shader.name,Does.Not.Contain("InternalError"),renderer.name+" would render pink.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ReactionStationRunsWithTheRightTool()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=EditorApplication.timeSinceStartup+10;!lab.Ready && EditorApplication.timeSinceStartup<startWait;) yield return null;
            lab.seedTransferDuration=0; lab.UseGalleryFile(null); lab.ResetLab(); yield return null;
            Assert.That(lab.Station,Is.Not.Null); Assert.That(lab.Station.Tools.Count,Is.EqualTo(3));
            Assert.That(lab.ReactionStatus(),Does.Contain("Drop finished molecules"));
            // Start from a different reaction: the vessel switches by itself when hydrogen goes in.
            lab.SelectReaction(lab.reactions.First(r=>r.requiredCondition==ReactionCondition.Ignition && r.inputs.Any(p=>p.molecule.id=="methane")));
            var molecules=new[]{Assemble(lab,"hydrogen"),Assemble(lab,"hydrogen"),Assemble(lab,"oxygen")};
            foreach(var view in molecules) { view.transform.position=lab.Environment.tray.position+Vector3.up*.2f; lab.MoleculeReleased(view); Assert.That(view.Docked,Is.True,view.Definition.id); }
            Assert.That(lab.ActiveReaction.inputs.Any(p=>p.molecule.id=="hydrogen"),Is.True,"Switched to the water reaction.");
            Assert.That(lab.ReactionStatus(),Does.Contain("FLAME"));
            // The wrong tool explains itself and does nothing.
            lab.Station.Tools.First(t=>t.Condition==ReactionCondition.PalladiumCatalyst).UseOnVessel();
            Assert.That(lab.IsReacting,Is.False); Assert.That(lab.Station.Label.text,Does.Contain("needs the FLAME"));
            // The flame runs it: atoms rearrange into two waters.
            lab.Station.Tools.First(t=>t.Condition==ReactionCondition.Ignition).UseOnVessel();
            Assert.That(lab.IsReacting,Is.True);
            for(var deadline=EditorApplication.timeSinceStartup+8;lab.IsReacting && EditorApplication.timeSinceStartup<deadline;) yield return null;
            yield return null;
            Assert.That(Object.FindObjectsByType<MoleculeView>().Count(v=>v.Definition.id=="water"),Is.EqualTo(2));
            // A molecule no reaction uses is refused with a message.
            var ethane=Assemble(lab,"ethane"); ethane.transform.position=lab.Environment.tray.position+Vector3.up*.2f; lab.MoleculeReleased(ethane);
            Assert.That(ethane.Docked,Is.False); Assert.That(lab.Station.Label.text,Does.Contain("No reaction"));
            yield return new ExitPlayMode();
        }

        sealed class FakeInfo : IMoleculeInfoSource
        {
            public int Calls;
            public IEnumerator Describe(int cid,Action<MoleculeInfo> done)
            {
                Calls++; yield return null;
                done(new MoleculeInfo{status=LookupStatus.Found,cid=cid,descriptionSource="ChEBI",
                    description="Water is an oxygen hydride consisting of an oxygen atom that is covalently bonded to two hydrogen atoms.",
                    uses=new[]{"Used as a solvent, suspending agent and industrial coolant."}});
            }
        }
        [UnityTest]
        public IEnumerator PickingUpAnExhibitShowsItsHologram()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.seedTransferDuration=0;
            var info=new FakeInfo();
            lab.MoleculeLookup=new FakeLookup{Next=new MoleculeLookupResult{status=LookupStatus.Found,cid=962,title="Water",iupacName="oxidane",molecularFormula="H2O"}};
            lab.MoleculeInfoSource=info; lab.hologramLinger=.2f; lab.UseGalleryFile(null); lab.ResetLab(); yield return null;
            lab.MoleculeIntoBin(Assemble(lab,"water"),true); yield return null;
            var exhibit=lab.GalleryRoom.Exhibits.First();
            Assert.That(lab.Hologram.Visible,Is.False);

            lab.ExhibitGrabbed(exhibit);
            Assert.That(lab.Hologram.Visible,Is.True); Assert.That(lab.Hologram.Title,Is.EqualTo("Water"));
            for(var deadline=EditorApplication.timeSinceStartup+3;!lab.Hologram.Body.Contains("oxygen hydride") && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(lab.Hologram.Body,Does.Contain("oxygen hydride")); Assert.That(lab.Hologram.Body,Does.Contain("Commonly used for"));
            Assert.That(lab.Hologram.Body,Does.Contain("industrial coolant")); Assert.That(lab.Hologram.Footer,Does.Contain("CID 962"));
            Assert.That(Vector3.Distance(lab.Hologram.transform.position,exhibit.transform.position),Is.LessThan(.6f),"The screen floats next to the exhibit.");

            lab.ExhibitReleased(exhibit);
            for(var deadline=EditorApplication.timeSinceStartup+1;lab.Hologram.Visible && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(lab.Hologram.Visible,Is.False);

            // A second pick-up uses the remembered answer: no new PubChem request.
            lab.ExhibitGrabbed(exhibit);
            Assert.That(lab.Hologram.Body,Does.Contain("oxygen hydride")); Assert.That(info.Calls,Is.EqualTo(1));
            yield return new ExitPlayMode();
        }

        [Test]
        public void GalleryFileSurvivesARestart()
        {
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"moleculelab-gallery-test-"+Guid.NewGuid().ToString("N")+".json");
            var water=AssetDatabase.LoadAssetAtPath<MoleculeDefinition>("Assets/MoleculeLab/Data/Water.asset");
            try
            {
                var gallery=new MoleculeGallery(path);
                Assert.That(gallery.Record(water),Is.True); Assert.That(gallery.Record(water),Is.False);
                var reopened=new MoleculeGallery(path);
                Assert.That(reopened.Entries.Count,Is.EqualTo(1));
                var entry=reopened.Entries[0];
                Assert.That(entry.id,Is.EqualTo("water")); Assert.That(entry.timesBuilt,Is.EqualTo(2));
                Assert.That(entry.symbols.Length,Is.EqualTo(3)); Assert.That(entry.positions.Length,Is.EqualTo(3)); Assert.That(entry.bondA.Length,Is.EqualTo(2));
                reopened.Clear(); Assert.That(new MoleculeGallery(path).Entries.Count,Is.Zero);
                Assert.That(new MoleculeGallery(null).Entries.Count,Is.Zero);
            }
            finally { if(System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        }
    }
}
