using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MoleculeLab.Tests
{
    /// <summary>The reaction rules on hand-made molecules: no scene and no Play mode, so these run in a second.</summary>
    public sealed class ReactionRuleTests
    {
        /// <summary>Heavy atoms (space separated), bonds between them ("0-1=2" style tuples), then hydrogens fill every free valence.</summary>
        static MolGraph Mol(string heavy,params (int a,int b,int order)[] bonds)
        {
            var g=new MolGraph(); var symbols=heavy.Split(' ');
            for(var i=0;i<symbols.Length;i++) g.Add(symbols[i],new Vector3(i*.19f,0,0));
            foreach(var (a,b,order) in bonds) g.Bonds.Add(new GraphBond(a,b,order));
            for(var i=0;i<symbols.Length;i++)
                for(var k=g.BondSum(i);k<ReactionRules.Valence(symbols[i]);k++)
                { var h=g.Add("H",g.Positions[i]+new Vector3(0,.12f,k*.05f)); g.Bonds.Add(new GraphBond(i,h,1)); }
            return g;
        }
        static MolGraph H2() { var g=new MolGraph(); g.Add("H",Vector3.zero); g.Add("H",Vector3.right*.12f); g.Bonds.Add(new GraphBond(0,1,1)); return g; }
        static MolGraph Diatomic(string x,int order) { var g=new MolGraph(); g.Add(x,Vector3.zero); g.Add(x,Vector3.right*.19f); g.Bonds.Add(new GraphBond(0,1,order)); return g; }
        static MolGraph O2() => Diatomic("O",2);
        static MolGraph Methane() => Mol("C");
        static MolGraph Ethane() => Mol("C C",(0,1,1));
        static MolGraph Ethene() => Mol("C C",(0,1,2));
        static MolGraph Ethyne() => Mol("C C",(0,1,3));
        static MolGraph Propene() => Mol("C C C",(0,1,2),(1,2,1));
        static MolGraph Water() => Mol("O");
        static MolGraph Ethanol() => Mol("C C O",(0,1,1),(1,2,1));
        static MolGraph AceticAcid() => Mol("C C O O",(0,1,1),(1,2,2),(1,3,1));
        static MolGraph Benzene() => Mol("C C C C C C",(0,1,2),(1,2,1),(2,3,2),(3,4,1),(4,5,2),(5,0,1));

        static ReactionPlan Plan(ReactionCondition condition,params (MolGraph graph,string name)[] contents) =>
            ReactionRules.Plan(contents.Select(c=>c.graph).ToList(),contents.Select(c=>c.name).ToList(),condition);
        static string[] Formulas(ReactionPlan plan) => plan.Products.Select(p=>p.Formula()).OrderBy(f=>f).ToArray();

        /// <summary>Every ready plan keeps all atoms and gives complete molecules.</summary>
        static void AssertConserved(ReactionPlan plan,IList<MolGraph> contents)
        {
            Assert.That(plan.Ready,Is.True,plan.Hint);
            var before=plan.Inputs.SelectMany(i=>contents[i].Symbols).OrderBy(s=>s); var after=plan.Products.SelectMany(p=>p.Symbols).OrderBy(s=>s);
            Assert.That(after,Is.EqualTo(before),"Atoms are never created or lost.");
            foreach(var product in plan.Products)
                for(var i=0;i<product.Count;i++) Assert.That(product.BondSum(i),Is.EqualTo(ReactionRules.Valence(product.Symbols[i])),product.Formula()+" atom "+i);
            Assert.That(plan.ProductNames.Count,Is.EqualTo(plan.Products.Count));
        }
        static ReactionPlan Ready(ReactionCondition condition,params (MolGraph graph,string name)[] contents)
        {
            var plan=Plan(condition,contents); AssertConserved(plan,contents.Select(c=>c.graph).ToList()); return plan;
        }

        [Test] public void MethaneBurnsWithTwoOxygen()
        {
            var plan=Ready(ReactionCondition.Ignition,(Methane(),"Methane"),(O2(),"Oxygen"),(O2(),"Oxygen"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"CO2","H2O","H2O"}));
            Assert.That(plan.Equation,Is.EqualTo("CH4 + 2 O2 -> CO2 + 2 H2O"));
        }
        [Test] public void CombustionSaysHowMuchOxygenIsMissing()
        {
            var plan=Plan(ReactionCondition.Ignition,(Methane(),"Methane"),(O2(),"Oxygen"));
            Assert.That(plan.State,Is.EqualTo(PlanState.Missing)); Assert.That(plan.Hint,Does.Contain("1 more O2"));
            plan=Plan(ReactionCondition.Ignition,(Ethane(),"Ethane"),(O2(),"Oxygen"));
            Assert.That(plan.Hint,Does.Contain("another C2H6"),"2 C2H6 + 7 O2: one ethane needs an odd number of O atoms.");
        }
        [Test] public void TwoEthaneBurnWithSevenOxygen()
        {
            var contents=new List<(MolGraph,string)>{(Ethane(),"Ethane"),(Ethane(),"Ethane")};
            for(var i=0;i<8;i++) contents.Add((O2(),"Oxygen"));
            var plan=Ready(ReactionCondition.Ignition,contents.ToArray());
            Assert.That(plan.Inputs.Count,Is.EqualTo(9),"Two ethane and seven O2; the eighth O2 stays in the vessel.");
            Assert.That(Formulas(plan).Count(f=>f=="CO2"),Is.EqualTo(4)); Assert.That(Formulas(plan).Count(f=>f=="H2O"),Is.EqualTo(6));
        }
        [Test] public void EthanolBurns()
        {
            var plan=Ready(ReactionCondition.Ignition,(Ethanol(),"Ethanol"),(O2(),"O"),(O2(),"O"),(O2(),"O"));
            Assert.That(plan.Equation,Is.EqualTo("C2H6O + 3 O2 -> 2 CO2 + 3 H2O"));
        }
        [Test] public void HydrogenAndChlorineMakeHydrogenChloride()
        {
            var plan=Ready(ReactionCondition.Ignition,(H2(),"Hydrogen"),(Diatomic("Cl",1),"Chlorine"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"ClH","ClH"})); Assert.That(plan.ProductNames[0],Is.EqualTo("Hydrogen chloride"));
        }
        [Test] public void MethaneAndChlorineInTheFlameGiveChloromethane()
        {
            var plan=Ready(ReactionCondition.Ignition,(Methane(),"Methane"),(Diatomic("Cl",1),"Chlorine"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"CH3Cl","ClH"}));
            Assert.That(plan.ProductNames,Does.Contain("Chloromethane"));
        }
        [Test] public void PalladiumAddsHydrogen()
        {
            var plan=Ready(ReactionCondition.PalladiumCatalyst,(Ethene(),"Ethene"),(H2(),"Hydrogen"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"C2H6"})); Assert.That(plan.ProductNames[0],Is.EqualTo("Ethane"));
            plan=Ready(ReactionCondition.PalladiumCatalyst,(Ethyne(),"Ethyne"),(H2(),"Hydrogen"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"C2H4"})); Assert.That(plan.ProductNames[0],Is.EqualTo("Ethene"));
            plan=Ready(ReactionCondition.PalladiumCatalyst,(Ethyne(),"Ethyne"),(H2(),"Hydrogen"),(H2(),"Hydrogen"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"C2H6"}));
            Assert.That(Plan(ReactionCondition.PalladiumCatalyst,(Ethene(),"Ethene")).Hint,Does.Contain("H2"));
        }
        [Test] public void BenzeneDoesNotHydrogenateLikeAnAlkene()
        {
            Assert.That(Plan(ReactionCondition.PalladiumCatalyst,(Benzene(),"Benzene"),(H2(),"Hydrogen")).State,Is.EqualTo(PlanState.NotApplicable));
            Assert.That(Plan(ReactionCondition.None,(Benzene(),"Benzene"),(Diatomic("Br",1),"Bromine")).Ready,Is.False);
        }
        [Test] public void AcidAddsWaterTheMarkovnikovWay()
        {
            var plan=Ready(ReactionCondition.AcidCatalyst,(Propene(),"Propene"),(Water(),"Water"));
            var alcohol=plan.Products.Single(); Assert.That(alcohol.Formula(),Is.EqualTo("C3H8O"));
            var oxygen=alcohol.Symbols.IndexOf("O"); var carbon=alcohol.Neighbors(oxygen).First(n=>alcohol.Symbols[n]=="C");
            Assert.That(alcohol.Neighbors(carbon).Count(n=>alcohol.Symbols[n]=="C"),Is.EqualTo(2),"The OH sits on the middle carbon.");
            Assert.That(plan.ProductNames[0],Is.EqualTo("Propan-2-ol"));
            Assert.That(Ready(ReactionCondition.AcidCatalyst,(Ethene(),"Ethene"),(Water(),"Water")).ProductNames[0],Is.EqualTo("Ethanol"));
        }
        [Test] public void AcidAndAlcoholMakeAnEster()
        {
            var plan=Ready(ReactionCondition.AcidCatalyst,(AceticAcid(),"Acetic acid"),(Ethanol(),"Ethanol"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"C4H8O2","H2O"}));
            Assert.That(plan.ProductNames,Does.Contain("Ethyl acetate"));
            Assert.That(ReactionRules.ClassName(plan.Products.First(p=>p.Formula()=="C4H8O2")),Does.StartWith("Ester"));
        }
        [Test] public void AcidTakesWaterOutOfAnAlcohol()
        {
            var plan=Ready(ReactionCondition.AcidCatalyst,(Ethanol(),"Ethanol"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"C2H4","H2O"})); Assert.That(plan.ProductNames,Does.Contain("Ethene"));
            Assert.That(Plan(ReactionCondition.AcidCatalyst,(Mol("C O",(0,1,1)),"Methanol")).State,Is.EqualTo(PlanState.Missing),"Methanol has no second carbon.");
        }
        [Test] public void BromineAddsWithoutATool()
        {
            var plan=Ready(ReactionCondition.None,(Ethene(),"Ethene"),(Diatomic("Br",1),"Bromine"));
            Assert.That(Formulas(plan),Is.EqualTo(new[]{"C2H4Br2"})); Assert.That(plan.ProductNames[0],Is.EqualTo("1,2-Dibromoethane"));
            var halide=new MolGraph(); halide.Add("H",Vector3.zero); halide.Add("Cl",Vector3.right*.15f); halide.Bonds.Add(new GraphBond(0,1,1));
            plan=Ready(ReactionCondition.None,(Propene(),"Propene"),(halide,"Hydrogen chloride"));
            Assert.That(plan.ProductNames[0],Is.EqualTo("2-Chloropropane"));
        }
        [Test] public void NothingHappensWithoutAMatchingRule()
        {
            Assert.That(Plan(ReactionCondition.PalladiumCatalyst,(Water(),"Water")).State,Is.EqualTo(PlanState.NotApplicable));
            Assert.That(Plan(ReactionCondition.None,(Ethane(),"Ethane"),(O2(),"Oxygen")).Ready,Is.False,"Fuels need the flame.");
        }
        [Test] public void ProductsGetTidyGeometry()
        {
            var plan=Ready(ReactionCondition.AcidCatalyst,(AceticAcid(),"Acetic acid"),(Ethanol(),"Ethanol"));
            foreach(var product in plan.Products)
                foreach(var bond in product.Bonds)
                {
                    var length=Vector3.Distance(product.Positions[bond.A],product.Positions[bond.B]);
                    var ideal=FreeStructure.BondLength(product.Symbols[bond.A],product.Symbols[bond.B],bond.Order);
                    Assert.That(length,Is.EqualTo(ideal).Within(ideal*.2f),product.Formula());
                }
        }
    }
}
