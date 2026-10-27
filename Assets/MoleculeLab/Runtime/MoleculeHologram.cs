using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoleculeLab
{
    /// <summary>
    /// A floating, see-through info screen that follows a gallery exhibit while it is held: name, formula,
    /// PubChem's description of what the compound is, and what it is commonly used for.
    /// Built from a world-space canvas (UI shaders support transparency on Quest without extra shader variants).
    /// </summary>
    public sealed class MoleculeHologram : MonoBehaviour
    {
        static readonly Color Glow=new Color(.35f,1f,.92f), Glass=new Color(.02f,.13f,.17f,.72f), Header=new Color(.35f,1f,.92f,.16f);
        public GalleryExhibit Target { get; private set; }
        public string EntryId { get; private set; }
        public bool Visible => gameObject.activeSelf;
        public string Title => title.text;
        public string Body => body.text;
        public string Footer => footer.text;
        TextMeshProUGUI title,subtitle,body,footer;
        RectTransform scan;
        LabController lab;
        float hideAt=-1, shownAt;

        public static MoleculeHologram Create(LabController owner,TMP_FontAsset font)
        {
            var go=new GameObject("Molecule hologram",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(owner.transform,false);
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=Camera.main; canvas.sortingOrder=5;
            var rect=(RectTransform)go.transform; rect.sizeDelta=new Vector2(900,640); go.transform.localScale=Vector3.one*.0005f;
            var hologram=go.AddComponent<MoleculeHologram>(); hologram.lab=owner; hologram.Build(font); go.SetActive(false);
            return hologram;
        }
        RectTransform Rect(string name,Vector2 position,Vector2 size,Transform parent=null)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent??transform,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.anchoredPosition=position; r.sizeDelta=size; return r;
        }
        Image Panel(string name,Vector2 position,Vector2 size,Color color)
        { var image=Rect(name,position,size).gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image; }
        TextMeshProUGUI Text(string name,Vector2 position,Vector2 size,float fontSize,Color color,TMP_FontAsset font,TextAlignmentOptions alignment)
        {
            var text=Rect(name,position,size).gameObject.AddComponent<TextMeshProUGUI>(); if(font!=null) text.font=font;
            text.fontSize=fontSize; text.color=color; text.alignment=alignment; text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Ellipsis; return text;
        }
        void Build(TMP_FontAsset font)
        {
            Panel("Glass",Vector2.zero,new Vector2(900,640),Glass);
            Panel("Header band",new Vector2(0,262),new Vector2(900,116),Header);
            foreach(var (p,s) in new[]{(new Vector2(0,318),new Vector2(900,4)),(new Vector2(0,-318),new Vector2(900,4)),(new Vector2(-448,0),new Vector2(4,640)),(new Vector2(448,0),new Vector2(4,640))})
                Panel("Frame",p,s,Glow);
            foreach(var x in new[]{-436f,436f}) foreach(var y in new[]{306f,-306f}) Panel("Corner",new Vector2(x,y),new Vector2(28,28),Glow);
            title=Text("Title",new Vector2(0,282),new Vector2(840,64),46,Color.white,font,TextAlignmentOptions.Left);
            subtitle=Text("Subtitle",new Vector2(0,232),new Vector2(840,40),24,Glow,font,TextAlignmentOptions.Left);
            body=Text("Body",new Vector2(0,-20),new Vector2(840,440),24,new Color(.88f,.97f,.97f),font,TextAlignmentOptions.TopLeft);
            footer=Text("Footer",new Vector2(0,-290),new Vector2(840,36),17,new Color(.55f,.85f,.85f),font,TextAlignmentOptions.Left);
            scan=Panel("Scan line",Vector2.zero,new Vector2(892,3),new Color(.35f,1f,.92f,.35f)).rectTransform;
        }

        public void Show(GalleryExhibit exhibit)
        {
            Target=exhibit; EntryId=exhibit.Entry.id; hideAt=-1; shownAt=Time.time;
            var entry=exhibit.Entry;
            title.text=entry.name;
            subtitle.text=entry.formula+(entry.cid>0?"   |   PubChem CID "+entry.cid:"")+(entry.timesBuilt>1?"   |   built "+entry.timesBuilt+" times":"");
            body.text="<color=#59FFEB>Asking PubChem about this molecule...</color>";
            footer.text="";
            gameObject.SetActive(true); Follow(true);
        }
        /// <summary>Fills in PubChem's answer, if the hologram is still showing that exhibit.</summary>
        public void SetInfo(string entryId,MoleculeInfo info,string iupacName)
        {
            if(entryId!=EntryId || Target==null) return;
            var entry=Target.Entry;
            if(info!=null && info.cid>0) subtitle.text=entry.formula+(string.IsNullOrEmpty(iupacName) || iupacName.Equals(entry.name,System.StringComparison.OrdinalIgnoreCase)?"":"   |   "+iupacName)+"   |   PubChem CID "+info.cid;
            if(info==null || info.status!=LookupStatus.Found)
            {
                body.text="<color=#59FFEB><b>What it is</b></color>\n"+(entry.detail=="Lab recipe"?"One of the lab's recipe molecules.":entry.detail)+
                    "\n\n<color=#FFB27A>"+(info!=null && !string.IsNullOrEmpty(info.message)?info.message:"PubChem has no record of this structure.")+"</color>";
                footer.text="PubChem data unavailable";
                return;
            }
            var text="<color=#59FFEB><b>What it is</b></color>\n"+(string.IsNullOrEmpty(info.description)?"PubChem has no description for this compound.":info.description);
            text+="\n\n<color=#59FFEB><b>Commonly used for</b></color>\n"+(info.uses==null || info.uses.Length==0?"PubChem lists no uses for this compound.":string.Join("\n",info.uses.Select(u=>"- "+u)));
            body.text=text;
            footer.text="Source: PubChem CID "+info.cid+(string.IsNullOrEmpty(info.descriptionSource)?"":"   |   description: "+info.descriptionSource);
        }
        public void HideLater(GalleryExhibit exhibit,float seconds) { if(exhibit==Target) hideAt=Time.time+seconds; }
        public void Hide() { Target=null; EntryId=null; gameObject.SetActive(false); }

        void Follow(bool snap)
        {
            var camera=Camera.main; if(Target==null) return;
            var anchor=Target.transform.position;
            var toCamera=camera!=null?camera.transform.position-anchor:Vector3.forward; toCamera.y=0;
            var side=toCamera.sqrMagnitude>1e-4f?Vector3.Cross(Vector3.up,toCamera.normalized):Vector3.right;
            // Beside and slightly above the molecule, so the screen never covers it.
            var goal=anchor+Vector3.up*.12f+side*.32f;
            transform.position=snap?goal:Vector3.Lerp(transform.position,goal,1-Mathf.Exp(-Time.deltaTime*10));
            if(camera!=null) transform.rotation=Quaternion.LookRotation(transform.position-camera.transform.position);
        }
        void LateUpdate()
        {
            if(Target==null || (hideAt>0 && Time.time>=hideAt)) { Hide(); return; }
            Follow(false);
            var reduced=lab!=null && lab.ReducedEffects;
            var appear=reduced?1:Mathf.Clamp01((Time.time-shownAt)/.18f);
            transform.localScale=new Vector3(.0005f,.0005f*Mathf.SmoothStep(.05f,1,appear),.0005f);
            scan.gameObject.SetActive(!reduced);
            if(!reduced) scan.anchoredPosition=new Vector2(0,310-Mathf.Repeat(Time.time*180,620));
        }
    }
}
