using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoleculeLab
{
    public enum PlanState { NotApplicable, Missing, Ready }

    /// <summary>One reaction the vessel could run with one tool: which molecules it uses up and what it makes, or what is still missing.</summary>
    public sealed class ReactionPlan
    {
        public string Rule="", Title="", Explanation="", Hint="", Equation="";
        public ReactionCondition Condition;
        public PlanState State;
        public bool Ready => State==PlanState.Ready;
        /// <summary>Indices into the vessel contents that the reaction uses up (the rest stay in the vessel).</summary>
        public readonly List<int> Inputs=new List<int>();
        public readonly List<MolGraph> Products=new List<MolGraph>();
        /// <summary>A suggested name per product; null means "name it from its structure".</summary>
        public readonly List<string> ProductNames=new List<string>();
        internal static ReactionPlan Missing(string rule,string hint) => new ReactionPlan{Rule=rule,State=PlanState.Missing,Hint=hint};
    }

    /// <summary>
    /// Reaction rules that work on any molecule the player builds, not only on fixed recipes: they look for patterns in the
    /// bonding graph (a C=C bond, an O-H group, a carboxylic acid...) and rewire the bonds. Atoms are never created or lost.
    /// Pure and offline: no network, no randomness.
    /// FLAME: combustion of any C/H/O fuel with O2; H2 + halogen; alkane + halogen (substitution).
    /// Pd CATALYST: H2 adds across C=C and C≡C bonds.
    /// ACID: acid + alcohol give an ester and water; water adds across a C=C bond; an alcohol loses water to give an alkene.
    /// No tool: Cl2, Br2 or HCl/HBr add straight across a C=C bond.
    /// </summary>
    public static class ReactionRules
    {
        public static readonly ReactionCondition[] Tools={ReactionCondition.Ignition,ReactionCondition.PalladiumCatalyst,ReactionCondition.AcidCatalyst};
        static readonly string[] Halogens={"F","Cl","Br"};

        public static int Valence(string symbol)
        {
            switch(symbol) { case "C": return 4; case "N": case "P": return 3; case "O": case "S": return 2; default: return 1; }
        }

        /// <summary>The best reaction for these vessel contents and this tool: a ready one if there is one, otherwise what is missing.</summary>
        public static ReactionPlan Plan(IReadOnlyList<MolGraph> contents,IReadOnlyList<string> names,ReactionCondition condition)
        {
            names=names??new string[contents.Count];
            var plans=new List<ReactionPlan>();
            switch(condition)
            {
                case ReactionCondition.Ignition: plans.Add(Combustion(contents)); plans.Add(HydrogenHalogen(contents)); plans.Add(Substitution(contents,names)); break;
                case ReactionCondition.PalladiumCatalyst: plans.Add(Hydrogenation(contents,names)); break;
                case ReactionCondition.AcidCatalyst: plans.Add(Esterification(contents,names)); plans.Add(Hydration(contents,names)); plans.Add(Dehydration(contents,names)); break;
                default: plans.Add(HalogenAddition(contents,names)); break;
            }
            var plan=plans.FirstOrDefault(p=>p!=null && p.Ready)??plans.FirstOrDefault(p=>p!=null && p.State==PlanState.Missing)
                ??new ReactionPlan{State=PlanState.NotApplicable,Hint=Idle(condition)};
            plan.Condition=condition;
            if(plan.Ready) plan.Equation=Equation(plan.Inputs.Select(i=>contents[i]),plan.Products);
            return plan;
        }
        static string Idle(ReactionCondition c) => c==ReactionCondition.Ignition?"burns a fuel (H2, CH4, ethanol...) with O2":
            c==ReactionCondition.PalladiumCatalyst?"adds H2 to a C=C bond or a C-C triple bond":c==ReactionCondition.AcidCatalyst?"adds water to a C=C bond, or joins an acid and an alcohol":"";

        public static string Equation(IEnumerable<MolGraph> inputs,IEnumerable<MolGraph> outputs)
        {
            string Side(IEnumerable<MolGraph> side) => string.Join(" + ",side.GroupBy(g=>g.Formula()).Select(g=>(g.Count()>1?g.Count()+" ":"")+g.Key));
            return Side(inputs)+" -> "+Side(outputs);
        }

        // ---------- recognising molecules ----------
        static int CountOf(MolGraph g,string s) => g.Symbols.Count(x=>x==s);
        static bool Only(MolGraph g,params string[] symbols) => g.Symbols.All(symbols.Contains);
        static bool IsDiatomic(MolGraph g,string symbol,int order) => g.Count==2 && g.Symbols[0]==symbol && g.Symbols[1]==symbol && g.Bonds.Count==1 && g.Bonds[0].Order==order;
        public static bool IsH2(MolGraph g) => IsDiatomic(g,"H",1);
        public static bool IsO2(MolGraph g) => IsDiatomic(g,"O",2);
        public static string Halogen2(MolGraph g) => Halogens.FirstOrDefault(x=>IsDiatomic(g,x,1));
        public static string HydrogenHalide(MolGraph g) => g.Count==2 && g.Bonds.Count==1 && CountOf(g,"H")==1?Halogens.FirstOrDefault(g.Symbols.Contains):null;
        public static bool IsWater(MolGraph g) => g.Count==3 && CountOf(g,"O")==1 && CountOf(g,"H")==2 && g.Bonds.Count==2;
        static bool IsCO2(MolGraph g) => g.Count==3 && CountOf(g,"C")==1 && CountOf(g,"O")==2;
        /// <summary>Twice the number of O atoms needed to burn it completely: 2 O per C, 1 O per 2 H, minus the O it already holds.</summary>
        static int OxygenHalves(MolGraph g) => 4*CountOf(g,"C")+CountOf(g,"H")-2*CountOf(g,"O");
        public static bool IsFuel(MolGraph g) => Only(g,"C","H","O") && (g.Symbols.Contains("C") || IsH2(g)) && !IsCO2(g) && OxygenHalves(g)>0;
        public static bool IsAlkane(MolGraph g) => Only(g,"C","H") && g.Symbols.Contains("C") && g.Bonds.All(b=>b.Order==1);
        /// <summary>Carbon-carbon double or triple bonds that can react (not the bonds of an aromatic ring such as benzene).</summary>
        public static List<int> PiBonds(MolGraph g,int minimumOrder=2) => Enumerable.Range(0,g.Bonds.Count)
            .Where(i=>g.Bonds[i].Order>=minimumOrder && g.Symbols[g.Bonds[i].A]=="C" && g.Symbols[g.Bonds[i].B]=="C" && !IsAromatic(g,g.Bonds[i])).ToList();
        static List<int> DoubleBonds(MolGraph g) => PiBonds(g).Where(i=>g.Bonds[i].Order==2).ToList();
        static IEnumerable<int> Hydrogens(MolGraph g,int atom) => g.Neighbors(atom).Where(n=>g.Symbols[n]=="H");
        static int Order(MolGraph g,int a,int b) { var i=g.BondIndex(a,b); return i<0?0:g.Bonds[i].Order; }

        /// <summary>C(=O)-O-H groups: (carbon, hydroxyl oxygen, its hydrogen).</summary>
        public static List<(int c,int o,int h)> Carboxyls(MolGraph g)
        {
            var result=new List<(int,int,int)>();
            for(var c=0;c<g.Count;c++)
            {
                if(g.Symbols[c]!="C") continue;
                var oxygens=g.Neighbors(c).Where(n=>g.Symbols[n]=="O").ToArray();
                if(!oxygens.Any(o=>Order(g,c,o)==2)) continue;
                foreach(var o in oxygens.Where(o=>Order(g,c,o)==1 && g.Degree(o)==2))
                { var h=FirstOr(Hydrogens(g,o),-1); if(h>=0) { result.Add((c,o,h)); break; } }
            }
            return result;
        }
        /// <summary>Alcohol O-H groups on a saturated carbon: (oxygen, carbon, hydrogen).</summary>
        public static List<(int o,int c,int h)> Alcohols(MolGraph g)
        {
            var result=new List<(int,int,int)>();
            for(var o=0;o<g.Count;o++)
            {
                if(g.Symbols[o]!="O" || g.Degree(o)!=2) continue;
                var h=FirstOr(Hydrogens(g,o),-1); var c=FirstOr(g.Neighbors(o).Where(n=>g.Symbols[n]=="C"),-1);
                if(h<0 || c<0 || g.Bonds.Any(b=>(b.A==c || b.B==c) && b.Order>1)) continue;
                result.Add((o,c,h));
            }
            return result;
        }
        /// <summary>A C=C bond inside a six-membered carbon ring of alternating single and double bonds (benzene-like) does not add H2 or water here.</summary>
        static bool IsAromatic(MolGraph g,GraphBond bond)
        {
            var path=new List<int>{bond.B};
            bool Search(int atom)
            {
                if(path.Count==6) return atom==bond.A && Alternating();
                foreach(var next in g.Neighbors(atom))
                {
                    if(g.Symbols[next]!="C" || path.Contains(next) && !(next==bond.A && path.Count==5)) continue;
                    if(path.Count==1 && next==bond.A) continue; // not the bond itself
                    path.Add(next); if(Search(next)) return true; path.RemoveAt(path.Count-1);
                }
                return false;
            }
            bool Alternating()
            {
                var ring=path.Take(6).ToArray(); // B ... A
                for(var i=0;i<6;i++) if(Order(g,ring[i],ring[(i+1)%6])+Order(g,ring[(i+1)%6],ring[(i+2)%6])!=3) return false;
                return true;
            }
            return Search(bond.B);
        }
        static int FirstOr(IEnumerable<int> items,int fallback) { foreach(var item in items) return item; return fallback; }
        static List<int> Indices(IReadOnlyList<MolGraph> contents,Func<MolGraph,bool> test) => Enumerable.Range(0,contents.Count).Where(i=>test(contents[i])).ToList();

        // ---------- FLAME ----------
        static ReactionPlan Combustion(IReadOnlyList<MolGraph> c)
        {
            var fuels=Indices(c,IsFuel); if(fuels.Count==0) return null;
            var oxygen=Indices(c,IsO2);
            var halves=fuels.Sum(i=>OxygenHalves(c[i]));
            const string rule="combustion";
            if(halves%4!=0)
            {
                var odd=fuels.First(i=>OxygenHalves(c[i])%4!=0);
                return ReactionPlan.Missing(rule,"add another "+c[odd].Formula()+": the O atoms must pair up into O2");
            }
            var needed=halves/4; var fuelText=string.Join(" + ",fuels.Select(i=>c[i].Formula()).Distinct());
            if(oxygen.Count<needed) return ReactionPlan.Missing(rule,oxygen.Count==0?"add O2 - "+fuelText+" needs "+needed+" O2 to burn":"add "+(needed-oxygen.Count)+" more O2 ("+needed+" in total)");
            var carbons=fuels.Sum(i=>CountOf(c[i],"C")); var hydrogens=fuels.Sum(i=>CountOf(c[i],"H"));
            var plan=new ReactionPlan{Rule=rule,State=PlanState.Ready,Title=carbons==0?"Hydrogen burns":"Combustion",
                Explanation=carbons==0?"Hydrogen burns in oxygen: every pair of H atoms ends up in a water molecule.":
                    "The fuel burns in oxygen: every C atom ends up in CO2 and every pair of H atoms in H2O."};
            plan.Inputs.AddRange(fuels); plan.Inputs.AddRange(oxygen.Take(needed));
            for(var i=0;i<carbons;i++) { plan.Products.Add(CO2()); plan.ProductNames.Add("Carbon dioxide"); }
            for(var i=0;i<hydrogens/2;i++) { plan.Products.Add(Water()); plan.ProductNames.Add("Water"); }
            return plan;
        }
        static ReactionPlan HydrogenHalogen(IReadOnlyList<MolGraph> c)
        {
            var h2=Indices(c,IsH2); var x2=Indices(c,g=>Halogen2(g)!=null);
            if(h2.Count==0 || x2.Count==0) return null;
            var x=Halogen2(c[x2[0]]);
            var plan=new ReactionPlan{Rule="hydrogen-halogen",State=PlanState.Ready,Title="H2 + "+x+"2",
                Explanation="The flame splits the molecules; H and "+x+" atoms swap partners to make two molecules of "+HalideName(x)+"."};
            plan.Inputs.Add(h2[0]); plan.Inputs.Add(x2[0]);
            for(var i=0;i<2;i++) { plan.Products.Add(Diatomic("H",x)); plan.ProductNames.Add(HalideName(x)); }
            return plan;
        }
        static ReactionPlan Substitution(IReadOnlyList<MolGraph> c,IReadOnlyList<string> names)
        {
            var alkanes=Indices(c,IsAlkane); var x2=Indices(c,g=>Halogen2(g)!=null);
            if(x2.Count==0) return null;
            if(alkanes.Count==0) return ReactionPlan.Missing("substitution","add H2 or an alkane such as CH4 to react with "+c[x2[0]].Formula());
            var pot=new Pot(); var a=pot.Add(c[alkanes[0]],alkanes[0]); var x=pot.Add(c[x2[0]],x2[0]);
            var g=pot.G; var halogen=Halogen2(c[x2[0]]);
            // Swap an H on the carbon that has the most of them (an end CH3) for a halogen atom.
            var carbon=Enumerable.Range(a,c[alkanes[0]].Count).Where(i=>g.Symbols[i]=="C").OrderByDescending(i=>g.HydrogenCount(i)).ThenBy(i=>i).First();
            var hydrogen=Hydrogens(g,carbon).First();
            pot.Unbond(carbon,hydrogen); pot.Unbond(x,x+1); pot.Bond(carbon,x); pot.Bond(hydrogen,x+1);
            var plan=new ReactionPlan{Rule="substitution",State=PlanState.Ready,Title="Substitution",
                Explanation="In the flame (or UV light) a "+halogen+" atom replaces one H atom of the alkane; the H leaves as "+HalideName(halogen)+"."};
            plan.Inputs.Add(alkanes[0]); plan.Inputs.Add(x2[0]);
            foreach(var (graph,source) in pot.Split())
            {
                plan.Products.Add(graph);
                plan.ProductNames.Add(HydrogenHalide(graph)!=null?HalideName(halogen):SubstitutedName(names[alkanes[0]],halogen,CountOf(graph,"C")));
            }
            return plan;
        }

        // ---------- Pd CATALYST ----------
        static ReactionPlan Hydrogenation(IReadOnlyList<MolGraph> c,IReadOnlyList<string> names)
        {
            var unsaturated=Indices(c,g=>PiBonds(g).Count>0); var h2=Indices(c,IsH2);
            if(unsaturated.Count==0) return null;
            if(h2.Count==0) return ReactionPlan.Missing("hydrogenation","add H2 - one H2 for each C=C bond");
            var pot=new Pot(); var plan=new ReactionPlan{Rule="hydrogenation",State=PlanState.Ready,Title="Hydrogenation",
                Explanation="On the palladium surface the H-H bond breaks and one H atom adds to each carbon of the C=C bond, which becomes a single bond."};
            var spare=new Queue<int>(h2);
            foreach(var m in unsaturated)
            {
                if(spare.Count==0) break;
                var start=pot.Add(c[m],m); plan.Inputs.Add(m);
                while(spare.Count>0)
                {
                    var g=pot.G; var candidates=PiBonds(g).Where(i=>pot.Source[g.Bonds[i].A]==m).OrderByDescending(i=>g.Bonds[i].Order);
                    var bond=FirstOr(candidates,-1);
                    if(bond<0) break;
                    var b=g.Bonds[bond]; var h=spare.Dequeue(); var hStart=pot.Add(c[h],h); plan.Inputs.Add(h);
                    pot.SetOrder(b.A,b.B,b.Order-1); pot.Unbond(hStart,hStart+1); pot.Bond(b.A,hStart); pot.Bond(b.B,hStart+1);
                }
            }
            foreach(var (graph,source) in pot.Split()) { plan.Products.Add(graph); plan.ProductNames.Add(HydrogenatedName(names[source],PiBonds(graph).Count>0)); }
            return plan;
        }

        // ---------- ACID ----------
        static ReactionPlan Esterification(IReadOnlyList<MolGraph> c,IReadOnlyList<string> names)
        {
            var acids=Indices(c,g=>Carboxyls(g).Count>0); var alcohols=Indices(c,g=>Alcohols(g).Count>0 && Carboxyls(g).Count==0);
            if(acids.Count==0) return null;
            if(alcohols.Count==0) return ReactionPlan.Missing("ester","add an alcohol such as ethanol to make an ester");
            var pot=new Pot(); var a=pot.Add(c[acids[0]],acids[0]); var b=pot.Add(c[alcohols[0]],alcohols[0]);
            var (carbon,hydroxyl,_)=Carboxyls(c[acids[0]])[0]; var (oxygen,_,hydrogen)=Alcohols(c[alcohols[0]])[0];
            carbon+=a; hydroxyl+=a; oxygen+=b; hydrogen+=b;
            // The acid's OH and the alcohol's H leave together as water; the alcohol's O joins the acid's carbon.
            pot.Unbond(carbon,hydroxyl); pot.Unbond(oxygen,hydrogen); pot.Bond(hydroxyl,hydrogen); pot.Bond(carbon,oxygen);
            var plan=new ReactionPlan{Rule="ester",State=PlanState.Ready,Title="Esterification",
                Explanation="Acid + alcohol -> ester + water. The acid's O-H and the alcohol's H leave as water; the two parts join through an O atom. Esters smell fruity."};
            plan.Inputs.Add(acids[0]); plan.Inputs.Add(alcohols[0]);
            foreach(var (graph,source) in pot.Split())
            { plan.Products.Add(graph); plan.ProductNames.Add(IsWater(graph)?"Water":EsterName(names[alcohols[0]],names[acids[0]])); }
            return plan;
        }
        static ReactionPlan Hydration(IReadOnlyList<MolGraph> c,IReadOnlyList<string> names)
        {
            var alkenes=Indices(c,g=>DoubleBonds(g).Count>0); var water=Indices(c,IsWater);
            if(alkenes.Count==0 && water.Count==0) return null;
            if(alkenes.Count==0)
                return Indices(c,g=>Alcohols(g).Count>0 || Carboxyls(g).Count>0).Count>0?null:
                    ReactionPlan.Missing("hydration",Indices(c,g=>PiBonds(g).Count>0).Count>0?"water only adds to C=C bonds: turn the triple bond into C=C with H2 and Pd first":"add an alkene such as ethene (C2H4)");
            if(water.Count==0) return ReactionPlan.Missing("hydration","add H2O to add water across the C=C bond");
            var pot=new Pot(); var m=alkenes[0]; var start=pot.Add(c[m],m); var w=pot.Add(c[water[0]],water[0]); var g=pot.G;
            var bond=g.Bonds[DoubleBonds(g)[0]];
            // Markovnikov: the OH goes to the carbon with fewer hydrogens, the H to the one with more.
            var toOxygen=g.HydrogenCount(bond.A)<=g.HydrogenCount(bond.B)?bond.A:bond.B; var toHydrogen=toOxygen==bond.A?bond.B:bond.A;
            var oxygen=Enumerable.Range(w,3).First(i=>g.Symbols[i]=="O"); var h=Hydrogens(g,oxygen).First();
            pot.SetOrder(bond.A,bond.B,1); pot.Unbond(oxygen,h); pot.Bond(toHydrogen,h); pot.Bond(toOxygen,oxygen);
            var plan=new ReactionPlan{Rule="hydration",State=PlanState.Ready,Title="Hydration",
                Explanation="The acid helps water add across the C=C bond: H goes to one carbon, OH to the other. The product is an alcohol."};
            plan.Inputs.Add(m); plan.Inputs.Add(water[0]);
            foreach(var (graph,source) in pot.Split()) { plan.Products.Add(graph); plan.ProductNames.Add(HydratedName(names[m],CountOf(graph,"C"))); }
            return plan;
        }
        static ReactionPlan Dehydration(IReadOnlyList<MolGraph> c,IReadOnlyList<string> names)
        {
            if(Indices(c,IsWater).Count>0 || Indices(c,g=>Carboxyls(g).Count>0).Count>0) return null;
            var alcohols=Indices(c,g=>Alcohols(g).Count>0); if(alcohols.Count==0) return null;
            foreach(var m in alcohols)
            {
                var pot=new Pot(); var start=pot.Add(c[m],m); var g=pot.G;
                foreach(var (o,carbon,_) in Alcohols(c[m]))
                {
                    var oxygen=o+start; var alpha=carbon+start;
                    var beta=FirstOr(g.Neighbors(alpha).Where(n=>g.Symbols[n]=="C" && Order(g,alpha,n)==1 && g.HydrogenCount(n)>0),-1);
                    if(beta<0) continue;
                    var h=Hydrogens(g,beta).First();
                    pot.Unbond(alpha,oxygen); pot.Unbond(beta,h); pot.Bond(oxygen,h); pot.SetOrder(alpha,beta,2);
                    var plan=new ReactionPlan{Rule="dehydration",State=PlanState.Ready,Title="Dehydration",
                        Explanation="Strong acid pulls water out of the alcohol: the OH and an H from the next carbon leave as H2O, and a C=C bond forms."};
                    plan.Inputs.Add(m);
                    foreach(var (graph,source) in pot.Split()) { plan.Products.Add(graph); plan.ProductNames.Add(IsWater(graph)?"Water":DehydratedName(names[m])); }
                    return plan;
                }
            }
            return ReactionPlan.Missing("dehydration",c[alcohols[0]].Formula()+" has no neighbouring C-H to lose water with; add an alkene and H2O, or an acid");
        }

        // ---------- no tool ----------
        static ReactionPlan HalogenAddition(IReadOnlyList<MolGraph> c,IReadOnlyList<string> names)
        {
            var alkenes=Indices(c,g=>DoubleBonds(g).Count>0); if(alkenes.Count==0) return null;
            var x2=Indices(c,g=>Halogen2(g)!=null); var hx=Indices(c,g=>HydrogenHalide(g)!=null);
            if(x2.Count==0 && hx.Count==0) return null;
            var pot=new Pot(); var m=alkenes[0]; pot.Add(c[m],m); var g=pot.G; var bond=g.Bonds[DoubleBonds(g)[0]];
            var reagent=x2.Count>0?x2[0]:hx[0]; var r=pot.Add(c[reagent],reagent);
            var halogen=x2.Count>0?Halogen2(c[reagent]):HydrogenHalide(c[reagent]);
            int first,second;
            if(x2.Count>0) { first=r; second=r+1; }
            else { var hydrogen=c[reagent].Symbols[0]=="H"?r:r+1; second=hydrogen; first=hydrogen==r?r+1:r; }
            // H-X: the halogen goes to the carbon with fewer hydrogens (Markovnikov).
            var toFirst=g.HydrogenCount(bond.A)<=g.HydrogenCount(bond.B)?bond.A:bond.B; var toSecond=toFirst==bond.A?bond.B:bond.A;
            pot.SetOrder(bond.A,bond.B,1); pot.Unbond(r,r+1); pot.Bond(toFirst,first); pot.Bond(toSecond,second);
            var plan=new ReactionPlan{Rule="halogen-addition",State=PlanState.Ready,Title=x2.Count>0?"Halogen addition":"Addition of "+c[reagent].Formula(),
                Explanation=x2.Count>0?"The "+halogen+"2 adds straight across the C=C bond, no tool needed. This is why brown bromine water turns colourless with an alkene.":
                    "H-"+halogen+" adds across the C=C bond: H to one carbon, "+halogen+" to the other."};
            plan.Inputs.Add(m); plan.Inputs.Add(reagent);
            foreach(var (graph,source) in pot.Split()) { plan.Products.Add(graph); plan.ProductNames.Add(AddedName(names[m],halogen,x2.Count>0,CountOf(graph,"C"))); }
            return plan;
        }

        // ---------- building products ----------
        static MolGraph CO2()
        {
            var g=new MolGraph(); var c=g.Add("C",Vector3.zero); var a=g.Add("O",new Vector3(-.15f,0,0)); var b=g.Add("O",new Vector3(.15f,0,0));
            g.Bonds.Add(new GraphBond(c,a,2)); g.Bonds.Add(new GraphBond(c,b,2)); return g;
        }
        static MolGraph Water()
        {
            var g=new MolGraph(); var o=g.Add("O",Vector3.zero); var a=g.Add("H",new Vector3(-.11f,-.085f,0)); var b=g.Add("H",new Vector3(.11f,-.085f,0));
            g.Bonds.Add(new GraphBond(o,a,1)); g.Bonds.Add(new GraphBond(o,b,1)); return g;
        }
        static MolGraph Diatomic(string a,string b)
        {
            var g=new MolGraph(); var i=g.Add(a,Vector3.zero); var j=g.Add(b,new Vector3(FreeStructure.BondLength(a,b,1),0,0)); g.Bonds.Add(new GraphBond(i,j,1)); return g;
        }

        /// <summary>Several input molecules side by side in one graph, so bonds can be rewired between them.</summary>
        sealed class Pot
        {
            public readonly MolGraph G=new MolGraph();
            public readonly List<int> Source=new List<int>();
            int molecules;
            public int Add(MolGraph m,int source)
            {
                var start=G.Count; var offset=new Vector3(molecules++*.6f,0,0);
                for(var i=0;i<m.Count;i++) { G.Add(m.Symbols[i],m.Positions[i]+offset); Source.Add(source); }
                foreach(var b in m.Bonds) G.Bonds.Add(new GraphBond(b.A+start,b.B+start,b.Order));
                return start;
            }
            public void Unbond(int a,int b) { var i=G.BondIndex(a,b); if(i<0) throw new InvalidOperationException("No bond to break."); G.Bonds.RemoveAt(i); }
            public void Bond(int a,int b,int order=1) { if(G.BondIndex(a,b)>=0) throw new InvalidOperationException("Already bonded."); G.Bonds.Add(new GraphBond(a,b,order)); }
            public void SetOrder(int a,int b,int order) { var i=G.BondIndex(a,b); var x=G.Bonds[i]; G.Bonds[i]=new GraphBond(x.A,x.B,order); }
            /// <summary>The separate molecules after rewiring, each with tidy geometry, and the input each came from.</summary>
            public List<(MolGraph graph,int source)> Split()
            {
                var result=new List<(MolGraph,int)>(); var seen=new bool[G.Count];
                for(var start=0;start<G.Count;start++)
                {
                    if(seen[start]) continue;
                    var atoms=new List<int>(); var queue=new Queue<int>(); queue.Enqueue(start); seen[start]=true;
                    while(queue.Count>0) { var a=queue.Dequeue(); atoms.Add(a); foreach(var n in G.Neighbors(a)) if(!seen[n]) { seen[n]=true; queue.Enqueue(n); } }
                    var map=new Dictionary<int,int>(); var part=new MolGraph();
                    foreach(var a in atoms) map[a]=part.Add(G.Symbols[a],G.Positions[a]);
                    foreach(var b in G.Bonds) if(map.ContainsKey(b.A)) part.Bonds.Add(new GraphBond(map[b.A],map[b.B],b.Order));
                    var heavy=FirstOr(atoms.Where(a=>G.Symbols[a]!="H"),atoms[0]);
                    Tidy(part); result.Add((part,Source[heavy]));
                }
                return result;
            }
        }

        /// <summary>Gives a rewired molecule sensible bond lengths and angles (atoms that moved far are brought next to their partner first).</summary>
        public static void Tidy(MolGraph g)
        {
            if(g.Count<2) return;
            var placed=new bool[g.Count]; var queue=new Queue<int>(); var root=Enumerable.Range(0,g.Count).FirstOrDefault(i=>g.Symbols[i]!="H");
            queue.Enqueue(root); placed[root]=true; var turn=0;
            while(queue.Count>0)
            {
                var a=queue.Dequeue();
                foreach(var n in g.Neighbors(a))
                {
                    if(placed[n]) continue; placed[n]=true; queue.Enqueue(n);
                    var bond=g.Bonds[g.BondIndex(a,n)]; var length=FreeStructure.BondLength(g.Symbols[a],g.Symbols[n],bond.Order);
                    if(Vector3.Distance(g.Positions[a],g.Positions[n])>length*1.6f || Vector3.Distance(g.Positions[a],g.Positions[n])<length*.4f)
                    {
                        var angle=++turn*2.39996f; // golden angle: neighbours spread out
                        g.Positions[n]=g.Positions[a]+new Vector3(Mathf.Cos(angle),.35f*Mathf.Sin(angle*1.7f),Mathf.Sin(angle)).normalized*length;
                    }
                }
            }
            var structure=new FreeStructure();
            for(var i=0;i<g.Count;i++) structure.AddAtom(g.Symbols[i],Valence(g.Symbols[i]),g.Positions[i],-1);
            foreach(var b in g.Bonds) structure.AddBond(b.A,b.B);
            for(var i=0;i<g.Bonds.Count;i++) if(g.Bonds[i].Order>1) structure.SetOrder(i,g.Bonds[i].Order);
            structure.Relax(400);
            var centre=structure.Centroid();
            for(var i=0;i<g.Count;i++) g.Positions[i]=structure.Position(i)-centre;
        }

        // ---------- names ----------
        static string Stem(string name,string suffix) => name!=null && name.Length>suffix.Length && name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)?name.Substring(0,name.Length-suffix.Length):null;
        static string Lower(string s) => string.IsNullOrEmpty(s)?s:char.ToLowerInvariant(s[0])+s.Substring(1);
        static string Upper(string s) => string.IsNullOrEmpty(s)?s:char.ToUpperInvariant(s[0])+s.Substring(1);
        static string Normal(string name)
        {
            switch(name) { case "Ethylene": return "Ethene"; case "Acetylene": return "Ethyne"; case "Propylene": return "Propene"; default: return name; }
        }
        static string Halo(string x) => x=="F"?"fluoro":x=="Cl"?"chloro":"bromo";
        public static string HalideName(string x) => "Hydrogen "+(x=="F"?"fluoride":x=="Cl"?"chloride":"bromide");
        static string HydrogenatedName(string name,bool stillUnsaturated)
        {
            name=Normal(name); var yne=Stem(name,"yne"); var ene=Stem(name,"ene");
            if(yne!=null) return yne+(stillUnsaturated?"ene":"ane");
            return ene!=null && !stillUnsaturated?ene+"ane":null;
        }
        static string HydratedName(string name,int carbons)
        {
            var ene=Stem(Normal(name),"ene"); if(ene==null) return null;
            return carbons<=2?ene+"anol":carbons==3?ene+"an-2-ol":null;
        }
        static string AddedName(string name,string halogen,bool both,int carbons)
        {
            var ene=Stem(Normal(name),"ene"); if(ene==null || carbons>3) return null;
            if(both) return "1,2-Di"+Halo(halogen)+Lower(ene)+"ane";
            return carbons<=2?Upper(Halo(halogen))+Lower(ene)+"ane":"2-"+Upper(Halo(halogen))+Lower(ene)+"ane";
        }
        static string SubstitutedName(string name,string halogen,int carbons)
        {
            if(Stem(name,"ane")==null) return null;
            return carbons<=2?Upper(Halo(halogen))+Lower(name):"1-"+Upper(Halo(halogen))+Lower(name);
        }
        static string EsterName(string alcohol,string acid)
        {
            var alkyl=Stem(alcohol,"anol"); if(alkyl==null) return null;
            string ate=null;
            switch(acid) { case "Acetic acid": ate="acetate"; break; case "Formic acid": ate="formate"; break; }
            if(ate==null) { var stem=Stem(acid,"ic acid"); if(stem!=null) ate=Lower(stem)+"ate"; }
            return ate==null?null:alkyl+"yl "+ate;
        }
        static string DehydratedName(string name)
        {
            var stem=Stem(name,"anol")??Stem(name,"an-1-ol")??Stem(name,"an-2-ol");
            return stem==null?null:stem+"ene";
        }
        /// <summary>A plain description when there is no better name: the kind of molecule plus its formula.</summary>
        public static string ClassName(MolGraph g)
        {
            string kind;
            if(Carboxyls(g).Count>0) kind="Carboxylic acid";
            else if(g.Symbols.Contains("O") && Enumerable.Range(0,g.Count).Any(o=>g.Symbols[o]=="O" && g.Degree(o)==2 && g.Neighbors(o).All(n=>g.Symbols[n]=="C") &&
                g.Neighbors(o).Any(n=>g.Neighbors(n).Any(x=>g.Symbols[x]=="O" && Order(g,n,x)==2)))) kind="Ester";
            else if(Alcohols(g).Count>0) kind="Alcohol";
            else if(Halogens.Any(g.Symbols.Contains) && g.Symbols.Contains("C")) kind="Haloalkane";
            else if(Only(g,"C","H") && PiBonds(g,3).Count>0) kind="Alkyne";
            else if(Only(g,"C","H") && PiBonds(g).Count>0) kind="Alkene";
            else if(IsAlkane(g)) kind="Alkane";
            else kind="Molecule";
            return kind+" "+g.Formula();
        }
    }
}
