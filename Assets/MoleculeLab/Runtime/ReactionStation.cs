using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoleculeLab
{
    /// <summary>
    /// The physical reaction station that replaces the REACT menu: a glass vessel over the reaction tray, a floating label
    /// that always says what is in it and what is missing, and three tools beside it (flame, palladium catalyst, acid).
    /// Drop finished molecules in; the lab picks the matching reaction. Hold the right tool over the vessel to run it.
    /// </summary>
    public sealed class ReactionStation : MonoBehaviour
    {
        public const float VesselWidth=.5f, VesselDepth=.46f, VesselHeight=.3f, ToolReach=.24f;
        public TextMeshPro Label { get; private set; }
        public IReadOnlyList<ConditionTool> Tools => tools;
        readonly List<ConditionTool> tools=new List<ConditionTool>();
        LabController lab;
        Transform tray;
        string flash; float flashUntil, nextRefresh;
        static Material glass;

        /// <summary>Centre of the vessel's opening, where a tool must be held.</summary>
        public Vector3 Mouth => tray.position+Vector3.up*(VesselHeight+.02f);

        public static ReactionStation Create(LabController lab,Transform bench,Transform tray,ClassroomKit kit)
        {
            var go=new GameObject("Reaction station (runtime)"); go.transform.SetParent(bench,false);
            go.transform.position=tray.position; go.transform.rotation=bench.rotation;
            var station=go.AddComponent<ReactionStation>(); station.lab=lab; station.tray=tray; station.Build(bench,kit); return station;
        }
        void Build(Transform bench,ClassroomKit kit)
        {
            // Glass vessel: four see-through walls with a bright rim, open at the top.
            if(glass==null) { glass=new Material(Canvas.GetDefaultCanvasMaterial()); glass.name="Vessel glass"; glass.color=new Color(.78f,.93f,1f,.13f); }
            void Wall(Vector3 p,Vector3 s)
            {
                var w=GameObject.CreatePrimitive(PrimitiveType.Cube); w.name="Vessel wall"; Destroy(w.GetComponent<Collider>());
                w.transform.SetParent(transform,false); w.transform.localPosition=p; w.transform.localScale=s;
                var r=w.GetComponent<Renderer>(); r.sharedMaterial=glass; r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var h=VesselHeight; var bottom=.015f;
            Wall(new Vector3(0,bottom+h*.5f,-VesselDepth*.5f),new Vector3(VesselWidth,h,.006f));
            Wall(new Vector3(0,bottom+h*.5f,VesselDepth*.5f),new Vector3(VesselWidth,h,.006f));
            Wall(new Vector3(-VesselWidth*.5f,bottom+h*.5f,0),new Vector3(.006f,h,VesselDepth));
            Wall(new Vector3(VesselWidth*.5f,bottom+h*.5f,0),new Vector3(.006f,h,VesselDepth));
            foreach(var y in new[]{bottom+.002f,bottom+h})
            {
                var rim=new GameObject("Vessel rim").AddComponent<LineRenderer>(); rim.transform.SetParent(transform,false);
                rim.useWorldSpace=false; rim.loop=true; rim.positionCount=4; rim.widthMultiplier=y>bottom+.01f?.008f:.005f;
                rim.sharedMaterial=LabVisuals.Material(new Color(.8f,.92f,.98f));
                rim.SetPositions(new[]{new Vector3(-VesselWidth*.5f,y,-VesselDepth*.5f),new Vector3(-VesselWidth*.5f,y,VesselDepth*.5f),new Vector3(VesselWidth*.5f,y,VesselDepth*.5f),new Vector3(VesselWidth*.5f,y,-VesselDepth*.5f)});
            }
            // The prefab's flat "REACTION TRAY" caption becomes the vessel's name.
            foreach(var t in bench.GetComponentsInChildren<TMP_Text>(true)) if(t.text.Trim()=="REACTION TRAY") t.text="REACTION VESSEL";
            Label=LabVisuals.Label(transform,"",new Vector3(0,VesselHeight+.14f,0),.03f);
            Label.rectTransform.sizeDelta=new Vector2(.7f,.3f); Label.color=Color.white; Label.outlineWidth=.2f; Label.outlineColor=new Color32(10,20,30,255);
            // Tools on the bench behind the vessel, each with a flat name plate.
            var tools=new[]{(ReactionCondition.Ignition,"FLAME",kit!=null?kit.toolFlame:null,new Color(.95f,.55f,.15f)),
                            (ReactionCondition.PalladiumCatalyst,"Pd CATALYST",kit!=null?kit.toolCatalyst:null,new Color(.55f,.58f,.62f)),
                            (ReactionCondition.AcidCatalyst,"ACID",kit!=null?kit.toolAcid:null,new Color(.95f,.9f,.35f))};
            // In a row along the front of the vessel, nearest the player.
            var spacing=.16f; var row=-(VesselDepth*.5f+.07f);
            for(var i=0;i<tools.Length;i++)
            {
                var home=transform.TransformPoint(new Vector3((i-1)*spacing,.0f,row));
                var tool=ConditionTool.Create(lab,this,tools[i].Item1,tools[i].Item2,tools[i].Item3,tools[i].Item4,home,transform.rotation);
                this.tools.Add(tool);
            }
        }
        public void Flash(string message) { flash=message; flashUntil=Time.time+4; Refresh(); }
        void Update() { if(Time.time>=nextRefresh) Refresh(); }
        public void Refresh()
        {
            nextRefresh=Time.time+.25f; if(Label==null || lab==null) return;
            var status=lab.ReactionStatus();
            Label.text=Time.time<flashUntil && !string.IsNullOrEmpty(flash)?"<color=#FFD27A>"+flash+"</color>\n<size=80%>"+status+"</size>":status;
        }
    }

    /// <summary>A grabbable condition tool. Held over the vessel it supplies its condition once; let go, it goes back to its spot.</summary>
    public sealed class ConditionTool : MonoBehaviour
    {
        public ReactionCondition Condition { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        LabController lab; ReactionStation station;
        Vector3 homeLocal; Quaternion homeRotationLocal; bool applied; // relative to the station, so they follow the height-adjustable bench
        GameObject glow;

        public static ConditionTool Create(LabController lab,ReactionStation station,ReactionCondition condition,string name,GameObject model,Color colour,Vector3 home,Quaternion rotation)
        {
            var go=new GameObject("Tool: "+name); go.transform.SetPositionAndRotation(home,rotation);
            go.transform.SetParent(station.transform,true);
            var tool=go.AddComponent<ConditionTool>(); tool.lab=lab; tool.station=station; tool.Condition=condition;
            tool.homeLocal=station.transform.InverseTransformPoint(home); tool.homeRotationLocal=Quaternion.Inverse(station.transform.rotation)*rotation;
            tool.Build(name,model,colour,home); return tool;
        }
        void Build(string name,GameObject model,Color colour,Vector3 home)
        {
            const float height=.15f;
            Bounds bounds=default; var have=false;
            if(model!=null)
            {
                var m=Instantiate(model,transform,false); m.name="Model";
                foreach(var c in m.GetComponentsInChildren<Collider>(true)) { c.enabled=false; Destroy(c); }
                foreach(var b in m.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
                var renderers=m.GetComponentsInChildren<Renderer>();
                if(renderers.Length>0)
                {
                    bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                    // Stand long models (the dropper) upright.
                    if(bounds.size.x>bounds.size.y*1.5f && bounds.size.x>=bounds.size.z) m.transform.Rotate(0,0,90,Space.World);
                    else if(bounds.size.z>bounds.size.y*1.5f) m.transform.Rotate(90,0,0,Space.World);
                    bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                    // Largest side to the tool size (the dropper model lies on its side; this keeps it hand-sized).
                    var scale=height/Mathf.Max(1e-4f,Mathf.Max(bounds.size.y,Mathf.Max(bounds.size.x,bounds.size.z))); m.transform.localScale*=scale;
                    bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                    m.transform.position+=new Vector3(transform.position.x-bounds.center.x,transform.position.y+.042f-bounds.min.y,transform.position.z-bounds.center.z);
                    have=true;
                }
                else Destroy(m);
            }
            if(!have) // simple stand-in: a coloured bottle
            {
                LabVisuals.Shape("Body",PrimitiveType.Cylinder,transform,new Vector3(0,.042f+.05f,0),new Vector3(.06f,.05f,.06f),colour,false);
                LabVisuals.Shape("Neck",PrimitiveType.Cylinder,transform,new Vector3(0,.042f+.12f,0),new Vector3(.025f,.02f,.025f),colour,false);
            }
            var box=gameObject.AddComponent<BoxCollider>(); box.center=new Vector3(0,.042f+height*.5f,0); box.size=new Vector3(.1f,height,.1f);
            var body=gameObject.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            Grab=gameObject.AddComponent<XRGrabInteractable>(); Grab.movementType=XRBaseInteractable.MovementType.Instantaneous; Grab.throwOnDetach=false; Grab.useDynamicAttach=true;
            Grab.selectExited.AddListener(_=>applied=false);
            glow=LabVisuals.Shape("Ready glow",PrimitiveType.Sphere,transform,new Vector3(0,.042f+height+.02f,0),Vector3.one*.035f,colour,false); glow.SetActive(false);
            var plate=LabVisuals.Label(station.transform,name,station.transform.InverseTransformPoint(home)+new Vector3(0,.043f,-.062f),.018f);
            var face=plate.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            plate.transform.localRotation=Quaternion.Euler(90,0,0); plate.rectTransform.sizeDelta=new Vector2(.17f,.05f); plate.color=Color.white; plate.fontStyle=FontStyles.Bold;
        }
        public bool NearVessel => station!=null && Vector3.Distance(transform.position,station.Mouth)<ReactionStation.ToolReach;
        void Update()
        {
            if(Grab==null) return;
            if(Grab.isSelected)
            {
                var near=NearVessel; if(glow!=null && glow.activeSelf!=near) glow.SetActive(near);
                if(near && !applied) { applied=true; lab.ApplyCondition(Condition); }
                if(!near) applied=false;
                return;
            }
            if(glow!=null && glow.activeSelf) glow.SetActive(false);
            var target=station.transform.TransformPoint(homeLocal); var turn=station.transform.rotation*homeRotationLocal;
            transform.position=Vector3.Lerp(transform.position,target,1-Mathf.Exp(-Time.deltaTime*8));
            transform.rotation=Quaternion.Slerp(transform.rotation,turn,1-Mathf.Exp(-Time.deltaTime*8));
        }
        /// <summary>For tests and the menu: as if the tool were held over the vessel.</summary>
        public void UseOnVessel() { lab.ApplyCondition(Condition); }
    }
}
