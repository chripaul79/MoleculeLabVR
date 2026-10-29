using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoleculeLab
{
    /// <summary>
    /// Extends the room behind the player (the old front wall becomes an opening) and shows every completed molecule
    /// on gallery tables. Built at runtime so the editable AcademicLab prefab is untouched.
    /// </summary>
    public sealed class GalleryRoom : MonoBehaviour
    {
        public const int SlotsPerTable=12, MaximumTables=2;
        /// <summary>Where the authored lab ended (its old front wall). The lab now runs on to <see cref="RoomStart"/>, where the gallery begins.</summary>
        public const float LabFront=-1.65f, Lengthen=.9f;
        public const float RoomStart=LabFront-Lengthen, RoomEnd=-4.45f-Lengthen, FirstTableZ=-2.05f-Lengthen, TableSpacing=1.3f, TableTop=.82f;
        static readonly Color Wall=new Color(.79f,.8f,.75f), Floor=new Color(.43f,.48f,.48f), Wood=new Color(.5f,.36f,.22f),
            Metal=new Color(.48f,.55f,.56f), Dark=new Color(.09f,.13f,.15f), Teal=new Color(.21f,.37f,.38f), Light=new Color(.95f,.97f,.86f);
        LabController lab;
        TextMeshPro counter;
        readonly List<Transform> tables=new List<Transform>();
        readonly Dictionary<string,GalleryExhibit> exhibits=new Dictionary<string,GalleryExhibit>();
        public int ExhibitCount => exhibits.Count;
        public IEnumerable<GalleryExhibit> Exhibits => exhibits.Values;
        public static IEnumerable<Vector3> SlotPositions(int tableCount)
        {
            for(var t=0;t<tableCount;t++) for(var i=0;i<SlotsPerTable;i++)
                yield return new Vector3(-1.25f+(i%6)*.5f,TableTop+.17f,FirstTableZ-t*TableSpacing+(i<6?.2f:-.2f));
        }

        public static GalleryRoom Create(LabEnvironment environment,LabController lab)
        {
            var go=new GameObject("Molecule gallery (runtime)"); go.transform.SetParent(environment.transform,false);
            var room=go.AddComponent<GalleryRoom>(); room.lab=lab; room.Build(environment); return room;
        }
        GameObject Box(string name,Vector3 position,Vector3 size,Color color,bool collider=false) =>
            LabVisuals.Shape(name,PrimitiveType.Cube,transform,position,size,color,collider);
        TextMeshPro Sign(string text,Vector3 position,float size,Color color)
        {
            var label=LabVisuals.Label(transform,text,position,size);
            var face=label.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            label.transform.localRotation=Quaternion.Euler(0,180,0); // readable from inside the gallery, looking away from the bench
            label.color=color; return label;
        }
        void Build(LabEnvironment environment)
        {
            var wall=environment.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Front wall");
            if(wall!=null) wall.gameObject.SetActive(false); else Debug.LogWarning("Gallery: 'Front wall' not found; the gallery may be hidden behind it.");
            var depth=RoomStart-RoomEnd; var centre=(RoomStart+RoomEnd)*.5f;
            // One floor (and teleport area) from the old lab front to the end wall, wide enough for the widened classroom too.
            var wide=6+2*ClassroomBuilder.Widen+.4f;
            Box("Gallery floor",new Vector3(0,-.06f,(LabFront+RoomEnd)*.5f),new Vector3(wide,.12f,LabFront-RoomEnd),Floor,true);
            // The lab is longer than authored: its side walls and ceiling continue from the old front to the gallery opening
            // (the classroom moves these walls out with its own when it widens the room).
            var strip=(LabFront+RoomStart)*.5f;
            foreach(var x in new[]{-3f,3f}) Box("Lab extension wall",new Vector3(x,1.5f,strip),new Vector3(.12f,3,Lengthen),Wall,true);
            Box("Lab extension ceiling",new Vector3(0,3.05f,strip),new Vector3(wide,.12f,Lengthen),Wall,true);
            Box("Gallery ceiling",new Vector3(0,3.05f,centre),new Vector3(6,.12f,depth),Wall,true);
            Box("Gallery left wall",new Vector3(-3,1.5f,centre),new Vector3(.12f,3,depth),Wall,true);
            Box("Gallery right wall",new Vector3(3,1.5f,centre),new Vector3(.12f,3,depth),Wall,true);
            Box("Gallery end wall",new Vector3(0,1.5f,RoomEnd),new Vector3(6,3,.12f),Wall,true);
            Box("Gallery timber rail",new Vector3(0,1.05f,RoomEnd+.08f),new Vector3(6,.065f,.045f),Wood);
            Box("Opening header",new Vector3(0,2.78f,RoomStart),new Vector3(6,.44f,.14f),Wall,true);
            foreach(var x in new[]{-2.82f,2.82f}) Box("Opening pillar",new Vector3(x,1.28f,RoomStart),new Vector3(.36f,2.56f,.14f),Wall,true);
            Box("Opening trim",new Vector3(0,2.55f,RoomStart),new Vector3(5.28f,.04f,.16f),Wood);
            for(var i=0;i<4;i++) Box("Floor joint",new Vector3(-1.5f+i,0,centre),new Vector3(.008f,.002f,depth),Metal);
            Box("Gallery light housing",new Vector3(0,2.93f,centre),new Vector3(2.6f,.07f,1.3f),Metal);
            Box("Gallery light",new Vector3(0,2.885f,centre),new Vector3(2.5f,.02f,1.2f),Light);
            ExhibitionPanel();
            EntranceSign();
        }
        static readonly Color Navy=new Color(.07f,.11f,.2f), Brass=new Color(.78f,.62f,.3f), Gold=new Color(1f,.83f,.45f), Cream=new Color(.93f,.9f,.82f);
        /// <summary>A ball-and-stick model in relief on a sign (atoms: position, radius, colour; bonds: atom pairs).</summary>
        static void Relief(Transform parent,(Vector3 p,float r,Color c)[] atoms,(int a,int b)[] bonds)
        {
            foreach(var (p,r,c) in atoms) LabVisuals.Shape("Relief atom",PrimitiveType.Sphere,parent,p,Vector3.one*r*2,c,false);
            foreach(var (a,b) in bonds)
            {
                var from=atoms[a].p; var to=atoms[b].p; var axis=to-from;
                var stick=LabVisuals.Shape("Relief bond",PrimitiveType.Cylinder,parent,(from+to)*.5f,new Vector3(.018f,axis.magnitude*.5f,.018f),new Color(.82f,.84f,.86f),false).transform;
                stick.localRotation=Quaternion.FromToRotation(Vector3.up,axis);
            }
        }
        /// <summary>
        /// The end wall: a framed museum panel in navy and brass with the gallery's name in gold, a water and a methane model
        /// in relief either side, and an engraved brass plaque underneath counting the molecules on display.
        /// </summary>
        void ExhibitionPanel()
        {
            // Built facing +z (into the gallery); the wall face may move forward when the classroom tiles it, so stand it off.
            var panel=new GameObject("Exhibition panel").transform; panel.SetParent(transform,false);
            panel.localPosition=new Vector3(0,2.08f,RoomEnd+.17f); panel.localRotation=Quaternion.Euler(0,180,0); // label space: face -z
            LabVisuals.Shape("Board",PrimitiveType.Cube,panel,new Vector3(0,0,.01f),new Vector3(2.3f,.86f,.03f),Navy,false);
            foreach(var (p,s) in new[]{(new Vector3(0,.44f,-.006f),new Vector3(2.36f,.03f,.04f)),(new Vector3(0,-.44f,-.006f),new Vector3(2.36f,.03f,.04f)),
                                       (new Vector3(-1.165f,0,-.006f),new Vector3(.03f,.91f,.04f)),(new Vector3(1.165f,0,-.006f),new Vector3(.03f,.91f,.04f))})
                LabVisuals.Shape("Brass frame",PrimitiveType.Cube,panel,p,s,Brass,false);
            LabVisuals.Shape("Gold rule",PrimitiveType.Cube,panel,new Vector3(0,-.02f,-.006f),new Vector3(1.2f,.008f,.004f),Brass,false);
            var title=LabelCraft.Print(panel,"<b>MOLECULE GALLERY</b>",new Vector3(0,.13f,-.007f),new Vector2(1.5f,.2f),1.4f,Gold);
            title.characterSpacing=12; title.textWrappingMode=TextWrappingModes.NoWrap;
            LabelCraft.Print(panel,"<i>Every molecule on these tables was built in this lab.\nPick one up to learn what it is and what it is used for.</i>",new Vector3(0,-.18f,-.007f),new Vector2(1.5f,.2f),.42f,Cream);
            // Water on the left, methane on the right, standing out from the board.
            var white=new Color(.95f,.95f,.95f);
            var left=new GameObject("Water relief").transform; left.SetParent(panel,false); left.localPosition=new Vector3(-.82f,.0f,-.06f);
            Relief(left,new[]{(Vector3.zero,.075f,new Color(.85f,.15f,.12f)),(new Vector3(-.12f,-.09f,0),.045f,white),(new Vector3(.12f,-.09f,0),.045f,white)},new[]{(0,1),(0,2)});
            var right=new GameObject("Methane relief").transform; right.SetParent(panel,false); right.localPosition=new Vector3(.82f,.0f,-.06f);
            Relief(right,new[]{(Vector3.zero,.07f,new Color(.2f,.2f,.22f)),(new Vector3(0,.15f,0),.042f,white),(new Vector3(-.13f,-.06f,-.02f),.042f,white),
                (new Vector3(.13f,-.06f,-.02f),.042f,white),(new Vector3(0,-.07f,-.1f),.042f,white)},new[]{(0,1),(0,2),(0,3),(0,4)});
            // The count, engraved on a brass plaque with screws.
            counter=LabelCraft.MetalPlate(transform,"Gallery counter plaque",new Vector3(0,1.5f,RoomEnd+.175f),Quaternion.Euler(0,180,0),new Vector2(.9f,.13f),"",.5f);
            foreach(var r in counter.transform.parent.GetComponentsInChildren<Renderer>()) if(r.name=="Plate") r.sharedMaterial=LabVisuals.Material(Brass);
        }
        /// <summary>
        /// A double-sided sign hanging on two rods under the opening's header: "MOLECULE GALLERY" towards the lab and
        /// "BACK TO THE LAB" towards the gallery.
        /// </summary>
        void EntranceSign()
        {
            var sign=new GameObject("Gallery entrance sign").transform; sign.SetParent(transform,false); sign.localPosition=new Vector3(0,2.3f,RoomStart);
            LabVisuals.Shape("Sign board",PrimitiveType.Cube,sign,Vector3.zero,new Vector3(1.3f,.26f,.03f),Navy,false);
            foreach(var y in new[]{.12f,-.12f}) LabVisuals.Shape("Brass edge",PrimitiveType.Cube,sign,new Vector3(0,y,0),new Vector3(1.32f,.018f,.036f),Brass,false);
            foreach(var x in new[]{-.55f,.55f}) LabVisuals.Shape("Hanging rod",PrimitiveType.Cylinder,sign,new Vector3(x,.2f,0),new Vector3(.012f,.08f,.012f),new Color(.3f,.3f,.32f),false);
            // Lab side (+z): text faces the lab.
            var front=new GameObject("Lab side").transform; front.SetParent(sign,false); front.localPosition=new Vector3(0,0,.016f); front.localRotation=Quaternion.Euler(0,180,0);
            var title=LabelCraft.Print(front,"<b>MOLECULE GALLERY</b>  <size=60%>your molecules on display</size>",new Vector3(0,0,-.001f),new Vector2(1.2f,.18f),.9f,Gold);
            title.textWrappingMode=TextWrappingModes.NoWrap;
            // Gallery side (-z).
            var back=new GameObject("Gallery side").transform; back.SetParent(sign,false); back.localPosition=new Vector3(0,0,-.016f);
            LabelCraft.Print(back,"<b>BACK TO THE LAB</b>",new Vector3(0,0,-.001f),new Vector2(1.2f,.18f),.9f,Gold).textWrappingMode=TextWrappingModes.NoWrap;
        }
        Transform Table(int index)
        {
            while(tables.Count<=index)
            {
                var z=FirstTableZ-tables.Count*TableSpacing;
                var table=new GameObject("Gallery table "+(tables.Count+1)).transform; table.SetParent(transform,false); table.localPosition=new Vector3(0,0,z);
                LabVisuals.Shape("Table top",PrimitiveType.Cube,table,new Vector3(0,TableTop-.03f,0),new Vector3(3.1f,.06f,.85f),Dark,true);
                LabVisuals.Shape("Table edge",PrimitiveType.Cube,table,new Vector3(0,TableTop-.055f,.43f),new Vector3(3.1f,.05f,.035f),Wood,false);
                foreach(var x in new[]{-1.45f,1.45f}) foreach(var dz in new[]{-.36f,.36f})
                    LabVisuals.Shape("Table leg",PrimitiveType.Cube,table,new Vector3(x,(TableTop-.06f)*.5f,dz),new Vector3(.05f,TableTop-.06f,.05f),Metal,true);
                for(var i=0;i<SlotsPerTable;i++)
                    LabVisuals.Shape("Exhibit stand",PrimitiveType.Cylinder,table,new Vector3(-1.25f+(i%6)*.5f,TableTop+.005f,i<6?.2f:-.2f),new Vector3(.16f,.005f,.16f),Teal,false);
                tables.Add(table);
            }
            return tables[index];
        }

        /// <summary>Shows the most recent exhibits (two tables of twelve); existing exhibits are kept, so updates are cheap.</summary>
        public void Show(MoleculeGallery gallery,ElementDefinition[] elements)
        {
            var capacity=SlotsPerTable*MaximumTables;
            var shown=gallery.Entries.Skip(Mathf.Max(0,gallery.Entries.Count-capacity)).ToList();
            foreach(var id in exhibits.Keys.Where(id=>shown.All(e=>e.id!=id)).ToList()) { var old=exhibits[id]; exhibits.Remove(id); old.gameObject.SetActive(false); Destroy(old.gameObject); }
            var slots=SlotPositions(Mathf.Max(1,(shown.Count+SlotsPerTable-1)/SlotsPerTable)).ToArray();
            Table(0); for(var t=1;t*SlotsPerTable<shown.Count;t++) Table(t);
            for(var i=0;i<shown.Count;i++)
            {
                var entry=shown[i];
                if(!exhibits.TryGetValue(entry.id,out var exhibit))
                {
                    var go=new GameObject("Exhibit "+entry.name); go.transform.SetParent(transform,false);
                    exhibit=go.AddComponent<GalleryExhibit>(); exhibit.Initialize(lab,entry,elements); exhibits[entry.id]=exhibit;
                }
                exhibit.SetSlot(transform.TransformPoint(slots[i]));
                exhibit.UpdateLabel(entry);
            }
            var total=gallery.Entries.Count;
            counter.text=total==0?"Every molecule you complete appears here.":
                total+" molecule"+(total==1?"":"s")+" built"+(total>capacity?"  (showing the latest "+capacity+")":"");
        }
    }

    /// <summary>A display copy of a completed molecule: slowly turning above its stand, grabbable, returns to its stand when let go.</summary>
    public sealed class GalleryExhibit : MonoBehaviour
    {
        public GalleryEntry Entry { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        Transform model;
        SphereCollider grabZone;
        /// <summary>The single collider used to pick the exhibit up.</summary>
        public SphereCollider GrabZone => grabZone;
        TextMeshPro label;
        Transform placard;
        LabController lab;
        Vector3 slot;
        bool placed;
        public void Initialize(LabController owner,GalleryEntry entry,ElementDefinition[] elements)
        {
            lab=owner; Entry=entry;
            model=new GameObject("Model").transform; model.SetParent(transform,false);
            var centre=entry.positions.Length==0?Vector3.zero:entry.positions.Aggregate(Vector3.zero,(a,b)=>a+b)/entry.positions.Length;
            var extent=.05f;
            for(var i=0;i<entry.symbols.Length;i++)
            {
                var element=elements.FirstOrDefault(e=>e!=null && e.symbol==entry.symbols[i]);
                var radius=(element!=null?element.radius:.05f)*LabVisuals.AtomScale;
                LabVisuals.Shape(entry.symbols[i],PrimitiveType.Sphere,model,entry.positions[i]-centre,Vector3.one*radius*2,element!=null?element.color:Color.gray);
                extent=Mathf.Max(extent,(entry.positions[i]-centre).magnitude+radius);
            }
            var count=Mathf.Min(entry.bondA.Length,Mathf.Min(entry.bondB.Length,entry.bondOrder.Length));
            for(var i=0;i<count;i++)
            {
                if(entry.bondA[i]<0 || entry.bondB[i]<0 || entry.bondA[i]>=entry.positions.Length || entry.bondB[i]>=entry.positions.Length) continue;
                var a=entry.positions[entry.bondA[i]]-centre; var b=entry.positions[entry.bondB[i]]-centre; var axis=b-a;
                var side=Vector3.Cross(axis,Vector3.forward); if(side.sqrMagnitude<1e-8f) side=Vector3.Cross(axis,Vector3.up); side.Normalize();
                // Aromatic rings are drawn with equal single sticks, as on the bench.
                var order=entry.aromatic!=null && i<entry.aromatic.Length && entry.aromatic[i]?1:Mathf.Clamp(entry.bondOrder[i],1,3);
                for(var j=0;j<order;j++)
                {
                    var stick=LabVisuals.Shape("Bond",PrimitiveType.Cylinder,model,(a+b)*.5f+side*((j-(order-1)*.5f)*.022f),new Vector3(.014f,axis.magnitude*.5f,.014f),new Color(.55f,.7f,.78f),false).transform;
                    stick.localRotation=Quaternion.FromToRotation(Vector3.up,axis.sqrMagnitude>1e-10f?axis:Vector3.up);
                }
            }
            // Fit every exhibit inside its 0.5 m stand spacing.
            var scale=Mathf.Min(1,.15f/extent); transform.localScale=Vector3.one*scale;
            // One generous, still grab target around the whole model: the small atoms turn slowly and were hard to pinch.
            foreach(var c in model.GetComponentsInChildren<Collider>()) c.enabled=false;
            grabZone=gameObject.AddComponent<SphereCollider>(); grabZone.radius=extent+.035f/scale;
            var body=gameObject.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            Grab=gameObject.AddComponent<XRGrabInteractable>();
            Grab.colliders.Clear(); Grab.colliders.Add(grabZone); Grab.enabled=false; Grab.enabled=true; // register only the grab zone
            // Instantaneous: the exhibit stays exactly in the hand (velocity tracking could be held back by the table and stand).
            Grab.throwOnDetach=false; Grab.movementType=XRBaseInteractable.MovementType.Instantaneous; Grab.useDynamicAttach=true;
            Grab.firstSelectEntered.AddListener(_=>{ if(lab!=null) lab.ExhibitGrabbed(this); });
            Grab.lastSelectExited.AddListener(_=>{ if(lab!=null) lab.ExhibitReleased(this); });
            // A museum placard on the table edge in front of the stand.
            label=LabelCraft.Placard(transform.parent,"Exhibit placard",Vector3.zero,Quaternion.identity,new Vector2(.2f,.06f),out placard);
        }
        public void SetSlot(Vector3 world)
        {
            slot=world;
            // Name plate flat on the table between the stand and the nearest table edge, readable from that side.
            var gallery=transform.parent; var local=gallery.InverseTransformPoint(world);
            var row=Mathf.RoundToInt((GalleryRoom.FirstTableZ-local.z)/GalleryRoom.TableSpacing); var tableZ=GalleryRoom.FirstTableZ-row*GalleryRoom.TableSpacing;
            var side=local.z>=tableZ?1f:-1f;
            placard.localPosition=new Vector3(local.x,GalleryRoom.TableTop,tableZ+side*.33f);
            placard.localRotation=Quaternion.Euler(0,side>0?180:0,0);
            if(!placed || Grab==null || !Grab.isSelected) { if(!placed) transform.position=world; placed=true; }
        }
        public void UpdateLabel(GalleryEntry entry)
        {
            Entry=entry;
            label.text="<b>"+entry.name+"</b>\n<size=75%>"+entry.formula+(entry.timesBuilt>1?"   built "+entry.timesBuilt+"x":"")+"</size>";
        }
        void Update()
        {
            if(Grab==null || Grab.isSelected) return;
            transform.position=Vector3.Lerp(transform.position,slot,1-Mathf.Exp(-Time.deltaTime*6));
            if(lab==null || !lab.ReducedEffects) model.Rotate(0,Time.deltaTime*20,0,Space.World);
        }
        void OnDestroy() { if(placard!=null) Destroy(placard.gameObject); }
    }
}
