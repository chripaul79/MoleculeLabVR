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
        LabController lab;
        GameObject outline;
        TMPro.TextMeshPro symbolLabel;
        public void Initialize(LabController owner,ElementDefinition element,int tokenId)
        {
            lab=owner; Element=element; TokenId=tokenId;
            var body=gameObject.AddComponent<Rigidbody>(); body.mass=.05f; body.useGravity=false; body.isKinematic=true;
            Grab=gameObject.AddComponent<XRGrabInteractable>(); Grab.throwOnDetach=false;
            Grab.movementType=XRBaseInteractable.MovementType.Instantaneous;
            Grab.useDynamicAttach=true; Grab.matchAttachPosition=true; Grab.matchAttachRotation=true;
            Grab.smoothPosition=true; Grab.smoothPositionAmount=20; Grab.tightenPosition=.8f;
            // Hydrogen's 8 cm target fits inside the closest 8.7 cm shelf socket spacing.
            GetComponent<SphereCollider>().radius=element.symbol=="H"?.04f/(element.radius*2):.5f;
            Grab.firstSelectEntered.AddListener(_=>TakeFromShelf());
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
            symbolLabel=label; label.color=element.symbol=="H"?new Color(.06f,.12f,.14f):Color.white;
            label.rectTransform.sizeDelta=new Vector2(.9f,.6f);
        }
        public bool TakeFromShelf()
        {
            if(!OnShelf) return true;
            if(!lab.Inventory.Transfer(TokenId,AtomInventory.Shelf,Owner)) return false;
            OnShelf=false; lab.RefreshStock(); return true;
        }
        public void ReturnHome()
        {
            if(Grab.isSelected) return;
            if(!OnShelf && !lab.Inventory.Transfer(TokenId,Owner,AtomInventory.Shelf)) return;
            OnShelf=true; transform.position=lab.Environment.SocketPosition(TokenId); transform.rotation=Quaternion.identity; lab.RefreshStock();
        }
        public void FaceSymbol(Camera camera)
        { if(camera!=null && symbolLabel!=null) symbolLabel.transform.position=transform.position+(camera.transform.position-transform.position).normalized*(Element.radius+.012f); }
        void Update()
        {
            if(lab==null || Grab==null) return;
            if(Grab.isSelected) lab.PreviewAtom(this);
            else if(!OnShelf && (transform.position.y<.3f || transform.position.magnitude>4)) ReturnHome();
            FaceSymbol(Camera.main);
            if(outline!=null && outline.activeSelf && Camera.main!=null) outline.transform.rotation=Quaternion.LookRotation(transform.position-Camera.main.transform.position);
        }
    }
}
