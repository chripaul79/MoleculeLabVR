using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoleculeLab.Editor
{
    public static class LabContentBuilder
    {
        const string Data="Assets/MoleculeLab/Data/";
        static T Asset<T>(string name) where T:ScriptableObject
        {
            var path=Data+name+".asset";
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(asset==null) { asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); }
            EditorUtility.SetDirty(asset); return asset;
        }
        [MenuItem("Molecule Lab/Create chemistry content (preserve existing scene)")]
        public static void Generate()
        {
            Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            var h=Element("Hydrogen","H",Color.white,.036f,1);
            var o=Element("Oxygen","O",new Color(.95f,.18f,.16f),.052f,2);
            var c=Element("Carbon","C",new Color(.2f,.24f,.3f),.055f,4);
            var water=Molecule("water","Water","H2O","Bent / H-O-H: 104.5 deg",
                new[]{new AtomSite(o,Vector3.zero),new AtomSite(h,new Vector3(-.79f,.612f,0)*.16f),new AtomSite(h,new Vector3(.79f,.612f,0)*.16f)},new[]{new BondDefinition(0,1),new BondDefinition(0,2)});
            var tetra=new[]{new Vector3(1,1,1).normalized,new Vector3(1,-1,-1).normalized,new Vector3(-1,1,-1).normalized,new Vector3(-1,-1,1).normalized};
            var methane=Molecule("methane","Methane","CH4","Tetrahedral / H-C-H: 109.5 deg",new[]{new AtomSite(c,Vector3.zero)}.Concat(tetra.Select(v=>new AtomSite(h,v*.17f))).ToArray(),Enumerable.Range(1,4).Select(i=>new BondDefinition(0,i)).ToArray());
            var ethaneAtoms=new AtomSite[8]; ethaneAtoms[0]=new AtomSite(c,new Vector3(-.1f,0,0)); ethaneAtoms[1]=new AtomSite(c,new Vector3(.1f,0,0));
            for(var side=0;side<2;side++) for(var j=0;j<3;j++)
            {
                var angle=(j*120+(side==1?60:0))*Mathf.Deg2Rad;
                var direction=new Vector3(side==0?-1f/3:1f/3,Mathf.Sqrt(8f/9)*Mathf.Cos(angle),Mathf.Sqrt(8f/9)*Mathf.Sin(angle));
                ethaneAtoms[2+side*3+j]=new AtomSite(h,ethaneAtoms[side].position+direction*.16f);
            }
            var ethane=Molecule("ethane","Ethane","C2H6","Tetrahedral carbons / approx. 109.5 deg\nC-C single bond permits torsion",ethaneAtoms,
                new[]{new BondDefinition(0,1)}.Concat(Enumerable.Range(2,6).Select(i=>new BondDefinition(i<5?0:1,i))).ToArray());
            ethane.rotatableBond=0; ethane.rotatingSites=new[]{5,6,7};
            var hydrogen=Molecule("hydrogen","Hydrogen","H2","Diatomic / single bond",new[]{new AtomSite(h,new Vector3(-.06f,0,0)),new AtomSite(h,new Vector3(.06f,0,0))},new[]{new BondDefinition(0,1)});
            var oxygen=Molecule("oxygen","Oxygen","O2","Diatomic / double bond",new[]{new AtomSite(o,new Vector3(-.08f,0,0)),new AtomSite(o,new Vector3(.08f,0,0))},new[]{new BondDefinition(0,1,2)});
            var radical=Molecule("methyl-radical","Methyl radical","CH3 radical","Simplified fragment / unpaired electron",new[]{new AtomSite(c,Vector3.zero)}.Concat(Enumerable.Range(0,3).Select(i=>new AtomSite(h,new Vector3(Mathf.Cos(i*120*Mathf.Deg2Rad),Mathf.Sin(i*120*Mathf.Deg2Rad),0)*.16f))).ToArray(),Enumerable.Range(1,3).Select(i=>new BondDefinition(0,i)).ToArray());
            radical.isRadical=true; radical.radicalSites=new[]{0}; radical.molecularFormula="CH3"; radical.electronicState="One unpaired electron on carbon; approximately trigonal planar.";
            oxygen.electronicState="Triplet ground state: two unpaired electrons. The O=O drawing does not show the molecular orbital electronic structure.";
            var reaction=Asset<ReactionDefinition>("WaterReaction"); reaction.id="water-formation"; reaction.title="Water formation"; reaction.requiredCondition=ReactionCondition.Ignition; reaction.conditions="Ignition; hydrogen/oxygen combustion"; reaction.equation="2 H2 + O2 = 2 H2O";
            reaction.explanation="Balanced atom rearrangement after explicit activation; not a simulation of reaction kinetics or heat.";
            reaction.inputs=new[]{new ReactionAmount(hydrogen,2),new ReactionAmount(oxygen,1)}; reaction.outputs=new[]{new ReactionAmount(water,2)};
            var extra=ExpandedChemistry.Create(c,h,o,ethane,water,methane,hydrogen,oxygen,reaction);
            foreach(var definition in new[]{water,methane,ethane,hydrogen,oxygen,radical}.Concat(extra)) if(!definition.IsValid(out var error)) throw new Exception(definition.name+": "+error);
            if(!ReactionEngine.IsBalanced(reaction)) throw new Exception("Unbalanced reaction");
            AssetDatabase.SaveAssets();
            if(File.Exists(QuestProjectSetup.ScenePath)) { Debug.Log("Chemistry definitions updated; existing scene and environment preserved."); return; }
            QuestProjectSetup.CreateFoundation();
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var name in new[]{"Foundation Workbench","Foundation Grab Test"}) { var go=GameObject.Find(name); if(go!=null) UnityEngine.Object.DestroyImmediate(go); }
            var root=new GameObject("Molecule Lab"); var lab=root.AddComponent<LabController>();
            lab.elements=new[]{h,o,c}; lab.molecules=new[]{water,methane,ethane,hydrogen,oxygen}; lab.methylRadical=radical; lab.waterReaction=reaction;
            lab.font=AssetDatabase.FindAssets("Inter-Regular t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();
            if(lab.font==null) lab.font=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.StartsWith("Assets/")).Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).First();
            var camera=Camera.main; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.045f,.075f); camera.farClipPlane=30;
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.6f,.66f,.73f);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>()) light.shadows=LightShadows.None;
            EditorSceneManager.SaveScene(scene,QuestProjectSetup.ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("MOLECULE_LAB_CONTENT_GENERATED");
        }
        static ElementDefinition Element(string name,string symbol,Color color,float radius,int valence)
        {
            var e=Asset<ElementDefinition>("Element_"+name); e.displayName=name; e.symbol=symbol; e.color=color; e.radius=radius; e.maximumBondOrder=valence; return e;
        }
        static MoleculeDefinition Molecule(string id,string name,string formula,string geometry,AtomSite[] atoms,BondDefinition[] bonds)
        {
            var m=Asset<MoleculeDefinition>(name.Replace(" ","")); m.id=id; m.displayName=name; m.formula=formula; m.geometry=geometry; m.atoms=atoms; m.bonds=bonds; m.seedIndex=0; m.rotatableBond=-1; m.rotatingSites=Array.Empty<int>(); m.molecularFormula=formula; m.isRadical=false; m.radicalSites=Array.Empty<int>(); m.aromaticRing=Array.Empty<int>(); return m;
        }
    }
}
