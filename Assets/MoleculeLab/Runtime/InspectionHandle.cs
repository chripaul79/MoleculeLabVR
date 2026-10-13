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
            LabVisuals.Label(transform,deform?"DEFORM\nGrip + move vertically":"TWIST\nGrip + turn wrist",new Vector3(0,.1f,0),.022f);
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
                transform.position=selected.transform.position+new Vector3(deform?-.3f:.3f,0,0); return;
            }
            if(target!=selected || selected.Grab.isSelected) { Cancel(); return; }
            var attach=hand.GetAttachTransform(interactable);
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
