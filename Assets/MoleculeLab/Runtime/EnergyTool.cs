using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
namespace MoleculeLab
{
    public sealed class EnergyTool : MonoBehaviour
    {
        LabController lab;
        XRGrabInteractable grab;
        Transform tip;
        float energy;
        MoleculeView chargingTarget;
        public void Initialize(LabController owner)
        {
            lab=owner;
            LabVisuals.Shape("Handle",PrimitiveType.Cylinder,transform,Vector3.zero,new Vector3(.045f,.09f,.045f),new Color(.23f,.3f,.38f));
            tip=LabVisuals.Shape("Energy tip",PrimitiveType.Sphere,transform,Vector3.up*.13f,Vector3.one*.055f,new Color(1,.65f,.1f)).transform;
            var rb=gameObject.AddComponent<Rigidbody>(); rb.useGravity=false; rb.isKinematic=true;
            grab=gameObject.AddComponent<XRGrabInteractable>(); grab.throwOnDetach=false;
            grab.lastSelectExited.AddListener(_=> { energy=0; chargingTarget=null; lab.EnergyProgress(0); });
        }
        public void Cancel()
        {
            if(grab!=null && grab.interactionManager!=null)
                foreach(var hand in grab.interactorsSelecting.ToArray()) grab.interactionManager.SelectCancel(hand,grab);
            energy=0; chargingTarget=null;
        }
        public void ReturnHome() { Cancel(); transform.position=lab.WandHome; transform.rotation=Quaternion.identity; }
        void Update()
        {
            var molecule=lab.Selected;
            var available=lab.Mode==LabMode.Inspect && molecule!=null && molecule.State.Complete && molecule.Definition.id=="ethane";
            if(!available && grab.isSelected) Cancel();
            grab.enabled=available;
            foreach(Transform child in transform) child.gameObject.SetActive(available);
            if(chargingTarget!=molecule) { energy=0; chargingTarget=molecule; }
            var valid=grab.isSelected && lab.Mode==LabMode.Inspect && molecule!=null && molecule.State.Complete && !molecule.Grab.isSelected && molecule.Definition.id=="ethane";
            if(valid)
            {
                var bond=molecule.Definition.bonds[molecule.Definition.rotatableBond];
                valid=Vector3.Distance(tip.position,(molecule.SitePosition(bond.a)+molecule.SitePosition(bond.b))*.5f)<.12f;
            }
            energy=valid?Mathf.Min(1,energy+Time.deltaTime*.5f):Mathf.Max(0,energy-Time.deltaTime);
            lab.EnergyProgress(energy);
            if(grab.isSelected) lab.Tag?.Request(transform,"Energy wand",valid?"Breaking the C-C bond: "+Mathf.RoundToInt(energy*100)+"%":"Hold the tip at ethane's middle C-C bond for two seconds to break it.",1,.4f);
            if(energy>=1) { lab.BreakSelected(); energy=0; }
            if(!grab.isSelected && (transform.position.y<.3f || transform.position.magnitude>4)) ReturnHome();
        }
    }
}
