using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace MoleculeLab.Editor
{
    public static class ExpandedChemistry
    {
        static T Asset<T>(string name) where T:ScriptableObject
        {
            var path="Assets/MoleculeLab/Data/"+name+".asset";
            var result=AssetDatabase.LoadAssetAtPath<T>(path);
            if(result==null) { result=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(result,path); }
            EditorUtility.SetDirty(result); return result;
        }
        static MoleculeDefinition Molecule(string name,string id,string formula,string geometry,AtomSite[] atoms,BondDefinition[] bonds)
        {
            var m=Asset<MoleculeDefinition>(name); m.id=id; m.displayName=name=="CarbonDioxide"?"Carbon dioxide":name;
            m.formula=m.molecularFormula=formula; m.geometry=geometry+"\nIdealized geometry; enlarged educational model.";
            m.atoms=atoms; m.bonds=bonds; m.seedIndex=0; m.rotatableBond=-1; m.rotatingSites=Array.Empty<int>();
            m.radicalSites=Array.Empty<int>(); m.aromaticRing=Array.Empty<int>(); m.isRadical=false; return m;
        }
        static ReactionDefinition Reaction(string asset,string id,string title,string equation,ReactionCondition condition,string conditions,string explanation,ReactionAmount[] inputs,ReactionAmount[] outputs,string source,bool reversible=false)
        {
            var r=Asset<ReactionDefinition>(asset); r.id=id; r.title=title; r.equation=equation; r.requiredCondition=condition;
            r.conditions=conditions; r.explanation=explanation; r.inputs=inputs; r.outputs=outputs; r.sources=new[]{source}; r.reversible=reversible;
            if(!ReactionEngine.IsBalanced(r)) throw new Exception("Unbalanced reaction: "+id); return r;
        }
        public static MoleculeDefinition[] Create(ElementDefinition c,ElementDefinition h,ElementDefinition o,MoleculeDefinition ethane,MoleculeDefinition water,MoleculeDefinition methane,MoleculeDefinition hydrogen,MoleculeDefinition oxygen,ReactionDefinition waterReaction)
        {
            var ethene=Molecule("Ethene","ethene","C2H4","Planar sp2 carbons / 120 deg; C=C cannot freely rotate",
                new[]{new AtomSite(c,new Vector3(-.1f,0,0)),new AtomSite(c,new Vector3(.1f,0,0)),
                new AtomSite(h,new Vector3(-.18f,.138564f,0)),new AtomSite(h,new Vector3(-.18f,-.138564f,0)),
                new AtomSite(h,new Vector3(.18f,.138564f,0)),new AtomSite(h,new Vector3(.18f,-.138564f,0))},
                new[]{new BondDefinition(0,1,2),new BondDefinition(0,2),new BondDefinition(0,3),new BondDefinition(1,4),new BondDefinition(1,5)});
            var ethyne=Molecule("Ethyne","ethyne","C2H2","Linear sp carbons / 180 deg; C-C triple bond",
                new[]{new AtomSite(c,new Vector3(-.09f,0,0)),new AtomSite(c,new Vector3(.09f,0,0)),new AtomSite(h,new Vector3(-.25f,0,0)),new AtomSite(h,new Vector3(.25f,0,0))},
                new[]{new BondDefinition(0,1,3),new BondDefinition(0,2),new BondDefinition(1,3)});
            var co2=Molecule("CarbonDioxide","carbon-dioxide","CO2","Linear / O-C-O: 180 deg",
                new[]{new AtomSite(c,Vector3.zero),new AtomSite(o,Vector3.left*.18f),new AtomSite(o,Vector3.right*.18f)},new[]{new BondDefinition(0,1,2),new BondDefinition(0,2,2)});
            var tetra=new[]{new Vector3(1,1,1).normalized,new Vector3(1,-1,-1).normalized,new Vector3(-1,1,-1).normalized,new Vector3(-1,-1,1).normalized};
            var methanolAtoms=new AtomSite[6]; methanolAtoms[0]=new AtomSite(c,Vector3.zero); methanolAtoms[1]=new AtomSite(o,tetra[0]*.19f);
            for(var i=0;i<3;i++) methanolAtoms[i+2]=new AtomSite(h,tetra[i+1]*.16f);
            var oh=Quaternion.AngleAxis(108.5f,Vector3.Cross(tetra[0],Vector3.up).normalized)*(-tetra[0]);
            methanolAtoms[5]=new AtomSite(h,methanolAtoms[1].position+oh*.145f);
            var methanol=Molecule("Methanol","methanol","CH4O","Tetrahedral carbon / 109.47 deg; bent C-O-H / approx. 108.5 deg",methanolAtoms,
                new[]{new BondDefinition(0,1),new BondDefinition(0,2),new BondDefinition(0,3),new BondDefinition(0,4),new BondDefinition(1,5)});
            methanol.formula="CH3OH";
            var ethanolAtoms=ethane.atoms.ToList(); var oxygenPosition=ethanolAtoms[7].position; ethanolAtoms[7]=new AtomSite(o,oxygenPosition);
            var oc=(ethanolAtoms[1].position-oxygenPosition).normalized;
            ethanolAtoms.Add(new AtomSite(h,oxygenPosition+Quaternion.AngleAxis(108.5f,Vector3.Cross(oc,Vector3.up).normalized)*oc*.145f));
            var ethanol=Molecule("Ethanol","ethanol","C2H6O","Tetrahedral carbons / 109.47 deg; bent C-O-H / approx. 108.5 deg",ethanolAtoms.ToArray(),ethane.bonds.Concat(new[]{new BondDefinition(7,8)}).ToArray()); ethanol.formula="C2H5OH";
            var benzeneAtoms=new AtomSite[12]; var benzeneBonds=new BondDefinition[12];
            for(var i=0;i<6;i++)
            {
                var direction=new Vector3(Mathf.Cos(i*Mathf.PI/3),Mathf.Sin(i*Mathf.PI/3),0);
                benzeneAtoms[i]=new AtomSite(c,direction*.20f); benzeneAtoms[i+6]=new AtomSite(h,direction*.355f);
                benzeneBonds[i]=new BondDefinition(i,(i+1)%6,i%2==0?2:1); benzeneBonds[i+6]=new BondDefinition(i,i+6);
            }
            var benzene=Molecule("Benzene","benzene","C6H6","Planar sp2 ring / 120 deg; six delocalized pi electrons",benzeneAtoms,benzeneBonds);
            benzene.aromaticRing=Enumerable.Range(0,6).ToArray(); benzene.electronicState="Equivalent aromatic C-C bonds. The circle represents delocalization; Kekule structures are resonance contributors, not alternating physical bonds.";
            waterReaction.sources=new[]{"https://openstax.org/books/chemistry-2e/pages/4-1-writing-and-balancing-chemical-equations"};
            Reaction("MethaneCombustion","methane-combustion","Methane combustion","CH4 + 2 O2 -> CO2 + 2 H2O",ReactionCondition.Ignition,"Ignition and sufficient oxygen for complete combustion",
                "Complete combustion forms carbon dioxide and water. Oxygen limitation can produce other products; those pathways and heat are not simulated.",
                new[]{new ReactionAmount(methane,1),new ReactionAmount(oxygen,2)},new[]{new ReactionAmount(co2,1),new ReactionAmount(water,2)},"https://openstax.org/books/chemistry-2e/pages/4-1-writing-and-balancing-chemical-equations");
            Reaction("EtheneHydrogenation","ethene-hydrogenation","Ethene hydrogenation","C2H4 + H2 -> C2H6",ReactionCondition.PalladiumCatalyst,"Hydrogen with palladium catalyst (Pd)",
                "Hydrogen adds across C=C to form ethane. Pd is a catalyst and is not consumed; catalyst surfaces and kinetics are not simulated.",
                new[]{new ReactionAmount(ethene,1),new ReactionAmount(hydrogen,1)},new[]{new ReactionAmount(ethane,1)},"https://openstax.org/books/organic-chemistry/pages/8-6-reduction-of-alkenes-hydrogenation");
            Reaction("EtheneHydration","ethene-hydration","Ethene hydration","C2H4 + H2O <=> C2H5OH",ReactionCondition.AcidCatalyst,"Acid-catalyzed hydration; equilibrium depends on conditions",
                "Water adds across C=C. Acid is catalytic. This animation shows only the forward net transformation, not the mechanism, equilibrium yield or complete conversion.",
                new[]{new ReactionAmount(ethene,1),new ReactionAmount(water,1)},new[]{new ReactionAmount(ethanol,1)},"https://openstax.org/books/organic-chemistry/pages/17-3-preparation-of-alcohols-a-review",true);
            return new[]{ethene,ethyne,methanol,ethanol,co2,benzene};
        }
    }
}
