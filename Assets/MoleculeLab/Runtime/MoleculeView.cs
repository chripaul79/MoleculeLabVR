using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
namespace MoleculeLab
{
    public sealed class MoleculeView : MonoBehaviour
    {
        public MoleculeState State { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public MoleculeDefinition Definition => State.Definition;
        public string FragmentPair;
        [System.NonSerialized] public MoleculeState ParentState;
        /// <summary>Docked molecules sit still in the reaction tray; everything else complete falls under gravity.</summary>
        public bool Docked { get => docked; set { docked=value; ApplyPhysics(); } }
        bool docked;
        public bool PlacementLocked;
        public Rigidbody Body { get; private set; }
        float floorTime;
        public string InventoryOwner { get; private set; } = System.Guid.NewGuid().ToString();
        public int[] TokenIds { get; private set; }
        Vector3[] snapFrom;
        float[] snapTime;
        LabController lab;
        Transform[] atomShapes;
        Transform[][] bondShapes;
        TextMeshPro label;
        LineRenderer aromaticCircle;
        float torsion, bend;
        public float Torsion => torsion;
        public void Initialize(LabController owner, MoleculeState state)
        {
            lab = owner; State = state;
            TokenIds=Enumerable.Repeat(-1,Definition.atoms.Length).ToArray();
            snapFrom=new Vector3[Definition.atoms.Length]; snapTime=new float[Definition.atoms.Length];
            atomShapes = new Transform[Definition.atoms.Length];
            for (var i = 0; i < atomShapes.Length; i++)
            {
                var atom = Definition.atoms[i];
                atomShapes[i] = LabVisuals.Shape(atom.element.symbol + " site " + i, PrimitiveType.Sphere, transform, atom.position,
                    Vector3.one * atom.element.radius * 2, atom.element.color).transform;
            }
            bondShapes = Definition.bonds.Select(b => Enumerable.Range(0,Definition.IsAromaticBond(b)?1:b.order).Select(_ =>
                LabVisuals.Shape("Bond", PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, new Color(.55f,.7f,.78f), false).transform).ToArray()).ToArray();
            label = LabVisuals.Label(transform, "", new Vector3(0,Definition.atoms.Min(a=>a.position.y-a.element.radius)-.055f,0), .024f);
            var body = gameObject.AddComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true; Body = body;
            body.mass = .3f; body.linearDamping = .6f; body.angularDamping = 2.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.selectMode = InteractableSelectMode.Multiple; Grab.throwOnDetach = false;
            Grab.movementType = XRBaseInteractable.MovementType.VelocityTracking; Grab.useDynamicAttach = true;
            Grab.addDefaultGrabTransformers = false;
            Grab.AddSingleGrabTransformer(gameObject.AddComponent<XRGeneralGrabTransformer>());
            Grab.AddMultipleGrabTransformer(gameObject.AddComponent<MoleculeScaleTransformer>());
            Grab.firstSelectEntered.AddListener(_ => { Docked = false; lab.Select(this); });
            Grab.lastSelectExited.AddListener(args => { if (!args.isCanceled && !lab.Resetting && HandMenuRay.IsSafeRelease(args.interactorObject)) lab.MoleculeReleased(this); });
            if(Definition.aromaticRing.Length>0)
            {
                var ring=new GameObject("Aromatic delocalization (six pi electrons)"); ring.transform.SetParent(transform,false);
                aromaticCircle=ring.AddComponent<LineRenderer>(); aromaticCircle.useWorldSpace=false; aromaticCircle.loop=true;
                aromaticCircle.positionCount=48; aromaticCircle.widthMultiplier=.007f; aromaticCircle.sharedMaterial=LabVisuals.Material(new Color(.4f,.8f,.85f));
                for(var i=0;i<48;i++) { var a=i*Mathf.PI*2/48; aromaticCircle.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.125f); }
            }
            State.Completed += OnCompleted;
            Refresh();
        }
        void OnDestroy() { if (State != null) State.Completed -= OnCompleted; }
        void OnCompleted() { Refresh(); lab.MoleculeCompleted(this); }
        public Vector3 SitePosition(int i) => atomShapes[i].position;
        public void Refresh()
        {
            for (var i = 0; i < atomShapes.Length; i++)
            {
                var occupied = State.IsOccupied(i); var element = Definition.atoms[i].element;
                atomShapes[i].GetComponent<Collider>().enabled = occupied && State.Complete;
                atomShapes[i].GetComponent<Renderer>().sharedMaterial = LabVisuals.Material(occupied ? element.color :
                    State.CanPlace(i,element,out _) ? new Color(.1f,.7f,.65f) : new Color(.16f,.22f,.27f));
                atomShapes[i].GetComponent<Renderer>().enabled=occupied;
                atomShapes[i].localScale = Vector3.one * element.radius * (occupied ? 2f : 1f);
            }
            Grab.enabled = State.Complete;
            ApplyPhysics();
            if(aromaticCircle!=null) aromaticCircle.enabled=Definition.aromaticRing.All(State.IsOccupied);
            label.text = Definition.displayName + "  " + Definition.formula;
            UpdateGeometry();
        }
        public int AnchorFor(int site)
        {
            foreach(var bond in Definition.bonds)
            {
                var other=bond.a==site?bond.b:bond.b==site?bond.a:-1;
                if(other>=0 && State.IsOccupied(other)) return other;
            }
            return -1;
        }
        public void AnimateSite(int site,Vector3 start) { snapFrom[site]=transform.InverseTransformPoint(start); snapTime[site]=.12f; }
        public void ShowGeometry(bool show)
        {
            label.text = Definition.displayName + "  " + Definition.formula + (show ? "\n" + Definition.geometry : "");
        }
        public void Twist(float degrees) { if(Definition.rotatableBond<0) return; System.Array.Clear(snapTime,0,snapTime.Length); torsion = degrees; UpdateGeometry(); }
        public void Bend(float degrees) { if(Definition.rotatableBond<0) return; System.Array.Clear(snapTime,0,snapTime.Length); bend = Mathf.Clamp(degrees, -15,15); UpdateGeometry(); }
        void UpdateGeometry()
        {
            for (var i = 0; i < atomShapes.Length; i++) atomShapes[i].localPosition = Definition.atoms[i].position;
            if (Definition.rotatableBond >= 0)
            {
                var bond = Definition.bonds[Definition.rotatableBond]; var pivot = Definition.atoms[bond.b].position;
                var axis = (pivot - Definition.atoms[bond.a].position).normalized;
                var twist = Quaternion.AngleAxis(torsion,axis); var flex = Quaternion.AngleAxis(bend, Vector3.forward);
                foreach (var i in Definition.rotatingSites) atomShapes[i].localPosition = pivot + flex * twist * (Definition.atoms[i].position-pivot);
            }
            for(var i=0;i<snapTime.Length;i++) if(snapTime[i]>0)
                atomShapes[i].localPosition=Vector3.Lerp(snapFrom[i],atomShapes[i].localPosition,1-snapTime[i]/.12f);
            for (var i = 0; i < bondShapes.Length; i++)
            {
                var bond = Definition.bonds[i]; var a = atomShapes[bond.a].localPosition; var b = atomShapes[bond.b].localPosition;
                for (var j = 0; j < bondShapes[i].Length; j++)
                {
                    var t = bondShapes[i][j]; t.gameObject.SetActive(State.IsOccupied(bond.a) && State.IsOccupied(bond.b));
                    var offset = Vector3.Cross(b-a, Vector3.forward).normalized * ((j-(bondShapes[i].Length-1)*.5f)*.022f);
                    t.localPosition = (a+b)*.5f + offset; t.localRotation = Quaternion.FromToRotation(Vector3.up,b-a);
                    t.localScale = new Vector3(.014f,(b-a).magnitude*.5f,.014f);
                }
            }
        }
        void Update()
        {
            if(snapTime!=null && snapTime.Any(t=>t>0)) { for(var i=0;i<snapTime.Length;i++) snapTime[i]=Mathf.Max(0,snapTime[i]-Time.deltaTime); UpdateGeometry(); }
            if (Grab == null || Grab.isSelected) { floorTime = 0; return; }
            ApplyPhysics();
            var position = transform.position;
            if (Docked) return;
            if (Body.isKinematic) { if (position.y < .3f || position.magnitude > 6) transform.position = lab.BuildPosition; return; }
            // Dropped molecules: anything that leaves the room, or lies on the floor for a few seconds, returns to the bench.
            if (position.y < -1 || position.magnitude > 15) ReturnToBench();
            else if (position.y < .3f) { floorTime += Time.deltaTime; if (floorTime > 3) ReturnToBench(); }
            else floorTime = 0;
        }
        /// <summary>Complete, undocked, unheld molecules are dynamic rigidbodies; while held, XRI manages the body itself.</summary>
        public void ApplyPhysics()
        {
            if (Body == null || State == null || (Grab != null && Grab.isSelected)) return;
            var dynamic = State.Complete && !docked && !PlacementLocked && !State.Consumed;
            if (Body.isKinematic != dynamic) return;
            if (!dynamic) StopMotion();
            Body.isKinematic = !dynamic; Body.useGravity = dynamic;
            if (dynamic) Body.WakeUp();
        }
        public void StopMotion()
        {
            if (Body == null || Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero;
        }
        void ReturnToBench()
        {
            StopMotion(); transform.position = lab.BenchDropPosition; transform.rotation = Quaternion.identity; floorTime = 0;
            lab.Notify(Definition.displayName + " fell and was returned to the bench.");
        }
        public void CancelSelection()
        {
            if (Grab == null) return;
            var manager = Grab.interactionManager;
            if (manager != null) foreach (var interactor in Grab.interactorsSelecting.ToArray()) manager.SelectCancel(interactor, Grab);
        }
    }
}
