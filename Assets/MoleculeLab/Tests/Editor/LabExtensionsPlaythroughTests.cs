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
    // Unlimited baskets, extra elements, gallery and gravity (27 October update).
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
        public IEnumerator UnlimitedBasketsElementsGalleryAndGravity()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); lab.seedTransferDuration=0; lab.freeRetrySeconds=600;
            var fake=new FakeLookup(); lab.MoleculeLookup=fake;
            lab.UseGalleryFile(null); lab.SetUnlimitedAtoms(true); lab.ResetLab(); yield return null;

            // Extra elements and their shelf.
            foreach(var symbol in new[]{"N","S","P","F","Cl","Br"})
            {
                Assert.That(lab.elements.Any(e=>e!=null && e.symbol==symbol),Is.True,symbol);
                Assert.That(Object.FindObjectsByType<LooseAtom>().Count(a=>a.Element.symbol==symbol),Is.EqualTo(ElementRack.SocketsPerBasket),symbol);
            }
            Assert.That(lab.Inventory.SocketCount,Is.EqualTo(48+6*ElementRack.SocketsPerBasket));

            // Solid room: walls and furniture block held atoms (velocity tracking needs enabled colliders).
            Assert.That(GameObject.Find("Rear wall").GetComponent<Collider>().enabled,Is.True);
            Assert.That(Shelf("C").Grab.movementType,Is.EqualTo(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.VelocityTracking));

            // Gallery room: the old front wall is open and a table waits behind the player.
            Assert.That(GameObject.Find("Front wall"),Is.Null); Assert.That(GameObject.Find("Gallery table 1"),Is.Not.Null);
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.Zero);

            // Unlimited: building water twice never empties the oxygen basket; the gallery keeps one exhibit with a count.
            var water=Assemble(lab,"water");
            Assert.That(lab.Inventory.Available("O"),Is.EqualTo(8)); Assert.That(lab.Inventory.Available("H"),Is.EqualTo(32));
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.EqualTo(1)); Assert.That(lab.Gallery.Entries[0].id,Is.EqualTo("water"));
            AssertInventory(lab);

            // Gravity: a finished molecule is a dynamic body and falls onto the bench.
            Assert.That(water.Body.isKinematic,Is.False); Assert.That(water.Body.useGravity,Is.True);
            var start=water.transform.position.y;
            for(var deadline=EditorApplication.timeSinceStartup+1.5;EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(water.transform.position.y,Is.LessThan(start-.15f)); Assert.That(water.transform.position.y,Is.GreaterThan(lab.Environment.bench.position.y),"It lands on the bench, not through it.");
            water.transform.position=lab.Environment.tray.position+Vector3.up*.2f; lab.MoleculeReleased(water);
            Assert.That(water.Docked,Is.True); Assert.That(water.Body.isKinematic,Is.True,"Docked molecules stay put.");

            Assemble(lab,"water"); yield return null;
            Assert.That(lab.GalleryRoom.ExhibitCount,Is.EqualTo(1)); Assert.That(lab.Gallery.Entries[0].timesBuilt,Is.EqualTo(2));

            // An atom put back on its basket leaves play, because the socket was already refilled.
            var carbon=Shelf("C"); Assert.That(carbon.TakeFromShelf(),Is.True);
            Assert.That(lab.Inventory.Available("C"),Is.EqualTo(8));
            carbon.transform.position+=Vector3.up*.05f; lab.AtomReleased(carbon); yield return null;
            Assert.That(carbon==null,Is.True,"Returned atom retired.");
            Assert.That(Object.FindObjectsByType<LooseAtom>().Count(a=>a.Element.symbol=="C"),Is.EqualTo(8));
            AssertInventory(lab);

            // Free build with a new element: ammonia goes to the gallery as a PubChem discovery.
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
            Assert.That(lab.Gallery.Entries.Select(e=>e.id),Does.Contain("pubchem-222")); Assert.That(lab.GalleryRoom.ExhibitCount,Is.EqualTo(2));
            Assert.That(lab.Inventory.Available("N"),Is.EqualTo(ElementRack.SocketsPerBasket));
            AssertInventory(lab);

            // Clearing the gallery needs two presses.
            lab.ClearGallery(); Assert.That(lab.Gallery.Entries.Count,Is.EqualTo(2));
            lab.ClearGallery(); yield return null;
            Assert.That(lab.Gallery.Entries.Count,Is.Zero); Assert.That(lab.GalleryRoom.ExhibitCount,Is.Zero);
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
            var lab=Object.FindAnyObjectByType<LabController>(); lab.seedTransferDuration=0;
            var info=new FakeInfo();
            lab.MoleculeLookup=new FakeLookup{Next=new MoleculeLookupResult{status=LookupStatus.Found,cid=962,title="Water",iupacName="oxidane",molecularFormula="H2O"}};
            lab.MoleculeInfoSource=info; lab.hologramLinger=.2f; lab.UseGalleryFile(null); lab.ResetLab(); yield return null;
            Assemble(lab,"water"); yield return null;
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
