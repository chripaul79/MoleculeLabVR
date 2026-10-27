using System.Globalization;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MoleculeLab.Tests
{
    public sealed class FreeBuildTests
    {
        static int Add(FreeStructure s,string symbol,int valence,int anchor)
        {
            var position=anchor<0?Vector3.zero:s.Position(anchor)+s.OpenDirections(anchor)[0]*FreeStructure.BondLength(s.Symbol(anchor),symbol,1);
            var atom=s.AddAtom(symbol,valence,position,-1);
            if(anchor>=0) s.AddBond(anchor,atom);
            return atom;
        }
        static void FillHydrogens(FreeStructure s)
        {
            for(var i=0;i<s.Count;i++) if(s.Symbol(i)!="H") while(s.OpenValence(i)>0) Add(s,"H",1,i);
        }
        static FreeStructure Ethanol()
        {
            var s=new FreeStructure(); var c1=Add(s,"C",4,-1); var c2=Add(s,"C",4,c1); Add(s,"O",2,c2); FillHydrogens(s); return s;
        }
        static FreeStructure Benzene()
        {
            var s=new FreeStructure(); var previous=Add(s,"C",4,-1);
            for(var i=1;i<6;i++) previous=Add(s,"C",4,previous);
            s.AddBond(5,0);
            foreach(var bond in new[]{0,2,4}) s.SetOrder(bond,2);
            FillHydrogens(s); return s;
        }
        static MoleculeDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<MoleculeDefinition>("Assets/MoleculeLab/Data/"+name+".asset");
        static ElementDefinition[] Elements => new[]{"Hydrogen","Oxygen","Carbon"}.Select(n=>AssetDatabase.LoadAssetAtPath<ElementDefinition>("Assets/MoleculeLab/Data/Element_"+n+".asset")).ToArray();

        [Test] public void HillFormulaMatchesPubChemConvention()
        {
            Assert.That(FreeChemistry.HillFormula(new[]{"O","H","H"}),Is.EqualTo("H2O"));
            Assert.That(FreeChemistry.HillFormula(new[]{"O","C","O"}),Is.EqualTo("CO2"));
            Assert.That(Ethanol().Graph.Formula(),Is.EqualTo("C2H6O"));
        }
        [Test] public void SmilesUsesExplicitBracketAtoms()
        {
            Assert.That(SmilesWriter.Write(Ethanol().Graph),Is.EqualTo("[CH3][CH2][OH]"));
            var water=new FreeStructure(); Add(water,"O",2,-1); FillHydrogens(water);
            Assert.That(SmilesWriter.Write(water.Graph),Is.EqualTo("[OH2]"));
            var hydrogen=new FreeStructure(); Add(hydrogen,"H",1,Add(hydrogen,"H",1,-1));
            Assert.That(SmilesWriter.Write(hydrogen.Graph),Is.EqualTo("[H][H]"));
            var co2=new FreeStructure(); var c=Add(co2,"C",4,-1); Add(co2,"O",2,c); Add(co2,"O",2,c); co2.SetOrder(0,2); co2.SetOrder(1,2);
            Assert.That(SmilesWriter.Write(co2.Graph),Is.EqualTo("[C](=[O])=[O]"));
        }
        [Test] public void SmilesWritesRingClosures()
        {
            Assert.That(SmilesWriter.Write(Benzene().Graph),Is.EqualTo("[CH]1=[CH][CH]=[CH][CH]=[CH]1"));
        }
        [Test] public void ValenceRulesRejectOverbonding()
        {
            var s=new FreeStructure(); var c=Add(s,"C",4,-1); var o=Add(s,"O",2,c);
            Assert.That(s.IsComplete(out var reason),Is.False); Assert.That(reason,Does.Contain("open bond"));
            Assert.That(s.CanSetOrder(0,2,out _),Is.True);
            FillHydrogens(s);
            Assert.That(s.IsComplete(out reason),Is.True,reason); Assert.That(s.Graph.Formula(),Is.EqualTo("CH4O"));
            Assert.That(s.CanSetOrder(0,2,out reason),Is.False); Assert.That(reason,Does.Contain("spare bond"));
            Assert.That(s.CanBond(c,o,out _),Is.False);
            var hydrogen=s.Count-1; Assert.That(s.OpenValence(hydrogen),Is.Zero);
            Assert.That(s.RemoveLastAtom(),Is.EqualTo(-1)); Assert.That(s.OpenValence(o),Is.EqualTo(1));
        }
        [Test] public void OnlyEndAtomsCanBePulledOff()
        {
            var s=Ethanol(); // C1 C2 O3 then hydrogens
            Assert.That(s.CanRemove(1,out var reason),Is.False); Assert.That(reason,Does.Contain("C2"));
            var hydrogen=s.Graph.Neighbors(0).First(n=>s.Symbol(n)=="H");
            Assert.That(s.CanRemove(hydrogen,out _),Is.True);
            var bonds=s.Graph.Bonds.Count; var count=s.Count;
            s.RemoveAtom(hydrogen);
            Assert.That(s.Count,Is.EqualTo(count-1)); Assert.That(s.Graph.Bonds.Count,Is.EqualTo(bonds-1));
            Assert.That(s.Graph.Bonds.All(b=>b.A<s.Count && b.B<s.Count),Is.True,"Later atoms are renumbered.");
            Assert.That(s.OpenValence(0),Is.EqualTo(1)); Assert.That(s.MissingText(),Is.EqualTo("C1 needs 1"));
            Assert.That(SmilesWriter.Write(s.Graph),Is.EqualTo("[CH2][CH2][OH]"));
        }
        [Test] public void GuidesFollowVseprGeometry()
        {
            var s=new FreeStructure(); var c=Add(s,"C",4,-1); var c2=Add(s,"C",4,c);
            var tetrahedral=s.OpenDirections(c2); var bond=(s.Position(c)-s.Position(c2)).normalized;
            Assert.That(tetrahedral.Length,Is.EqualTo(3));
            foreach(var d in tetrahedral) Assert.That(Vector3.Angle(d,bond),Is.EqualTo(109.47f).Within(.5f));
            Assert.That(Vector3.Angle(tetrahedral[0],tetrahedral[1]),Is.EqualTo(109.47f).Within(.5f));
            s.SetOrder(0,2);
            var trigonal=s.OpenDirections(c2);
            Assert.That(trigonal.Length,Is.EqualTo(2));
            foreach(var d in trigonal) Assert.That(Vector3.Angle(d,bond),Is.EqualTo(120f).Within(.5f));
            s.SetOrder(0,3);
            var linear=s.OpenDirections(c2);
            Assert.That(linear.Length,Is.EqualTo(1)); Assert.That(Vector3.Angle(linear[0],bond),Is.EqualTo(180f).Within(.5f));
        }
        [Test] public void RelaxationRestoresIdealEthane()
        {
            var s=new FreeStructure(); var c=Add(s,"C",4,-1); Add(s,"C",4,c); FillHydrogens(s);
            for(var i=0;i<s.Count;i++) s.SetPosition(i,s.Position(i)+new Vector3(Mathf.Sin(i*1.7f),Mathf.Cos(i*2.3f),Mathf.Sin(i*.9f))*.04f);
            var centroid=s.Centroid();
            s.Relax(400);
            Assert.That(Vector3.Distance(s.Position(0),s.Position(1)),Is.EqualTo(FreeStructure.HeavyBond).Within(.006f));
            var hydrogens=s.Graph.Neighbors(0).Where(n=>s.Symbol(n)=="H").ToArray();
            Assert.That(Vector3.Angle(s.Position(hydrogens[0])-s.Position(0),s.Position(hydrogens[1])-s.Position(0)),Is.EqualTo(109.47f).Within(3f));
            Assert.That(Vector3.Distance(s.Centroid(),centroid),Is.LessThan(.0001f));
        }
        [Test] public void MolfileParsingIgnoresDeviceCulture()
        {
            var culture=Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture=new CultureInfo("nb-NO"); // decimal comma
                var g=MolfileParser.Parse(PubChemFixtures.EthanolSdf,out var charged);
                Assert.That(charged,Is.False); Assert.That(g.Count,Is.EqualTo(9)); Assert.That(g.Bonds.Count,Is.EqualTo(8));
                Assert.That(g.Symbols[0],Is.EqualTo("O")); Assert.That(g.Positions[0].x,Is.EqualTo(-1.1712f).Within(1e-4f));
                Assert.That(g.Formula(),Is.EqualTo("C2H6O"));
            }
            finally { Thread.CurrentThread.CurrentCulture=culture; }
            Assert.Throws<System.FormatException>(()=>MolfileParser.Parse("not a molfile",out _));
        }
        [Test] public void MatcherComparesConstitutionNotKekuleForm()
        {
            var ethanol=Ethanol().Graph;
            var map=StructureMatcher.Match(ethanol,MolfileParser.Parse(PubChemFixtures.EthanolSdf,out _));
            Assert.That(map,Is.Not.Null);
            var parsed=MolfileParser.Parse(PubChemFixtures.EthanolSdf,out _);
            for(var i=0;i<ethanol.Count;i++) Assert.That(parsed.Symbols[map[i]],Is.EqualTo(ethanol.Symbols[i]));
            foreach(var bond in ethanol.Bonds) Assert.That(parsed.BondIndex(map[bond.A],map[bond.B]),Is.GreaterThanOrEqualTo(0));
            Assert.That(StructureMatcher.Match(ethanol,MolfileParser.Parse(PubChemFixtures.AceticAcidSdf,out _)),Is.Null);
            // Dimethyl ether has ethanol's formula but different bonding.
            var ether=new FreeStructure(); var c=Add(ether,"C",4,-1); var o=Add(ether,"O",2,c); Add(ether,"C",4,o); FillHydrogens(ether);
            Assert.That(ether.Graph.Formula(),Is.EqualTo("C2H6O")); Assert.That(StructureMatcher.Match(ether.Graph,parsed),Is.Null);
            Assert.That(StructureMatcher.Match(Benzene().Graph,MolfileParser.Parse(PubChemFixtures.BenzeneSdf,out _)),Is.Not.Null);
        }
        [Test] public void CatalogueRecipesAreRecognised()
        {
            var water=new FreeStructure(); Add(water,"O",2,-1); FillHydrogens(water);
            Assert.That(StructureMatcher.Match(water.Graph,MolGraph.From(Load("Water"))),Is.Not.Null);
            Assert.That(StructureMatcher.Match(Ethanol().Graph,MolGraph.From(Load("Ethanol"))),Is.Not.Null);
            Assert.That(StructureMatcher.Match(Ethanol().Graph,MolGraph.From(Load("Methanol"))),Is.Null);
            Assert.That(StructureMatcher.Match(Benzene().Graph,MolGraph.From(Load("Benzene"))),Is.Not.Null);
        }
        [Test] public void PropertyRepliesAreInterpreted()
        {
            var found=PubChemClient.InterpretProperties(200,PubChemFixtures.EthanolProperties,null);
            Assert.That(found.status,Is.EqualTo(LookupStatus.Found)); Assert.That(found.cid,Is.EqualTo(702));
            Assert.That(found.title,Is.EqualTo("Ethanol")); Assert.That(found.molecularFormula,Is.EqualTo("C2H6O"));
            Assert.That(PubChemClient.InterpretProperties(200,PubChemFixtures.NotFoundProperties,null).status,Is.EqualTo(LookupStatus.NotFound));
            var rejected=PubChemClient.InterpretProperties(400,PubChemFixtures.BadRequest,"HTTP/1.1 400 Bad Request");
            Assert.That(rejected.status,Is.EqualTo(LookupStatus.Rejected)); Assert.That(rejected.message,Does.Contain("standardize"));
            Assert.That(PubChemClient.InterpretProperties(0,null,"Cannot resolve destination host").status,Is.EqualTo(LookupStatus.Error));
            Assert.That(PubChemClient.InterpretProperties(503,"",null).status,Is.EqualTo(LookupStatus.Error));
            Assert.That(PubChemClient.InterpretProperties(200,"<html>",null).status,Is.EqualTo(LookupStatus.Error));
            Assert.That(PubChemClient.PropertyUrl("[CH]#[CH]"),Does.EndWith("smiles=%5BCH%5D%23%5BCH%5D"));
            Assert.That(PubChemClient.ConformerUrl(702),Does.EndWith("compound/cid/702/SDF?record_type=3d"));
        }
        [Test] public void DiscoveredDefinitionUsesPubChemConformer()
        {
            var built=Ethanol().Graph; var conformer=MolfileParser.Parse(PubChemFixtures.EthanolSdf,out _);
            var map=StructureMatcher.Match(built,conformer);
            var info=PubChemFixtures.Found(702,"Ethanol","ethanol","C2H6O",PubChemFixtures.EthanolSdf);
            var definition=DiscoveredMolecule.Create(built,conformer,map,info,Elements,out var usedConformer);
            try
            {
                Assert.That(usedConformer,Is.True);
                Assert.That(definition.IsValid(out var error),Is.True,error);
                Assert.That(definition.id,Is.EqualTo("pubchem-702")); Assert.That(definition.displayName,Is.EqualTo("Ethanol"));
                var oxygen=built.Symbols.IndexOf("O"); var carbon=built.Neighbors(oxygen).First(n=>built.Symbols[n]=="C");
                var expected=Vector3.Distance(conformer.Positions[map[oxygen]],conformer.Positions[map[carbon]])*DiscoveredMolecule.MetresPerAngstrom;
                Assert.That(Vector3.Distance(definition.atoms[oxygen].position,definition.atoms[carbon].position),Is.EqualTo(expected).Within(1e-4f));
                var state=new MoleculeState(definition,true); Assert.That(state.Complete,Is.True);
            }
            finally { Object.DestroyImmediate(definition); }
            var fallback=DiscoveredMolecule.Create(built,null,null,info,Elements,out usedConformer);
            Assert.That(usedConformer,Is.False); Assert.That(fallback.IsValid(out var fallbackError),Is.True,fallbackError);
            Object.DestroyImmediate(fallback);
        }
    }
}
