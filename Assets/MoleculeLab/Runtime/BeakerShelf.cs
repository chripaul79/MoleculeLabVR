using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace MoleculeLab
{
    /// <summary>
    /// A shelf of glass beakers, one per element. Each beaker shows a few atoms; take one and the beaker refills at once,
    /// so it never runs out. Beakers are drawn procedurally unless LabController.beakerPrefab is assigned (e.g. from an
    /// imported lab-items asset pack), in which case that model is used and scaled to fit.
    /// </summary>
    public sealed class BeakerShelf : MonoBehaviour
    {
        public const int AtomsPerBeaker=12;
        public const float BeakerRadius=.06f, BeakerHeight=.15f;
        public Transform[] Sockets { get; private set; }
        public string[] SocketSymbols { get; private set; }
        static readonly Color Board=new Color(.9f,.89f,.85f), Metal=new Color(.48f,.55f,.56f), GlassEdge=new Color(.78f,.9f,.95f);
        /// <summary>A pile of marbles: 6 on the bottom (one in the middle), 4 above, 2 on top.</summary>
        static readonly Vector3[] AtomSpots=Pile();
        static Vector3[] Pile()
        {
            var spots=new System.Collections.Generic.List<Vector3>{new Vector3(0,.016f,0)};
            void Ring(int n,float radius,float y,float turn) { for(var i=0;i<n;i++) { var a=(turn+i*360f/n)*Mathf.Deg2Rad; spots.Add(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius)); } }
            Ring(5,.03f,.016f,18); Ring(4,.02f,.04f,45); Ring(2,.012f,.062f,0);
            return spots.ToArray();
        }
        static Mesh glassMesh;
        static Material glassMaterial;

        /// <param name="tiers">Elements per shelf tier (bottom first), up to three per tier.</param>
        public static BeakerShelf Create(Transform workstation,string name,Vector3 localPosition,float yaw,ElementDefinition[][] tiers,GameObject beakerPrefab,Material wood=null,Material metal=null,GameObject table=null)
        {
            var go=new GameObject(name); go.transform.SetParent(workstation,false);
            go.transform.localPosition=localPosition; go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var shelf=go.AddComponent<BeakerShelf>(); shelf.Build(tiers,beakerPrefab,table);
            // Boards and uprights in the lab pack's wood and metal, so the shelf matches the room's furniture.
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if(r.name=="Shelf upright" && metal!=null) r.sharedMaterial=metal;
            }
            return shelf;
        }
        public const float TableHeight=.8f;
        void Build(ElementDefinition[][] tiers,GameObject beakerPrefab,GameObject table)
        {
            var sockets=new List<Transform>(); var symbols=new List<string>();
            // On a lab-pack table: one row of beakers per tier, front row nearest the player. Without one: the two-tier shelf.
            var floor=-transform.position.y; var depth=tiers.Length>1?.62f:.42f;
            var onTable=table!=null && FitModel(table,transform,"Beaker table",new Vector3(0,floor,0),new Vector3(.95f,TableHeight,depth))!=null;
            if(!onTable) foreach(var x in new[]{-.55f,.55f}) LabVisuals.Shape("Shelf upright",PrimitiveType.Cube,transform,new Vector3(x,-.16f,.2f),new Vector3(.035f,1.3f,.035f),Metal,true);
            for(var tier=0;tier<tiers.Length;tier++)
            {
                var y=onTable?floor+TableHeight:-.02f+tier*.34f; var z=onTable?(tier-(tiers.Length-1)*.5f)*.27f:tier*.12f;
                if(!onTable) LabVisuals.Shape("Shelf board",PrimitiveType.Cube,transform,new Vector3(0,y,z),new Vector3(1.05f,.025f,.3f),Board,true);
                for(var column=0;column<tiers[tier].Length;column++)
                {
                    var element=tiers[tier][column]; if(element==null) continue;
                    var beaker=new GameObject("Beaker "+element.symbol).transform; beaker.SetParent(transform,false);
                    beaker.localPosition=new Vector3((column-1)*(onTable?.29f:.32f),y+(onTable?.002f:.0125f),z);
                    if(beakerPrefab!=null) PlaceModel(beaker,beakerPrefab); else DrawGlass(beaker);
                    for(var i=0;i<AtomsPerBeaker;i++)
                    {
                        var socket=new GameObject("Atom spot "+element.symbol+" "+i).transform; socket.SetParent(beaker,false);
                        socket.localPosition=AtomSpots[i%AtomSpots.Length]; sockets.Add(socket); symbols.Add(element.symbol);
                    }
                    // Name plate on the shelf in front of the beaker, tilted up so it reads from standing height without covering the atoms.
                    var label=LabVisuals.Label(beaker,"<b>"+element.symbol+"</b>  <size=70%>"+element.displayName+"</size>",new Vector3(0,.02f,-BeakerRadius-.04f),.034f);
                    var face=label.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
                    label.transform.localRotation=Quaternion.Euler(50,0,0);
                    label.rectTransform.sizeDelta=new Vector2(.28f,.06f);
                    label.color=onTable?Color.white:new Color(.1f,.14f,.2f); // readable on the wooden table / the light shelf
                    if(onTable) { label.outlineWidth=.25f; label.outlineColor=new Color32(30,20,10,255); }
                }
            }
            Sockets=sockets.ToArray(); SocketSymbols=symbols.ToArray();
        }
        /// <summary>An asset-pack model stretched to <paramref name="size"/>, standing on <paramref name="bottomCentre"/>, front towards the player (-z), with a solid box.</summary>
        static Transform FitModel(GameObject prefab,Transform parent,string name,Vector3 bottomCentre,Vector3 size)
        {
            var holder=new GameObject(name).transform; holder.SetParent(parent,false); holder.localPosition=bottomCentre;
            var model=Instantiate(prefab,holder,false); model.name=prefab.name; model.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var c in model.GetComponentsInChildren<Collider>(true)) { c.enabled=false; Destroy(c); }
            foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
            var any=false; var bounds=new Bounds();
            foreach(var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(!r.TryGetComponent<MeshFilter>(out var f) || f.sharedMesh==null) continue;
                var m=holder.worldToLocalMatrix*r.transform.localToWorldMatrix; var lb=f.sharedMesh.bounds;
                for(var i=0;i<8;i++)
                {
                    var p=m.MultiplyPoint3x4(lb.center+Vector3.Scale(lb.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(!any) { bounds=new Bounds(p,Vector3.zero); any=true; } else bounds.Encapsulate(p);
                }
            }
            if(!any || bounds.size.x<1e-3f || bounds.size.y<1e-3f || bounds.size.z<1e-3f) { Destroy(holder.gameObject); return null; }
            // Scale in holder space: wrap the rotated model in a scaler so the stretch follows the holder's axes.
            var scaler=new GameObject("Fit").transform; scaler.SetParent(holder,false); model.transform.SetParent(scaler,true);
            var scale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            scaler.localScale=scale; scaler.localPosition=new Vector3(-bounds.center.x*scale.x,-bounds.min.y*scale.y,-bounds.center.z*scale.z);
            var box=holder.gameObject.AddComponent<BoxCollider>(); box.center=new Vector3(0,size.y*.5f,0); box.size=size;
            return holder;
        }
        /// <summary>See-through glass: an unlit transparent canvas material (always present in builds that use UI) on a cylinder.</summary>
        static void DrawGlass(Transform beaker)
        {
            if(glassMesh==null)
            {
                var temp=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                glassMesh=Instantiate(temp.GetComponent<MeshFilter>().sharedMesh); glassMesh.name="Beaker glass";
                glassMesh.colors=Enumerable.Repeat(Color.white,glassMesh.vertexCount).ToArray();
                Destroy(temp);
            }
            if(glassMaterial==null) { glassMaterial=new Material(Canvas.GetDefaultCanvasMaterial()); glassMaterial.name="Beaker glass"; glassMaterial.color=new Color(.75f,.92f,1f,.07f); }
            var glass=new GameObject("Glass"); glass.transform.SetParent(beaker,false);
            glass.transform.localPosition=new Vector3(0,BeakerHeight*.5f,0); glass.transform.localScale=new Vector3(BeakerRadius*2,BeakerHeight*.5f,BeakerRadius*2);
            glass.AddComponent<MeshFilter>().sharedMesh=glassMesh; glass.AddComponent<MeshRenderer>().sharedMaterial=glassMaterial;
            // Crisp rim, base and a spout so the shape reads as glassware from any angle.
            Ring(beaker,"Rim",BeakerHeight,BeakerRadius,.004f);
            Ring(beaker,"Base",.004f,BeakerRadius,.003f);
            LabVisuals.Shape("Spout",PrimitiveType.Cube,beaker,new Vector3(0,BeakerHeight-.004f,BeakerRadius+.004f),new Vector3(.02f,.006f,.012f),GlassEdge,false);
            for(var i=1;i<=3;i++) LabVisuals.Shape("Graduation",PrimitiveType.Cube,beaker,new Vector3(BeakerRadius*.62f,i*.035f,-BeakerRadius*.78f),new Vector3(.018f,.002f,.002f),GlassEdge,false);
        }
        static void Ring(Transform parent,string name,float y,float radius,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent,false);
            line.useWorldSpace=false; line.loop=true; line.positionCount=28; line.widthMultiplier=width; line.sharedMaterial=LabVisuals.Material(GlassEdge);
            for(var i=0;i<28;i++) { var a=i*Mathf.PI*2/28; line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius)); }
        }
        /// <summary>An imported beaker model, scaled so its largest size matches the procedural beaker. Colliders are removed so atoms can be lifted out.</summary>
        static void PlaceModel(Transform beaker,GameObject prefab)
        {
            var model=Instantiate(prefab,beaker,false); model.name="Beaker model";
            // Clearer glass than the pack's frosted look, so the atoms inside are easy to see.
            foreach(var r in model.GetComponentsInChildren<Renderer>())
            {
                foreach(var m in r.materials)
                    if(m.renderQueue>=2900 || (m.HasProperty("_Surface") && m.GetFloat("_Surface")>.5f))
                    {
                        m.SetColor("_BaseColor",new Color(.86f,.95f,1f,.2f)); if(m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",.97f);
                        // Plain alpha blending: premultiplied glass looks like white plastic.
                        m.SetFloat("_Blend",0); m.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    }
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            var renderers=model.GetComponentsInChildren<Renderer>(); if(renderers.Length==0) return;
            var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            var size=Mathf.Max(bounds.size.y,Mathf.Max(bounds.size.x,bounds.size.z));
            if(size>1e-4f) model.transform.localScale*=BeakerHeight/size;
            bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            // Stand it on the shelf, centred under the atom spots whatever the model's pivot.
            model.transform.position+=new Vector3(beaker.position.x-bounds.center.x,beaker.position.y-bounds.min.y,beaker.position.z-bounds.center.z);
            foreach(var r in renderers) r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            // A thin rim ring so the clear glass still reads as a beaker from across the room.
            bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            var unit=Mathf.Max(1e-4f,beaker.lossyScale.x);
            var radius=Mathf.Min(bounds.extents.x,bounds.extents.z)/unit*.92f;
            Ring(beaker,"Rim",(bounds.max.y-beaker.position.y)/unit-.004f,radius,.0035f);
        }
    }
}
