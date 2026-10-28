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
    public sealed class FreeBuildPlaythroughTests
    {
        sealed class FakeLookup : IMoleculeLookup
        {
            public MoleculeLookupResult Next=new MoleculeLookupResult{status=LookupStatus.Error,message="No connection to PubChem."};
            public bool Hang;
            public string LastSmiles;
            public int Calls;
            public IEnumerator Lookup(string smiles,Action<MoleculeLookupResult> done)
            {
                Calls++; LastSmiles=smiles; yield return null;
                while(Hang) yield return null;
                var copy=JsonUtility.FromJson<MoleculeLookupResult>(JsonUtility.ToJson(Next)); copy.smiles=smiles; done(copy);
            }
        }
        static void AssertInventory(LabController lab)
        {
            var views=Object.FindObjectsByType<MoleculeView>(); var atoms=Object.FindObjectsByType<LooseAtom>();
            var free=lab.FreeView!=null?lab.FreeView.Structure.TokenIds.ToArray():new int[0];
            var ids=views.SelectMany(v=>v.TokenIds.Where(id=>id>=0)).Concat(atoms.Select(a=>a.TokenId)).Concat(free).ToArray();
            var live=lab.Inventory.Live.Select(t=>t.Id).OrderBy(x=>x).ToArray();
            Assert.That(ids.OrderBy(x=>x).ToArray(),Is.EqualTo(live),"Every atom in play appears exactly once.");
            foreach(var id in free) Assert.That(lab.Inventory.Tokens[id].Owner,Is.EqualTo(lab.FreeView.Owner));
            foreach(var v in views) foreach(var id in v.TokenIds.Where(id=>id>=0)) Assert.That(lab.Inventory.Tokens[id].Owner,Is.EqualTo(v.InventoryOwner));
        }
        static LooseAtom Shelf(string symbol) => Object.FindObjectsByType<LooseAtom>().First(a=>a.Element.symbol==symbol && a.OnShelf);
        /// <summary>Simulates picking a basket atom and releasing it on the first guide dot of an atom.</summary>
        static void Place(LabController lab,string symbol,int anchor)
        {
            var atom=Shelf(symbol);
            atom.transform.position=lab.FreeView.GuideWorldPositions(anchor).First();
            Assert.That(lab.TryAttach(atom),Is.True,"Could not bond "+symbol+" to atom "+anchor);
        }
        static void Drop(LabController lab,string symbol)
        {
            var atom=Shelf(symbol); atom.transform.position=lab.FreeBuildPosition;
            Assert.That(lab.TryAttach(atom),Is.True,"Dropping "+symbol+" on the build spot should start a structure.");
        }
        static Transform AtomShape(FreeBuildView view,int index) => view.GetComponentsInChildren<FreeAtomHandle>().First(h=>h.Index==index).transform;

        [UnityTest]
        public IEnumerator SnappingChecksPubChemAutomaticallyWhenComplete()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.seedTransferDuration=0; lab.freeRetrySeconds=600;
            lab.UseGalleryFile(null); lab.ResetLab(); yield return null;
            var fake=new FakeLookup(); lab.MoleculeLookup=fake;
            lab.SetMode(LabMode.Free); Assert.That(lab.Mode,Is.EqualTo(LabMode.Free));

            // Acetic acid, built only by dropping and snapping atoms plus one push-together double bond.
            Drop(lab,"C"); var view=lab.FreeView;
            Assert.That(view.StatusText,Does.Contain("Not complete: 4 open bonds"));
            Place(lab,"C",0); Assert.That(view.StatusText,Does.Contain("6 open bonds"));
            Place(lab,"O",1);
            lab.FreeBondRequested(view,1,2); Assert.That(view.BondText(),Does.Contain("double"));
            Place(lab,"O",1);
            for(var i=0;i<3;i++) Place(lab,"H",0);
            Assert.That(view.StatusText,Does.Contain("Not complete: 1 open bond")); Assert.That(view.StatusText,Does.Contain("O4 needs 1"));
            Assert.That(fake.Calls,Is.Zero,"Incomplete structures are never sent to PubChem.");
            fake.Next=PubChemFixtures.Found(176,"Acetic Acid","acetic acid","C2H4O2",PubChemFixtures.AceticAcidSdf);
            Place(lab,"H",3);
            Assert.That(lab.IsCheckingStructure,Is.True,"Completing the structure starts the check by itself.");
            Assert.That(view.Locked,Is.True); Assert.That(view.StatusText,Does.Contain("Checking PubChem"));
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            yield return null;
            Assert.That(fake.LastSmiles,Is.EqualTo("[CH3][C](=[O])[OH]"));
            Assert.That(lab.FreeView,Is.Null,"An identified molecule is locked into an ordinary molecule.");
            Assert.That(lab.Selected.Definition.id,Is.EqualTo("pubchem-176"));
            Assert.That(lab.Selected.Definition.displayName,Is.EqualTo("Acetic Acid"));
            Assert.That(lab.Selected.GetComponentsInChildren<TMPro.TextMeshPro>().Any(t=>t.text.Contains("Acetic Acid")),Is.True);
            Assert.That(lab.Selected.State.Complete,Is.True); Assert.That(lab.Discoveries,Does.Contain("Acetic Acid"));
            AssertInventory(lab);

            // Water matches the curated recipe, so the product is the reaction-ready catalogue definition.
            fake.Next=PubChemFixtures.Found(962,"Water","oxidane","H2O",null);
            Drop(lab,"O"); Place(lab,"H",0); Place(lab,"H",0);
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            yield return null;
            Assert.That(lab.FreeView,Is.Null); Assert.That(lab.Selected.Definition,Is.SameAs(lab.molecules.First(m=>m.id=="water")));
            AssertInventory(lab);

            // Not in PubChem: stays editable with a clear label; pulling an H off makes it incomplete again.
            fake.Next=new MoleculeLookupResult{status=LookupStatus.NotFound,message="none"};
            Drop(lab,"O"); Place(lab,"H",0); Place(lab,"H",0); view=lab.FreeView;
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(lab.FreeView,Is.Not.Null); Assert.That(view.Locked,Is.False); Assert.That(view.StatusText,Does.Contain("not in PubChem"));
            lab.FreeAtomPulled(view,2); yield return null;
            Assert.That(lab.Inventory.Available("H"),Is.EqualTo(lab.Inventory.Capacity("H")),"The beaker stays full; the pulled atom leaves play."); Assert.That(view.StatusText,Does.Contain("Not complete: 1 open bond"));
            AssertInventory(lab);

            // Offline, but a catalogue match is still recognised.
            fake.Next=new MoleculeLookupResult{status=LookupStatus.Error,message="No connection to PubChem."};
            Place(lab,"H",0);
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            yield return null;
            Assert.That(lab.FreeView,Is.Null); Assert.That(lab.Selected.Definition.id,Is.EqualTo("water"));
            AssertInventory(lab);

            // Offline and unknown to the catalogue (formaldehyde): stays editable and says it will retry.
            Drop(lab,"C"); Place(lab,"O",0); view=lab.FreeView; lab.FreeBondRequested(view,0,1);
            Place(lab,"H",0); Place(lab,"H",0);
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(lab.FreeView,Is.Not.Null); Assert.That(view.Structure.Graph.Formula(),Is.EqualTo("CH2O"));
            Assert.That(view.StatusText,Does.Contain("retrying")); AssertInventory(lab);

            // Leaving free build returns the unfinished structure.
            lab.SetMode(LabMode.Build); yield return null;
            Assert.That(lab.FreeView,Is.Null); AssertInventory(lab);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SqueezingBondedAtomsMakesADoubleBondWithoutMovingTheMolecule()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.seedTransferDuration=0; lab.freeRetrySeconds=600;
            var fake=new FakeLookup{Next=new MoleculeLookupResult{status=LookupStatus.NotFound,message="none"}};
            lab.MoleculeLookup=fake; lab.UseGalleryFile(null); lab.ResetLab(); yield return null;
            lab.SetMode(LabMode.Free); Drop(lab,"C"); Place(lab,"C",0);
            var view=lab.FreeView; yield return null;
            Vector3 Anchor() => view.transform.TransformPoint(view.Structure.Position(0));
            var before=Anchor();

            // Pull the second carbon away (no gesture) and let go: it springs back; the first carbon stays put.
            view.BeginDrag(1); AtomShape(view,1).position+=new Vector3(.12f,.05f,0); yield return null;
            view.EndDrag(1,true); yield return null; yield return null;
            Assert.That(Vector3.Distance(Anchor(),before),Is.LessThan(.01f),"Releasing a dragged atom must not move the molecule.");
            // Let the released atom glide back before grabbing it again (frame times vary in the editor).
            for(var settle=EditorApplication.timeSinceStartup+.6;EditorApplication.timeSinceStartup<settle;) yield return null;
            Assert.That(view.Structure.Graph.Bonds[0].Order,Is.EqualTo(1));

            // Squeeze it into the first carbon along the bond: double bond, molecule still in place.
            var axis=(AtomShape(view,1).position-AtomShape(view,0).position).normalized;
            view.BeginDrag(1); AtomShape(view,1).position=AtomShape(view,0).position+axis*.1f; yield return null;
            Assert.That(view.EvaluateGesture(1,out var gesture,out var partner,out var ready,out var text),Is.True);
            Assert.That(gesture,Is.EqualTo(FreeGesture.Bond),$"d01={Vector3.Distance(AtomShape(view,1).position,AtomShape(view,0).position):0.000} text={text} structure01={Vector3.Distance(view.Structure.Position(0),view.Structure.Position(1)):0.000}");
            Assert.That(partner,Is.EqualTo(0)); Assert.That(ready,Is.True,text);
            view.EndDrag(1,true); yield return null; yield return null;
            Assert.That(view.Structure.Graph.Bonds[0].Order,Is.EqualTo(2));
            Assert.That(Vector3.Distance(Anchor(),before),Is.LessThan(.01f));

            // After the hydrogens are on, squeezing explains why no further bond is possible.
            lab.FillHydrogens(); yield return null;
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(lab.FreeView,Is.SameAs(view),"Ethene not found in this test, so it stays editable.");
            axis=(AtomShape(view,1).position-AtomShape(view,0).position).normalized;
            view.BeginDrag(1); AtomShape(view,1).position=AtomShape(view,0).position+axis*.1f; yield return null;
            Assert.That(view.EvaluateGesture(1,out gesture,out _,out ready,out text),Is.True);
            Assert.That(gesture,Is.EqualTo(FreeGesture.None)); Assert.That(ready,Is.False); Assert.That(text,Does.Contain("no spare bond"));
            view.EndDrag(1,true); yield return null;
            Assert.That(view.Structure.Graph.Bonds[0].Order,Is.EqualTo(2));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GesturesRingsRemovalAndResetKeepEveryAtom()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.seedTransferDuration=0; lab.freeRetrySeconds=600;
            lab.UseGalleryFile(null); lab.ResetLab(); yield return null;
            var fake=new FakeLookup(); lab.MoleculeLookup=fake;
            lab.StartFreeStructure("C"); Assert.That(lab.Mode,Is.EqualTo(LabMode.Free));
            for(var i=0;i<3;i++) Place(lab,"C",i);
            var view=lab.FreeView;

            // Grab the chain end, push it into the first carbon and let go: the ring closes.
            view.BeginDrag(3);
            AtomShape(view,3).position=AtomShape(view,0).position+Vector3.right*.05f;
            Assert.That(view.EvaluateGesture(3,out var gesture,out var partner,out var ready,out var text),Is.True);
            Assert.That(gesture,Is.EqualTo(FreeGesture.Bond)); Assert.That(partner,Is.EqualTo(0)); Assert.That(ready,Is.True,text);
            view.EndDrag(3,true); yield return null;
            Assert.That(view.Structure.Graph.Bonds.Count,Is.EqualTo(4)); Assert.That(view.Structure.GraphDistance(3,0),Is.EqualTo(1));

            // Pushing two bonded carbons together raises the bond order.
            lab.FreeBondRequested(view,0,1); Assert.That(view.BondText(),Does.Contain("double"));
            lab.FillHydrogens(); yield return null;
            Assert.That(view.Structure.Graph.Formula(),Is.EqualTo("C4H6"));
            for(var deadline=EditorApplication.timeSinceStartup+5;lab.IsCheckingStructure && EditorApplication.timeSinceStartup<deadline;) yield return null;
            Assert.That(fake.Calls,Is.EqualTo(1)); Assert.That(view.StatusText,Does.Contain("retrying"));
            AssertInventory(lab);

            // Pull the last hydrogen far away and let go: it leaves the structure (the beaker is endless).
            var hydrogen=view.Structure.Count-1;
            view.BeginDrag(hydrogen);
            AtomShape(view,hydrogen).position+=Vector3.up*.6f;
            Assert.That(view.EvaluateGesture(hydrogen,out gesture,out _,out ready,out _),Is.True);
            Assert.That(gesture,Is.EqualTo(FreeGesture.Remove)); Assert.That(ready,Is.True);
            view.EndDrag(hydrogen,true); yield return null;
            Assert.That(lab.Inventory.Available("H"),Is.EqualTo(lab.Inventory.Capacity("H"))); Assert.That(view.StatusText,Does.Contain("Not complete"));
            AssertInventory(lab);

            // A carbon holding other atoms cannot be pulled off; the structure is unchanged.
            var count=view.Structure.Count; lab.FreeAtomPulled(view,0); yield return null;
            Assert.That(view.Structure.Count,Is.EqualTo(count));

            // Undo removes the newest atom; Fill hydrogens completes the structure again.
            count=view.Structure.Count; lab.UndoFreeAtom(); yield return null;
            Assert.That(view.Structure.Count,Is.EqualTo(count-1)); AssertInventory(lab);

            // Switching tabs while PubChem is still answering cancels the check instead of blocking the menu.
            fake.Hang=true; lab.FillHydrogens();
            Assert.That(lab.IsCheckingStructure,Is.True);
            lab.SetMode(LabMode.Build); Assert.That(lab.Mode,Is.EqualTo(LabMode.Build),"Tabs always switch."); Assert.That(lab.IsCheckingStructure,Is.False);
            Assert.That(lab.FreeView,Is.Null); AssertInventory(lab);

            // Clear while checking: no late conversion, the structure is gone.
            lab.SetMode(LabMode.Free); Drop(lab,"O"); Place(lab,"H",0); Place(lab,"H",0);
            Assert.That(lab.IsCheckingStructure,Is.True);
            lab.ClearFreeStructure(true); fake.Hang=false; yield return null; yield return null;
            Assert.That(lab.IsCheckingStructure,Is.False); Assert.That(lab.FreeView,Is.Null);
            Assert.That(Object.FindObjectsByType<MoleculeView>().Length,Is.Zero); AssertInventory(lab);

            // Reset while PubChem is still answering: no late conversion, every atom home.
            fake.Hang=true; lab.SetMode(LabMode.Free); Drop(lab,"O"); Place(lab,"H",0); Place(lab,"H",0);
            Assert.That(lab.IsCheckingStructure,Is.True);
            lab.ResetLab(); fake.Hang=false; yield return null; yield return null;
            Assert.That(lab.IsCheckingStructure,Is.False); Assert.That(lab.FreeView,Is.Null);
            Assert.That(Object.FindObjectsByType<MoleculeView>().Length,Is.Zero);
            Assert.That(Object.FindObjectsByType<LooseAtom>().Length,Is.EqualTo(lab.Inventory.SocketCount));
            AssertInventory(lab);

            // Dropping a lone atom starts a structure; pulling that atom away again empties it.
            lab.SetMode(LabMode.Free); Drop(lab,"O");
            Assert.That(lab.FreeView.Structure.Count,Is.EqualTo(1)); AssertInventory(lab);
            lab.FreeAtomPulled(lab.FreeView,0); yield return null;
            Assert.That(lab.FreeView,Is.Null); Assert.That(lab.Inventory.Available("O"),Is.EqualTo(lab.Inventory.Capacity("O")));
            AssertInventory(lab);
            yield return new ExitPlayMode();
        }
    }
}
