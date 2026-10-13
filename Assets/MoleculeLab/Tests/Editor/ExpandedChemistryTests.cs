using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace MoleculeLab.Tests
{
    public sealed class ExpandedChemistryTests
    {
        static MoleculeDefinition Molecule(string name)=>AssetDatabase.LoadAssetAtPath<MoleculeDefinition>("Assets/MoleculeLab/Data/"+name+".asset");
        [TestCase("Ethene")][TestCase("Ethyne")][TestCase("Methanol")][TestCase("Ethanol")][TestCase("CarbonDioxide")][TestCase("Benzene")]
        public void ExpandedDefinitionsValidateAndBuild(string name)
        {
            var m=Molecule(name); Assert.That(m.IsValid(out var reason),Is.True,reason); var state=new MoleculeState(m);
            for(var i=1;i<m.atoms.Length;i++) Assert.That(state.TryPlace(i,m.atoms[i].element,out reason),Is.True,reason);
            Assert.That(state.Complete,Is.True);
        }
        [Test] public void BenzeneClosureAndEquivalentGeometry()
        {
            var b=Molecule("Benzene"); var state=new MoleculeState(b);
            for(var i=1;i<5;i++) state.TryPlace(i,b.atoms[i].element,out _);
            Assert.That(state.TryPlace(5,b.atoms[5].element,out _),Is.True);
            Assert.That(b.bonds.Count(e=>(e.a==5 && state.IsOccupied(e.b))||(e.b==5 && state.IsOccupied(e.a))),Is.EqualTo(2));
            Assert.That(state.TryPlace(5,b.atoms[5].element,out _),Is.False);
            for(var i=0;i<6;i++) Assert.That(Vector3.Angle(b.atoms[(i+5)%6].position-b.atoms[i].position,b.atoms[(i+1)%6].position-b.atoms[i].position),Is.EqualTo(120).Within(.001f));
            Assert.That(b.rotatableBond,Is.EqualTo(-1)); Assert.That(new MoleculeState(b,true).TryBreak(0),Is.False);
        }
        [TestCase("Ethene",0,120f)][TestCase("Ethyne",0,180f)][TestCase("CarbonDioxide",0,180f)]
        [TestCase("Methanol",0,109.471f)][TestCase("Ethanol",0,109.471f)][TestCase("Ethanol",1,109.471f)]
        public void AuthoredHybridizationAngles(string name,int center,float expected)
        {
            var m=Molecule(name); var neighbors=m.bonds.Where(b=>b.a==center||b.b==center).Select(b=>b.a==center?b.b:b.a).ToArray();
            for(var a=0;a<neighbors.Length;a++) for(var b=a+1;b<neighbors.Length;b++)
                Assert.That(Vector3.Angle(m.atoms[neighbors[a]].position-m.atoms[center].position,m.atoms[neighbors[b]].position-m.atoms[center].position),Is.EqualTo(expected).Within(.03));
        }
        [Test] public void IncorrectFormulaAndValenceRejected()
        {
            var m=Object.Instantiate(Molecule("Methanol")); m.molecularFormula="CH3O"; Assert.That(m.IsValid(out _),Is.False);
            m.molecularFormula="CH4O"; m.bonds=m.bonds.Take(4).ToArray(); Assert.That(m.IsValid(out _),Is.False); Object.DestroyImmediate(m);
        }
        [TestCase("WaterReaction")][TestCase("MethaneCombustion")][TestCase("EtheneHydrogenation")][TestCase("EtheneHydration")]
        public void ConditionsAndConservation(string name)
        {
            var r=AssetDatabase.LoadAssetAtPath<ReactionDefinition>("Assets/MoleculeLab/Data/"+name+".asset");
            var input=r.inputs.SelectMany(p=>Enumerable.Range(0,p.count).Select(_=>new MoleculeState(p.molecule,true))).ToArray();
            Assert.That(ReactionEngine.IsBalanced(r),Is.True);
            Assert.That(ReactionEngine.TryReact(r,input,out _,out _),Is.False);
            Assert.That(input.Any(m=>m.Consumed),Is.False);
            var wrong=r.requiredCondition==ReactionCondition.Ignition?ReactionCondition.AcidCatalyst:ReactionCondition.Ignition;
            Assert.That(ReactionEngine.TryReact(r,input,out _,out _,wrong),Is.False);
            Assert.That(ReactionEngine.TryReact(r,input,out var output,out var reason,r.requiredCondition),Is.True,reason);
            foreach(var element in new[]{"C","H","O"})
                Assert.That(output.Sum(m=>m.Definition.atoms.Count(a=>a.element.symbol==element)),Is.EqualTo(input.Sum(m=>m.Definition.atoms.Count(a=>a.element.symbol==element))));
            Assert.That(ReactionEngine.TryReact(r,input,out _,out _,r.requiredCondition),Is.False);
        }
        [Test] public void HandGestureCannotChangeTargetsUntilRelease()
        {
            var state=new HandPinchState(); var a=new object(); var b=new object();
            state.Step(1,0,a,null,null); Assert.That(state.Pinched,Is.False);
            state.Step(0,0,null,null,null); state.Step(1,1,a,null,null);
            state.Step(1,2,b,null,null); Assert.That(state.Target,Is.SameAs(a));
            state.Cancel(); state.Step(1,3,b,null,null); Assert.That(state.Pinched,Is.False);
            state.Step(0,4,null,null,null); state.Step(1,5,null,null,b); Assert.That(state.Menu,Is.True);
        }
    }
}
