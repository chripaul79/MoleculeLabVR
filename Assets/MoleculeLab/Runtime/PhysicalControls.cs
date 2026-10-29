using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MoleculeLab
{
    /// <summary>
    /// A red push button on the bench that resets the whole lab (only the gallery is kept).
    /// It is pressed with a fingertip or controller pushed down onto the cap; pinching or gripping it up close also works.
    /// </summary>
    public sealed class ResetButton : MonoBehaviour
    {
        public const float Travel=.018f, Radius=.055f, HoldSeconds=.15f;
        LabController lab;
        Transform cap;
        float depth, heldFor, nextScan;
        bool armed=true;
        readonly List<Transform> controllers=new List<Transform>();
        readonly List<XRHandSubsystem> handSystems=new List<XRHandSubsystem>();
        XROrigin origin;
        public int Presses { get; private set; }
        float pressedUntil;
        /// <summary>Cap top when not pressed, in world space.</summary>
        public Vector3 CapTop => transform.TransformPoint(new Vector3(0,.06f,0));

        System.Action onPress;
        string text="RESET LAB\npush or point";
        public static ResetButton Create(Transform bench,LabController lab,Vector3 localPosition) => Create(bench,lab,localPosition,null,null,"Reset button (runtime)");
        /// <summary>The same push button with its own label and action (for example emptying the reaction vessel).</summary>
        public static ResetButton Create(Transform parent,LabController lab,Vector3 localPosition,string text,System.Action onPress,string name)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;
            var button=go.AddComponent<ResetButton>(); button.lab=lab; button.onPress=onPress; if(text!=null) button.text=text; button.Build(); return button;
        }
        void Build()
        {
            LabVisuals.Shape("Button housing",PrimitiveType.Cylinder,transform,new Vector3(0,.02f,0),new Vector3(.15f,.02f,.15f),new Color(.12f,.13f,.15f),true);
            LabVisuals.Shape("Warning ring",PrimitiveType.Cylinder,transform,new Vector3(0,.041f,0),new Vector3(.125f,.001f,.125f),new Color(.95f,.8f,.1f),false);
            cap=LabVisuals.Shape("Red cap",PrimitiveType.Cylinder,transform,new Vector3(0,.05f,0),new Vector3(.095f,.01f,.095f),new Color(.85f,.08f,.06f),true).transform;
            var interactable=cap.gameObject.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(args=>
            {
                // Pressed up close or with a ray from a distance: either way it resets, and the cap visibly goes down.
                pressedUntil=Time.time+.35f; Fire();
            });
            var label=LabVisuals.Label(transform,text,new Vector3(0,.13f,.07f),.02f);
            var face=label.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            label.color=Color.white;
        }
        IEnumerable<Vector3> Probes()
        {
            if(Time.unscaledTime>=nextScan)
            {
                nextScan=Time.unscaledTime+2;
                controllers.Clear(); controllers.AddRange(FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None).Select(i=>i.transform));
                origin=FindAnyObjectByType<XROrigin>();
                SubsystemManager.GetSubsystems(handSystems);
            }
            foreach(var t in controllers) if(t!=null && t.gameObject.activeInHierarchy) yield return t.position;
            var space=origin!=null && origin.CameraFloorOffsetObject!=null?origin.CameraFloorOffsetObject.transform:null;
            foreach(var system in handSystems)
            {
                if(system==null || !system.running) continue;
                foreach(var hand in new[]{system.leftHand,system.rightHand})
                {
                    if(!hand.isTracked) continue;
                    foreach(var id in new[]{XRHandJointID.IndexTip,XRHandJointID.MiddleTip,XRHandJointID.ThumbTip})
                        if(hand.GetJoint(id).TryGetPose(out var pose)) yield return space!=null?space.TransformPoint(pose.position):pose.position;
                }
            }
        }
        /// <summary>How far a point would push the cap down (0 when outside the button).</summary>
        public float PushDepth(Vector3 world)
        {
            var local=transform.InverseTransformPoint(world);
            if(new Vector2(local.x,local.z).magnitude>Radius) return 0;
            var below=.06f-local.y;
            return below<-.005f || below>.09f?0:Mathf.Clamp(below+.005f,0,Travel);
        }
        public void Fire()
        {
            if(!armed || lab==null) return;
            armed=false; Presses++; heldFor=0;
            if(onPress!=null) onPress(); else lab.ResetFromButton();
        }
        void Update()
        {
            var target=0f;
            foreach(var p in Probes()) target=Mathf.Max(target,PushDepth(p));
            if(Time.time<pressedUntil) target=Travel; // pressed with a ray
            depth=Mathf.MoveTowards(depth,target,Time.deltaTime*.4f);
            cap.localPosition=new Vector3(0,.05f-depth,0);
            if(depth>=Travel*.8f) { heldFor+=Time.deltaTime; if(heldFor>=HoldSeconds) Fire(); }
            else { heldFor=0; if(depth<Travel*.25f) armed=true; }
        }
    }

    /// <summary>A floor bin: finished molecules dropped in are either added to the gallery or discarded.</summary>
    public sealed class MoleculeBin : MonoBehaviour
    {
        public bool IsGallery { get; private set; }
        public string LastFeedback => feedback!=null?feedback.text:"";
        LabController lab;
        TextMeshPro feedback;
        float clearAt;

        public static MoleculeBin Create(Transform parent,LabController lab,bool gallery,Vector3 localPosition)
        {
            var go=new GameObject(gallery?"Gallery bin (runtime)":"Trash bin (runtime)"); go.transform.SetParent(parent,false); go.transform.localPosition=localPosition;
            var bin=go.AddComponent<MoleculeBin>(); bin.lab=lab; bin.IsGallery=gallery; bin.Build(); return bin;
        }
        /// <summary>The gallery drop: a round hole in the workbench top. A finished molecule released over it is kept in the gallery.</summary>
        public static MoleculeBin CreateHole(Transform bench,LabController lab,Vector3 localPosition)
        {
            var go=new GameObject("Gallery hole (runtime)"); go.transform.SetParent(bench,false); go.transform.localPosition=localPosition;
            var bin=go.AddComponent<MoleculeBin>(); bin.lab=lab; bin.IsGallery=true; bin.BuildHole(); return bin;
        }
        public const float HoleRadius=.16f;
        void BuildHole()
        {
            const float top=.041f; // the bench's working surface, in bench space
            LabVisuals.Shape("Hole",PrimitiveType.Cylinder,transform,new Vector3(0,top,0),new Vector3(HoleRadius*2,.0012f,HoleRadius*2),new Color(.015f,.02f,.025f),false);
            LabVisuals.Shape("Hole shadow",PrimitiveType.Cylinder,transform,new Vector3(0,top+.0004f,0),new Vector3(HoleRadius*1.55f,.0012f,HoleRadius*1.55f),Color.black,false);
            void Rim(string n,float radius,float width,Color colour)
            {
                var rim=new GameObject(n).AddComponent<LineRenderer>(); rim.transform.SetParent(transform,false);
                rim.useWorldSpace=false; rim.loop=true; rim.positionCount=48; rim.widthMultiplier=width; rim.sharedMaterial=LabVisuals.Material(colour);
                for(var i=0;i<48;i++) { var a=i*Mathf.PI*2/48; rim.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,top+.002f,Mathf.Sin(a)*radius)); }
            }
            Rim("Rim",HoleRadius,.012f,new Color(.78f,.8f,.82f)); Rim("Glow ring",HoleRadius+.02f,.006f,new Color(.3f,.85f,.75f));
            // A plate on the bench in front of the hole, lying flat and readable from where the player stands.
            var sign=LabVisuals.Label(transform,"GALLERY\n<size=60%>drop here to keep</size>",new Vector3(0,top+.002f,-HoleRadius-.07f),.032f);
            var face=sign.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            sign.transform.localRotation=Quaternion.Euler(90,0,0); sign.rectTransform.sizeDelta=new Vector2(.4f,.1f);
            sign.color=new Color(.95f,.97f,1f); sign.fontStyle=FontStyles.Bold; sign.outlineWidth=.2f; sign.outlineColor=new Color32(20,24,30,255);
            feedback=LabVisuals.Label(transform,"",new Vector3(0,.38f,0),.03f); feedback.color=Color.white;
            var opening=new GameObject("Drop zone"); opening.transform.SetParent(transform,false);
            var trigger=opening.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.center=new Vector3(0,top+.12f,0); trigger.size=new Vector3(HoleRadius*2,.24f,HoleRadius*2);
            opening.AddComponent<BinDropZone>().bin=this;
        }
        void Build()
        {
            var body=IsGallery?new Color(.13f,.42f,.46f):new Color(.24f,.25f,.28f);
            LabVisuals.Shape("Bin body",PrimitiveType.Cylinder,transform,new Vector3(0,.4f,0),new Vector3(.4f,.4f,.4f),body,false);
            LabVisuals.Shape("Bin opening",PrimitiveType.Cylinder,transform,new Vector3(0,.801f,0),new Vector3(.34f,.001f,.34f),new Color(.03f,.04f,.05f),false);
            var rim=new GameObject("Rim").AddComponent<LineRenderer>(); rim.transform.SetParent(transform,false);
            rim.useWorldSpace=false; rim.loop=true; rim.positionCount=40; rim.widthMultiplier=.012f; rim.sharedMaterial=LabVisuals.Material(new Color(.8f,.84f,.86f));
            for(var i=0;i<40;i++) { var a=i*Mathf.PI*2/40; rim.SetPosition(i,new Vector3(Mathf.Cos(a)*.2f,.805f,Mathf.Sin(a)*.2f)); }
            var sign=LabVisuals.Label(transform,IsGallery?"GALLERY\nkeep it":"TRASH\ndiscard it",new Vector3(0,1.02f,0),.04f);
            sign.color=Color.white; sign.fontStyle=FontStyles.Bold;
            feedback=LabVisuals.Label(transform,"",new Vector3(0,1.22f,0),.03f); feedback.color=Color.white;
            var opening=new GameObject("Drop zone"); opening.transform.SetParent(transform,false);
            var trigger=opening.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.center=new Vector3(0,.46f,0); trigger.size=new Vector3(.38f,.9f,.38f);
            opening.AddComponent<BinDropZone>().bin=this;
        }
        public void Flash(string text) { if(feedback==null) return; feedback.text=text; clearAt=Time.time+2.5f; }
        void Update() { if(feedback!=null && feedback.text.Length>0 && Time.time>=clearAt) feedback.text=""; }

        /// <summary>Called for anything inside the bin. Only released objects count, never ones still in a hand.</summary>
        public void Consider(Collider other)
        {
            var body=other.attachedRigidbody; if(body==null || lab==null) return;
            var molecule=body.GetComponent<MoleculeView>();
            if(molecule!=null) { if(molecule.Grab!=null && !molecule.Grab.isSelected && molecule.State.Complete && !molecule.Docked) lab.MoleculeIntoBin(molecule,IsGallery); return; }
            var atom=body.GetComponent<LooseAtom>();
            if(atom!=null && !atom.OnShelf && atom.Grab!=null && !atom.Grab.isSelected) atom.ReturnHome();
        }
    }

    public sealed class BinDropZone : MonoBehaviour
    {
        public MoleculeBin bin;
        void OnTriggerStay(Collider other) { if(bin!=null) bin.Consider(other); }
    }
}
