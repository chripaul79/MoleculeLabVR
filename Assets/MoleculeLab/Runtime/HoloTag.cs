using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoleculeLab
{
    /// <summary>
    /// A small see-through hologram tag, styled like the gallery hologram, for text that belongs to something that moves:
    /// the name of a molecule in your hand, or a hint while you build. Text that belongs to a place is printed on a surface
    /// instead (stickers, plates, the tablet). Several things may ask for the tag in one frame; the most important request wins.
    /// </summary>
    public sealed class HoloTag : MonoBehaviour
    {
        static readonly Color Glow=new Color(.35f,1f,.92f), Glass=new Color(.02f,.13f,.17f,.72f), BodyText=new Color(.88f,.97f,.97f);
        const float Scale=.00042f;
        public bool Visible => gameObject.activeSelf;
        public string Title => title.text;
        public string Body => body.text;
        public Transform Target => target;
        TextMeshProUGUI title, body;
        RectTransform scan;
        Transform target;
        LabController lab;
        int priority=-1;
        float until=-1, shownAt;

        public static HoloTag Create(LabController owner,TMP_FontAsset font,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(owner.transform,false);
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=Camera.main; canvas.sortingOrder=6;
            ((RectTransform)go.transform).sizeDelta=new Vector2(640,210); go.transform.localScale=Vector3.one*Scale;
            var tag=go.AddComponent<HoloTag>(); tag.lab=owner; tag.Build(font); go.SetActive(false);
            return tag;
        }
        RectTransform Rect(string name,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(transform,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.anchoredPosition=position; r.sizeDelta=size; return r;
        }
        Image Panel(string name,Vector2 position,Vector2 size,Color color)
        { var image=Rect(name,position,size).gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; return image; }
        TextMeshProUGUI Text(string name,Vector2 position,Vector2 size,float fontSize,Color color,TMP_FontAsset font)
        {
            var text=Rect(name,position,size).gameObject.AddComponent<TextMeshProUGUI>(); if(font!=null) text.font=font;
            text.fontSize=fontSize; text.color=color; text.alignment=TextAlignmentOptions.TopLeft; text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Ellipsis; return text;
        }
        void Build(TMP_FontAsset font)
        {
            Panel("Glass",Vector2.zero,new Vector2(640,210),Glass);
            foreach(var (p,s) in new[]{(new Vector2(0,103),new Vector2(640,4)),(new Vector2(0,-103),new Vector2(640,4)),(new Vector2(-318,0),new Vector2(4,210)),(new Vector2(318,0),new Vector2(4,210))})
                Panel("Frame",p,s,Glow);
            foreach(var x in new[]{-308f,308f}) foreach(var y in new[]{93f,-93f}) Panel("Corner",new Vector2(x,y),new Vector2(20,20),Glow);
            title=Text("Title",new Vector2(0,62),new Vector2(590,56),38,Color.white,font);
            body=Text("Body",new Vector2(0,-30),new Vector2(590,124),24,BodyText,font);
            body.enableAutoSizing=true; body.fontSizeMin=16; body.fontSizeMax=24;
            scan=Panel("Scan line",Vector2.zero,new Vector2(632,3),new Color(.35f,1f,.92f,.3f)).rectTransform;
        }

        /// <summary>
        /// Shows text beside <paramref name="target"/> for at least <paramref name="seconds"/> (callers that want it to stay ask
        /// every frame). A request cannot replace a still-showing request of higher priority.
        /// </summary>
        public void Request(Transform target,string titleText,string bodyText,int priority,float seconds=.15f)
        {
            if(target==null) return;
            if(gameObject.activeSelf && Time.time<until && priority<this.priority) return;
            var fresh=!gameObject.activeSelf || target!=this.target;
            this.target=target; this.priority=priority; until=Time.time+seconds;
            if(title.text!=titleText) title.text=titleText;
            bodyText=bodyText??"";
            if(body.text!=bodyText) body.text=bodyText;
            if(fresh) { shownAt=Time.time; gameObject.SetActive(true); Follow(true); }
        }
        /// <summary>Hides the tag (only if it is showing for <paramref name="forTarget"/>, when given).</summary>
        public void Hide(Transform forTarget=null)
        {
            if(forTarget!=null && forTarget!=target) return;
            target=null; priority=-1; gameObject.SetActive(false);
        }
        void Follow(bool snap)
        {
            if(target==null) return;
            var camera=Camera.main; var anchor=target.position;
            var toCamera=camera!=null?camera.transform.position-anchor:Vector3.back; toCamera.y=0;
            var side=toCamera.sqrMagnitude>1e-4f?Vector3.Cross(Vector3.up,toCamera.normalized):Vector3.right;
            // Above and to one side, so it never covers what you are working on.
            var goal=anchor+Vector3.up*.16f+side*.2f;
            transform.position=snap?goal:Vector3.Lerp(transform.position,goal,1-Mathf.Exp(-Time.deltaTime*12));
            if(camera!=null) transform.rotation=Quaternion.LookRotation(transform.position-camera.transform.position);
        }
        void LateUpdate()
        {
            if(target==null || Time.time>=until) { Hide(); return; }
            Follow(false);
            var reduced=lab!=null && lab.ReducedEffects;
            var appear=reduced?1:Mathf.Clamp01((Time.time-shownAt)/.15f);
            transform.localScale=new Vector3(Scale,Scale*Mathf.SmoothStep(.05f,1,appear),Scale);
            scan.gameObject.SetActive(!reduced);
            if(!reduced) scan.anchoredPosition=new Vector2(0,100-Mathf.Repeat(Time.time*120,200));
        }
    }
}
