using TMPro;
using UnityEngine;
namespace MoleculeLab
{
    public sealed class BondPreview : MonoBehaviour
    {
        public LooseAtom Atom { get; private set; }
        public MoleculeView Target { get; private set; }
        public int Site { get; private set; }=-1;
        LabController lab;
        LineRenderer arc,ring;
        readonly LineRenderer[] guides=new LineRenderer[8];
        TextMeshPro label;
        bool wasReady;
        LineRenderer Line(string name,int points,float width)
        {
            var go=new GameObject(name); go.transform.SetParent(transform); var line=go.AddComponent<LineRenderer>();
            line.useWorldSpace=true; line.positionCount=points; line.widthMultiplier=width;
            line.sharedMaterial=LabVisuals.Material(new Color(.2f,.75f,.65f)); line.enabled=false; return line;
        }
        public void Initialize(LabController owner)
        {
            lab=owner; arc=Line("Teaching arc",13,.003f); ring=Line("Release target",33,.004f);
            for(var i=0;i<guides.Length;i++) guides[i]=Line("Bond direction "+i,17,.0018f);
            label=LabVisuals.Label(transform,"",Vector3.zero,.024f); label.gameObject.SetActive(false);
        }
        static void Circle(LineRenderer line,Vector3 center,Vector3 direction,float radius)
        {
            var rotation=Quaternion.FromToRotation(Vector3.forward,direction.normalized);
            for(var i=0;i<line.positionCount;i++) { var angle=i*Mathf.PI*2/(line.positionCount-1); line.SetPosition(i,center+rotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius); }
        }
        public void Show(LooseAtom atom)
        {
            if(Atom!=null && Atom!=atom && Atom.Grab.isSelected) return;
            if(!lab.FindPlacement(atom,Atom==atom?Target:null,Site,out var target,out var site,out var result)) { Clear(atom); return; }
            Atom=atom; Target=target; Site=site;
            var end=target.SitePosition(site); var start=atom.transform.position;
            arc.enabled=true; ring.enabled=true; label.gameObject.SetActive(true);
            var anchor=target.SitePosition(target.AnchorFor(site));
            Circle(ring,end,Camera.main!=null?end-Camera.main.transform.position:end-anchor,result.Ready?.048f:.03f);
            ring.widthMultiplier=result.Ready?.005f:.002f;
            for(var i=0;i<arc.positionCount;i++)
            {
                var t=i/(float)(arc.positionCount-1);
                var wobble=lab.ReducedEffects?Vector3.zero:Vector3.up*(Mathf.Sin(t*22+Time.time*5)*.009f*Mathf.Sin(t*Mathf.PI));
                arc.SetPosition(i,Vector3.Lerp(start,end,t)+wobble);
            }
            label.transform.position=end+Vector3.up*.09f;
            label.text=result.Ready?"Release to bond":result.Distance>lab.snapDistance?"Move closer":"Follow the bond direction";
            if(result.Ready && !wasReady) lab.ReadyCue(); wasReady=result.Ready;
        }
        public void Clear(LooseAtom atom=null)
        {
            if(atom!=null && Atom!=atom) return;
            Atom=null; Target=null; Site=-1; wasReady=false;
            foreach(var guide in guides) if(guide!=null) guide.enabled=false;
            if(arc!=null) { arc.enabled=false; ring.enabled=false; label.gameObject.SetActive(false); }
        }
        void LateUpdate()
        {
            foreach(var guide in guides) guide.enabled=false;
            var view=lab.Selected;
            if(lab.Mode!=LabMode.Build || view==null || view.State.Complete || view.PlacementLocked) return;
            var count=0;
            for(var site=0;site<view.Definition.atoms.Length && count<guides.Length;site++)
            {
                if(!view.State.CanPlace(site,view.Definition.atoms[site].element,out _)) continue;
                var index=view.AnchorFor(site); if(index<0) continue;
                var symbol=view.Definition.atoms[index].element.symbol;
                if(symbol!="C" && symbol!="O") continue;
                var anchor=view.SitePosition(index); var end=view.SitePosition(site); var direction=(end-anchor).normalized;
                var radius=view.Definition.atoms[index].element.radius*LabVisuals.AtomScale*view.transform.lossyScale.x;
                var patch=anchor+direction*(radius+.004f);
                var rotation=Quaternion.FromToRotation(Vector3.forward,direction);
                var side=rotation*Vector3.right*.035f; var up=rotation*Vector3.up*.035f;
                var guide=guides[count++]; guide.enabled=true;
                var points=new Vector3[17];
                for(var j=0;j<9;j++) { var angle=j*Mathf.PI/4; points[j]=patch+rotation*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.013f; }
                var rays=new[]{patch,end+side,patch,end-side,patch,end+up,patch,end-up};
                for(var j=0;j<8;j++) points[j+9]=rays[j];
                guide.SetPositions(points);
            }
            if(Atom!=null && !Atom.Grab.isSelected) Clear();
        }
    }
}
