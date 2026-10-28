using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
namespace MoleculeLab
{
    // One normalized pinch and one owner for the entire gesture.
    public sealed class HandPinchState
    {
        public bool Pinched { get; private set; }
        public object Target { get; private set; }
        public bool Menu { get; private set; }
        public bool Down { get; private set; }
        public bool Up { get; private set; }
        bool armed; float began;
        public void Cancel() { armed=false; Pinched=false; Target=null; Menu=false; Down=Up=false; }
        public void Step(float strength,float time,object near,object far,object ui,float press=.8f,float release=.6f,float grace=.15f)
        {
            Down=Up=false;
            if(strength<=release)
            {
                Up=Pinched && Target!=null; Pinched=false; armed=true;
                // Keep release ownership for this frame; clear on the next press.
                return;
            }
            if(!armed) return;
            if(!Pinched && strength>=press)
            {
                // A menu button under the ray wins over a far object (an atom or molecule floating in front of the panel);
                // only something within direct reach of the hand beats the menu.
                Pinched=true; began=time; Target=near??ui??far; Menu=near==null && ui!=null; Down=Target!=null;
            }
            else if(Pinched && Target==null && time-began<=grace && near!=null)
            { Target=near; Menu=false; Down=true; }
        }
    }
    public sealed class HandPinchReader : IXRInputButtonReader
    {
        readonly HandPinchState state; readonly bool menu;
        readonly System.Func<bool> available; readonly System.Func<float> strength;
        public HandPinchReader(HandPinchState state,bool menu,System.Func<bool> available,System.Func<float> strength)
        { this.state=state; this.menu=menu; this.available=available; this.strength=strength; }
        bool Owns=>available() && state.Target!=null && state.Menu==menu;
        public bool ReadIsPerformed()=>Owns && state.Pinched;
        public bool ReadWasPerformedThisFrame()=>Owns && state.Down;
        public bool ReadWasCompletedThisFrame()=>Owns && state.Up;
        public float ReadValue()=>ReadIsPerformed()?strength():0;
        public bool TryReadValue(out float value) { value=ReadValue(); return true; }
    }
    // Name retained to preserve existing scene/prefab references. No separate ray is created.
    [DefaultExecutionOrder(-10000)]
    public sealed class HandMenuRay : MonoBehaviour, IXRTargetFilter
    {
        public const int MenuLayer=5;
        public NearFarInteractor direct;
        public Transform aim;
        [Header("Hand acquisition (metres / normalized pinch strength)")]
        public float acquisitionDistance=.045f, targetSwitchMargin=.01f, pressThreshold=.72f, releaseThreshold=.5f, earlyPinchSeconds=.2f; // forgiving: hand tracking is noisy
        public bool showDiagnostics;
        public readonly HandPinchState Gesture=new HandPinchState();
        readonly List<XRHandSubsystem> systems=new List<XRHandSubsystem>();
        readonly List<IXRInteractable> targets=new List<IXRInteractable>();
        XRHandSubsystem subsystem; bool left,initialized;
        HandAtomCaster caster;
        XRUIInputModule uiModule;
        PointerEventData pressedPointer;
        float strength;
        int downs,ups,clicks;
        public bool TrackingAvailable => subsystem!=null && subsystem.running && (left?subsystem.leftHand:subsystem.rightHand).isTracked;
        public static bool IsSafeRelease(IXRSelectInteractor interactor)
        {
            var hand=(interactor as Component)?.GetComponent<HandMenuRay>();
            return hand==null || hand.TrackingAvailable;
        }
        public static void Configure(NearFarInteractor interactor)
        {
            interactor.enableNearCasting=true; interactor.enableFarCasting=true; interactor.enableUIInteraction=true;
            interactor.blockUIOnInteractableSelection=true;
            interactor.farAttachMode=InteractorFarAttachMode.Far;
            interactor.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.StateChange;
        }
        void BindTracking()
        {
            if(subsystem!=null && subsystem.running) return;
            if(subsystem!=null) subsystem.trackingLost-=Lost;
            SubsystemManager.GetSubsystems(systems); subsystem=systems.FirstOrDefault(s=>s.running);
            if(subsystem!=null) subsystem.trackingLost+=Lost;
        }
        void Lost(XRHand hand) { if((hand.handedness==Handedness.Left)==left) Cancel(); }
        void Awake()
        {
            if(direct==null) direct=GetComponent<NearFarInteractor>();
            left=GetComponentsInParent<Transform>(true).Any(t=>t.name=="Left Hand");
            Configure(direct); BindTracking();
            caster=gameObject.AddComponent<HandAtomCaster>(); caster.owner=this; caster.fallback=direct.nearInteractionCaster;
            caster.castOrigin=caster.fallback.castOrigin; direct.nearInteractionCaster=caster;
            direct.targetFilter=this;
            direct.selectInput.bypass=new HandPinchReader(Gesture,false,()=>TrackingAvailable,()=>strength); direct.uiPressInput.bypass=new HandPinchReader(Gesture,true,()=>TrackingAvailable,()=>strength);
            direct.selectExited.AddListener(OnSelectExit);
            initialized=true;
        }
        void Start()
        {
            uiModule=FindAnyObjectByType<XRUIInputModule>();
            if(uiModule!=null) { uiModule.pointerDown+=PointerDown; uiModule.pointerUp+=PointerUp; uiModule.pointerClick+=PointerClick; }
        }
        bool Mine(PointerEventData data)=>uiModule!=null && uiModule.GetInteractor(data.pointerId)==direct;
        void PointerDown(GameObject target,PointerEventData data) { if(Mine(data)) { pressedPointer=data; downs++; } }
        void PointerUp(GameObject target,PointerEventData data) { if(Mine(data)) { ups++; pressedPointer=null; } }
        void PointerClick(GameObject target,PointerEventData data) { if(Mine(data)) clicks++; }
        void OnSelectExit(SelectExitEventArgs args) { if(args.isCanceled) Cancel(); }
        public void Cancel()
        {
            if(pressedPointer!=null) pressedPointer.eligibleForClick=false;
            Gesture.Cancel();
            if(direct==null) return;
            direct.enableUIInteraction=false;
            if(direct.interactionManager!=null)
                foreach(var selected in direct.interactablesSelected.ToArray()) direct.interactionManager.SelectCancel(direct,selected);
        }
        void OnEnable() { Gesture.Cancel(); }
        void Update()
        {
            BindTracking();
            if(!TrackingAvailable) { Cancel(); return; }
            var reader=direct.selectInput.bypass; direct.selectInput.bypass=null;
            strength=Mathf.Clamp01(direct.selectInput.ReadValue()); direct.selectInput.bypass=reader;
            var near=caster.FindAtom();
            targets.Clear(); direct.GetValidTargets(targets);
            object target=targets.FirstOrDefault();
            object ui=null;
            if(direct.TryGetCurrentUIRaycastResult(out var hit)) ui=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if(Gesture.Pinched && Gesture.Menu && near!=null) Cancel();
            Gesture.Step(strength,Time.unscaledTime,near!=null?near.Grab:null,target,ui,pressThreshold,releaseThreshold,earlyPinchSeconds);
            direct.enableUIInteraction=!direct.hasSelection && near==null && (!Gesture.Pinched || Gesture.Menu);
        }
        public bool canProcess=>isActiveAndEnabled;
        public void Link(IXRInteractor interactor) { }
        public void Unlink(IXRInteractor interactor) { }
        public void Process(IXRInteractor interactor,List<IXRInteractable> input,List<IXRInteractable> results)
        {
            results.Clear();
            if(!TrackingAvailable) return;
            foreach(var target in input)
                if(!Gesture.Pinched || (!Gesture.Menu && ReferenceEquals(Gesture.Target,target))) results.Add(target);
        }
        void OnDisable() { if(initialized) Cancel(); }
        void OnDestroy()
        {
            if(subsystem!=null) subsystem.trackingLost-=Lost;
            if(uiModule!=null) { uiModule.pointerDown-=PointerDown; uiModule.pointerUp-=PointerUp; uiModule.pointerClick-=PointerClick; }
        }
        void OnGUI()
        {
            if(!showDiagnostics || !(Application.isEditor || Debug.isDebugBuild)) return;
            GUI.Box(new Rect(10,left?10:110,540,95),$"{(left?"LEFT":"RIGHT")} tracked={TrackingAvailable} strength={strength:F2}\nTarget={Gesture.Target} menu={Gesture.Menu} held={Gesture.Pinched}\nUI down/up/click: {downs}/{ups}/{clicks}");
        }
    }
}
