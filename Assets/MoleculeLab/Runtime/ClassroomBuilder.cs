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
        const float OldWallFace=2.94f, OldRearFace=3.29f, OldCeiling=2.99f, ShellDepth=.08f;
        float WallFace=OldWallFace, RearFace=OldRearFace, CeilingUnderside=OldCeiling;
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
            Repaint(shell,gallery);
            HideOldDressing(shell,gallery);
            if(kit!=null && kit.labWall!=null) BuildShell(shell,gallery);
            Lighting();
            for(var i=0;i<2;i++) Window(new Vector3(WallFace,1.72f,i==0?-.5f:1.55f),1.75f,1.45f);
            BuildWhiteboard();
            if(kit!=null) { Furniture(); if(gallery!=null) GalleryWing(gallery); }
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
            Tiles(kit.labFloor,"Lab floor",new Vector3(0,0,roomCentre),roomSize,false);
            var roof=Tiles(kit.labRoof,"Lab ceiling",new Vector3(0,OldCeiling,roomCentre),roomSize,true);
            if(roof>0) CeilingUnderside=OldCeiling-Mathf.Min(roof,.12f);
            if(gallery!=null)
            {
                var length=GalleryRoom.RoomStart-GalleryRoom.RoomEnd-.06f; var mid=(GalleryRoom.RoomStart+GalleryRoom.RoomEnd+.06f)*.5f;
                Tiles(kit.labFloor,"Lab gallery floor",new Vector3(0,0,mid),new Vector2(OldWallFace*2,length),false);
                Tiles(kit.labRoof,"Lab gallery ceiling",new Vector3(0,OldCeiling,mid),new Vector2(OldWallFace*2,length),true);
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
            text.enableAutoSizing=true; text.fontSizeMin=.3f; text.fontSizeMax=1.1f;
            text.text="<size=150%><b>Today: build a molecule</b></size>\n\n"+
                "1  Take atoms out of the beakers\n2  Snap them together on the bench\n3  Finished? <b>GALLERY</b> bin keeps it,\n    <b>TRASH</b> bin throws it away\n\n"+
                "<size=80%>Red button on the bench: reset the lab</size>";
        }

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

            // Two student desks under the windows.
            foreach(var z in new[]{-.5f,1.55f})
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
            var b=top.bounds; var height=b.max.y-environment.transform.position.y;
            var centre=environment.transform.InverseTransformPoint(new Vector3(b.center.x,environment.transform.position.y,b.center.z));
            var table=Place(kit.desk,"Lab workbench",centre,180,Fit.Stretch,0,false,false,new Vector3(b.size.x,height,b.size.z+.04f));
            if(table==null) return;
            foreach(var r in parts) r.enabled=false;
            table.SetParent(bench,true);
        }
        /// <summary>A chair in front of a desk, pushed in a little and facing it.</summary>
        void Seat(Transform desk,string name,float yaw)
        {
            if(desk==null) return;
            // Stool tucked half under the desk: seat below the desktop, and it stays out of the walkway around the bench.
            var depth=Size(desk).z; var front=desk.localPosition+desk.localRotation*Vector3.forward*(depth-.2f);
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
            TileWall("Gallery wall end",new Vector3(-OldWallFace,0,end),new Vector3(OldWallFace,0,end),0);
            foreach(var side in new[]{-1,1})
                TileWall(side<0?"Gallery wall left":"Gallery wall right",new Vector3(side*OldWallFace,0,start),new Vector3(side*OldWallFace,0,end+ShellDepth),side<0?90:-90);
            GallerySigns(gallery,end+ShellDepth);
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
