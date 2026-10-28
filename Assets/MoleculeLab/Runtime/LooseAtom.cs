using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace MoleculeLab
{
    public sealed class LooseAtom : MonoBehaviour
    {
        public static readonly System.Collections.Generic.HashSet<LooseAtom> Active=new System.Collections.Generic.HashSet<LooseAtom>();
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }
        public ElementDefinition Element { get; private set; }
        public XRGrabInteractable Grab { get; private set; }
        public int TokenId { get; private set; }
        public string Owner => "loose:"+TokenId;
        public bool OnShelf { get; private set; } = true;
        /// <summary>Time the atom was last let go off the shelf; unused atoms despawn after LabController.idleAtomSeconds.</summary>
        public float ReleasedAt { get; private set; } = -1;
        LabController lab;
        GameObject outline;
        TMPro.TextMeshPro symbolLabel;
        public void Initialize(LabController owner,ElementDefinition element,int tokenId)
        {
            lab=owner; Element=element; TokenId=tokenId;
            var body=gameObject.AddComponent<Rigidbody>(); body.mass=.05f; body.useGravity=false; body.isKinematic=true;
            body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            Grab=gameObject.AddComponent<XRGrabInteractable>(); Grab.throwOnDetach=false;
            // Velocity tracking: a held atom is pushed by physics towards the hand, so it stops at the bench, shelves and walls.
            Grab.movementType=XRBaseInteractable.MovementType.VelocityTracking;
            Grab.useDynamicAttach=true; Grab.matchAttachPosition=true; Grab.matchAttachRotation=true;
            Grab.smoothPosition=true; Grab.smoothPositionAmount=20; Grab.tightenPosition=.8f;
            // Hydrogen's 8 cm target fits inside the closest 8.7 cm shelf socket spacing.
            // Small atoms keep a comfortable grab target: at least 3 cm radius, whatever their visible size.
            GetComponent<SphereCollider>().radius=Mathf.Max(.5f,.03f/Mathf.Max(.001f,transform.localScale.x));
            // Loose atoms pass through each other, so a crowded basket never jams a grab or a refill.
            var own=GetComponent<Collider>();
            foreach(var other in Active) if(other!=this && other!=null) { var collider=other.GetComponent<Collider>(); if(collider!=null) Physics.IgnoreCollision(own,collider); }
            Grab.firstSelectEntered.AddListener(_=>{ ReleasedAt=-1; TakeFromShelf(); });
            Grab.lastSelectExited.AddListener(_=>MarkReleased());
            Grab.lastSelectExited.AddListener(args=> { if(args.isCanceled || !HandMenuRay.IsSafeRelease(args.interactorObject)) lab.ClearPreview(this); else if(!lab.Resetting) { if(!lab.TryAttach(this)) lab.AtomReleased(this); } });
            outline=new GameObject("Hover ring"); outline.transform.SetParent(transform,false);
            var line=outline.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.loop=true; line.positionCount=32; line.widthMultiplier=.025f;
            line.sharedMaterial=LabVisuals.Material(Color.white); line.startColor=line.endColor=new Color(.15f,.85f,.7f);
            for(var i=0;i<32;i++) { var a=i*Mathf.PI*2/32; line.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.59f); }
            outline.SetActive(false);
            Grab.selectEntered.AddListener(_=> { outline.SetActive(true); line.startColor=line.endColor=Color.white; });
            Grab.selectExited.AddListener(_=> { line.startColor=line.endColor=new Color(.15f,.85f,.7f); outline.SetActive(Grab.isHovered); });
            Grab.firstHoverEntered.AddListener(_=>outline.SetActive(true)); Grab.lastHoverExited.AddListener(_=>outline.SetActive(Grab.isSelected));
            var label=LabVisuals.Label(transform,element.symbol,new Vector3(0,.04f,-.51f),.017f/transform.localScale.x);
            symbolLabel=label; label.color=ElementCatalog.IsLight(element.color)?new Color(.06f,.12f,.14f):Color.white;
            label.rectTransform.sizeDelta=new Vector2(.9f,.6f);
            fullScale=transform.localScale; ShowShelfSize(OnShelf);
        }
        /// <summary>Starts the idle timer: if the atom is still unused after LabController.idleAtomSeconds it despawns.</summary>
        public void MarkReleased() => ReleasedAt=Time.time;
        /// <summary>In a beaker atoms are small, like marbles; picked up they grow to their normal size.</summary>
        public const float ShelfScale=1f; // full tennis-ball size in the bucket too (carbon is 6.6 cm)
        Vector3 fullScale;
        void ShowShelfSize(bool onShelf)
        {
            if(fullScale==Vector3.zero) return;
            transform.localScale=fullScale*(onShelf?ShelfScale:1);
            if(symbolLabel!=null) symbolLabel.gameObject.SetActive(!onShelf); // the beaker's name plate says what they are
        }
        public bool TakeFromShelf()
        {
            if(!OnShelf) return true;
            if(!lab.Inventory.Transfer(TokenId,AtomInventory.Shelf,Owner)) return false;
            OnShelf=false; ShowShelfSize(false); lab.RefreshStock(); return true;
        }
        public void ReturnHome()
        {
            if(Grab.isSelected) return;
            if(!OnShelf && !lab.Inventory.Transfer(TokenId,Owner,AtomInventory.Shelf)) return;
            // Unlimited baskets: the socket already holds a fresh atom, so this one leaves play.
            if(lab.Inventory.IsRetired(TokenId)) { lab.RetireLooseAtom(this); return; }
            OnShelf=true; transform.position=lab.Environment.SocketPosition(lab.Inventory.Tokens[TokenId].Socket); transform.rotation=Quaternion.identity; ShowShelfSize(true); lab.RefreshStock();
        }
        public void FaceSymbol(Camera camera)
        { if(camera!=null && symbolLabel!=null) symbolLabel.transform.position=transform.position+(camera.transform.position-transform.position).normalized*(transform.lossyScale.x*.5f+.012f); }
        void Update()
        {
            if(lab==null || Grab==null) return;
            if(Grab.isSelected) lab.PreviewAtom(this);
            else if(!OnShelf && (transform.position.y<.3f || transform.position.magnitude>12)) ReturnHome();
            // An atom taken from a beaker and left unused for a while disappears (the beaker is endless).
            else if(!OnShelf && ReleasedAt>0 && Time.time-ReleasedAt>lab.idleAtomSeconds) ReturnHome();
            FaceSymbol(Camera.main);
            if(outline!=null && outline.activeSelf && Camera.main!=null) outline.transform.rotation=Quaternion.LookRotation(transform.position-Camera.main.transform.position);
        }
    }
}
