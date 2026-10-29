using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace MoleculeLab
{
    public sealed class InspectionHandle : MonoBehaviour
    {
        LabController lab;
        bool deform;
        XRSimpleInteractable interactable;
        IXRSelectInteractor hand;
        MoleculeView target;
        Vector3 initialUp, initialPosition;
        float angle, initialTorsion;
        GameObject visual;
        public void Initialize(LabController owner,bool deformation)
        {
            lab=owner; deform=deformation;
            visual=LabVisuals.Shape("Grip handle",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.07f,deform?new Color(.2f,.65f,1):new Color(1,.65f,.12f));
            interactable=gameObject.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(args=>
            {
                target=lab.Selected; if(target==null) return;
                hand=args.interactorObject; var pose=hand.GetAttachTransform(interactable);
                initialUp=pose.up; initialPosition=pose.position;
                initialTorsion=target.Torsion;
            });
            interactable.selectExited.AddListener(_=> { if(target!=null && deform) target.Bend(0); hand=null; target=null; });
        }
        public void Cancel()
        {
            if(interactable!=null && interactable.interactionManager!=null)
                foreach(var selector in interactable.interactorsSelecting.ToArray()) interactable.interactionManager.SelectCancel(selector,interactable);
        }
        void Update()
        {
            var selected=lab.Selected;
            var visible=lab.Mode==LabMode.Inspect && selected!=null && selected.State.Complete && selected.Definition.rotatableBond>=0;
            foreach(Transform child in transform) child.gameObject.SetActive(visible);
            interactable.enabled=visible;
            if(!visible) return;
            if(hand==null)
            {
                // Either side of the molecule as the player sees it, also while the molecule is held in the other hand.
                var right=Camera.main!=null?Vector3.ProjectOnPlane(Camera.main.transform.right,Vector3.up):Vector3.right;
                if(right.sqrMagnitude<1e-4f) right=Vector3.right;
                transform.position=selected.transform.position+right.normalized*(deform?-.26f:.26f)+Vector3.up*.03f; return;
            }
            if(target!=selected) { Cancel(); return; } // holding the molecule itself is fine
            var attach=hand.GetAttachTransform(interactable);
            lab.Tag?.Request(target.transform,deform?"Bend":"Twist",deform?"Move your hand up or down; let go and it springs back.":"Turn your wrist to rotate one end around the C-C bond.",2,.3f);
            if(deform)
            {
                var amount=Mathf.Clamp((attach.position.y-initialPosition.y)*100,-15,15); target.Bend(amount);
                if(Mathf.Abs(amount)>4) lab.Achieve("bend");
            }
            else
            {
                var bond=target.Definition.bonds[target.Definition.rotatableBond];
                var axis=(target.SitePosition(bond.b)-target.SitePosition(bond.a)).normalized;
                angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(initialUp,axis),Vector3.ProjectOnPlane(attach.up,axis),axis);
                target.Twist(initialTorsion+angle); if(Mathf.Abs(angle)>15) lab.Achieve("twist");
            }
        }
    }
}
