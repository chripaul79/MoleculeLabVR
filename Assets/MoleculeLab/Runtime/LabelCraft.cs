using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MoleculeLab
{
    /// <summary>
    /// Text as real things you would find in a lab: paper labels held down with tape, stickers, embossed label tape, engraved
    /// metal plates, museum placards and a small LCD read-out. Each is a thin physical piece on a surface with the text printed on it.
    /// Flat pieces are built in a "label space" whose face is local -z and whose "up" is local +y (as TextMeshPro);
    /// <see cref="Flat"/> lays that space on a tabletop, readable from the front (-z) of the parent.
    /// </summary>
    public static class LabelCraft
    {
        public static readonly Color PaperWhite=new Color(.97f,.96f,.91f), PaperCream=new Color(.96f,.92f,.8f), Ink=new Color(.1f,.14f,.32f),
            TapeColour=new Color(.86f,.8f,.6f), Graphite=new Color(.12f,.12f,.13f), Aluminium=new Color(.78f,.79f,.8f);
        /// <summary>Lying on a horizontal surface, text readable from the front (-z).</summary>
        public static readonly Quaternion Flat=Quaternion.Euler(90,0,0);

        static Transform Root(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var root=new GameObject(name).transform; root.SetParent(parent,false); root.localPosition=position; root.localRotation=rotation; return root;
        }
        /// <summary>A thin slab in label space: its back on the surface (z = 0), its face towards -z.</summary>
        static GameObject Slab(Transform root,string name,Vector3 centre,Vector3 size,Color colour,float spin=0)
        {
            var slab=LabVisuals.Shape(name,PrimitiveType.Cube,root,new Vector3(centre.x,centre.y,-size.z*.5f+centre.z),size,colour,false);
            if(spin!=0) slab.transform.localRotation=Quaternion.Euler(0,0,spin);
            return slab;
        }
        /// <summary>Printed text on a label face (no billboarding).</summary>
        public static TextMeshPro Print(Transform root,string text,Vector3 position,Vector2 size,float fontSize,Color colour,TMP_FontAsset font=null,bool autoSize=true)
        {
            var label=LabVisuals.Label(root,text,position,fontSize*.1f);
            var face=label.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Object.Destroy(face); }
            label.transform.localRotation=Quaternion.identity; label.rectTransform.sizeDelta=size; label.color=colour;
            if(font!=null) label.font=font;
            if(autoSize) { label.enableAutoSizing=true; label.fontSizeMax=fontSize; label.fontSizeMin=fontSize*.35f; }
            label.margin=Vector4.zero; return label;
        }

        /// <summary>A sheet of paper with handwriting, held down by two strips of tape across its top corners.</summary>
        public static TextMeshPro PaperNote(Transform parent,string name,Vector3 position,Quaternion rotation,Vector2 size,string text,float fontSize,Color? ink=null,float tilt=0,bool tape=true)
        {
            var root=Root(parent,name,position,rotation*Quaternion.Euler(0,0,tilt));
            Slab(root,"Paper",Vector3.zero,new Vector3(size.x,size.y,.0008f),PaperCream);
            if(tape)
                foreach(var side in new[]{-1f,1f})
                    Slab(root,"Tape",new Vector3(side*(size.x*.5f-.012f),size.y*.5f-.008f,-.0008f),new Vector3(.05f,.017f,.0006f),TapeColour,side*-38f);
            return Print(root,text,new Vector3(0,-.002f,-.0012f),size-new Vector2(.02f,.016f),fontSize,ink??Ink,ClassroomBuilder.Handwriting());
        }
        /// <summary>A printed sticker with a coloured band along its top (like a reagent label).</summary>
        public static TextMeshPro Sticker(Transform parent,string name,Vector3 position,Quaternion rotation,Vector2 size,string text,Color band,float fontSize)
        {
            var root=Root(parent,name,position,rotation);
            Slab(root,"Sticker",Vector3.zero,new Vector3(size.x,size.y,.0006f),PaperWhite);
            Slab(root,"Colour band",new Vector3(0,size.y*.5f-size.y*.12f,-.0006f),new Vector3(size.x,size.y*.24f,.0003f),band);
            return Print(root,text,new Vector3(0,-size.y*.1f,-.001f),new Vector2(size.x-.01f,size.y*.7f),fontSize,new Color(.1f,.1f,.12f));
        }
        /// <summary>Embossed label-maker tape: a black strip with raised white capitals.</summary>
        public static TextMeshPro LabelTape(Transform parent,string name,Vector3 position,Quaternion rotation,string text,float height=.022f)
        {
            text=text.ToUpperInvariant();
            var width=text.Length*height*.62f+height;
            var root=Root(parent,name,position,rotation);
            Slab(root,"Tape",Vector3.zero,new Vector3(width,height,.0012f),Graphite);
            var label=Print(root,"<b>"+text+"</b>",new Vector3(0,0,-.0016f),new Vector2(width-height*.4f,height),height*6.2f,new Color(.95f,.95f,.93f),null,false);
            label.characterSpacing=8; label.alignment=TextAlignmentOptions.Center;
            return label;
        }
        /// <summary>An engraved aluminium plate held by two screws.</summary>
        public static TextMeshPro MetalPlate(Transform parent,string name,Vector3 position,Quaternion rotation,Vector2 size,string text,float fontSize)
        {
            var root=Root(parent,name,position,rotation);
            Slab(root,"Plate",Vector3.zero,new Vector3(size.x,size.y,.0025f),Aluminium);
            foreach(var side in new[]{-1f,1f})
                LabVisuals.Shape("Screw",PrimitiveType.Sphere,root,new Vector3(side*(size.x*.5f-.012f),0,-.0025f),new Vector3(.009f,.009f,.003f),new Color(.45f,.46f,.48f),false);
            return Print(root,text,new Vector3(0,0,-.003f),new Vector2(size.x-.05f,size.y-.01f),fontSize,new Color(.12f,.12f,.14f));
        }
        /// <summary>
        /// A museum placard on the table: a small holder with a card leaning back towards the viewer. Built in table space
        /// (y up, viewer at -z); returns the card text and the placard's root.
        /// </summary>
        public static TextMeshPro Placard(Transform parent,string name,Vector3 position,Quaternion rotation,Vector2 size,out Transform root)
        {
            root=Root(parent,name,position,rotation);
            LabVisuals.Shape("Holder",PrimitiveType.Cube,root,new Vector3(0,.012f,0),new Vector3(size.x+.012f,.024f,.04f),new Color(.16f,.13f,.11f),false);
            var card=Root(root,"Card",new Vector3(0,.026f,0),Quaternion.Euler(55,0,0));
            Slab(card,"Card face",new Vector3(0,size.y*.5f,0),new Vector3(size.x,size.y,.002f),PaperWhite);
            return Print(card,"",new Vector3(0,size.y*.5f,-.0025f),size-new Vector2(.012f,.008f),size.y*2.6f,new Color(.12f,.12f,.14f));
        }
        /// <summary>A small LCD read-out on a little stand, green text on a dark screen (for messages that change).</summary>
        public static TextMeshPro Lcd(Transform parent,string name,Vector3 position,Quaternion rotation,Vector2 size)
        {
            var root=Root(parent,name,position,rotation);
            LabVisuals.Shape("Stand",PrimitiveType.Cube,root,new Vector3(0,.008f,.01f),new Vector3(size.x*.5f,.016f,.04f),new Color(.3f,.31f,.33f),false);
            var screen=Root(root,"Display",new Vector3(0,.016f,0),Quaternion.Euler(20,0,0));
            Slab(screen,"Bezel",new Vector3(0,size.y*.5f+.006f,0),new Vector3(size.x+.012f,size.y+.012f,.008f),new Color(.2f,.21f,.23f));
            Slab(screen,"Screen",new Vector3(0,size.y*.5f+.006f,-.008f),new Vector3(size.x,size.y,.0006f),new Color(.03f,.07f,.05f));
            var text=Print(screen,"",new Vector3(0,size.y*.5f+.006f,-.0092f),size-new Vector2(.008f,.006f),size.y*3.2f,new Color(.45f,1f,.55f));
            text.fontStyle=FontStyles.Bold; return text;
        }

        /// <summary>
        /// A sticker wrapped around a vertical cylinder (a beaker): a curved paper patch with a coloured band and text bent to
        /// follow the glass. <paramref name="parent"/>'s origin is on the cylinder axis; the sticker is centred on its front (-z).
        /// </summary>
        public static TextMeshPro CurvedSticker(Transform parent,string name,float radius,float centreHeight,Vector2 size,string text,Color band,float fontSize)
        {
            var root=Root(parent,name,Vector3.zero,Quaternion.identity);
            var halfAngle=size.x*.5f/radius;
            Patch(root,"Sticker",radius,centreHeight-size.y*.5f,centreHeight+size.y*.5f,halfAngle,PaperWhite);
            Patch(root,"Colour band",radius+.0004f,centreHeight+size.y*.26f,centreHeight+size.y*.5f,halfAngle,band);
            var label=Print(root,text,new Vector3(0,centreHeight-size.y*.1f,0),new Vector2(size.x-.012f,size.y*.62f),fontSize,new Color(.1f,.1f,.12f));
            label.gameObject.AddComponent<WrapTextOnCylinder>().radius=radius+.0008f;
            return label;
        }
        /// <summary>A curved strip of a cylinder's surface, facing outwards, centred on -z.</summary>
        static GameObject Patch(Transform parent,string name,float radius,float bottom,float top,float halfAngle,Color colour,int segments=14)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var triangles=new List<int>();
            for(var i=0;i<=segments;i++)
            {
                var a=-halfAngle+2*halfAngle*i/segments; var normal=new Vector3(Mathf.Sin(a),0,-Mathf.Cos(a));
                vertices.Add(normal*radius+Vector3.up*bottom); vertices.Add(normal*radius+Vector3.up*top); normals.Add(normal); normals.Add(normal);
                if(i==segments) continue;
                var b=i*2; triangles.AddRange(new[]{b,b+1,b+2,b+1,b+3,b+2}); // clockwise seen from outside
            }
            var mesh=new Mesh{name=name}; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=LabVisuals.Material(colour);
            return go;
        }
    }

    /// <summary>
    /// Bends a TextMeshPro text around a vertical cylinder whose axis is the text's local y axis, so printing on a curved
    /// sticker follows the glass. Re-applied whenever TextMeshPro rebuilds the text.
    /// </summary>
    public sealed class WrapTextOnCylinder : MonoBehaviour
    {
        public float radius=.16f;
        TextMeshPro text;
        bool pending=true, wrapping;
        void Awake() { text=GetComponent<TextMeshPro>(); }
        void OnEnable() { TMPro_EventManager.TEXT_CHANGED_EVENT.Add(Changed); pending=true; }
        void OnDisable() { TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(Changed); }
        void Changed(Object changed) { if(!wrapping && changed==text) pending=true; }
        void LateUpdate() { if(pending) Wrap(); }
        public void Wrap()
        {
            if(text==null) return;
            pending=false; wrapping=true;
            text.ForceMeshUpdate();
            foreach(var info in text.textInfo.meshInfo)
            {
                var vertices=info.vertices; if(vertices==null) continue;
                for(var i=0;i<vertices.Length;i++)
                {
                    var v=vertices[i]; var a=v.x/radius;
                    vertices[i]=new Vector3(Mathf.Sin(a)*(radius-v.z),v.y,-Mathf.Cos(a)*(radius-v.z));
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            if(text.mesh!=null) text.mesh.RecalculateBounds();
            wrapping=false;
        }
    }
}
