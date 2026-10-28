using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace MoleculeLab.Tests
{
    /// <summary>Every menu button can be reached by a ray from where the player stands: nothing solid sits in front of the panel.</summary>
    public sealed class MenuReachTests
    {
        [UnityTest]
        public IEnumerator NothingBlocksTheMenuButtons()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null; lab.UseGalleryFile(null);
            var panel=GameObject.Find("Lab control panel"); Assert.That(panel,Is.Not.Null);
            var raycaster=panel.GetComponent<TrackedDeviceGraphicRaycaster>(); Assert.That(raycaster!=null && raycaster.enabled,Is.True,"The panel needs an enabled XR raycaster.");
            var module=Object.FindAnyObjectByType<XRUIInputModule>(); Assert.That(module!=null && module.isActiveAndEnabled,Is.True,"An enabled XR UI input module is needed.");
            Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>().Count(e=>e.isActiveAndEnabled),Is.EqualTo(1),"Exactly one active EventSystem.");
            var head=Camera.main.transform.position;
            var eyes=new[]{head,lab.BenchDropPosition+new Vector3(0,1.0f,-.7f)};
            var report=new System.Text.StringBuilder();
            foreach(var button in panel.GetComponentsInChildren<Button>().Where(b=>b.isActiveAndEnabled))
                foreach(var eye in eyes)
                {
                    var target=button.transform.position; var direction=target-eye; var distance=direction.magnitude-.01f;
                    foreach(var hit in Physics.RaycastAll(eye,direction.normalized,distance,raycaster.blockingMask,QueryTriggerInteraction.Ignore))
                        report.Append(button.name).Append(" blocked by ").Append(hit.collider.name).Append(" (").Append(hit.collider.transform.parent!=null?hit.collider.transform.parent.name:"").Append(") at ").Append(hit.distance.ToString("0.00")).Append('\n');
                }
            Debug.Log("Menu reach: checkFor3DOcclusion="+raycaster.checkFor3DOcclusion+" mask="+raycaster.blockingMask.value+" eye="+head+"\n"+report);
            Assert.That(report.Length==0 || !raycaster.checkFor3DOcclusion,Is.True,report.ToString());
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Points each of the rig's real Near-Far interactors (both controllers and both hands) at the FREE BUILD tab and
        /// presses it through the real XR UI module. Every one of them must switch the tab.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryRigRayCanPressATab()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            yield return new EnterPlayMode(); yield return null;
            var lab=Object.FindAnyObjectByType<LabController>(); for(var startWait=UnityEditor.EditorApplication.timeSinceStartup+10;!lab.Ready && UnityEditor.EditorApplication.timeSinceStartup<startWait;) yield return null;
            lab.UseGalleryFile(null);
            // No headset here: stop the rig from hiding or re-posing its devices, and drive the presses ourselves.
            foreach(var b in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include))
                if(b!=null && (b.GetType().Name=="XRInputModalityManager" || b.GetType().Name=="TrackedPoseDriver" || b.GetType().Name=="XRTransformStabilizer" || b is HandMenuRay)) b.enabled=false;
            var panel=GameObject.Find("Lab control panel");
            var label=panel.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.text.Trim()=="FREE BUILD");
            var tab=label.GetComponentInParent<Button>(); Assert.That(tab,Is.Not.Null);
            var report=new System.Text.StringBuilder(); var failures=0;
            var interactors=Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include).ToArray();
            report.Append(interactors.Length).Append(" near-far interactors\n");
            foreach(var ray in interactors)
            {
                var path=ray.transform.parent!=null?ray.transform.parent.name+"/"+ray.name:ray.name;
                var wasUI=ray.enableUIInteraction; var wasActive=ray.gameObject.activeInHierarchy; var wasEnabled=ray.enabled;
                foreach(var t in ray.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
                ray.enabled=true; ray.enableUIInteraction=true; ray.enableFarCasting=true;
                var from=tab.transform.position+new Vector3(0,-.3f,-.9f); var look=Quaternion.LookRotation(tab.transform.position-from);
                // Pose the whole device (controller or hand root), then the interactor and its ray origin on top of it.
                if(ray.transform.parent!=null) ray.transform.parent.SetPositionAndRotation(from,look);
                ray.transform.SetPositionAndRotation(from,look);
                foreach(var t in ray.transform.parent!=null?ray.transform.parent.GetComponentsInChildren<Transform>(true):new Transform[0])
                    if(t.name.Contains("Stabilized") || t.name.Contains("Aim") || t.name.Contains("Attach")) t.SetPositionAndRotation(from,look);
                if(ray.farInteractionCaster is InteractionCasterBase caster && caster.castOrigin!=null) caster.castOrigin.SetPositionAndRotation(from,look);
                var state=new HandPinchState(); float strength=0;
                ray.uiPressInput.bypass=new HandPinchReader(state,true,()=>true,()=>strength);
                ray.selectInput.bypass=new HandPinchReader(state,false,()=>true,()=>strength);
                state.Step(0,0,null,null,null);
                lab.SetMode(LabMode.Build);
                for(var i=0;i<5;i++) yield return null;
                var hit=ray.TryGetCurrentUIRaycastResult(out var result)?result.gameObject.name:"nothing";
                strength=1; state.Step(1,1,null,null,tab.gameObject); yield return null; yield return null;
                strength=0; state.Step(0,2,null,null,tab.gameObject); yield return null; yield return null;
                var origin=ray.farInteractionCaster is InteractionCasterBase c2 && c2.effectiveCastOrigin!=null?c2.effectiveCastOrigin:ray.transform;
                var ok=lab.Mode==LabMode.Free; if(!ok) failures++;
                report.Append("   ray from ").Append(origin.name).Append(' ').Append(origin.position).Append(" towards ").Append(origin.forward).Append(" tab at ").Append(tab.transform.position).Append('\n');
                report.Append(ok?"OK   ":"FAIL ").Append(path).Append(" | was active=").Append(wasActive).Append(" enabled=").Append(wasEnabled).Append(" ui=").Append(wasUI)
                      .Append(" | ui hit=").Append(hit).Append(" | layers=").Append(ray.interactionLayers.value).Append('\n');
                ray.uiPressInput.bypass=null; ray.selectInput.bypass=null; ray.gameObject.SetActive(false);
            }
            Debug.Log("Menu press report:\n"+report);
            Assert.That(failures,Is.Zero,report.ToString());
            yield return new ExitPlayMode();
        }
    }
}
