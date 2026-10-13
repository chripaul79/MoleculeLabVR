using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
namespace MoleculeLab
{
    // Acquisition geometry is independent of the object's physics collider.
    public sealed class HandAtomCaster : MonoBehaviour,IInteractionCaster
    {
        public HandMenuRay owner;
        public IInteractionCaster fallback;
        public Transform castOrigin { get; set; }
        public Transform effectiveCastOrigin=>fallback.effectiveCastOrigin;
        public bool isInitialized=>fallback!=null && fallback.isInitialized;
        LooseAtom current;
        public static bool Prefer(float currentDistance,float challenger,float margin)=>challenger+margin<currentDistance;
        public LooseAtom FindAtom()
        {
            if(fallback==null || effectiveCastOrigin==null) return null;
            var point=effectiveCastOrigin.position;
            float Distance(LooseAtom a)=>Vector3.Distance(point,a.transform.position)-a.Element.radius;
            bool Valid(LooseAtom a)=>a!=null && a.Element!=null && a.isActiveAndEnabled && a.Grab!=null && a.Grab.enabled && !a.Grab.isSelected && Distance(a)<=owner.acquisitionDistance;
            if(!Valid(current)) current=null;
            var best=current!=null?Distance(current):float.MaxValue;
            foreach(var atom in LooseAtom.Active)
            {
                if(!Valid(atom)) continue;
                var d=Distance(atom);
                if(current==null || Prefer(best,d,owner.targetSwitchMargin)) { current=atom; best=d; }
            }
            return current;
        }
        public bool TryGetColliderTargets(XRInteractionManager manager,List<Collider> targets)
        {
            targets.Clear(); var atom=FindAtom();
            if(atom!=null) { targets.Add(atom.GetComponent<Collider>()); return true; }
            fallback.TryGetColliderTargets(manager,targets);
            // Loose atoms outside the explicit surface-distance rule cannot be near targets.
            targets.RemoveAll(c=>c==null || c.GetComponent<LooseAtom>()!=null);
            return targets.Count>0;
        }
    }
}
