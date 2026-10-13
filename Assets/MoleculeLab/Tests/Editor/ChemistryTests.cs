using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoleculeLab.Tests
{
    public sealed class ChemistryTests
    {
        MoleculeDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<MoleculeDefinition>("Assets/MoleculeLab/Data/"+name+".asset");
        ReactionDefinition Reaction => AssetDatabase.LoadAssetAtPath<ReactionDefinition>("Assets/MoleculeLab/Data/WaterReaction.asset");
        [TestCase("Water")][TestCase("Methane")][TestCase("Ethane")][TestCase("Hydrogen")][TestCase("Oxygen")][TestCase("Methylradical")]
        public void AuthoredDefinitionsAreValid(string name)
        { var m=Load(name); Assert.That(m,Is.Not.Null); Assert.That(m.IsValid(out var error),Is.True,error); }
        [Test] public void WrongElementDoesNotMutateSite()
        {
            var water=Load("Water"); var state=new MoleculeState(water);
            Assert.That(state.TryPlace(1,water.atoms[0].element,out var reason),Is.False);
            Assert.That(reason,Does.Contain("H")); Assert.That(state.IsOccupied(1),Is.False); Assert.That(state.Complete,Is.False);
        }
        [Test] public void OccupiedSiteCannotBeReplaced()
        {
            var m=Load("Water"); var state=new MoleculeState(m);
            Assert.That(state.TryPlace(0,m.atoms[0].element,out _),Is.False);
            Assert.That(state.TryPlace(1,m.atoms[1].element,out _),Is.True);
            Assert.That(state.TryPlace(1,m.atoms[1].element,out _),Is.False);
        }
        [Test] public void DisconnectedPlacementRejected()
        {
            var m=Load("Ethane"); var state=new MoleculeState(m);
            Assert.That(state.TryPlace(5,m.atoms[5].element,out _),Is.False);
            Assert.That(state.TryPlace(1,m.atoms[1].element,out _),Is.True);
            Assert.That(state.TryPlace(5,m.atoms[5].element,out _),Is.True);
        }
        [Test] public void CompletionEventFiresOnce()
        {
            var m=Load("Water"); var state=new MoleculeState(m); var events=0; state.Completed+=()=>events++;
            Assert.That(state.Complete,Is.False);
            state.TryPlace(1,m.atoms[1].element,out _); state.TryPlace(2,m.atoms[2].element,out _); state.TryPlace(2,m.atoms[2].element,out _);
            Assert.That(state.Complete,Is.True); Assert.That(events,Is.EqualTo(1));
        }
        [Test] public void BreakingAndRejoiningConservesAllAtoms()
        {
            var state=new MoleculeState(Load("Ethane"),true);
            Assert.That(state.TryBreak(1),Is.False); Assert.That(state.TryBreak(0),Is.True); Assert.That(state.Complete,Is.False);
            var fragments=state.Fragments(); Assert.That(fragments.Length,Is.EqualTo(2));
            Assert.That(fragments.Select(x=>x.Length),Is.All.EqualTo(4));
            Assert.That(fragments.SelectMany(x=>x).Distinct().Count(),Is.EqualTo(8));
            foreach(var fragment in fragments)
            {
                Assert.That(fragment.Count(i=>state.Definition.atoms[i].element.symbol=="C"),Is.EqualTo(1));
                Assert.That(fragment.Count(i=>state.Definition.atoms[i].element.symbol=="H"),Is.EqualTo(3));
            }
            Assert.That(state.TryBreak(0),Is.False); Assert.That(state.TryRejoin(0),Is.True);
            Assert.That(state.TryRejoin(0),Is.False); Assert.That(state.Complete,Is.True); Assert.That(state.Fragments().Length,Is.EqualTo(1));
        }
        [Test] public void IncompleteMoleculeCannotBreak()
        { Assert.That(new MoleculeState(Load("Ethane")).TryBreak(0),Is.False); }
        MoleculeState[] Inputs() => new[]{new MoleculeState(Load("Hydrogen"),true),new MoleculeState(Load("Hydrogen"),true),new MoleculeState(Load("Oxygen"),true)};
        [Test] public void ReactionIsBalancedAndProductsReusable()
        {
            var input=Inputs(); Assert.That(ReactionEngine.IsBalanced(Reaction),Is.True);
            Assert.That(ReactionEngine.TryReact(Reaction,input,out var products,out var reason,ReactionCondition.Ignition),Is.True,reason);
            Assert.That(products.Length,Is.EqualTo(2)); Assert.That(products.All(s=>s.Complete && s.Definition.id=="water"),Is.True);
            Assert.That(input.All(s=>s.Consumed),Is.True);
            Assert.That(ReactionEngine.TryReact(Reaction,input,out var repeated,out _,ReactionCondition.Ignition),Is.False); Assert.That(repeated,Is.Empty);
        }
        [Test] public void DuplicateObjectIsNotTwoReactants()
        {
            var input=Inputs(); input[1]=input[0];
            Assert.That(ReactionEngine.TryReact(Reaction,input,out _,out _,ReactionCondition.Ignition),Is.False); Assert.That(input.Any(s=>s.Consumed),Is.False);
        }
        [Test] public void MissingOrWrongReactantsNotConsumed()
        {
            var input=Inputs(); Assert.That(ReactionEngine.TryReact(Reaction,input.Take(2).ToArray(),out _,out _,ReactionCondition.Ignition),Is.False);
            input[2]=new MoleculeState(Load("Water"),true); Assert.That(ReactionEngine.TryReact(Reaction,input,out _,out _,ReactionCondition.Ignition),Is.False);
            Assert.That(input.Any(s=>s.Consumed),Is.False);
        }
        [Test] public void IncompleteReactantRejected()
        {
            var input=Inputs(); input[0]=new MoleculeState(Load("Hydrogen"));
            Assert.That(ReactionEngine.TryReact(Reaction,input,out _,out _,ReactionCondition.Ignition),Is.False);
        }
        [Test] public void UnbalancedRecipeRejected()
        {
            var recipe=Object.Instantiate(Reaction); recipe.outputs=new[]{new ReactionAmount(Load("Water"),1)};
            Assert.That(ReactionEngine.IsBalanced(recipe),Is.False); Object.DestroyImmediate(recipe);
        }
        [TestCase(.01f,.25f)][TestCase(1f,1f)][TestCase(10f,6f)]
        public void ScaleHasAbsoluteBounds(float requested,float expected) => Assert.That(MoleculeScaleTransformer.ClampScale(requested),Is.EqualTo(expected));
        [Test] public void WaterAndMethaneAnglesMatchReference()
        {
            var w=Load("Water"); Assert.That(Vector3.Angle(w.atoms[1].position,w.atoms[2].position),Is.EqualTo(104.5f).Within(.1f));
            var m=Load("Methane"); for(var a=1;a<5;a++) for(var b=a+1;b<5;b++) Assert.That(Vector3.Angle(m.atoms[a].position,m.atoms[b].position),Is.EqualTo(109.471f).Within(.02f));
        }
        [Test] public void EthaneLocalGeometryIsTetrahedral()
        {
            var m=Load("Ethane");
            for(var center=0;center<2;center++)
            {
                var neighbors=m.bonds.Where(b=>b.a==center||b.b==center).Select(b=>b.a==center?b.b:b.a).ToArray();
                for(var a=0;a<neighbors.Length;a++) for(var b=a+1;b<neighbors.Length;b++)
                    Assert.That(Vector3.Angle(m.atoms[neighbors[a]].position-m.atoms[center].position,m.atoms[neighbors[b]].position-m.atoms[center].position),Is.EqualTo(109.471f).Within(.02f));
            }
        }
    }
}
