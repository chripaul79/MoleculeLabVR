using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace MoleculeLab.Editor
{
    public static class AcademicLabBuilder
    {
        const string Folder="Assets/MoleculeLab/Environment";
        static Color wall=new Color(.79f,.8f,.75f), teal=new Color(.21f,.37f,.38f), dark=new Color(.09f,.13f,.15f), wood=new Color(.5f,.36f,.22f), metal=new Color(.48f,.55f,.56f);
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 s,Color c,bool collider=false) => LabVisuals.Shape(name,PrimitiveType.Cube,parent,p,s,c,collider);
        static Transform Group(string name,Transform parent,Vector3 p)
        { var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=p; return go.transform; }
        static TextMeshPro Sign(string text,Transform parent,Vector3 p,float size,Color color)
        {
            var label=LabVisuals.Label(parent,text,p,size); Object.DestroyImmediate(label.GetComponent<FaceViewer>()); label.color=color; return label;
        }
        [MenuItem("Molecule Lab/Install academic environment")]
        public static void Install()
        {
            EditorSceneManager.OpenScene(QuestProjectSetup.ScenePath);
            if(Object.FindAnyObjectByType<LabEnvironment>()!=null) { Debug.Log("Academic environment already installed; existing decoration preserved."); return; }
            Directory.CreateDirectory(Folder+"/Materials"); AssetDatabase.Refresh();
            var lab=Object.FindAnyObjectByType<LabController>(); TMP_Settings.defaultFontAsset=lab.font;
            var plane=GameObject.Find("Plane"); if(plane!=null) Object.DestroyImmediate(plane);
            var root=new GameObject("Academic chemistry laboratory"); var environment=root.AddComponent<LabEnvironment>();
            var room=Group("Room shell and fixed exhibits",root.transform,Vector3.zero);
            Box("Floor 6 x 5 metres",room,new Vector3(0,-.06f,.85f),new Vector3(6,.12f,5),new Color(.43f,.48f,.48f),true);
            Box("Rear wall",room,new Vector3(0,1.5f,3.35f),new Vector3(6,3,.12f),wall);
            Box("Left wall",room,new Vector3(-3,1.5f,.85f),new Vector3(.12f,3,5),wall);
            Box("Right wall",room,new Vector3(3,1.5f,.85f),new Vector3(.12f,3,5),wall);
            Box("Front wall",room,new Vector3(0,1.5f,-1.65f),new Vector3(6,3,.12f),wall);
            Box("Ceiling",room,new Vector3(0,3.05f,.85f),new Vector3(6,.12f,5),wall);
            for(var i=0;i<7;i++) Box("Floor joint",room,new Vector3(-3+i,0,.85f),new Vector3(.008f,.002f,5),metal);
            for(var i=0;i<6;i++) Box("Floor joint",room,new Vector3(0,0,-1.65f+i),new Vector3(6,.002f,.008f),metal);
            Box("Rear timber rail",room,new Vector3(0,1.05f,3.27f),new Vector3(6,.065f,.045f),wood);
            foreach(var x in new[]{-1.6f,1.6f})
            {
                Box("Ceiling light housing",room,new Vector3(x,2.93f,.85f),new Vector3(.7f,.07f,2),metal);
                Box("Diffused ceiling light",room,new Vector3(x,2.885f,.85f),new Vector3(.62f,.02f,1.85f),new Color(.95f,.97f,.86f));
            }
            for(var i=0;i<9;i++) Box("Ventilation grille",room,new Vector3(-2.22f+i*.105f,2.6f,3.25f),new Vector3(.035f,.22f,.035f),metal);
            Box("Window frame",room,new Vector3(2.92f,1.95f,.7f),new Vector3(.08f,1.15f,2.25f),wood);
            Box("Opaque daylight glazing",room,new Vector3(2.865f,1.95f,.7f),new Vector3(.025f,1.03f,2.13f),new Color(.57f,.71f,.74f));
            Box("Window mullion",room,new Vector3(2.83f,1.95f,.7f),new Vector3(.04f,1.03f,.04f),metal);
            for(var i=0;i<5;i++)
            {
                var x=-2.4f+i*1.18f;
                Box("Fixed reagent base cabinet",room,new Vector3(x,.42f,2.91f),new Vector3(1.1f,.8f,.65f),teal);
                Box("Recessed plinth",room,new Vector3(x,.08f,2.93f),new Vector3(.96f,.16f,.55f),dark);
                Box("Rear counter",room,new Vector3(x,.86f,2.88f),new Vector3(1.15f,.06f,.75f),dark);
                Box("Sealed cabinet front",room,new Vector3(x,.48f,2.57f),new Vector3(1.02f,.62f,.02f),new Color(.29f,.43f,.43f));
            }
            var display=Group("Reagent display - fixed",room,new Vector3(-1.95f,1.4f,3.07f));
            Box("Display back",display,Vector3.zero,new Vector3(1.7f,.75f,.12f),teal);
            for(var i=0;i<8;i++)
            {
                var p=new Vector3(-.68f+i*.19f,-.21f,-.13f);
                LabVisuals.Shape("Fixed reagent bottle",PrimitiveType.Cylinder,display,p,new Vector3(.1f,.12f,.1f),new Color(.39f,.3f,.18f),false);
                Box("Bottle label",display,p+new Vector3(0,0,-.053f),new Vector3(.065f,.07f,.003f),wall);
                LabVisuals.Shape("Sealed cap",PrimitiveType.Cylinder,display,p+Vector3.up*.135f,new Vector3(.07f,.02f,.07f),dark,false);
            }
            Box("Display sill",display,new Vector3(0,-.36f,-.08f),new Vector3(1.78f,.04f,.32f),wood);
            Sign("REAGENT ARCHIVE / FIXED DISPLAY",display,new Vector3(0,.23f,-.1f),.022f,Color.white);
            var poster=Group("Periodic table exhibit",room,new Vector3(.1f,1.83f,3.25f));
            Box("Poster frame",poster,Vector3.zero,new Vector3(1.9f,1.05f,.04f),wood);
            Box("Poster paper",poster,new Vector3(0,0,-.025f),new Vector3(1.84f,.99f,.01f),wall);
            Sign("ELEMENTS / FIRST 18",poster,new Vector3(0,.37f,-.04f),.035f,dark);
            var symbols=new[]{"H","He","Li","Be","B","C","N","O","F","Ne","Na","Mg","Al","Si","P","S","Cl","Ar"};
            for(var i=0;i<18;i++)
            {
                var row=i<2?0:i<10?1:2; var col=i==0?0:i==1?7:(i-2)%8;
                var p=new Vector3(-.77f+col*.22f,.13f-row*.23f,-.045f);
                Box("Element tile",poster,p,new Vector3(.2f,.2f,.01f),teal);
                var label=Sign((i+1)+"  "+symbols[i],poster,p+Vector3.back*.01f,.023f,Color.white); label.rectTransform.sizeDelta=new Vector2(.2f,.17f);
            }
            Sign("CHEMISTRY / TEACHING LAB 03",room,new Vector3(0,2.55f,3.24f),.055f,dark);
            Sign("EYE PROTECTION\nACADEMIC LABORATORY",room,new Vector3(2.28f,1.85f,3.24f),.03f,dark);
            // Instruments are deliberately behind the workstation, away from the reachable interaction area.
            Box("Spectrometer enclosure",room,new Vector3(1.45f,1.03f,2.85f),new Vector3(.62f,.29f,.44f),wall);
            Box("Instrument status screen",room,new Vector3(1.45f,1.07f,2.62f),new Vector3(.3f,.12f,.015f),teal);
            Sign("SPECTROMETER / STANDBY",room,new Vector3(1.45f,1.28f,2.65f),.02f,dark);
            for(var i=0;i<3;i++)
            {
                var p=new Vector3(-1.3f+i*.2f,.97f,2.76f);
                LabVisuals.Shape("Fixed flask body",PrimitiveType.Sphere,room,p,new Vector3(.13f,.15f,.13f),metal,false);
                LabVisuals.Shape("Fixed flask neck",PrimitiveType.Cylinder,room,p+Vector3.up*.1f,new Vector3(.045f,.06f,.045f),metal,false);
            }
            PlaceAsset("computerScreen",room,new Vector3(2.25f,.9f,2.92f),.45f,teal);
            PlaceAsset("computerKeyboard",room,new Vector3(2.25f,.9f,2.57f),.38f,dark);
            PlaceAsset("books",room,new Vector3(-2.45f,.91f,2.8f),.32f,wood);
            PlaceAsset("plantSmall1",room,new Vector3(2.7f,.92f,2.98f),.22f,teal);
            PlaceAsset("chairDesk",room,new Vector3(2.25f,0,2.1f),.9f,dark);
            environment.workstation=Group("Height adjustable workstation",root.transform,new Vector3(0,.82f,0));
            environment.bench=Group("Central worktable",environment.workstation,new Vector3(0,0,.85f));
            Box("Workbench",environment.bench,Vector3.zero,new Vector3(2.05f,.08f,.85f),dark,true);
            Box("Worktable timber edge",environment.bench,new Vector3(0,-.025f,-.43f),new Vector3(2.05f,.05f,.035f),wood);
            foreach(var x in new[]{-.85f,.85f}) Box("Table support",environment.bench,new Vector3(x,-.42f,.05f),new Vector3(.055f,.8f,.6f),metal);
            environment.tray=Box("Reaction tray",environment.bench,new Vector3(.65f,.065f,-.04f),new Vector3(.55f,.025f,.48f),teal,true).transform;
            Sign("REACTION TRAY",environment.bench,new Vector3(.64f,.13f,-.3f),.025f,Color.white);
            Sign("ASSEMBLY",environment.bench,new Vector3(-.13f,.09f,-.43f),.028f,Color.white);
            environment.panelMount=Group("Instruction display mount",environment.bench,new Vector3(.05f,.63f,.32f));
            Box("Display stand",environment.bench,new Vector3(.05f,.25f,.36f),new Vector3(.055f,.5f,.065f),metal);
            Box("Display back",environment.panelMount,new Vector3(0,0,.01f),new Vector3(1.18f,.68f,.035f),dark);
            var shelf=Group("Reachable atom shelf",environment.workstation,new Vector3(-.72f,0,.12f));
            shelf.localRotation=Quaternion.Euler(0,-65,0);
            environment.sockets=new Transform[48]; environment.stockLabels=new TextMeshPro[4];
            foreach(var x in new[]{-.57f,.57f}) Box("Shelf upright",shelf,new Vector3(x,.03f,.2f),new Vector3(.035f,.85f,.035f),metal);
            for(var basket=0;basket<4;basket++)
            {
                var tier=basket/2; var column=basket%2;
                var tray=Group("Fixed atom basket "+basket,shelf,new Vector3((column-.5f)*.56f,-.02f+tier*.34f,tier*.12f));
                Box("Basket base",tray,Vector3.zero,new Vector3(.54f,.025f,.4f),teal,true);
                Box("Basket left lip",tray,new Vector3(-.265f,.035f,0),new Vector3(.018f,.065f,.4f),metal);
                Box("Basket right lip",tray,new Vector3(.265f,.035f,0),new Vector3(.018f,.065f,.4f),metal);
                Box("Basket rear lip",tray,new Vector3(0,.035f,.195f),new Vector3(.54f,.065f,.018f),metal);
                var first=basket==0?0:basket==1?8:basket==2?16:32; var count=basket<2?8:16;
                for(var i=0;i<count;i++)
                {
                    var p=new Vector3(-.195f+(i%4)*.13f,.075f,-.135f+(i/4)*(count==8?.23f:.087f));
                    environment.sockets[first+i]=Group("Atom socket "+(first+i),tray,p);
                    LabVisuals.Shape("Empty socket marker",PrimitiveType.Cylinder,tray,new Vector3(p.x,.016f,p.z),new Vector3(.065f,.002f,.065f),dark,false);
                }
                environment.stockLabels[basket]=Sign("",tray,new Vector3(0,-.025f,-.215f),.022f,Color.white);
                environment.stockLabels[basket].rectTransform.sizeDelta=new Vector2(.54f,.065f);
            }
            environment.UpdateCounts(new AtomInventory());
            foreach(var label in root.GetComponentsInChildren<TextMeshPro>()) label.font=lab.font;
            // Save generated materials so the editable prefab has no transient references.
            var materials=root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null && !AssetDatabase.Contains(m)).Distinct().ToArray();
            for(var i=0;i<materials.Length;i++) AssetDatabase.CreateAsset(materials[i],AssetDatabase.GenerateUniqueAssetPath(Folder+"/Materials/LabMaterial.mat"));
            var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(root,Folder+"/AcademicLab.prefab",InteractionMode.AutomatedAction);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.72f,.74f,.7f);
            foreach(var light in Object.FindObjectsByType<Light>()) { light.shadows=LightShadows.None; light.intensity=.8f; }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("ACADEMIC_LAB_INSTALLED");
        }
        public static void PolishLayout()
        {
            EditorSceneManager.OpenScene(QuestProjectSetup.ScenePath);
            var path=Folder+"/AcademicLab.prefab"; var root=PrefabUtility.LoadPrefabContents(path);
            var shelf=root.transform.Find("Height adjustable workstation/Reachable atom shelf");
            shelf.localPosition=new Vector3(-.72f,0,.12f); shelf.localRotation=Quaternion.Euler(0,-65,0);
            for(var i=0;i<4;i++) shelf.Find("Fixed atom basket "+i).localPosition=new Vector3((i%2-.5f)*.56f,-.02f+(i/2)*.34f,i/2*.12f);
            var posts=shelf.Cast<Transform>().Where(t=>t.name=="Shelf upright").ToArray();
            for(var i=0;i<posts.Length;i++) posts[i].localPosition=new Vector3(i==0?-.57f:.57f,.03f,.2f);
            var poster=root.transform.Find("Room shell and fixed exhibits/Periodic table exhibit");
            var tiles=poster.Cast<Transform>().Where(t=>t.name=="Element tile").ToArray();
            var labels=poster.GetComponentsInChildren<TextMeshPro>().Where(t=>t.text!="ELEMENTS / FIRST 18").ToArray();
            for(var i=0;i<18;i++)
            {
                var row=i<2?0:i<10?1:2; var col=i==0?0:i==1?7:(i-2)%8;
                var position=new Vector3(-.77f+col*.22f,.13f-row*.23f,-.045f);
                tiles[i].localPosition=position; tiles[i].localScale=new Vector3(.2f,.2f,.01f);
                labels[i].transform.localPosition=position+Vector3.back*.01f; labels[i].rectTransform.sizeDelta=new Vector2(.2f,.17f);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        }
        public static void FinalizeEnvironment()
        {
            var path=Folder+"/AcademicLab.prefab"; var root=PrefabUtility.LoadPrefabContents(path);
            var room=root.transform.Find("Room shell and fixed exhibits");
            var vents=room.Cast<Transform>().Where(t=>t.name=="Ventilation grille").ToArray();
            for(var i=0;i<vents.Length;i++) vents[i].localPosition=new Vector3(-2.22f+i*.105f,2.6f,3.25f);
            var shelf=root.transform.Find("Height adjustable workstation/Reachable atom shelf");
            foreach(var post in shelf.Cast<Transform>().Where(t=>t.name=="Shelf upright"))
            { var position=post.localPosition; position.y=-.16f; post.localPosition=position; post.localScale=new Vector3(.035f,1.3f,.035f); }
            foreach(var child in room.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets();
        }
        public static void FinalizeAndBuild()
        {
            FinalizeEnvironment(); QuestProjectSetup.BuildQuest();
        }
        static void PlaceAsset(string name,Transform parent,Vector3 position,float maxSize,Color color)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoleculeLab/Art/KenneyFurniture/"+name+".obj"); if(asset==null) return;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset); go.name="Fixed Kenney "+name; go.transform.SetParent(parent,false);
            var renderers=go.GetComponentsInChildren<Renderer>(); var bounds=new Bounds(Vector3.zero,Vector3.zero);
            if(renderers.Length>0) { bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds); }
            var size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            go.transform.localScale=Vector3.one*(maxSize/Mathf.Max(.001f,size)); go.transform.localPosition=position-Vector3.up*bounds.min.y*go.transform.localScale.x;
            foreach(var renderer in renderers) renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>LabVisuals.Material(color)).ToArray();
        }
    }
}
