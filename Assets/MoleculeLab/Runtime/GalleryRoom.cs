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
        public const float RoomStart=-1.65f, RoomEnd=-4.45f, FirstTableZ=-2.05f, TableSpacing=1.3f, TableTop=.82f;
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
            Box("Gallery floor",new Vector3(0,-.06f,centre),new Vector3(6,.12f,depth),Floor,true);
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
            Sign("MOLECULE GALLERY",new Vector3(0,2.3f,RoomEnd+.07f),.07f,Dark);
            counter=Sign("",new Vector3(0,1.95f,RoomEnd+.07f),.03f,Dark);
            counter.rectTransform.sizeDelta=new Vector2(3,.3f);
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
        TextMeshPro label;
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
            transform.localScale=Vector3.one*Mathf.Min(1,.15f/extent);
            var body=gameObject.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            Grab=gameObject.AddComponent<XRGrabInteractable>();
            Grab.throwOnDetach=false; Grab.movementType=XRBaseInteractable.MovementType.VelocityTracking; Grab.useDynamicAttach=true;
            Grab.firstSelectEntered.AddListener(_=>{ if(lab!=null) lab.ExhibitGrabbed(this); });
            Grab.lastSelectExited.AddListener(_=>{ if(lab!=null) lab.ExhibitReleased(this); });
            label=LabVisuals.Label(transform.parent,"",Vector3.zero,.022f); label.rectTransform.sizeDelta=new Vector2(.48f,.2f);
        }
        public void SetSlot(Vector3 world)
        {
            slot=world; label.transform.position=world+Vector3.down*.13f;
            if(!placed || Grab==null || !Grab.isSelected) { if(!placed) transform.position=world; placed=true; }
        }
        public void UpdateLabel(GalleryEntry entry)
        {
            Entry=entry;
            label.text=entry.name+"\n"+entry.formula+(entry.timesBuilt>1?"  x"+entry.timesBuilt:"");
        }
        void Update()
        {
            if(Grab==null || Grab.isSelected) return;
            transform.position=Vector3.Lerp(transform.position,slot,1-Mathf.Exp(-Time.deltaTime*6));
            if(lab==null || !lab.ReducedEffects) model.Rotate(0,Time.deltaTime*20,0,Space.World);
        }
        void OnDestroy() { if(label!=null) Destroy(label.gameObject); }
    }
}
