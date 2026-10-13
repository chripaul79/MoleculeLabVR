using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
namespace MoleculeLab.Editor
{
    public static class LabUpgrade
    {
        [MenuItem("Molecule Lab/Apply interaction and chemistry update")]
        public static void Apply()
        {
            LabContentBuilder.Generate();
            EditorSceneManager.OpenScene(QuestProjectSetup.ScenePath);
            var lab=UnityEngine.Object.FindAnyObjectByType<LabController>();
            var names=new[]{"Water","Methane","Ethane","Hydrogen","Oxygen","Ethene","Ethyne","Methanol","Ethanol","CarbonDioxide","Benzene"};
            lab.molecules=names.Select(n=>AssetDatabase.LoadAssetAtPath<MoleculeDefinition>("Assets/MoleculeLab/Data/"+n+".asset")).ToArray();
            lab.reactions=new[]{"WaterReaction","MethaneCombustion","EtheneHydrogenation","EtheneHydration"}.Select(n=>AssetDatabase.LoadAssetAtPath<ReactionDefinition>("Assets/MoleculeLab/Data/"+n+".asset")).ToArray();
            lab.snapDistance=.05f; lab.snapAngle=15; lab.previewDistance=.18f;
            foreach(var m in lab.molecules) if(!m.IsValid(out var error)) throw new Exception(m.name+": "+error);
            var origin=UnityEngine.Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>().gameObject;
            if(PrefabUtility.IsPartOfPrefabInstance(origin)) PrefabUtility.UnpackPrefabInstance(origin,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var poke in origin.GetComponentsInChildren<XRPokeInteractor>(true)) { poke.enableUIInteraction=false; poke.enabled=false; }
            foreach(var handName in new[]{"Left Hand","Right Hand"})
            {
                var hand=origin.GetComponentsInChildren<Transform>(true).First(t=>t.name==handName);
                foreach(var poke in hand.GetComponentsInChildren<XRPokeInteractor>(true)) { poke.enableUIInteraction=false; poke.enabled=false; }
                foreach(var component in hand.GetComponentsInChildren<MonoBehaviour>(true))
                    if(component!=null && component.GetType().Name=="PokeGestureDetector") component.enabled=false;
                var near=hand.GetComponentInChildren<NearFarInteractor>(true);
                if(near==null) throw new Exception("Missing direct hand interactor: "+handName);
                HandMenuRay.Configure(near);
                near.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.StateChange;
                // The old curve visuals would otherwise show an object-grab ray.
                foreach(var component in near.GetComponents<MonoBehaviour>())
                    if(component.GetType().Name=="CurveVisualController") component.enabled=true;
                var gate=near.GetComponent<HandMenuRay>() ?? near.gameObject.AddComponent<HandMenuRay>();
                gate.direct=near;
                var caster=near.farInteractionCaster as InteractionCasterBase;
                gate.aim=caster!=null?caster.castOrigin:hand.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Aim Pose");
            }
            System.IO.Directory.CreateDirectory("Assets/MoleculeLab/Prefabs");
            PrefabUtility.SaveAsPrefabAssetAndConnect(origin,"Assets/MoleculeLab/Prefabs/MoleculeLabRig.prefab",InteractionMode.AutomatedAction);
            var environment=UnityEngine.Object.FindAnyObjectByType<LabEnvironment>();
            var root=environment.gameObject;
            if(PrefabUtility.IsPartOfPrefabInstance(root)) PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var shelf=environment.workstation.Find("Reachable atom shelf"); shelf.localPosition=new Vector3(-.92f,0,.12f);
            var benchRenderer=environment.bench.Find("Workbench").GetComponent<Renderer>();
            var shelfRenderers=shelf.GetComponentsInChildren<MeshRenderer>();
            // Clear the front of the worktop as well as shifting left, without pushing stock out of reach.
            var back=shelfRenderers.Max(r=>r.bounds.max.z); var limit=benchRenderer.bounds.min.z-.05f;
            if(back>limit) shelf.position+=Vector3.back*(back-limit);
            environment.panelMount.Find("Display back").localScale=new Vector3(1.18f,.82f,.035f);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,"Assets/MoleculeLab/Environment/AcademicLab.prefab",InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(lab); EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            EditorSceneManager.SaveScene(lab.gameObject.scene); AssetDatabase.SaveAssets();
            Debug.Log("MOLECULE_LAB_UPGRADE_COMPLETE: 11 molecules, 4 reactions; shelf x="+shelf.localPosition.x);
        }
    }
}
