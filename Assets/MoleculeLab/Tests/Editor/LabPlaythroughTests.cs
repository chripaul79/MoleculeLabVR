using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MoleculeLab.Tests
{
    public sealed class LabPlaythroughTests
    {
        void VerifyTwoHandScaling(MoleculeView view)
        {
            var leftObject=new GameObject("Test left hand"); var rightObject=new GameObject("Test right hand");
            leftObject.AddComponent<SphereCollider>().isTrigger=true; rightObject.AddComponent<SphereCollider>().isTrigger=true;
            var left=leftObject.AddComponent<XRDirectInteractor>(); var right=rightObject.AddComponent<XRDirectInteractor>();
            var center=view.transform.position;
            left.transform.position=center+Vector3.left*.1f; right.transform.position=center+Vector3.right*.1f;
            var manager=view.Grab.interactionManager;
            manager.SelectEnter((IXRSelectInteractor)left,view.Grab); manager.SelectEnter((IXRSelectInteractor)right,view.Grab);
            Assert.That(view.Grab.interactorsSelecting.Count,Is.EqualTo(2));
            var transformer=view.GetComponent<MoleculeScaleTransformer>(); var pose=new Pose(center,Quaternion.identity); var scale=Vector3.one;
            transformer.OnGrabCountChanged(view.Grab,pose,scale);
            var midpoint=center+Vector3.right*.3f;
            left.transform.position=midpoint+Vector3.left*.2f; right.transform.position=midpoint+Vector3.right*.2f;
            transformer.Process(view.Grab,XRInteractionUpdateOrder.UpdatePhase.Dynamic,ref pose,ref scale);
            Assert.That(scale.x,Is.EqualTo(2).Within(.001f)); Assert.That(Vector3.Distance(pose.position,midpoint),Is.LessThan(.001f));
            left.transform.position=midpoint+Vector3.left*2; right.transform.position=midpoint+Vector3.right*2;
            transformer.Process(view.Grab,XRInteractionUpdateOrder.UpdatePhase.Dynamic,ref pose,ref scale);
            Assert.That(scale.x,Is.EqualTo(6).Within(.001f));
            manager.SelectCancel((IXRSelectInteractor)left,view.Grab); manager.SelectCancel((IXRSelectInteractor)right,view.Grab);
            Assert.That(view.Grab.isSelected,Is.False);
            Object.Destroy(leftObject); Object.Destroy(rightObject);
        }
        void AssertInventory(LabController lab)
        {
            var views=Object.FindObjectsByType<MoleculeView>(); var atoms=Object.FindObjectsByType<LooseAtom>();
            var ids=views.SelectMany(v=>v.TokenIds.Where(id=>id>=0)).Concat(atoms.Select(a=>a.TokenId)).ToArray();
            var live=lab.Inventory.Live.Select(t=>t.Id).OrderBy(x=>x).ToArray();
            Assert.That(ids.OrderBy(x=>x).ToArray(),Is.EqualTo(live),"Every atom in play appears exactly once.");
            foreach(var v in views) foreach(var id in v.TokenIds.Where(id=>id>=0)) Assert.That(lab.Inventory.Tokens[id].Owner,Is.EqualTo(v.InventoryOwner));
            foreach(var atom in atoms) Assert.That(lab.Inventory.Tokens[atom.TokenId].Owner,Is.EqualTo(atom.OnShelf?AtomInventory.Shelf:atom.Owner));
        }
        MoleculeView Assemble(LabController lab,string id)
        {
            var definition=lab.molecules.First(m=>m.id==id); lab.StartMolecule(definition);
            var view=lab.Selected;
            for(var i=0;i<definition.atoms.Length;i++)
            {
                if(view.State.IsOccupied(i)) continue;
                
                var atom=Object.FindObjectsByType<LooseAtom>().First(a=>a.Element==definition.atoms[i].element && a.OnShelf);
                atom.transform.position=view.SitePosition(i);
                Assert.That(lab.TryAttach(atom),Is.True,"Could not attach "+id+" site "+i);
            }
            Assert.That(view.State.Complete,Is.True); Assert.That(view.Grab.enabled,Is.True); return view;
        }
        [UnityTest]
        public IEnumerator ExpandedCatalogueAndReactionPlaythrough()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); lab.seedTransferDuration=0;
            // This lesson test covers the finite stock and must not write to the player's gallery file.
            lab.UseGalleryFile(null); lab.SetUnlimitedAtoms(false); lab.ResetLab(); yield return null;
            foreach(var graphic in GameObject.Find("Lab control panel").GetComponentsInChildren<UnityEngine.UI.Graphic>())
                Assert.That(graphic.gameObject.layer,Is.EqualTo(HandMenuRay.MenuLayer));
            foreach(var button in GameObject.Find("Lab control panel").GetComponentsInChildren<UnityEngine.UI.Button>())
                Assert.That(((RectTransform)button.transform).rect.height*button.transform.lossyScale.y,Is.GreaterThanOrEqualTo(.05f));
            Assert.That(lab.molecules.Length,Is.EqualTo(11)); Assert.That(lab.reactions.Length,Is.EqualTo(4));
            foreach(var definition in lab.molecules)
            {
                lab.ResetLab(); yield return null;
                var molecule=Assemble(lab,definition.id); yield return null; AssertInventory(lab);
                if(definition.id=="benzene")
                {
                    Assert.That(molecule.GetComponentsInChildren<LineRenderer>().Length,Is.EqualTo(1));
                    var original=molecule.SitePosition(1); molecule.Twist(90); molecule.Bend(15);
                    Assert.That(molecule.SitePosition(1),Is.EqualTo(original));
                }
                lab.RecycleSelected(); yield return null; AssertInventory(lab);
                Assert.That(lab.Inventory.Available("H"),Is.EqualTo(32));
            }
            foreach(var reaction in lab.reactions)
            {
                lab.ResetLab(); yield return null; lab.SelectReaction(reaction);
                foreach(var amount in reaction.inputs)
                    for(var n=0;n<amount.count;n++)
                    {
                        var input=Assemble(lab,amount.molecule.id);
                        // Dock while still in Build mode; the recipe persists across molecule selection.
                        Assert.That(lab.Mode,Is.EqualTo(LabMode.Build));
                        input.transform.position=lab.Environment.tray.position+Vector3.up*.2f; lab.MoleculeReleased(input);
                        Assert.That(input.Docked,Is.True);
                    }
                yield return null; AssertInventory(lab);
                lab.SetMode(LabMode.React); Assert.That(lab.CanActivate(out _),Is.False);
                lab.ActivateReaction(); Assert.That(lab.IsReacting,Is.False);
                lab.SelectCondition(reaction.requiredCondition); Assert.That(lab.CanActivate(out var reason),Is.True,reason);
                lab.ActivateReaction(); lab.ActivateReaction();
                var deadline=EditorApplication.timeSinceStartup+8;
                while(lab.IsReacting && EditorApplication.timeSinceStartup<deadline) yield return null;
                Assert.That(lab.IsReacting,Is.False); yield return null; AssertInventory(lab);
                foreach(var output in reaction.outputs) Assert.That(Object.FindObjectsByType<MoleculeView>().Count(v=>v.Definition==output.molecule),Is.EqualTo(output.count));
            }
            lab.ResetLab(); yield return null;
            var moleculeToKeep=Assemble(lab,"hydrogen"); moleculeToKeep.transform.position=lab.Environment.tray.position; lab.MoleculeReleased(moleculeToKeep);
            lab.SelectReaction(lab.reactions[1]); Assert.That(moleculeToKeep.Docked,Is.False); Assert.That(moleculeToKeep.State.Consumed,Is.False);
            yield return null; AssertInventory(lab);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator FullLessonAndResetSmokeTest()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode();
            yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); Assert.That(lab,Is.Not.Null);
            lab.seedTransferDuration=0;
            lab.UseGalleryFile(null); lab.SetUnlimitedAtoms(false); lab.ResetLab(); yield return null;
            Assert.That(Camera.main,Is.Not.Null);
            Assert.That(Object.FindObjectsByType<LooseAtom>().Length,Is.EqualTo(lab.Inventory.SocketCount));
            Assert.That(Object.FindObjectsByType<LabController>().Length,Is.EqualTo(1));
            var water=Assemble(lab,"water"); lab.SetMode(LabMode.Inspect); Assert.That(lab.Selected,Is.EqualTo(water));
            VerifyTwoHandScaling(water);
            var ethane=Assemble(lab,"ethane"); lab.SetMode(LabMode.Inspect);
            var length=Vector3.Distance(ethane.SitePosition(1),ethane.SitePosition(5));
            var original=ethane.SitePosition(5); ethane.Twist(60);
            Assert.That(Vector3.Distance(original,ethane.SitePosition(5)),Is.GreaterThan(.05f));
            Assert.That(Vector3.Distance(ethane.SitePosition(1),ethane.SitePosition(5)),Is.EqualTo(length).Within(.0001f));
            ethane.Bend(15); ethane.Bend(0);
            lab.BreakSelected(); yield return null;
            var fragments=Object.FindObjectsByType<MoleculeView>().Where(v=>v.FragmentPair!=null).ToArray();
            Assert.That(fragments.Length,Is.EqualTo(2)); AssertInventory(lab);
            fragments[0].transform.position=lab.BuildPosition; fragments[1].transform.position=lab.BuildPosition+Vector3.right*.1f;
            lab.MoleculeReleased(fragments[0]); yield return null;
            Assert.That(Object.FindObjectsByType<MoleculeView>().Count(v=>v.FragmentPair!=null),Is.EqualTo(0));
            AssertInventory(lab); Assert.That(lab.Selected.Definition.id,Is.EqualTo("ethane")); Assert.That(lab.Selected.State.Complete,Is.True);
            var a=Assemble(lab,"hydrogen"); var b=Assemble(lab,"hydrogen"); var c=Assemble(lab,"oxygen");
            lab.SetMode(LabMode.React);
            foreach(var view in new[]{a,b,c}) { view.transform.position=GameObject.Find("Reaction tray").transform.position+Vector3.up*.2f; lab.MoleculeReleased(view); Assert.That(view.Docked,Is.True); }
            lab.SelectCondition(ReactionCondition.Ignition); lab.ActivateReaction(); lab.SelectCondition(ReactionCondition.Ignition); lab.ActivateReaction();
            Assert.That(a.State.Consumed && b.State.Consumed && c.State.Consumed,Is.True,"Reaction must atomically consume its inputs.");
            var deadline=EditorApplication.timeSinceStartup+8;
            while((a!=null || b!=null || c!=null) && EditorApplication.timeSinceStartup<deadline) yield return null;
            Assert.That(a==null && b==null && c==null,Is.True);
            Assert.That(Object.FindObjectsByType<MoleculeView>().Count(v=>v.Definition.id=="water"),Is.EqualTo(3));
            AssertInventory(lab);
            lab.RecycleSelected(); yield return null; AssertInventory(lab);
            lab.ResetLab(); yield return null;
            Assert.That(Object.FindObjectsByType<MoleculeView>().Length,Is.EqualTo(0));
            Assert.That(lab.Selected,Is.Null);
            Assert.That(Object.FindObjectsByType<LooseAtom>().Length,Is.EqualTo(lab.Inventory.SocketCount));
            // Reset must cancel the reaction coroutine and cannot spawn delayed products.
            a=Assemble(lab,"hydrogen"); b=Assemble(lab,"hydrogen"); c=Assemble(lab,"oxygen"); lab.SetMode(LabMode.React);
            foreach(var view in new[]{a,b,c}) { view.transform.position=GameObject.Find("Reaction tray").transform.position+Vector3.up*.2f; lab.MoleculeReleased(view); }
            lab.SelectCondition(ReactionCondition.Ignition); lab.ActivateReaction(); lab.ResetLab();
            deadline=EditorApplication.timeSinceStartup+2.5;
            while(EditorApplication.timeSinceStartup<deadline) yield return null;
            Assert.That(Object.FindObjectsByType<MoleculeView>().Length,Is.EqualTo(0));
            Assert.That(lab.Selected,Is.Null);
            // Finite stock includes recipe seeds and does not replenish on repeated grabs.
            for(var i=0;i<8;i++) Assemble(lab,"methane");
            AssertInventory(lab); Assert.That(lab.Inventory.Available("C"),Is.Zero); Assert.That(lab.Inventory.Available("H"),Is.Zero);
            var count=Object.FindObjectsByType<MoleculeView>().Length;
            lab.StartMolecule(lab.molecules.First(m=>m.id=="methane"));
            Assert.That(Object.FindObjectsByType<MoleculeView>().Length,Is.EqualTo(count));
            lab.RecycleSelected(); yield return null; AssertInventory(lab);
            Assert.That(lab.Inventory.Available("C"),Is.EqualTo(1)); Assert.That(lab.Inventory.Available("H"),Is.EqualTo(4));
            lab.ResetLab(); yield return null;
            lab.StartMolecule(lab.molecules.First(m=>m.id=="water"));
            var wrong=Object.FindObjectsByType<LooseAtom>().First(a=>a.Element.symbol=="C");
            wrong.transform.position=lab.Selected.SitePosition(1);
            Assert.That(lab.TryAttach(wrong),Is.False); Assert.That(wrong.OnShelf,Is.True);
            var correct=Object.FindObjectsByType<LooseAtom>().First(a=>a.Element.symbol=="H");
            correct.transform.position=lab.Selected.SitePosition(1);
            Assert.That(lab.FindPlacement(correct,null,-1,out var candidate,out var site,out var preview),Is.True);
            Assert.That(preview.Ready,Is.True); Assert.That(lab.TryAttach(correct),Is.True);
            Assert.That(candidate.State.IsOccupied(site),Is.True);
            lab.StartMolecule(lab.molecules.First(m=>m.id=="methane")); yield return null;
            Assert.That(lab.Inventory.Available("O"),Is.EqualTo(8)); Assert.That(lab.Inventory.Available("H"),Is.EqualTo(32)); AssertInventory(lab);
            yield return new ExitPlayMode();
        }
    }
}
