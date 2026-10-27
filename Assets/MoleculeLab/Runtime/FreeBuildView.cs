using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

namespace MoleculeLab
{
    public readonly struct FreePlacement
    {
        public readonly int Anchor;
        /// <summary>Where the new atom will sit, in the structure's local space.</summary>
        public readonly Vector3 Local;
        /// <summary>The visible guide dot the player aims at, in world space.</summary>
        public readonly Vector3 Guide;
        public readonly float Distance;
        public readonly bool Ready;
        public FreePlacement(int anchor,Vector3 local,Vector3 guide,float distance,bool ready) { Anchor=anchor; Local=local; Guide=guide; Distance=distance; Ready=ready; }
    }

    /// <summary>What releasing a grabbed structure atom will do.</summary>
    public enum FreeGesture { None, Bond, Remove }

    /// <summary>Keeps a structure atom's current index after other atoms are removed.</summary>
    public sealed class FreeAtomHandle : MonoBehaviour { public int Index; }

    /// <summary>
    /// Renders and edits a FreeStructure without menus. Loose atoms bond at teal guide dots (VSEPR directions).
    /// Grab a structure atom and push it into another to add a bond (double/triple, or close a ring);
    /// pull an end atom away to remove it. A floating label always says whether the structure is complete.
    /// </summary>
    public sealed class FreeBuildView : MonoBehaviour
    {
        static readonly Color BondColor=new Color(.55f,.7f,.78f), SelectedColor=new Color(1f,.78f,.25f), GuideColor=new Color(.2f,.85f,.75f);
        public static readonly Color IncompleteColor=new Color(1f,.82f,.35f), CheckingColor=new Color(.4f,1f,.85f), ProblemColor=new Color(1f,.55f,.35f);
        /// <summary>Releasing an end atom this far (metres, centre to centre) from its neighbour removes it.</summary>
        public const float PullOffDistance=.34f;
        public FreeStructure Structure { get; } = new FreeStructure();
        public string Owner { get; } = "free:"+Guid.NewGuid().ToString("N");
        public int SelectedBond { get; private set; } = -1;
        public bool Locked { get; private set; }
        public int Dragging => dragging;
        public LooseAtom PreviewAtom => previewAtom;
        public string StatusText => statusLabel!=null?statusLabel.text:"";
        LabController lab;
        readonly List<Transform> atomShapes=new List<Transform>();
        readonly List<XRGrabInteractable> grabs=new List<XRGrabInteractable>();
        readonly List<FreeAtomHandle> handles=new List<FreeAtomHandle>();
        readonly List<Vector3> shown=new List<Vector3>();
        readonly List<Transform[]> bondShapes=new List<Transform[]>();
        readonly List<Transform> guides=new List<Transform>();
        readonly List<(int atom,Vector3 direction)> guideSites=new List<(int,Vector3)>();
        LineRenderer arc,ring;
        TextMeshPro label,statusLabel;
        LooseAtom previewAtom;
        int dragging=-1;
        bool wasReady;
        // A grab release is acted on in the next Update, outside the XR interaction event.
        bool releasePending; FreeGesture pendingGesture; int pendingAtom=-1, pendingPartner=-1;
        int centroidExclude=-1; // the atom just released: the next relaxation keeps the others where they are

        public void Initialize(LabController owner)
        {
            lab=owner;
            arc=Line("Free bond preview",2,.003f); ring=Line("Free bond target",33,.004f);
            label=LabVisuals.Label(transform,"",Vector3.zero,.024f); label.gameObject.SetActive(false);
            statusLabel=LabVisuals.Label(transform,"",Vector3.up*.15f,.03f);
            statusLabel.rectTransform.sizeDelta=new Vector2(1.4f,.3f);
        }
        LineRenderer Line(string name,int points,float width)
        {
            var go=new GameObject(name); go.transform.SetParent(transform,false); var line=go.AddComponent<LineRenderer>();
            line.useWorldSpace=true; line.positionCount=points; line.widthMultiplier=width;
            line.sharedMaterial=LabVisuals.Material(GuideColor); line.enabled=false; return line;
        }
        public void SetStatus(string text,Color color)
        {
            if(statusLabel==null) return;
            statusLabel.text=text; statusLabel.color=color;
        }

        // ---------- editing ----------
        public int AddAtom(ElementDefinition element,int token,Vector3 local,Vector3 worldFrom,int anchor=-1,bool relax=true)
        {
            var index=Structure.AddAtom(element.symbol,element.maximumBondOrder,local,token);
            if(anchor>=0) { Structure.AddBond(anchor,index); SelectedBond=Structure.Graph.Bonds.Count-1; }
            var shape=LabVisuals.Shape(element.symbol+" free atom",PrimitiveType.Sphere,transform,local,Vector3.one*element.radius*2,element.color).transform;
            var handle=shape.gameObject.AddComponent<FreeAtomHandle>(); handle.Index=index;
            var body=shape.gameObject.AddComponent<Rigidbody>(); body.useGravity=false; body.isKinematic=true;
            body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var grab=shape.gameObject.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach=false; grab.movementType=XRBaseInteractable.MovementType.VelocityTracking;
            // A distance grab must not pull the atom into the hand: it stays on the ray, so it can still be pushed into its partner.
            grab.farAttachMode=InteractableFarAttachMode.Far;
            grab.useDynamicAttach=true; grab.matchAttachPosition=true; grab.matchAttachRotation=false; grab.trackRotation=false;
            grab.firstSelectEntered.AddListener(_=>BeginDrag(handle.Index));
            grab.lastSelectExited.AddListener(args=>EndDrag(handle.Index,!args.isCanceled && !lab.Resetting && HandMenuRay.IsSafeRelease(args.interactorObject)));
            grab.enabled=!Locked;
            // Atoms of one structure never block each other, so they can be squeezed together for double bonds and rings.
            var own=shape.GetComponent<Collider>();
            foreach(var other in atomShapes) { var collider=other.GetComponent<Collider>(); if(collider!=null && own!=null) Physics.IgnoreCollision(own,collider); }
            atomShapes.Add(shape); grabs.Add(grab); handles.Add(handle); shown.Add(transform.InverseTransformPoint(worldFrom));
            RebuildBonds();
            if(relax) Relax(); else RefreshGuides();
            return index;
        }
        public int Attach(ElementDefinition element,int token,FreePlacement placement,Vector3 worldFrom,bool relax=true) =>
            AddAtom(element,token,placement.Local,worldFrom,placement.Anchor,relax);

        public int RemoveLastAtom() => RemoveAtom(Structure.Count-1);
        /// <summary>Removes one atom (the caller checks Structure.CanRemove); returns its inventory token.</summary>
        public int RemoveAtom(int index)
        {
            if(index<0 || index>=Structure.Count) return -1;
            CancelGrabs();
            centroidExclude=-1; // the released atom is the one being removed
            var token=Structure.RemoveAtom(index);
            var shape=atomShapes[index]; shape.gameObject.SetActive(false); Destroy(shape.gameObject);
            atomShapes.RemoveAt(index); grabs.RemoveAt(index); handles.RemoveAt(index); shown.RemoveAt(index);
            for(var i=index;i<handles.Count;i++) handles[i].Index=i;
            SelectedBond=Mathf.Min(SelectedBond,Structure.Graph.Bonds.Count-1);
            RebuildBonds(); Relax();
            return token;
        }
        public void SelectBond(int delta)
        {
            var count=Structure.Graph.Bonds.Count; if(count==0) { SelectedBond=-1; return; }
            SelectedBond=SelectedBond<0?0:((SelectedBond+delta)%count+count)%count;
            RebuildBonds();
        }
        public bool TrySetOrder(int order,out string reason)
        {
            if(!Structure.CanSetOrder(SelectedBond,order,out reason)) return false;
            Structure.SetOrder(SelectedBond,order); RebuildBonds(); Relax(); return true;
        }
        /// <summary>Adds one more bond between two atoms: a new bond (closing a ring) or a raised bond order.</summary>
        public bool CanAddBond(int atom,int partner,out string reason)
        {
            var bond=Structure.Graph.BondIndex(atom,partner);
            if(bond>=0)
            {
                var order=Structure.Graph.Bonds[bond].Order;
                if(order>=3) { reason="Triple is the highest bond order."; return false; }
                return Structure.CanSetOrder(bond,order+1,out reason);
            }
            return Structure.CanBond(atom,partner,out reason);
        }
        public void AddBond(int atom,int partner)
        {
            var bond=Structure.Graph.BondIndex(atom,partner);
            if(bond>=0) { Structure.SetOrder(bond,Structure.Graph.Bonds[bond].Order+1); SelectedBond=bond; }
            else { Structure.AddBond(atom,partner); SelectedBond=Structure.Graph.Bonds.Count-1; }
            RebuildBonds(); Relax();
        }
        public void Relax() { Structure.Relax(140,dragging,centroidExclude); centroidExclude=-1; RefreshGuides(); }
        public string BondText(int bond=-2)
        {
            if(bond==-2) bond=SelectedBond;
            if(bond<0 || bond>=Structure.Graph.Bonds.Count) return "none";
            var b=Structure.Graph.Bonds[bond];
            return Structure.Label(b.A)+(b.Order==3?"#":b.Order==2?"=":"-")+Structure.Label(b.B)+" ("+(b.Order==3?"triple":b.Order==2?"double":"single")+")";
        }
        public void SetLocked(bool locked)
        {
            if(locked) CancelGrabs();
            Locked=locked; foreach(var grab in grabs) if(grab!=null) grab.enabled=!locked;
            if(locked) ClearPreview();
        }
        public void CancelGrabs()
        {
            foreach(var grab in grabs)
            {
                if(grab==null || grab.interactionManager==null) continue;
                foreach(var interactor in grab.interactorsSelecting.ToArray()) grab.interactionManager.SelectCancel(interactor,grab);
            }
            dragging=-1;
        }

        // ---------- grabbing structure atoms ----------
        public void BeginDrag(int atom)
        {
            if(Locked || atom<0 || atom>=Structure.Count) return;
            dragging=atom; ClearPreview();
        }
        public void EndDrag(int atom,bool commit)
        {
            if(dragging!=atom) return;
            pendingGesture=FreeGesture.None; pendingAtom=atom; pendingPartner=-1;
            if(commit && EvaluateGesture(atom,out var gesture,out var partner,out var ready,out _) && ready) { pendingGesture=gesture; pendingPartner=partner; }
            dragging=-1; HidePreviewLines(); releasePending=true; centroidExclude=atom;
        }
        /// <summary>
        /// Decides what releasing a held structure atom would do. Pulled far from its only neighbour: remove it.
        /// Pushed into another atom with a free bond: add a bond (ring) or raise the existing bond's order.
        /// </summary>
        public bool EvaluateGesture(int atom,out FreeGesture gesture,out int partner,out bool ready,out string text)
        {
            gesture=FreeGesture.None; partner=-1; ready=false; text="";
            if(atom<0 || atom>=Structure.Count) return false;
            var position=atomShapes[atom].position;
            // Bonding wins when another atom with a free bond is close; otherwise a far pull removes an end atom.
            var best=float.MaxValue;
            for(var other=0;other<Structure.Count;other++)
            {
                if(other==atom || !CanAddBond(atom,other,out _)) continue;
                var d=Vector3.Distance(position,atomShapes[other].position);
                if(d<best) { best=d; partner=other; }
            }
            if(partner>=0 && best<=lab.previewDistance)
            {
                gesture=FreeGesture.Bond; ready=best<=lab.ringSnapDistance;
                var bond=Structure.Graph.BondIndex(atom,partner);
                var what=bond<0?"close a ring":Structure.Graph.Bonds[bond].Order==1?"make a double bond":"make a triple bond";
                text=(ready?"Release to ":"Push closer to ")+what;
                return true;
            }
            partner=-1;
            // Pushed right into an atom it cannot bond more to: say why instead of silently doing nothing.
            var blocked=-1; var blockedDistance=lab.ringSnapDistance; var blockedReason="";
            for(var other=0;other<Structure.Count;other++)
            {
                if(other==atom) continue;
                var d=Vector3.Distance(position,atomShapes[other].position);
                if(d<blockedDistance && !CanAddBond(atom,other,out var why)) { blocked=other; blockedDistance=d; blockedReason=why; }
            }
            if(blocked>=0) { partner=blocked; text=blockedReason; return true; }
            var neighbors=Structure.Graph.Neighbors(atom).ToArray();
            if(neighbors.Length<=1 && Structure.CanRemove(atom,out _))
            {
                var anchor=neighbors.Length==1?atomShapes[neighbors[0]].position:transform.position;
                var pulled=Vector3.Distance(position,anchor);
                if(pulled>=PullOffDistance*.75f)
                {
                    gesture=FreeGesture.Remove; partner=neighbors.Length==1?neighbors[0]:-1; ready=pulled>=PullOffDistance;
                    text=ready?"Release to put "+Structure.Label(atom)+" back":"Pull further to remove";
                    return true;
                }
            }
            return false;
        }

        // ---------- loose-atom placement ----------
        public float GuideLength(int anchor) => FreeStructure.BondLength(Structure.Symbol(anchor),"C",1)*.92f;
        public IEnumerable<Vector3> GuideWorldPositions(int anchor) =>
            guideSites.Where(s=>s.atom==anchor).Select(s=>transform.TransformPoint(Structure.Position(s.atom)+s.direction*GuideLength(s.atom)));
        public bool FindPlacement(LooseAtom atom,out FreePlacement placement)
        {
            placement=default;
            if(Locked || dragging>=0 || atom==null || atom.Element==null || atom.Element.maximumBondOrder<1) return false;
            var best=float.MaxValue;
            foreach(var (anchor,direction) in guideSites)
            {
                var guide=transform.TransformPoint(Structure.Position(anchor)+direction*GuideLength(anchor));
                var d=Vector3.Distance(guide,atom.transform.position);
                if(d>=best) continue;
                best=d;
                var local=Structure.Position(anchor)+direction*FreeStructure.BondLength(Structure.Symbol(anchor),atom.Element.symbol,1);
                placement=new FreePlacement(anchor,local,guide,d,d<=lab.freeSnapDistance);
            }
            return best<=lab.previewDistance;
        }
        public void ShowPreview(LooseAtom atom)
        {
            if(previewAtom!=null && previewAtom!=atom && previewAtom.Grab!=null && previewAtom.Grab.isSelected) return;
            if(!FindPlacement(atom,out var placement)) { ClearPreview(atom); return; }
            previewAtom=atom;
            DrawPreview(atom.transform.position,placement.Guide,placement.Ready,
                placement.Ready?"Release to bond to "+Structure.Label(placement.Anchor):"Move onto a guide dot");
        }
        public void ClearPreview(LooseAtom atom=null)
        {
            if(atom!=null && previewAtom!=atom) return;
            previewAtom=null; if(dragging<0) HidePreviewLines();
        }
        void DrawPreview(Vector3 from,Vector3 to,bool ready,string text)
        {
            arc.enabled=true; ring.enabled=true; label.gameObject.SetActive(true);
            arc.SetPosition(0,from); arc.SetPosition(1,to);
            var facing=Camera.main!=null?to-Camera.main.transform.position:Vector3.forward;
            var rotation=Quaternion.FromToRotation(Vector3.forward,facing.sqrMagnitude>1e-6f?facing.normalized:Vector3.forward);
            var radius=ready?.04f:.026f;
            for(var i=0;i<ring.positionCount;i++) { var angle=i*Mathf.PI*2/(ring.positionCount-1); ring.SetPosition(i,to+rotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius); }
            ring.widthMultiplier=ready?.005f:.002f;
            label.transform.position=to+Vector3.up*.08f; label.text=text;
            if(ready && !wasReady) lab.ReadyCue(); wasReady=ready;
        }
        void HidePreviewLines()
        {
            wasReady=false;
            if(arc!=null) { arc.enabled=false; ring.enabled=false; label.gameObject.SetActive(false); }
        }

        // ---------- visuals ----------
        void RefreshGuides()
        {
            guideSites.Clear();
            for(var i=0;i<Structure.Count;i++) foreach(var direction in Structure.OpenDirections(i)) guideSites.Add((i,direction));
            while(guides.Count<guideSites.Count)
                guides.Add(LabVisuals.Shape("Open bond guide",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.026f,GuideColor,false).transform);
            for(var k=0;k<guides.Count;k++)
            {
                var active=k<guideSites.Count; guides[k].gameObject.SetActive(active);
                if(active) guides[k].localPosition=Structure.Position(guideSites[k].atom)+guideSites[k].direction*GuideLength(guideSites[k].atom);
            }
        }
        void RebuildBonds()
        {
            foreach(var set in bondShapes) foreach(var shape in set) if(shape!=null) { shape.gameObject.SetActive(false); Destroy(shape.gameObject); }
            bondShapes.Clear();
            for(var i=0;i<Structure.Graph.Bonds.Count;i++)
            {
                var color=i==SelectedBond?SelectedColor:BondColor;
                bondShapes.Add(Enumerable.Range(0,Structure.Graph.Bonds[i].Order)
                    .Select(_=>LabVisuals.Shape("Free bond",PrimitiveType.Cylinder,transform,Vector3.zero,Vector3.one,color,false).transform).ToArray());
            }
        }
        void Update()
        {
            if(lab==null) return;
            if(releasePending)
            {
                releasePending=false;
                if(pendingGesture==FreeGesture.Bond) lab.FreeBondRequested(this,pendingAtom,pendingPartner);
                else if(pendingGesture==FreeGesture.Remove) lab.FreeAtomPulled(this,pendingAtom);
                else Relax();
                centroidExclude=-1;
                if(this==null || Structure.Count==0) return;
            }
            if(dragging>=0 && dragging<atomShapes.Count)
            {
                var held=atomShapes[dragging].localPosition; Structure.SetPosition(dragging,held); shown[dragging]=held;
                if(EvaluateGesture(dragging,out var gesture,out var partner,out var ready,out var text))
                    DrawPreview(atomShapes[dragging].position,partner>=0?atomShapes[partner].position:transform.position,ready,text);
                else HidePreviewLines();
            }
            var blend=1-Mathf.Exp(-Time.deltaTime*12);
            for(var i=0;i<atomShapes.Count;i++)
            {
                if(i==dragging) continue;
                shown[i]=Vector3.Lerp(shown[i],Structure.Position(i),blend); atomShapes[i].localPosition=shown[i];
            }
            KeepAboveBench();
            UpdateBonds();
            var top=shown.Count==0?0:shown.Max(p=>p.y);
            statusLabel.transform.localPosition=new Vector3(Structure.Count==0?0:shown.Average(p=>p.x),top+.13f,Structure.Count==0?0:shown.Average(p=>p.z));
            var showGuides=lab.Mode==LabMode.Free && !Locked && dragging<0;
            for(var k=0;k<guides.Count;k++) if(guides[k].gameObject.activeSelf!=(showGuides && k<guideSites.Count)) guides[k].gameObject.SetActive(showGuides && k<guideSites.Count);
        }
        /// <summary>The structure starts just above the bench; when atoms would sink into it, lift the whole structure.</summary>
        void KeepAboveBench()
        {
            var lowest=float.MaxValue;
            for(var i=0;i<atomShapes.Count;i++)
            {
                if(i==dragging) continue;
                var bottom=transform.TransformPoint(Structure.Position(i)).y-atomShapes[i].lossyScale.y*.5f;
                if(bottom<lowest) lowest=bottom;
            }
            var lift=lab.BenchTop+.03f-lowest;
            if(lowest<float.MaxValue && lift>.0005f) transform.position+=Vector3.up*(lift*(1-Mathf.Exp(-Time.deltaTime*10)));
        }
        void LateUpdate()
        {
            if(previewAtom!=null && (previewAtom.Grab==null || !previewAtom.Grab.isSelected)) ClearPreview();
        }
        void UpdateBonds()
        {
            var view=Camera.main!=null?transform.InverseTransformDirection(Camera.main.transform.forward):Vector3.forward;
            for(var i=0;i<bondShapes.Count && i<Structure.Graph.Bonds.Count;i++)
            {
                var bond=Structure.Graph.Bonds[i]; var a=shown[bond.A]; var b=shown[bond.B]; var axis=b-a;
                var side=Vector3.Cross(axis,view);
                if(side.sqrMagnitude<1e-8f) side=Vector3.Cross(axis,Vector3.up);
                if(side.sqrMagnitude<1e-8f) side=Vector3.Cross(axis,Vector3.right);
                side.Normalize();
                var set=bondShapes[i];
                for(var j=0;j<set.Length;j++)
                {
                    var shape=set[j];
                    shape.localPosition=(a+b)*.5f+side*((j-(set.Length-1)*.5f)*.022f);
                    shape.localRotation=Quaternion.FromToRotation(Vector3.up,axis.sqrMagnitude>1e-10f?axis:Vector3.up);
                    shape.localScale=new Vector3(.014f,axis.magnitude*.5f,.014f);
                }
            }
        }
    }
}
