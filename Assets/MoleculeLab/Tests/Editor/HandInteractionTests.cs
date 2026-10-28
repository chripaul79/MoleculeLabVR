using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace MoleculeLab.Tests
{
    public sealed class HandInteractionTests
    {
        [Test] public void PinchThresholdsAndOwnershipAreStable()
        {
            var s=new HandPinchState(); var atom=new object(); var button=new object();
            s.Step(0,0,null,null,null); s.Step(.79f,1,atom,null,button); Assert.That(s.Pinched,Is.False);
            s.Step(.8f,2,atom,null,button); Assert.That(s.Target,Is.SameAs(atom)); Assert.That(s.Menu,Is.False);
            s.Step(.65f,3,null,null,button); Assert.That(s.Pinched,Is.True); Assert.That(s.Target,Is.SameAs(atom));
            s.Step(.6f,4,null,null,button); Assert.That(s.Up,Is.True); Assert.That(s.Pinched,Is.False);
        }
        [Test] public void MenuUnderTheRayBeatsAFarObject()
        {
            // An atom or molecule floating in front of the panel used to swallow tab presses.
            var s=new HandPinchState(); var far=new object(); var tab=new object(); var near=new object();
            s.Step(0,0,null,null,null); s.Step(1,1,null,far,tab);
            Assert.That(s.Target,Is.SameAs(tab)); Assert.That(s.Menu,Is.True);
            s.Step(0,2,null,null,null); s.Step(1,3,null,far,null);
            Assert.That(s.Target,Is.SameAs(far),"With no menu under the ray, distance grabs still work."); Assert.That(s.Menu,Is.False);
            s.Step(0,4,null,null,null); s.Step(1,5,near,far,tab);
            Assert.That(s.Target,Is.SameAs(near),"Something in the hand's reach wins."); Assert.That(s.Menu,Is.False);
        }
        [TestCase(.14f,true)] [TestCase(.16f,false)]
        public void EarlyPinchHasBoundedAcquisitionWindow(float delay,bool accepted)
        {
            var s=new HandPinchState(); var atom=new object(); s.Step(0,0,null,null,null);
            s.Step(1,1,null,null,null); s.Step(1,1+delay,atom,null,null);
            Assert.That(s.Target!=null,Is.EqualTo(accepted));
        }
        [Test] public void CandidateRequiresOneCentimetreAdvantage()
        {
            Assert.That(HandAtomCaster.Prefer(.025f,.02f,.01f),Is.False);
            Assert.That(HandAtomCaster.Prefer(.025f,.014f,.01f),Is.True);
        }
        sealed class TestCaster : IInteractionCaster
        {
            public bool isInitialized=>true;
            public Transform castOrigin { get; set; }
            public Transform effectiveCastOrigin=>castOrigin;
            public bool TryGetColliderTargets(XRInteractionManager manager,System.Collections.Generic.List<Collider> targets) { targets.Clear(); return false; }
        }
        [Test] public void AcquisitionUsesSurfaceMarginAndStableNearestAtom()
        {
            var root=new GameObject("Acquisition test"); root.SetActive(false);
            var element=ScriptableObject.CreateInstance<ElementDefinition>(); element.symbol="H"; element.radius=.036f;
            var a=GameObject.CreatePrimitive(PrimitiveType.Sphere); var b=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                a.transform.localScale=b.transform.localScale=Vector3.one*.072f; b.transform.position=Vector3.right*.09f;
                var atomA=a.AddComponent<LooseAtom>(); atomA.Initialize(null,element,0);
                var atomB=b.AddComponent<LooseAtom>(); atomB.Initialize(null,element,1);
                // Edit-mode fixtures do not invoke the runtime OnEnable registry hooks.
                LooseAtom.Active.Add(atomA); LooseAtom.Active.Add(atomB);
                var owner=root.AddComponent<HandMenuRay>(); var caster=root.AddComponent<HandAtomCaster>(); caster.owner=owner; caster.fallback=new TestCaster { castOrigin=root.transform };
                // Reach is 4.5 cm beyond the atom's surface (radius 3.6 cm): 8.1 cm from its centre.
                root.transform.position=Vector3.left*.083f; Assert.That(caster.FindAtom(),Is.Null);
                root.transform.position=Vector3.left*.079f; Assert.That(caster.FindAtom(),Is.SameAs(atomA));
                root.transform.position=Vector3.right*.048f; Assert.That(caster.FindAtom(),Is.SameAs(atomA),"Small target differences must not flicker");
                root.transform.position=Vector3.right*.052f; Assert.That(caster.FindAtom(),Is.SameAs(atomB));
            }
            finally { LooseAtom.Active.Remove(a.GetComponent<LooseAtom>()); LooseAtom.Active.Remove(b.GetComponent<LooseAtom>()); Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(root); Object.DestroyImmediate(element); }
        }
        [UnityTest] public IEnumerator OldManualQueueCanStarveWhileImmediateReaderCannot()
        {
            yield return new EnterPlayMode();
            var old=new XRInputButtonReader(); old.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;
            old.QueueManualState(true,1); yield return null;
            // This is what the old -10000 Update did before the UI read.
            old.QueueManualState(true,1); Assert.That(old.ReadIsPerformed(),Is.False);
            var state=new HandPinchState(); var button=new object();
            var reader=new HandPinchReader(state,true,()=>true,()=>1);
            state.Step(0,0,null,null,null); state.Step(1,1,null,null,button);
            Assert.That(reader.ReadIsPerformed(),Is.True);
            yield return null; Assert.That(reader.ReadIsPerformed(),Is.True);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator NearFarPinchProducesRealPointerEventsAndCancelsWithoutClick()
        {
            EditorSceneManager.playModeStartScene=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return ExerciseUI();
            yield return new ExitPlayMode();
        }
        static IEnumerator ExerciseUI()
        {
            var camera=new GameObject("UI test camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.stereoTargetEye=StereoTargetEyeMask.None; camera.pixelRect=new Rect(0,0,640,480);
            var es=new GameObject("EventSystem",typeof(EventSystem),typeof(XRUIInputModule));
            var module=es.GetComponent<XRUIInputModule>(); Assert.That(module,Is.Not.Null,"UI module created"); module.enableMouseInput=false; module.enableTouchInput=false;
            var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(TrackedDeviceGraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>(); Assert.That(canvas,Is.Not.Null,"Canvas created"); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=camera;
            canvas.transform.position=new Vector3(0,0,2); ((RectTransform)canvas.transform).sizeDelta=new Vector2(2,2);
            var buttonObject=new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button));
            Assert.That(buttonObject.transform,Is.InstanceOf<RectTransform>(),"Button RectTransform");
            buttonObject.transform.SetParent(canvas.transform,false); ((RectTransform)buttonObject.transform).sizeDelta=Vector2.one;
            var button=buttonObject.GetComponent<Button>(); Assert.That(button,Is.Not.Null,"Button created"); int down=0,up=0,click=0; PointerEventData pending=null;
            module.pointerDown+=(o,e)=> { down++; pending=e; }; module.pointerUp+=(o,e)=>up++; button.onClick.AddListener(()=>click++);
            var ray=new GameObject("NearFar").AddComponent<NearFarInteractor>(); HandMenuRay.Configure(ray); ray.transform.position=new Vector3(.05f,0,0);
            var state=new HandPinchState(); float strength=0;
            ray.uiPressInput.bypass=new HandPinchReader(state,true,()=>true,()=>strength);
            ray.selectInput.bypass=new HandPinchReader(state,false,()=>true,()=>strength);
            state.Step(0,0,null,null,null);
            Canvas.ForceUpdateCanvases();
            for(int i=0;i<5;i++) { yield return null; ray.transform.position+=Vector3.right*.001f; }
            Assert.That(ray.TryGetCurrentUIRaycastResult(out var hit),Is.True,"Ray must hit real canvas graphics; graphic depth="+buttonObject.GetComponent<Image>().depth+" caster ready="+ray.farInteractionCaster.isInitialized);
            strength=1; state.Step(strength,1,null,null,buttonObject); yield return null; yield return null;
            Assert.That(down,Is.EqualTo(1)); Assert.That(click,Is.Zero);
            strength=0; state.Step(strength,2,null,null,buttonObject); yield return null; yield return null;
            Assert.That(up,Is.EqualTo(1)); Assert.That(click,Is.EqualTo(1));
            strength=1; state.Step(strength,3,null,null,buttonObject); yield return null; yield return null;
            Assert.That(down,Is.EqualTo(2)); pending.eligibleForClick=false;
            state.Cancel(); ray.enableUIInteraction=false; yield return null; yield return null;
            Assert.That(click,Is.EqualTo(1),"Cancelled UI press must never click.");
            // The same reader/interactor must also support far object selection.
            canvasObject.transform.position=Vector3.right*4+Vector3.forward*2;
            ray.enableUIInteraction=true; state.Step(0,4,null,null,null);
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.transform.position=new Vector3(.055f,0,1.5f); sphere.transform.localScale=Vector3.one*.1f;
            sphere.AddComponent<Rigidbody>().useGravity=false;
            var grab=sphere.AddComponent<XRGrabInteractable>(); grab.useDynamicAttach=true; grab.throwOnDetach=false;
            for(int i=0;i<4;i++) yield return null;
            strength=1; state.Step(1,5,null,grab,null); yield return null; yield return null;
            Assert.That(ray.hasSelection,Is.True,"Distance pinch selects an object");
            Assert.That(sphere.transform.position.z,Is.GreaterThan(1),"Far attachment must not pull the object into the hand");
            Assert.That(click,Is.EqualTo(1),"Object pinch cannot activate the menu");
            ray.interactionManager.SelectCancel((IXRSelectInteractor)ray,(IXRSelectInteractable)grab); state.Cancel(); Object.Destroy(sphere);
            Object.Destroy(ray.gameObject); Object.Destroy(canvasObject); Object.Destroy(es); Object.Destroy(camera.gameObject);
            yield return null;
        }
    }
}
