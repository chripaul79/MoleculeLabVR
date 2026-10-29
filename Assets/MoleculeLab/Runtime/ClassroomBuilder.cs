using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoleculeLab
{
    /// <summary>
    /// Turns the academic lab into a bright classroom at runtime: painted walls, a tiled floor, big daylight windows,
    /// LED ceiling panels, a whiteboard, lab counters with glassware, desks and chairs from the imported lab pack, and a
    /// sci-fi gallery wing. Only visuals change: the room's own walls and floors keep their colliders (teleport, physics).
    /// </summary>
    public sealed class ClassroomBuilder : MonoBehaviour
    {
        // Room in environment space: x -3..3, z -1.65..3.35, 3 m high. Wall faces are 6 cm inside those lines.
        // Inner faces of the room. The sci-fi shell panels sit in front of the old walls, so these move inwards when it is built.
        const float GalleryFace=2.94f, OldRearFace=3.29f, OldCeiling=2.99f, ShellDepth=.08f;
        /// <summary>The classroom is this much wider on each side than the authored 6 m room (the gallery keeps its width).</summary>
        public const float Widen=.8f; // both side walls move out this far (more room for the reaction corner)
        float OldWallFace=GalleryFace;
        float WallFace=GalleryFace, RearFace=OldRearFace, CeilingUnderside=OldCeiling;
        /// <summary>True when the walls, floor and ceiling come from the lab pack.</summary>
        public bool SciFiShell { get; private set; }
        static readonly Color WallPaint=new Color(.96f,.95f,.91f), CeilingPaint=new Color(.99f,.99f,.98f), AccentPaint=new Color(.62f,.8f,.84f),
            WindowFrame=new Color(.97f,.97f,.96f), Sill=new Color(.74f,.6f,.44f), Daylight=new Color(.8f,.91f,1f), LedPanel=new Color(1f,.99f,.95f),
            BoardSurface=new Color(.985f,.985f,.98f), BoardFrame=new Color(.72f,.74f,.76f), Ink=new Color(.1f,.2f,.42f), FloorFallback=new Color(.86f,.82f,.74f);

        public int PlacedModels { get; private set; }
        public int Windows { get; private set; }
        public Transform Whiteboard { get; private set; }
        public IReadOnlyList<string> Skipped => skipped;
        readonly List<string> skipped=new List<string>();
        LabEnvironment environment;
        ClassroomKit kit;
        static Mesh quad;
        static Material unlit;

        public static ClassroomBuilder Create(LabEnvironment environment,ClassroomKit kit,Transform gallery)
        {
            var go=new GameObject("Bright classroom (runtime)"); go.transform.SetParent(environment.transform,false);
            var builder=go.AddComponent<ClassroomBuilder>(); builder.environment=environment; builder.kit=kit;
            builder.Build(gallery); return builder;
        }

        void Build(Transform gallery)
        {
            var shell=environment.GetComponentsInChildren<Transform>(true);
            if(kit!=null && kit.labWall!=null) WidenRoom(shell);
            Repaint(shell,gallery);
            HideOldDressing(shell,gallery);
            if(kit!=null && kit.labWall!=null) BuildShell(shell,gallery);
            Lighting();
            // Right wall: the reactions corner at the front, two windows further back.
            foreach(var z in new[]{1.0f,2.45f}) Window(new Vector3(WallFace,1.72f,z),1.25f,1.45f);
            BuildWhiteboard();
            ReactionsBoard();
            if(kit!=null) { Furniture(); if(gallery!=null) GalleryWing(gallery); }
            PeriodicTable(shell);
        }

        // ---------- surfaces ----------
        void Repaint(Transform[] shell,Transform gallery)
        {
            var wall=Paint(WallPaint,.2f); var ceiling=Paint(CeilingPaint,.1f);
            var floor=FloorMaterial(6,5); var galleryFloor=FloorMaterial(6,2.8f);
            foreach(var t in shell)
            {
                var r=t.GetComponent<Renderer>(); if(r==null) continue;
                switch(t.name)
                {
                    // Walls and ceilings don't cast shadows: the sun shining "through" the painted windows must reach the room.
                    case "Left wall": case "Right wall": case "Rear wall": case "Front wall":
                    case "Gallery left wall": case "Gallery right wall": case "Gallery end wall": case "Opening header": case "Opening pillar":
                        r.sharedMaterial=wall; r.shadowCastingMode=ShadowCastingMode.Off; break;
                    case "Ceiling": case "Gallery ceiling": r.sharedMaterial=ceiling; r.shadowCastingMode=ShadowCastingMode.Off; break;
                    case "Floor 6 x 5 metres": r.sharedMaterial=floor; break;
                    case "Gallery floor": r.sharedMaterial=galleryFloor; break;
                }
            }
            if(kit!=null && kit.labWall!=null) return; // the lab pack's wall modules have their own trim
            // A soft teal skirting band, the classroom's accent colour.
            var accent=Paint(AccentPaint,.25f);
            Strip("Skirting rear",new Vector3(0,.06f,RearFace-.006f),new Vector3(5.88f,.12f,.012f),accent);
            Strip("Skirting left",new Vector3(-WallFace+.006f,.06f,.85f),new Vector3(.012f,.12f,4.88f),accent);
            Strip("Skirting right",new Vector3(WallFace-.006f,.06f,.85f),new Vector3(.012f,.12f,4.88f),accent);
        }
        Material FloorMaterial(float x,float z)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name="Classroom floor";
            if(kit!=null && kit.floorTiles!=null)
            {
                material.SetTexture("_BaseMap",kit.floorTiles); material.SetTextureScale("_BaseMap",new Vector2(x*.5f,z*.5f)); material.color=new Color(1,1,1);
                if(kit.floorNormal!=null) { material.SetTexture("_BumpMap",kit.floorNormal); material.SetTextureScale("_BumpMap",new Vector2(x*.5f,z*.5f)); material.EnableKeyword("_NORMALMAP"); }
            }
            else material.color=FloorFallback;
            material.SetFloat("_Smoothness",.45f); return material;
        }
        static Material Paint(Color color,float smoothness)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=color; material.SetFloat("_Smoothness",smoothness); return material;
        }
        void Strip(string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform,false); go.transform.localPosition=position; go.transform.localScale=size;
            var r=go.GetComponent<Renderer>(); r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.Off;
        }

        // ---------- room shell: walls, floor and ceiling from the 3D Laboratory Environment pack ----------
        void BuildShell(Transform[] shell,Transform gallery)
        {
            // Walls: white panel modules tiled along the left, right and rear walls (the front is the opening to the gallery).
            var placed=TileWall("Wall panel right",new Vector3(OldWallFace,0,GalleryRoom.RoomStart+.08f),new Vector3(OldWallFace,0,OldRearFace),-90)
                      +TileWall("Wall panel left",new Vector3(-OldWallFace,0,GalleryRoom.RoomStart+.08f),new Vector3(-OldWallFace,0,OldRearFace),90)
                      +TileWall("Wall panel rear",new Vector3(-OldWallFace,0,OldRearFace),new Vector3(OldWallFace,0,OldRearFace),180);
            if(placed==0) return;
            SciFiShell=true;
            // Posters, signs and grilles fixed to the old walls move forward so the panels don't swallow them.
            ShiftWallDecor(shell);
            WallFace=OldWallFace-ShellDepth; RearFace=OldRearFace-ShellDepth;
            // Floor and ceiling: the pack's tiles and ceiling modules, laid out over the room (and the gallery).
            var roomCentre=(GalleryRoom.RoomStart+OldRearFace)*.5f; var roomSize=new Vector2(OldWallFace*2,OldRearFace-GalleryRoom.RoomStart);
            WorldFloor("Lab floor",new Vector3(0,0,roomCentre),roomSize);
            var roof=Tiles(kit.labRoof,"Lab ceiling",new Vector3(0,OldCeiling,roomCentre),roomSize,true);
            if(roof>0) CeilingUnderside=OldCeiling-Mathf.Min(roof,.12f);
            if(gallery!=null)
            {
                var length=GalleryRoom.RoomStart-GalleryRoom.RoomEnd-.06f; var mid=(GalleryRoom.RoomStart+GalleryRoom.RoomEnd+.06f)*.5f;
                WorldFloor("Lab gallery floor",new Vector3(0,0,mid),new Vector2(GalleryFace*2,length));
                Tiles(kit.labRoof,"Lab gallery ceiling",new Vector3(0,OldCeiling,mid),new Vector2(GalleryFace*2,length),true);
            }
            foreach(var t in shell)
            {
                if(t==null || t.IsChildOf(environment.workstation)) continue;
                var r=t.GetComponent<Renderer>(); if(r==null) continue;
                // The old surfaces stay solid (physics, teleport) but are no longer drawn where the shell covers them.
                if(t.name=="Floor 6 x 5 metres" || t.name=="Ceiling" || (gallery!=null && (t.name=="Gallery floor" || t.name=="Gallery ceiling"))) r.enabled=false;
            }
        }
        /// <summary>Wall modules side by side from <paramref name="from"/> to <paramref name="to"/>, backs against the old wall, full height.</summary>
        int TileWall(string name,Vector3 from,Vector3 to,float yaw)
        {
            var probe=Place(kit.labWall,"probe",Vector3.zero,0,Fit.Height,OldCeiling,true,false); if(probe==null) return 0;
            var natural=Mathf.Max(.3f,Size(probe).x); Destroy(probe.gameObject); PlacedModels--;
            var length=Vector3.Distance(from,to); var count=Mathf.Max(1,Mathf.RoundToInt(length/natural)); var segment=length/count;
            var placed=0;
            for(var i=0;i<count;i++)
            {
                var prefab=kit.labWall;
                var point=Vector3.Lerp(from,to,(i+.5f)/count);
                if(Place(prefab,name+" "+(i+1),point,yaw,Fit.Stretch,0,true,false,new Vector3(segment,OldCeiling,ShellDepth))!=null) placed++;
            }
            return placed;
        }
        /// <summary>Floor tiles or ceiling modules in a grid over <paramref name="size"/>, each at its natural size (rounded to fit). Returns the thickness.</summary>
        float Tiles(GameObject prefab,string name,Vector3 centre,Vector2 size,bool ceiling)
        {
            var probe=Place(prefab,"probe",Vector3.zero,0,Fit.Native,0,false,false); if(probe==null) return 0;
            var cell=Size(probe); Destroy(probe.gameObject); PlacedModels--;
            var nx=Mathf.Clamp(Mathf.RoundToInt(size.x/Mathf.Max(.2f,cell.x)),1,12); var nz=Mathf.Clamp(Mathf.RoundToInt(size.y/Mathf.Max(.2f,cell.z)),1,12);
            var thickness=0f;
            for(var i=0;i<nx;i++) for(var j=0;j<nz;j++)
            {
                var c=new Vector3(centre.x-size.x*.5f+(i+.5f)*size.x/nx,centre.y,centre.z-size.y*.5f+(j+.5f)*size.y/nz);
                thickness=Mathf.Max(thickness,Slab(prefab,name+" "+(i+1)+"-"+(j+1),c,new Vector2(size.x/nx,size.y/nz),ceiling));
            }
            return thickness;
        }
        /// <summary>A floor or ceiling module stretched over <paramref name="size"/> (x by z). Floors sit just on top of the old floor; ceilings hang just under the old one. Returns the slab's thickness.</summary>
        float Slab(GameObject prefab,string name,Vector3 centre,Vector2 size,bool ceiling)
        {
            var slab=Place(prefab,name,Vector3.zero,0,Fit.Plan,0,false,false,new Vector3(size.x,0,size.y)); if(slab==null) return 0;
            var thickness=Size(slab).y;
            slab.localPosition=new Vector3(centre.x,ceiling?centre.y-thickness:.004f-thickness,centre.z);
            foreach(var r in slab.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode=ShadowCastingMode.Off;
            }
            return thickness;
        }
        /// <summary>Moves thin things mounted on the old walls (posters, labels, grilles) forward by the panel depth.</summary>
        void ShiftWallDecor(Transform[] shell)
        {
            var skip=new HashSet<string>{"Left wall","Right wall","Rear wall","Front wall","Ceiling","Floor 6 x 5 metres"};
            var moved=new List<Transform>();
            foreach(var t in shell)
            {
                if(t==null || t==environment.transform || skip.Contains(t.name) || t.IsChildOf(environment.workstation) || t.IsChildOf(transform)) continue;
                if(t.GetComponentInParent<GalleryRoom>()!=null) continue;
                if(moved.Exists(m=>t.IsChildOf(m))) continue;
                var renderers=t.GetComponentsInChildren<Renderer>(true); if(renderers.Length==0) continue;
                var b=renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds);
                var local=environment.transform.InverseTransformPoint(b.center); var size=b.size;
                Vector3 push;
                if(OldWallFace-Mathf.Abs(local.x)<.12f && size.x<.15f && size.z<3f) push=new Vector3(-Mathf.Sign(local.x)*ShellDepth,0,0);
                else if(OldRearFace-local.z<.12f && size.z<.15f && size.x<4f) push=new Vector3(0,0,-ShellDepth);
                else continue;
                if(b.min.y<.02f && b.max.y>2.9f) continue; // full-height structure, not decoration
                t.position+=environment.transform.TransformVector(push); moved.Add(t);
            }
        }

        /// <summary>
        /// Moves the side walls out by <see cref="Widen"/> (and stretches the rear wall, floor and ceiling to match), carrying
        /// anything mounted on them. Short wall pieces close the gap beside the gallery opening.
        /// </summary>
        void WidenRoom(Transform[] shell)
        {
            var env=environment.transform; var stretch=(GalleryFace+.06f+Widen)/(GalleryFace+.06f);
            var moved=new List<Transform>();
            foreach(var t in shell)
            {
                if(t==null || t==env || t.IsChildOf(environment.workstation) || t.IsChildOf(transform) || t.GetComponentInParent<GalleryRoom>()!=null) continue;
                switch(t.name)
                {
                    case "Left wall": case "Right wall":
                        t.position+=env.TransformVector(new Vector3(Mathf.Sign(env.InverseTransformPoint(t.position).x)*Widen,0,0)); moved.Add(t); continue;
                    case "Rear wall": case "Floor 6 x 5 metres": case "Ceiling":
                        var s=t.localScale; s.x*=stretch; t.localScale=s; moved.Add(t); continue;
                }
                if(moved.Exists(m=>t.IsChildOf(m))) continue;
                var renderers=t.GetComponentsInChildren<Renderer>(true); if(renderers.Length==0) continue;
                var b=renderers[0].bounds; foreach(var r in renderers) b.Encapsulate(r.bounds);
                var local=env.InverseTransformPoint(b.center);
                // Thin things on a side wall (signs, the old windows) move out with it. Statically batched ones can't be
                // moved visually once the scene runs, so those are simply hidden.
                if(GalleryFace-Mathf.Abs(local.x)<.2f && b.size.x<.3f && local.z>GalleryRoom.RoomStart)
                {
                    if(renderers.Any(r=>r.isPartOfStaticBatch)) t.gameObject.SetActive(false);
                    else t.position+=env.TransformVector(new Vector3(Mathf.Sign(local.x)*Widen,0,0));
                    moved.Add(t);
                }
            }
            // The old walls are statically batched: their colliders moved, but they would still be drawn where they were.
            // Hide them and draw plain painted walls at the new size instead (the lab-pack panels go in front).
            var wallPaint=Paint(WallPaint,.2f);
            foreach(var t in moved)
            {
                if(t.name!="Left wall" && t.name!="Right wall" && t.name!="Rear wall") continue;
                var r=t.GetComponent<Renderer>(); if(r==null) continue; r.enabled=false;
                var copy=GameObject.CreatePrimitive(PrimitiveType.Cube); copy.name=t.name+" (wider room)"; Destroy(copy.GetComponent<Collider>());
                copy.transform.SetParent(transform,false);
                copy.transform.position=t.position; copy.transform.rotation=t.rotation; copy.transform.localScale=t.lossyScale;
                copy.GetComponent<Renderer>().sharedMaterial=wallPaint;
            }
            OldWallFace=GalleryFace+Widen; WallFace=OldWallFace;
            // The lengthened part of the room (built with the gallery) gets its side walls moved out the same way.
            foreach(var t in environment.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Lab extension wall"))
                t.position+=env.TransformVector(new Vector3(Mathf.Sign(env.InverseTransformPoint(t.position).x)*Widen,0,0));
            // Close the strip between the wider room and the gallery opening.
            var paint=Paint(WallPaint,.2f);
            foreach(var side in new[]{-1,1})
            {
                var piece=GameObject.CreatePrimitive(PrimitiveType.Cube); piece.name="Front wall extension";
                piece.transform.SetParent(transform,false); piece.transform.localPosition=new Vector3(side*(GalleryFace+.06f+Widen*.5f),1.5f,GalleryRoom.RoomStart);
                piece.transform.localScale=new Vector3(Widen+.12f,3f,.12f); piece.GetComponent<Renderer>().sharedMaterial=paint;
            }
        }

        // ---------- what the classroom replaces ----------
        void HideOldDressing(Transform[] shell,Transform gallery)
        {
            var always=new[]{"Opaque daylight glazing","Window frame","Window mullion","Ceiling light housing","Diffused ceiling light","Floor joint","Rear timber rail"};
            var withFurniture=new[]{"Rear counter","Sealed cabinet front","Fixed reagent base cabinet","Recessed plinth","Fixed flask body","Fixed flask neck",
                "Spectrometer enclosure","Instrument status screen","Reagent display - fixed"};
            var furnished=kit!=null && kit.labCounter!=null;
            foreach(var t in shell)
            {
                if(t==null || t.IsChildOf(environment.workstation)) continue;
                var name=t.name;
                if(always.Contains(name) || (furnished && (withFurniture.Contains(name) || name.StartsWith("Fixed Kenney")))) t.gameObject.SetActive(false);
            }
            foreach(var label in environment.GetComponentsInChildren<TMP_Text>(true))
            {
                var text=label.text??""; if(label.transform.IsChildOf(environment.workstation)) continue;
                if(text.StartsWith("EYE PROTECTION") || text.StartsWith("SPECTROMETER") || (furnished && text.StartsWith("REAGENT ARCHIVE"))) label.gameObject.SetActive(false);
            }
            if(gallery!=null)
                foreach(Transform t in gallery) if(t.name=="Floor joint" || t.name=="Gallery timber rail" || t.name=="Gallery light housing" || t.name=="Gallery light") t.gameObject.SetActive(false);
        }

        // ---------- light ----------
        void Lighting()
        {
            // Daylight through the windows on the right, plus a bright, slightly warm ambient fill.
            var sun=RenderSettings.sun;
            if(sun==null) sun=FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun!=null) { sun.color=new Color(1f,.96f,.88f); sun.intensity=1.15f; sun.transform.rotation=Quaternion.Euler(42,-72,0); }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.9f,.93f,.98f);
            RenderSettings.ambientEquatorColor=new Color(.78f,.78f,.76f);
            // Trilight's ground colour lights downward-facing surfaces: a light one keeps the ceiling classroom-white instead of grey.
            RenderSettings.ambientGroundColor=new Color(.8f,.79f,.76f);
            // LED panels in a 2 x 3 grid, and two soft lights that make them read as the light source.
            foreach(var x in new[]{-1.5f,1.5f}) foreach(var z in new[]{-.6f,.9f,2.4f})
            {
                Strip("LED panel frame",new Vector3(x,CeilingUnderside-.008f,z),new Vector3(1.24f,.014f,.64f),Paint(new Color(.88f,.89f,.9f),.3f));
                Panel("LED panel",new Vector3(x,CeilingUnderside-.016f,z),Quaternion.Euler(-90,0,0),new Vector2(1.2f,.6f),LedPanel);
            }
            foreach(var x in new[]{-1.4f,1.4f}) PointLight("Ceiling light",new Vector3(x,2.6f,.9f),1.1f,6.5f);
            PointLight("Gallery light",new Vector3(0,2.6f,-3.05f),1.2f,5f);
            foreach(var z in new[]{-2.4f,-3.7f}) Panel("Gallery LED panel",new Vector3(0,CeilingUnderside-.016f,z),Quaternion.Euler(-90,0,0),new Vector2(2.4f,.5f),LedPanel);
        }
        void PointLight(string name,Vector3 position,float intensity,float range)
        {
            var light=new GameObject(name).AddComponent<Light>(); light.transform.SetParent(transform,false); light.transform.localPosition=position;
            light.type=LightType.Point; light.color=new Color(1f,.97f,.92f); light.intensity=intensity; light.range=range; light.shadows=LightShadows.None;
        }

        /// <summary>A flat, self-lit panel (daylight, LED). Uses the UI shader, which every build includes and which ignores scene lighting.</summary>
        Transform Panel(string name,Vector3 position,Quaternion rotation,Vector2 size,Color color)
        {
            if(quad==null)
            {
                quad=new Mesh{name="Classroom panel"};
                quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)};
                quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
                quad.colors=Enumerable.Repeat(Color.white,4).ToArray();
                quad.triangles=new[]{0,2,1,2,3,1}; quad.RecalculateNormals(); quad.RecalculateBounds();
            }
            if(unlit==null) { unlit=new Material(Canvas.GetDefaultCanvasMaterial()); unlit.name="Classroom self-lit"; }
            var go=new GameObject(name); go.transform.SetParent(transform,false);
            go.transform.localPosition=position; go.transform.localRotation=rotation; go.transform.localScale=new Vector3(size.x,size.y,1);
            go.AddComponent<MeshFilter>().sharedMesh=quad;
            var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=unlit; r.shadowCastingMode=ShadowCastingMode.Off;
            var block=new MaterialPropertyBlock(); block.SetColor("_Color",color); r.SetPropertyBlock(block);
            return go.transform;
        }

        // ---------- windows ----------
        /// <param name="centre">Centre of the glass on the right wall's inner face.</param>
        void Window(Vector3 centre,float width,float height)
        {
            var frame=Paint(WindowFrame,.35f); var sill=Paint(Sill,.3f);
            var root=new GameObject("Classroom window "+(Windows+1)).transform; root.SetParent(transform,false); root.localPosition=centre;
            // Facing -x (into the room): quad normal is -z of the quad, so turn it so it faces the room.
            var glass=Panel("Daylight",Vector3.zero,Quaternion.Euler(0,90,0),new Vector2(width,height),Daylight); glass.SetParent(root,false);
            glass.localPosition=new Vector3(-.004f,0,0);
            var horizon=Panel("Distant trees",new Vector3(-.006f,-height*.32f,0),Quaternion.Euler(0,90,0),new Vector2(width,height*.18f),new Color(.62f,.78f,.6f)); horizon.SetParent(root,false);
            horizon.localPosition=new Vector3(-.006f,-height*.32f,0);
            void Bar(string n,Vector3 p,Vector3 s,Material m)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=n; Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root,false); go.transform.localPosition=p; go.transform.localScale=s; go.GetComponent<Renderer>().sharedMaterial=m;
            }
            var t=.06f;
            Bar("Frame top",new Vector3(-.03f,height*.5f+t*.5f,0),new Vector3(.07f,t,width+2*t),frame);
            Bar("Frame bottom",new Vector3(-.03f,-height*.5f-t*.5f,0),new Vector3(.07f,t,width+2*t),frame);
            Bar("Frame left",new Vector3(-.03f,0,-width*.5f-t*.5f),new Vector3(.07f,height,t),frame);
            Bar("Frame right",new Vector3(-.03f,0,width*.5f+t*.5f),new Vector3(.07f,height,t),frame);
            Bar("Mullion",new Vector3(-.025f,0,0),new Vector3(.05f,height,.04f),frame);
            Bar("Transom",new Vector3(-.025f,height*.18f,0),new Vector3(.05f,.035f,width),frame);
            Bar("Sill",new Vector3(-.09f,-height*.5f-t-.015f,0),new Vector3(.2f,.03f,width+2*t+.1f),sill);
            Windows++;
        }

        // ---------- whiteboard ----------
        void BuildWhiteboard()
        {
            var board=new GameObject("Whiteboard").transform; board.SetParent(transform,false);
            board.localPosition=new Vector3(-WallFace,1.55f,.15f); board.localRotation=Quaternion.Euler(0,90,0); // front faces +x, into the room
            Whiteboard=board;
            void Part(string n,Vector3 p,Vector3 s,Color c)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=n; Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(board,false); go.transform.localPosition=p; go.transform.localScale=s; go.GetComponent<Renderer>().sharedMaterial=Paint(c,.7f);
            }
            Part("Board",new Vector3(0,0,.015f),new Vector3(2.4f,1.2f,.02f),BoardSurface);
            Part("Frame top",new Vector3(0,.62f,.018f),new Vector3(2.48f,.04f,.036f),BoardFrame);
            Part("Frame bottom",new Vector3(0,-.62f,.018f),new Vector3(2.48f,.04f,.036f),BoardFrame);
            Part("Frame left",new Vector3(-1.22f,0,.018f),new Vector3(.04f,1.2f,.036f),BoardFrame);
            Part("Frame right",new Vector3(1.22f,0,.018f),new Vector3(.04f,1.2f,.036f),BoardFrame);
            Part("Marker tray",new Vector3(0,-.66f,.05f),new Vector3(1.6f,.02f,.07f),BoardFrame);
            foreach(var (x,c) in new[]{(-.5f,new Color(.1f,.25f,.75f)),(-.38f,new Color(.8f,.15f,.15f)),(-.26f,new Color(.1f,.55f,.25f))})
                Part("Marker",new Vector3(x,-.64f,.055f),new Vector3(.1f,.018f,.018f),c);
            var text=LabVisuals.Label(board,"",new Vector3(0,.02f,.03f),.05f);
            var face=text.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            // Text reads from the front: its +z must point into the wall.
            text.transform.localRotation=Quaternion.Euler(0,180,0);
            text.rectTransform.sizeDelta=new Vector2(2.2f,1.1f); text.color=Ink; text.alignment=TextAlignmentOptions.TopLeft;
            text.enableAutoSizing=true; text.fontSizeMin=.3f; text.fontSizeMax=1.3f;
            var handwriting=Handwriting(); if(handwriting!=null) text.font=handwriting;
            text.text="<size=150%><b>Today: build a molecule</b></size>\n\n"+
                "1  Take atoms out of the beakers\n2  Snap them together on the bench\n3  Finished? Drop it in the <b>GALLERY</b> hole to keep it,\n    or in the <b>TRASH</b> bin to throw it away\n\n"+
                "<size=80%>Red button on the bench: reset the lab</size>";
        }

        /// <summary>Inner face of the side walls (after widening and panels); the reaction table stands against the right one.</summary>
        public float SideWall => WallFace;
        public Transform ReactionsWhiteboard { get; private set; }
        /// <summary>A whiteboard above the reaction table explaining the vessel and the three tools.</summary>
        void ReactionsBoard()
        {
            var board=new GameObject("Reactions whiteboard").transform; board.SetParent(transform,false);
            board.localPosition=new Vector3(WallFace,1.78f,ReactionCornerZ+.1f); board.localRotation=Quaternion.Euler(0,-90,0); // front faces -x, into the room
            ReactionsWhiteboard=board;
            void Part(string n,Vector3 p,Vector3 s,Color c)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=n; Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(board,false); go.transform.localPosition=p; go.transform.localScale=s; go.GetComponent<Renderer>().sharedMaterial=Paint(c,.7f);
            }
            const float w=1.35f, h=1.0f; // fits between the front wall and the atom cupboards
            Part("Board",new Vector3(0,0,.015f),new Vector3(w,h,.02f),BoardSurface);
            Part("Frame top",new Vector3(0,h*.5f+.02f,.018f),new Vector3(w+.08f,.04f,.036f),BoardFrame);
            Part("Frame bottom",new Vector3(0,-h*.5f-.02f,.018f),new Vector3(w+.08f,.04f,.036f),BoardFrame);
            Part("Frame left",new Vector3(-w*.5f-.02f,0,.018f),new Vector3(.04f,h,.036f),BoardFrame);
            Part("Frame right",new Vector3(w*.5f+.02f,0,.018f),new Vector3(.04f,h,.036f),BoardFrame);
            var text=LabVisuals.Label(board,"",new Vector3(0,0,.045f),.05f); // in front of the frame, which used to hide the first letters
            var face=text.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
            text.transform.localRotation=Quaternion.Euler(0,180,0);
            text.rectTransform.pivot=new Vector2(.5f,.5f); text.rectTransform.sizeDelta=new Vector2(w-.18f,h-.12f); text.margin=Vector4.zero; text.color=Ink; text.alignment=TextAlignmentOptions.TopLeft;
            text.enableAutoSizing=true; text.fontSizeMin=.25f; text.fontSizeMax=1.2f;
            var handwriting=Handwriting(); if(handwriting!=null) text.font=handwriting;
            text.text="<size=135%><b>How reactions work</b></size>\n"+
                "1  Build molecules - or bring one from the gallery\n"+
                "2  Drop them in the glass <b>vessel</b> below.\n    Its label says what each tool would do.\n"+
                "3  Hold a <b>tool</b> over the vessel:\n"+
                "    <color=#C8501E><b>FLAME</b></color>  burns fuels with O2;  H2 + Cl2\n"+
                "    <color=#555A66><b>Pd CATALYST</b></color>  adds H2 to double and triple C-C bonds\n"+
                "    <color=#9A8A10><b>ACID</b></color>  water + C=C;  acid + alcohol = ester\n"+
                "    <color=#7A4A20><b>no tool</b></color>  Br2 or Cl2 adds to C=C at once\n"+
                "<size=85%>Atoms are never lost - they only change partners.</size>";
        }
        public const float ReactionCornerZ=-1.8f; // front-right corner, towards the gallery door, well clear of the atom cupboards

        static TMP_FontAsset handwritingFont;
        /// <summary>Patrick Hand (SIL Open Font License, Resources/Fonts), turned into a dynamic TextMeshPro font the first time it is needed.</summary>
        public static TMP_FontAsset Handwriting()
        {
            if(handwritingFont!=null) return handwritingFont;
            var font=Resources.Load<Font>("Fonts/PatrickHand-Regular"); if(font==null) return null;
            handwritingFont=TMP_FontAsset.CreateFontAsset(font); if(handwritingFont!=null) handwritingFont.name="Patrick Hand (runtime)";
            return handwritingFont;
        }


        // ---------- periodic table ----------
        const string ElementSymbols="H He Li Be B C N O F Ne Na Mg Al Si P S Cl Ar K Ca Sc Ti V Cr Mn Fe Co Ni Cu Zn Ga Ge As Se Br Kr Rb Sr Y Zr Nb Mo Tc Ru Rh Pd Ag Cd In Sn Sb Te I Xe Cs Ba La Ce Pr Nd Pm Sm Eu Gd Tb Dy Ho Er Tm Yb Lu Hf Ta W Re Os Ir Pt Au Hg Tl Pb Bi Po At Rn Fr Ra Ac Th Pa U Np Pu Am Cm Bk Cf Es Fm Md No Lr Rf Db Sg Bh Hs Mt Ds Rg Cn Nh Fl Mc Lv Ts Og";
        const string ElementCategories="5701455567013455670122222222223445670122222222223344670188888888888888822222222233336701999999999999999222222222333367";
        static readonly Color[] CategoryColours=
        {
            new Color(1f,.62f,.56f), new Color(1f,.8f,.56f), new Color(1f,.92f,.62f), new Color(.78f,.88f,.74f), new Color(.66f,.86f,.76f),
            new Color(.62f,.84f,1f), new Color(.56f,.93f,.9f), new Color(.8f,.72f,1f), new Color(1f,.74f,.88f), new Color(.94f,.68f,.76f)
        };
        static readonly string[] CategoryNames={"Alkali metals","Alkaline earth","Transition metals","Other metals","Metalloids","Non-metals","Halogens","Noble gases","Lanthanides","Actinides"};
        /// <summary>Row (0 = top) and column (0..17) of element Z in the classic layout; lanthanides and actinides in two rows below.</summary>
        static (float row,int column) TableCell(int z)
        {
            if(z==1) return (0,0); if(z==2) return (0,17);
            if(z>=57 && z<=71) return (7.4f,z-57+2); if(z>=89 && z<=103) return (8.4f,z-89+2);
            int[] starts={3,11,19,37,55,87}; var period=1; for(var i=0;i<starts.Length;i++) if(z>=starts[i]) period=i+2;
            var index=z-starts[period-2]; var row=period-1;
            if(period<=3) return (row,index<2?index:index+10);   // s-block, then p-block from group 13
            if(period<=5) return (row,index);                   // 18 elements in a row
            return (row,index<2?index:index-14);                // after the 14 f-block elements shown below
        }
        /// <summary>A full, colour-coded periodic table on the rear wall, replacing the prefab's 18-tile poster.</summary>
        void PeriodicTable(Transform[] shell)
        {
            foreach(var t in shell) if(t!=null && t.name=="Periodic table exhibit") t.gameObject.SetActive(false);
            foreach(var label in environment.GetComponentsInChildren<TMP_Text>(true))
                if((label.text??"").StartsWith("CHEMISTRY / TEACHING") || (label.text??"").StartsWith("ELEMENTS / FIRST")) label.gameObject.SetActive(false);
            const float cell=.145f, gap=.007f; const int columns=18; const float rows=9.4f;
            var board=new GameObject("Periodic table").transform; board.SetParent(transform,false);
            board.localPosition=new Vector3(-1.75f,1.86f,RearFace-.012f); // left of the menu screen; local -z faces the room
            var width=columns*cell+.16f; var height=rows*cell+.3f;
            void Block(string n,Vector3 p,Vector3 s,Color c)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=n; Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(board,false); go.transform.localPosition=p; go.transform.localScale=s;
                var r=go.GetComponent<Renderer>(); r.sharedMaterial=LabVisuals.Material(c); r.shadowCastingMode=ShadowCastingMode.Off;
            }
            Block("Frame",new Vector3(0,0,.006f),new Vector3(width+.05f,height+.05f,.012f),new Color(.2f,.3f,.38f));
            Block("Paper",new Vector3(0,0,0),new Vector3(width,height,.006f),new Color(.98f,.98f,.97f));
            var left=-columns*cell*.5f+cell*.5f; var top=height*.5f-.2f-cell*.5f;
            var symbols=ElementSymbols.Split(' ');
            for(var z=1;z<=symbols.Length;z++)
            {
                var (row,column)=TableCell(z);
                var p=new Vector3(left+column*cell,top-row*cell,-.006f);
                Block("Element "+symbols[z-1],p,new Vector3(cell-gap,cell-gap,.004f),CategoryColours[ElementCategories[z-1]-'0']);
                var text=LabVisuals.Label(board,"<size=48%>"+z+"</size>\n<b>"+symbols[z-1]+"</b>",p+new Vector3(0,0,-.006f),.05f);
                var face=text.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; Destroy(face); }
                text.transform.localRotation=Quaternion.identity; text.rectTransform.sizeDelta=new Vector2(cell,cell); text.color=new Color(.1f,.13f,.18f);
                text.textWrappingMode=TextWrappingModes.NoWrap;
            }
            // Placeholders for the f-block in group 3, and a title in the gap above the transition metals.
            foreach(var (row,label) in new[]{(5f,"57-71"),(6f,"89-103")})
            {
                var p=new Vector3(left+2*cell,top-row*cell,-.006f);
                Block("f-block",p,new Vector3(cell-gap,cell-gap,.004f),CategoryColours[row<6?8:9]);
                var t=LabVisuals.Label(board,label,p+new Vector3(0,0,-.006f),.022f); var f=t.GetComponent<FaceViewer>(); if(f!=null) { f.enabled=false; Destroy(f); }
                t.transform.localRotation=Quaternion.identity; t.rectTransform.sizeDelta=new Vector2(cell,cell); t.color=new Color(.1f,.13f,.18f);
            }
            var title=LabVisuals.Label(board,"<b>PERIODIC TABLE OF THE ELEMENTS</b>",new Vector3(0,height*.5f-.1f,-.008f),.07f);
            var tf=title.GetComponent<FaceViewer>(); if(tf!=null) { tf.enabled=false; Destroy(tf); }
            title.transform.localRotation=Quaternion.identity; title.rectTransform.sizeDelta=new Vector2(width,.14f); title.color=new Color(.12f,.2f,.3f);
            // Colour key in the empty space above the transition metals.
            for(var i=0;i<CategoryNames.Length;i++)
            {
                var p=new Vector3(left+(2.4f+(i%5)*1.9f)*cell,top-(.35f+(i/5)*.55f)*cell,-.006f);
                Block("Key",p,new Vector3(cell*.35f,cell*.35f,.004f),CategoryColours[i]);
                var k=LabVisuals.Label(board,CategoryNames[i],p+new Vector3(cell*1.05f,0,-.004f),.022f); var kf=k.GetComponent<FaceViewer>(); if(kf!=null) { kf.enabled=false; Destroy(kf); }
                k.transform.localRotation=Quaternion.identity; k.rectTransform.sizeDelta=new Vector2(cell*1.6f,cell*.4f); k.alignment=TextAlignmentOptions.Left; k.color=new Color(.15f,.18f,.22f);
            }
        }

        /// <summary>A plain wooden door with a frame, handle and small window: the way out of the gallery.</summary>
        void Door(Vector3 bottomCentre,float yaw)
        {
            var door=new GameObject("Gallery door").transform; door.SetParent(transform,false); door.localPosition=bottomCentre; door.localRotation=Quaternion.Euler(0,yaw,0);
            void Part(string n,Vector3 p,Vector3 s,Color c)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=n; Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(door,false); go.transform.localPosition=p; go.transform.localScale=s; go.GetComponent<Renderer>().sharedMaterial=LabVisuals.Material(c);
            }
            var frame=new Color(.9f,.9f,.88f); var wood=new Color(.62f,.42f,.24f);
            Part("Door leaf",new Vector3(0,1.03f,.025f),new Vector3(.92f,2.06f,.045f),wood);
            Part("Frame left",new Vector3(-.5f,1.06f,.03f),new Vector3(.07f,2.12f,.06f),frame);
            Part("Frame right",new Vector3(.5f,1.06f,.03f),new Vector3(.07f,2.12f,.06f),frame);
            Part("Frame top",new Vector3(0,2.13f,.03f),new Vector3(1.07f,.07f,.06f),frame);
            Part("Window",new Vector3(0,1.55f,.05f),new Vector3(.3f,.45f,.01f),new Color(.75f,.88f,.95f));
            Part("Handle",new Vector3(.36f,1.02f,.065f),new Vector3(.14f,.025f,.03f),new Color(.75f,.76f,.78f));
            Part("Kick plate",new Vector3(0,.12f,.05f),new Vector3(.88f,.2f,.008f),new Color(.7f,.72f,.74f));
            var sign=LabVisuals.Label(door,"EXIT",new Vector3(0,2.3f,.06f),.05f); var f=sign.GetComponent<FaceViewer>(); if(f!=null) { f.enabled=false; Destroy(f); }
            sign.transform.localRotation=Quaternion.Euler(0,180,0); sign.color=new Color(.1f,.55f,.25f); sign.fontStyle=FontStyles.Bold;
        }

        /// <summary>One floor surface whose texture is laid out in world space, so the room and the gallery tiles line up exactly.</summary>
        void WorldFloor(string name,Vector3 centre,Vector2 size)
        {
            const float repeat=2f; // metres per texture repeat
            var mesh=new Mesh{name=name};
            var x0=centre.x-size.x*.5f; var x1=centre.x+size.x*.5f; var z0=centre.z-size.y*.5f; var z1=centre.z+size.y*.5f;
            mesh.vertices=new[]{new Vector3(x0,0,z0),new Vector3(x0,0,z1),new Vector3(x1,0,z1),new Vector3(x1,0,z0)};
            mesh.uv=new[]{new Vector2(x0,z0)/repeat,new Vector2(x0,z1)/repeat,new Vector2(x1,z1)/repeat,new Vector2(x1,z0)/repeat};
            mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go=new GameObject(name); go.transform.SetParent(transform,false); go.transform.localPosition=new Vector3(0,.003f,0);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            if(floorMaterial==null) { floorMaterial=FloorMaterial(1,1); floorMaterial.SetTextureScale("_BaseMap",Vector2.one); floorMaterial.SetTextureScale("_BumpMap",Vector2.one); }
            var r=go.AddComponent<MeshRenderer>(); r.sharedMaterial=floorMaterial; r.shadowCastingMode=ShadowCastingMode.Off;
        }
        Material floorMaterial;

        // ---------- furniture from the lab pack ----------
        void Furniture()
        {
            // Lab counters along the rear wall, under the periodic table.
            var counters=new List<Transform>();
            var probe=Place(kit.labCounter,"probe",Vector3.zero,180,Fit.Height,.9f,true,false);
            if(probe!=null)
            {
                var width=Size(probe).x; Destroy(probe.gameObject); PlacedModels--;
                var count=Mathf.Clamp(Mathf.FloorToInt(4.2f/Mathf.Max(.3f,width)),1,4); var start=-(count-1)*width*.5f;
                for(var i=0;i<count;i++) counters.Add(Place(kit.labCounter,"Lab counter "+(i+1),new Vector3(start+i*width,0,RearFace),180,Fit.Height,.9f,true,true));
            }
            var sets=new[]
            {
                new[]{(kit.erlenmeyer,.2f),(kit.florence,.22f),(kit.beakerWater,.13f),(kit.cylinder,.3f),(kit.testTube,.16f)},
                new[]{(kit.beakerWater,.11f),(kit.chinaDish,.05f),(kit.funnel,.14f),(kit.erlenmeyer,.17f)}, // no tap: there is no sink
                new[]{(kit.instrument,.38f),(kit.spiritLamp,.12f),(kit.boilingStand,.42f)},
                new[]{(kit.florence,.2f),(kit.cylinder,.26f),(kit.erlenmeyerEmpty,.18f)},
            };
            for(var i=0;i<counters.Count;i++) Clutter(counters[i],sets[Mathf.Min(i,sets.Length-1)]);

            // Teacher's desk and shelves on the left wall, beside the whiteboard.
            var desk=Place(kit.desk,"Teacher desk",new Vector3(-WallFace,0,2.0f),90,Fit.Height,.78f,true,true);
            Seat(desk,"Teacher chair",-90);
            var rack=Place(kit.wallRack,"Wall shelf",new Vector3(-WallFace,1.75f,2.0f),90,Fit.Width,.95f,true,false);
            Clutter(rack,new[]{(kit.storageBox,.16f),(kit.erlenmeyerEmpty,.18f),(kit.storageBox,.16f)});
            if(desk!=null) Clutter(desk,new[]{(kit.beakerWater,.12f),(kit.spiritLamp,.1f)});

            Workbench();

            // A student desk under the far window (the near one stood behind the beaker table).
            foreach(var z in new[]{1.55f})
            {
                var student=Place(kit.desk,"Student desk",new Vector3(WallFace,0,z),-90,Fit.Height,.76f,true,true);
                Seat(student,"Student chair",90);
            }
        }
        /// <summary>
        /// The central workbench drawn with the lab pack's table: the old top, edge and legs stop rendering (their colliders
        /// stay, so atoms, molecules and the bench-top height are unchanged) and the model follows the height-adjustable bench.
        /// </summary>
        void Workbench()
        {
            var bench=environment.bench; if(bench==null || kit.desk==null) return;
            var parts=bench.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Workbench" || r.name=="Worktable timber edge" || r.name=="Table support").ToArray();
            var top=parts.FirstOrDefault(r=>r.name=="Workbench"); if(top==null) return;
            // The renderer is statically batched, so its bounds still show the authored 2.05 m; the bench is now LabController.BenchWidth wide.
            var b=top.bounds; var height=b.max.y-environment.transform.position.y;
            var centre=environment.transform.InverseTransformPoint(new Vector3(bench.position.x,environment.transform.position.y,b.center.z));
            var table=Place(kit.desk,"Lab workbench",centre,180,Fit.Stretch,0,false,false,new Vector3(LabController.BenchWidth,height,b.size.z+.04f));
            if(table==null) return;
            foreach(var r in parts) r.enabled=false;
            table.SetParent(bench,true);
        }
        /// <summary>A chair in front of a desk, pushed in a little and facing it.</summary>
        void Seat(Transform desk,string name,float yaw)
        {
            if(desk==null) return;
            // Stool tucked half under the desk: seat below the desktop, and it stays out of the walkway around the bench.
            var depth=Size(desk).z; var front=desk.localPosition+desk.localRotation*Vector3.forward*(depth+.28f); // in front of the cabinet, not inside it
            Place(kit.chair,name,front,yaw,Fit.Height,.62f,false,true);
        }
        /// <summary>Stands small objects in a row on top of a placed piece of furniture.</summary>
        void Clutter(Transform surface,(GameObject prefab,float height)[] items)
        {
            if(surface==null) return;
            var size=Size(surface); var usable=size.x*.8f; var n=items.Count(i=>i.prefab!=null); if(n==0) return;
            var k=0;
            foreach(var (prefab,height) in items)
            {
                if(prefab==null) continue;
                var along=-usable*.5f+usable*(k+.5f)/n; k++;
                var local=new Vector3(along,size.y,size.z*.45f); // a little behind the middle of the top
                var point=surface.localPosition+surface.localRotation*local;
                var item=Place(prefab,prefab.name,point,surface.localEulerAngles.y+Random.Range(-25f,25f),Fit.Height,height,false,false);
                if(item!=null) foreach(var r in item.GetComponentsInChildren<Renderer>()) r.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        // ---------- gallery wing ----------
        void GalleryWing(Transform gallery)
        {
            if(!SciFiShell) return;
            const float start=GalleryRoom.RoomStart-.08f, end=GalleryRoom.RoomEnd+.06f;
            TileWall("Gallery wall end",new Vector3(-GalleryFace,0,end),new Vector3(GalleryFace,0,end),0);
            foreach(var side in new[]{-1,1})
                TileWall(side<0?"Gallery wall left":"Gallery wall right",new Vector3(side*GalleryFace,0,start),new Vector3(side*GalleryFace,0,end+ShellDepth),side<0?90:-90);
            GallerySigns(gallery,end+ShellDepth);
            Door(new Vector3(GalleryFace-.85f,0,end+ShellDepth),0);
        }

        /// <summary>Brings the gallery's signs in front of the end-wall panels.</summary>
        void GallerySigns(Transform gallery,float face)
        {
            foreach(var label in gallery.GetComponentsInChildren<TextMeshPro>(true))
                if(label.transform.localPosition.z<GalleryRoom.RoomEnd+.3f)
                { var p=label.transform.localPosition; p.z=Mathf.Max(p.z,face+.04f); label.transform.localPosition=p; label.color=new Color(.08f,.13f,.2f); }
        }

        // ---------- placement ----------
        public enum Fit { Native, Height, Width, Stretch, Plan }
        readonly Dictionary<Transform,Vector3> footprints=new Dictionary<Transform,Vector3>();
        Vector3 Size(Transform placed) => placed!=null && footprints.TryGetValue(placed,out var size)?size:Vector3.zero;

        /// <summary>
        /// Instantiates a model so that, at yaw 0, it stands on <paramref name="point"/> facing +z (the packs' convention).
        /// With <paramref name="backToWall"/> its back touches the point instead of its centre. The model is measured
        /// from its meshes, so pivots and import scales don't matter.
        /// </summary>
        Transform Place(GameObject prefab,string name,Vector3 point,float yaw,Fit fit,float target,bool backToWall,bool solid,Vector3 stretch=default)
        {
            if(prefab==null) return null;
            var holder=new GameObject(name).transform; holder.SetParent(transform,false);
            var model=Instantiate(prefab,holder,false); model.name=prefab.name;
            model.transform.localPosition=Vector3.zero; model.transform.localRotation=Quaternion.identity;
            foreach(var c in model.GetComponentsInChildren<Collider>(true)) { c.enabled=false; Destroy(c); }
            foreach(var l in model.GetComponentsInChildren<Light>(true)) l.enabled=false;
            foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
            foreach(var a in model.GetComponentsInChildren<AudioSource>(true)) a.enabled=false;
            if(!Measure(model.transform,holder,out var bounds)) { skipped.Add(name+" (no mesh)"); Destroy(holder.gameObject); return null; }
            var size=bounds.size; var scale=Vector3.one;
            switch(fit)
            {
                case Fit.Height: scale=Vector3.one*(target/Mathf.Max(1e-4f,size.y)); break;
                case Fit.Width: scale=Vector3.one*(target/Mathf.Max(1e-4f,size.x)); break;
                case Fit.Stretch:
                    var sx=stretch.x/Mathf.Max(1e-4f,size.x); var sy=stretch.y/Mathf.Max(1e-4f,size.y);
                    scale=new Vector3(sx,sy,stretch.z>0?stretch.z/Mathf.Max(1e-4f,size.z):Mathf.Min(sx,sy,1)); break;
                case Fit.Plan: // x and z from the target; y squashed to the target thickness (floors, ceilings)
                    var px=stretch.x/Mathf.Max(1e-4f,size.x); var pz=stretch.z/Mathf.Max(1e-4f,size.z);
                    scale=new Vector3(px,stretch.y>0?Mathf.Clamp(stretch.y/Mathf.Max(1e-4f,size.y),.02f,40):Mathf.Min(px,pz),pz); break;
            }
            // A model lying flat or authored at a strange unit would be blown up or shrunk to nothing: skip it.
            if(scale.x<.02f || scale.x>40 || scale.y<.02f || scale.y>40) { skipped.Add(name+" (unexpected size "+size+")"); Destroy(holder.gameObject); return null; }
            model.transform.localScale=Vector3.Scale(model.transform.localScale,scale);
            var centre=Vector3.Scale(bounds.center,scale); size=Vector3.Scale(size,scale);
            var min=centre-size*.5f;
            model.transform.localPosition=new Vector3(-centre.x,-min.y,backToWall?-min.z:-centre.z);
            holder.localPosition=point; holder.localRotation=Quaternion.Euler(0,yaw,0);
            footprints[holder]=size;
            if(solid)
            {
                var box=holder.gameObject.AddComponent<BoxCollider>();
                box.center=new Vector3(0,size.y*.5f,backToWall?size.z*.5f:0); box.size=size;
            }
            if(kit!=null && prefab==kit.desk) BeakerShelf.SolidCore(holder,new Vector3(0,0,backToWall?size.z*.5f:0),size);
            // Opaque furniture is drawn from both sides: some pack models have no back faces (open-backed drawers).
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
                foreach(var m in r.materials) if(m.renderQueue<2500 && m.HasProperty("_Cull")) { m.SetFloat("_Cull",0); m.doubleSidedGI=true; }
            PlacedModels++;
            return holder;
        }
        /// <summary>Exact bounds of every mesh under <paramref name="model"/>, in <paramref name="space"/>'s coordinates.</summary>
        static bool Measure(Transform model,Transform space,out Bounds bounds)
        {
            bounds=default; var any=false;
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
            {
                Bounds local;
                if(r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh!=null) local=filter.sharedMesh.bounds;
                else if(r is SkinnedMeshRenderer skinned) local=skinned.localBounds;
                else continue;
                var toSpace=space.worldToLocalMatrix*r.transform.localToWorldMatrix;
                for(var i=0;i<8;i++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var p=toSpace.MultiplyPoint3x4(corner);
                    if(!any) { bounds=new Bounds(p,Vector3.zero); any=true; } else bounds.Encapsulate(p);
                }
            }
            return any && bounds.size.sqrMagnitude>1e-8f;
        }
    }
}
