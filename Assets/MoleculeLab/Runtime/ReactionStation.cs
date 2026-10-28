using System.Collections;
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
        public const float VesselWidth=.5f, VesselDepth=.4f, VesselHeight=.3f, ToolReach=.24f, TableHeight=.82f;
        /// <summary>The docking point (the lab's "Reaction tray"): centre of the vessel floor.</summary>
        public Transform Anchor { get; private set; }
        public TextMeshPro Label { get; private set; }
        public IReadOnlyList<ConditionTool> Tools => tools;
        readonly List<ConditionTool> tools=new List<ConditionTool>();
        LabController lab;
        Transform tray;
        string flash; float flashUntil, nextRefresh;
        static Material glass;

        /// <summary>The vessel's floor: drops and powder land here.</summary>
        public Transform Floor { get; private set; }
        ParticleSystem flashBurst, fire, steam, sparkles, bubbles; Transform liquid; Material liquidMaterial; AudioSource sound; Color glassColour;
        /// <summary>The last effect played (for tests): "fire", "sparkles" or "bubbles".</summary>
        public string LastEffect { get; private set; }
        /// <summary>Centre of the vessel's opening, where a tool must be held.</summary>
        public Vector3 Mouth => tray.position+Vector3.up*(VesselHeight+.02f);

        /// <summary>A reaction table standing at <paramref name="floorPosition"/> (parent space), its front (tools) facing the room.</summary>
        public static ReactionStation Create(LabController lab,Transform parent,Vector3 floorPosition,float yaw,GameObject tableModel,ClassroomKit kit)
        {
            var root=new GameObject("Reaction corner (runtime)").transform; root.SetParent(parent,false); root.localPosition=floorPosition; root.localRotation=Quaternion.Euler(0,yaw,0);
            var size=new Vector3(1.1f,TableHeight,.68f);
            var table=tableModel!=null?BeakerShelf.FitModel(tableModel,root,"Reaction table",Vector3.zero,size):null;
            if(table==null)
            {
                LabVisuals.Shape("Reaction table top",PrimitiveType.Cube,root,new Vector3(0,TableHeight-.02f,0),new Vector3(size.x,.04f,size.z),new Color(.62f,.42f,.24f),true);
                LabVisuals.Shape("Reaction table base",PrimitiveType.Cube,root,new Vector3(0,(TableHeight-.04f)*.5f,0),new Vector3(size.x*.95f,TableHeight-.04f,size.z*.9f),new Color(.86f,.84f,.79f),true);
            }
            var go=new GameObject("Reaction station (runtime)"); go.transform.SetParent(root,false); go.transform.localPosition=new Vector3(0,TableHeight,.04f);
            // A teal base plate under the vessel (solid, so products land on it), and the docking anchor at its centre.
            LabVisuals.Shape("Vessel base",PrimitiveType.Cube,go.transform,new Vector3(0,.0075f,0),new Vector3(VesselWidth+.04f,.015f,VesselDepth+.04f),new Color(.2f,.42f,.44f),true);
            var anchor=new GameObject("Reaction tray").transform; anchor.SetParent(go.transform,false); anchor.localPosition=new Vector3(0,.015f,0);
            var station=go.AddComponent<ReactionStation>(); station.lab=lab; station.tray=anchor; station.Anchor=anchor; station.Build(root,kit); return station;
        }
        /// <summary>Where product number <paramref name="index"/> of <paramref name="count"/> appears: inside the vessel, spread along it.</summary>
        /// <summary>Where product number index appears: up to three in a row, further rows a little higher and further back.</summary>
        public Vector3 ProductSpot(int index,int count)
        {
            var row=index/3; var inRow=Mathf.Min(3,count-row*3); var column=index%3;
            return transform.TransformPoint(new Vector3((column-(inRow-1)*.5f)*.15f,.2f+row*.1f,row*.09f-(count>3?.05f:0)));
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
            Floor=new GameObject("Vessel floor").transform; Floor.SetParent(transform,false); Floor.localPosition=new Vector3(0,bottom+.004f,0);
            BuildReactionEffects(bottom);
            var caption=LabVisuals.Label(transform,"REACTION VESSEL",new Vector3(0,.017f,VesselDepth*.5f+.05f),.025f);
            var cf=caption.GetComponent<FaceViewer>(); if(cf!=null) { cf.enabled=false; Destroy(cf); }
            // Flat on the base plate in front of the vessel, readable from where the player stands.
            caption.rectTransform.sizeDelta=new Vector2(.5f,.05f); caption.color=Color.white;
            caption.transform.localPosition=new Vector3(0,.017f,-(VesselDepth*.5f+.005f)); caption.transform.localRotation=Quaternion.Euler(90,0,0);
            Label=LabVisuals.Label(transform,"",new Vector3(0,VesselHeight+.14f,0),.03f);
            Label.rectTransform.sizeDelta=new Vector2(.7f,.3f); Label.color=Color.white; Label.outlineWidth=.2f; Label.outlineColor=new Color32(10,20,30,255);
            // Tools on the bench behind the vessel, each with a flat name plate.
            var tools=new[]{(ReactionCondition.Ignition,"FLAME",kit!=null?kit.toolFlame:null,new Color(.95f,.55f,.15f)),
                            (ReactionCondition.PalladiumCatalyst,"Pd CATALYST",kit!=null?kit.toolCatalyst:null,new Color(.55f,.58f,.62f)),
                            (ReactionCondition.AcidCatalyst,"ACID",kit!=null?kit.toolAcid:null,new Color(.95f,.9f,.35f))};
            // In a row along the front of the vessel, nearest the player.
            var spacing=.2f; var row=-(VesselDepth*.5f+.1f);
            for(var i=0;i<tools.Length;i++)
            {
                var home=transform.TransformPoint(new Vector3((i-1)*spacing,.0f,row));
                var tool=ConditionTool.Create(lab,this,tools[i].Item1,tools[i].Item2,tools[i].Item3,tools[i].Item4,home,transform.rotation);
                this.tools.Add(tool);
            }
        }
        void BuildReactionEffects(float bottom)
        {
            glassColour=glass.color;
            sound=gameObject.AddComponent<AudioSource>(); sound.spatialBlend=1; sound.minDistance=.5f; sound.volume=.7f; sound.playOnAwake=false;
            var floor=new Vector3(0,bottom+.01f,0);
            flashBurst=ReactionEffects.Create("Flash",transform,floor+Vector3.up*.08f,.18f,.25f,.09f,ReactionEffects.Fade(new Color(1f,.97f,.75f),new Color(1f,.7f,.2f)),24);
            var flashShape=flashBurst.shape; flashShape.shapeType=ParticleSystemShapeType.Sphere; flashShape.radius=.05f;
            fire=ReactionEffects.Create("Fire",transform,floor,.8f,.32f,.07f,ReactionEffects.Fade(new Color(1f,.8f,.25f),new Color(.85f,.15f,.03f)),120,-.15f);
            var fireShape=fire.shape; fireShape.angle=32; fireShape.radius=.08f;
            steam=ReactionEffects.Create("Steam",transform,floor+Vector3.up*.05f,1.8f,.1f,.05f,ReactionEffects.Fade(new Color(.95f,.97f,1f),Color.white,.35f),60,-.05f);
            var steamSize=steam.sizeOverLifetime; steamSize.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.5f,1,2.2f));
            var steamShape=steam.shape; steamShape.angle=18; steamShape.radius=.1f;
            sparkles=ReactionEffects.Create("Sparkles",transform,floor,.45f,.06f,.014f,ReactionEffects.Fade(Color.white,new Color(.5f,.95f,1f)),120);
            var sparkleShape=sparkles.shape; sparkleShape.shapeType=ParticleSystemShapeType.Box; sparkleShape.scale=new Vector3(VesselWidth*.7f,VesselDepth*.7f,.01f);
            bubbles=ReactionEffects.Create("Bubbles",transform,floor,1f,.16f,.016f,ReactionEffects.Fade(new Color(.8f,.95f,1f),new Color(.9f,1f,1f),.75f),120);
            var bubbleShape=bubbles.shape; bubbleShape.shapeType=ParticleSystemShapeType.Box; bubbleShape.scale=new Vector3(VesselWidth*.7f,VesselDepth*.7f,.01f);
            // A shallow liquid layer for the acid reaction, invisible until it is used.
            liquid=GameObject.CreatePrimitive(PrimitiveType.Cube).transform; liquid.name="Vessel liquid"; Destroy(liquid.GetComponent<Collider>());
            liquid.SetParent(transform,false); liquid.localPosition=new Vector3(0,bottom+.025f,0); liquid.localScale=new Vector3(VesselWidth-.012f,.04f,VesselDepth-.012f);
            liquidMaterial=new Material(Canvas.GetDefaultCanvasMaterial()){name="Vessel liquid"};
            var lr=liquid.GetComponent<Renderer>(); lr.sharedMaterial=liquidMaterial; lr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; liquid.gameObject.SetActive(false);
        }
        /// <summary>The visible part of a reaction, matched to its condition. Runs while the molecules rearrange (about 1.7 s).</summary>
        public void PlayReaction(ReactionCondition condition) { StopAllCoroutines(); StartCoroutine(RunReaction(condition)); }
        IEnumerator RunReaction(ReactionCondition condition)
        {
            switch(condition)
            {
                case ReactionCondition.Ignition:
                    LastEffect="fire"; sound.PlayOneShot(ReactionEffects.Whoosh);
                    flashBurst.Emit(16); fire.Emit(70);
                    yield return new WaitForSeconds(.25f); fire.Emit(40);
                    yield return new WaitForSeconds(.45f);
                    ReactionEffects.Rate(steam,22); yield return new WaitForSeconds(1.6f); ReactionEffects.Rate(steam,0);
                    break;
                case ReactionCondition.PalladiumCatalyst:
                    LastEffect="sparkles"; sound.PlayOneShot(ReactionEffects.Fizz);
                    ReactionEffects.Rate(sparkles,80); glass.color=new Color(.75f,1f,.85f,.22f);
                    yield return new WaitForSeconds(1.5f);
                    ReactionEffects.Rate(sparkles,0); glass.color=glassColour;
                    break;
                default:
                    // No tool (a halogen adding to C=C): brown bromine colour fading to clear, a few bubbles. Acid: yellow to clear blue.
                    var noTool=condition==ReactionCondition.None;
                    LastEffect=noTool?"decolourise":"bubbles"; sound.PlayOneShot(ReactionEffects.Bubbling);
                    liquid.gameObject.SetActive(true); ReactionEffects.Rate(bubbles,noTool?12:45);
                    var from=noTool?new Color(.72f,.33f,.08f):new Color(.95f,.92f,.4f); var to=noTool?new Color(.92f,.96f,1f):new Color(.55f,.8f,1f);
                    for(var t=0f;t<1.7f;t+=Time.deltaTime)
                    {
                        var u=t/1.7f; var c=Color.Lerp(from,to,u); c.a=.4f*Mathf.Sin(Mathf.PI*Mathf.Min(1,u*1.15f))+.05f;
                        liquidMaterial.color=c; yield return null;
                    }
                    ReactionEffects.Rate(bubbles,0); liquid.gameObject.SetActive(false);
                    break;
            }
        }
        /// <summary>The products have appeared: the glass glows briefly and chimes.</summary>
        public void Glow() { StartCoroutine(RunGlow()); }
        IEnumerator RunGlow()
        {
            sound.PlayOneShot(ReactionEffects.Chime,.8f);
            for(var t=0f;t<.9f;t+=Time.deltaTime)
            {
                var k=Mathf.Sin(Mathf.PI*t/.9f); glass.color=Color.Lerp(glassColour,new Color(.85f,1f,1f,.45f),k); yield return null;
            }
            glass.color=glassColour;
        }
        void OnDisable() { if(glass!=null && glassColour.a>0) glass.color=glassColour; }
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
        ParticleSystem effect; AudioSource sound; float nextDrip;
        /// <summary>Editor previews only: behave as if held over the vessel (effect on, stays where placed).</summary>
        [System.NonSerialized] public bool previewHeld;
        /// <summary>The tool's own effect: flame (lamp), drops (acid) or powder (catalyst).</summary>
        public ParticleSystem Effect => effect;

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
            BuildEffect(height);
            var plate=LabVisuals.Label(station.transform,name,station.transform.InverseTransformPoint(home)+new Vector3(0,.043f,-.062f),.018f);
            var face=plate.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            plate.transform.localRotation=Quaternion.Euler(90,0,0); plate.rectTransform.sizeDelta=new Vector2(.17f,.05f); plate.color=Color.white; plate.fontStyle=FontStyles.Bold;
        }
        void BuildEffect(float height)
        {
            sound=gameObject.AddComponent<AudioSource>(); sound.spatialBlend=1; sound.minDistance=.3f; sound.volume=.5f; sound.playOnAwake=false;
            switch(Condition)
            {
                case ReactionCondition.Ignition: // a flickering flame on the wick: rises in world space whichever way the lamp is held
                    effect=ReactionEffects.Create("Flame",transform,new Vector3(0,.042f+height+.004f,0),.32f,.02f,.026f,
                        ReactionEffects.Fade(new Color(1f,.95f,.6f),new Color(1f,.35f,.05f)),70,-.35f);
                    var fs=effect.shape; fs.angle=4; fs.radius=.003f;
                    var fsize=effect.sizeOverLifetime; fsize.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.6f,1,.05f));
                    break;
                case ReactionCondition.AcidCatalyst: // drops fall from the tip and splash on the vessel floor
                    effect=ReactionEffects.Create("Acid drops",transform,new Vector3(0,.04f,0),1.4f,0,.017f,ReactionEffects.Fade(new Color(.92f,.98f,.55f),new Color(.85f,.95f,.6f),.95f),24,1);
                    var ds=effect.shape; ds.angle=0; ds.radius=.001f;
                    var splash=ReactionEffects.Create("Splash",effect.transform,Vector3.zero,.35f,.22f,.006f,ReactionEffects.Fade(new Color(.92f,.98f,.6f),new Color(.8f,.9f,.6f),.9f),80,1);
                    var ss=splash.shape; ss.shapeType=ParticleSystemShapeType.Hemisphere; ss.radius=.01f;
                    var se=splash.emission; se.SetBursts(new[]{new ParticleSystem.Burst(0,8)}); var sm=splash.main; sm.loop=false; splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    LandOnVesselFloor(effect,splash);
                    break;
                case ReactionCondition.PalladiumCatalyst: // a sprinkle of grey powder
                    effect=ReactionEffects.Create("Catalyst powder",transform,new Vector3(0,.042f+height*.8f,0),1.3f,.03f,.01f,ReactionEffects.Fade(new Color(.42f,.44f,.48f),new Color(.3f,.32f,.35f),1f),120,.25f);
                    var ps=effect.shape; ps.angle=28; ps.radius=.02f;
                    LandOnVesselFloor(effect,null);
                    break;
            }
        }
        void LandOnVesselFloor(ParticleSystem ps,ParticleSystem onLanding)
        {
            var collision=ps.collision; collision.enabled=true; collision.type=ParticleSystemCollisionType.Planes; collision.SetPlane(0,station.Floor);
            collision.bounce=0; collision.lifetimeLoss=1; collision.radiusScale=.5f;
            if(onLanding!=null) { var sub=ps.subEmitters; sub.enabled=true; sub.AddSubEmitter(onLanding,ParticleSystemSubEmitterType.Collision,ParticleSystemSubEmitterProperties.InheritNothing); }
        }
        void UpdateEffect(bool held,bool near)
        {
            if(effect==null) return;
            switch(Condition)
            {
                case ReactionCondition.Ignition:
                    // Lit while in the hand; flares up over the vessel.
                    ReactionEffects.Rate(effect,held?(near?95:45):0);
                    var main=effect.main; main.startSize=near?.042f:.026f;
                    break;
                case ReactionCondition.AcidCatalyst:
                    ReactionEffects.Rate(effect,held && near?3.5f:0);
                    if(held && near && Time.time>=nextDrip) { nextDrip=Time.time+.29f; sound.pitch=.9f+Random.value*.3f; sound.PlayOneShot(ReactionEffects.Drip,.35f); }
                    break;
                case ReactionCondition.PalladiumCatalyst:
                    ReactionEffects.Rate(effect,held && near?60:0);
                    break;
            }
        }
        public bool NearVessel => station!=null && Vector3.Distance(transform.position,station.Mouth)<ReactionStation.ToolReach;
        void Update()
        {
            if(Grab==null) return;
            if(previewHeld) { UpdateEffect(true,true); return; }
            UpdateEffect(Grab.isSelected,Grab.isSelected && NearVessel);
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
