using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MoleculeLab
{
    /// <summary>
    /// Shows the new classroom in the Scene and Game tabs without pressing Play. The room, beakers, reaction corner, gallery
    /// and menu are built by code at runtime; here the same code builds them onto a hidden copy of the environment, while
    /// the scene's own environment is simply not drawn. Nothing of the preview is saved: the copy is marked DontSave, the
    /// original is only hidden with a non-serialized render switch, and the lighting the classroom changes is put back
    /// before every save. Off in Play mode and during test runs. Molecule Lab > Show new design in edit mode toggles it.
    /// </summary>
    [InitializeOnLoad]
    static class ClassroomEditPreview
    {
        const string MenuPath="Molecule Lab/Show new design in edit mode", PreferenceKey="MoleculeLab.EditPreview", Suspended="MoleculeLab.PreviewSuspended";
        const string PreviewName="Classroom preview (editor only, not saved)";
        static GameObject root;
        static readonly List<Renderer> hidden=new List<Renderer>();
        static LightingSnapshot lighting;
        static double nextCheck;
        static bool reported, reportedWait;
        static bool Enabled { get => EditorPrefs.GetBool(PreferenceKey,true); set => EditorPrefs.SetBool(PreferenceKey,value); }

        static ClassroomEditPreview()
        {
            EditorApplication.update+=Poll;
            EditorApplication.playModeStateChanged+=state=>{ if(state==PlayModeStateChange.ExitingEditMode) Remove(); };
            EditorSceneManager.sceneSaving+=(scene,path)=>{ if(root!=null) Restore(false); };
            EditorSceneManager.sceneSaved+=scene=>{ if(root!=null) Hide(); };
            EditorSceneManager.sceneClosing+=(scene,removing)=>Remove();
            AssemblyReloadEvents.beforeAssemblyReload+=Remove;
        }
        [MenuItem(MenuPath,false,40)] static void Toggle() { Enabled=!Enabled; if(!Enabled) Remove(); nextCheck=0; }
        [MenuItem(MenuPath,true)] static bool ToggleState() { Menu.SetChecked(MenuPath,Enabled); return true; }
        [MenuItem("Molecule Lab/Rebuild edit-mode preview",false,41)] static void Rebuild() { Remove(); nextCheck=0; }

        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextCheck) return; nextCheck=EditorApplication.timeSinceStartup+1.5;
            var wanted=Enabled && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating
                && !SessionState.GetBool(Suspended,false) && !BuildPipeline.isBuildingPlayer;
            if(!wanted) { if(root!=null) Remove(); if(!reportedWait && Enabled) { reportedWait=true; Debug.Log("Classroom preview waiting: playing="+EditorApplication.isPlayingOrWillChangePlaymode+" compiling="+EditorApplication.isCompiling+" updating="+EditorApplication.isUpdating+" suspended="+SessionState.GetBool(Suspended,false)); } return; }
            if(root!=null) { CaptureIfAsked(); return; }
            Build();
        }

        static void Build()
        {
            // A preview left over from before a script reload is found by name and replaced.
            foreach(var stale in Resources.FindObjectsOfTypeAll<GameObject>().Where(g=>g!=null && g.name==PreviewName && g.transform.parent==null).ToArray()) Object.DestroyImmediate(stale);
            var scene=SceneManager.GetActiveScene();
            var original=Object.FindObjectsByType<LabEnvironment>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(e=>e.gameObject.scene==scene);
            var lab=Object.FindObjectsByType<LabController>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).FirstOrDefault(l=>l.gameObject.scene==scene);
            if(original==null || lab==null)
            {
                if(!reported) { reported=true; Debug.Log("Classroom preview: the open scene ("+scene.name+") has no lab environment or LabController, so there is nothing to preview."); }
                nextCheck=EditorApplication.timeSinceStartup+10; return;
            }

            lighting=LightingSnapshot.Take();
            var rootsBefore=new HashSet<GameObject>(scene.GetRootGameObjects());
            root=new GameObject(PreviewName); root.hideFlags=HideFlags.DontSave;
            try
            {
                // The copy keeps the original's position; its own lights are off (the scene's lights still light it).
                var copy=Object.Instantiate(original.gameObject,original.transform.parent,true); copy.name=original.name+" (preview)";
                foreach(var light in copy.GetComponentsInChildren<Light>(true)) light.enabled=false;
                // A throwaway LabController with the scene one's settings, so the scene's own component is never touched.
                var builder=new GameObject("Preview lab").AddComponent<LabController>(); builder.transform.SetParent(root.transform,false);
                EditorUtility.CopySerialized(lab,builder);
                builder.BuildEditorPreview(copy.GetComponent<LabEnvironment>());
                copy.transform.SetParent(root.transform,true);
                // Anything the builders put at the scene root joins the preview, so it is cleaned up with it.
                foreach(var made in scene.GetRootGameObjects()) if(made!=root && !rootsBefore.Contains(made)) made.transform.SetParent(root.transform,true);
                foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags=HideFlags.DontSave;
                original.GetComponentsInChildren(true,hidden);
                Hide();
                SceneView.RepaintAll(); UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                Debug.Log("Showing the new classroom in the Scene and Game tabs (editor preview, not saved). Molecule Lab > Show new design in edit mode turns it off.");
            }
            catch(System.Exception exception)
            {
                Debug.LogWarning("Edit-mode classroom preview failed (Play mode is unaffected): "+exception);
                Remove(); nextCheck=EditorApplication.timeSinceStartup+30;
            }
        }
        /// <summary>Temp/MoleculeLab.capture-edit-mode: saves what the Game tab's camera sees (for checking the preview remotely).</summary>
        static void CaptureIfAsked()
        {
            const string request="Temp/MoleculeLab.capture-edit-mode";
            if(!System.IO.File.Exists(request)) return;
            System.IO.File.Delete(request);
            var camera=Camera.main; if(camera==null) return;
            var target=new RenderTexture(1600,1000,24); var previous=camera.targetTexture;
            camera.targetTexture=target; camera.Render(); camera.targetTexture=previous;
            RenderTexture.active=target; var image=new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); RenderTexture.active=null;
            System.IO.Directory.CreateDirectory("Documentation/Classroom");
            System.IO.File.WriteAllBytes("Documentation/Classroom/0-edit-mode-game-tab.png",image.EncodeToPNG());
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        }
        static void Hide() { foreach(var r in hidden) if(r!=null) r.forceRenderingOff=true; lighting?.ApplyClassroom(); }
        /// <summary>Shows the scene's own environment again and puts the lighting back as it is saved.</summary>
        static void Restore(bool forget)
        {
            foreach(var r in hidden) if(r!=null) r.forceRenderingOff=false;
            lighting?.Restore();
            if(forget) { hidden.Clear(); lighting=null; }
        }
        static void Remove()
        {
            Restore(true);
            if(root!=null) Object.DestroyImmediate(root);
            root=null;
        }

        /// <summary>The scene lighting the classroom changes (sun and ambient), so the saved scene keeps its own.</summary>
        sealed class LightingSnapshot
        {
            Light sun; Color sunColour; float sunIntensity; Quaternion sunRotation;
            AmbientMode mode; Color sky, equator, ground;
            Color classroomSun; float classroomIntensity; Quaternion classroomRotation; AmbientMode classroomMode; Color classroomSky, classroomEquator, classroomGround;
            bool captured;
            public static LightingSnapshot Take()
            {
                var s=new LightingSnapshot{sun=RenderSettings.sun};
                if(s.sun==null) s.sun=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
                if(s.sun!=null) { s.sunColour=s.sun.color; s.sunIntensity=s.sun.intensity; s.sunRotation=s.sun.transform.rotation; }
                s.mode=RenderSettings.ambientMode; s.sky=RenderSettings.ambientSkyColor; s.equator=RenderSettings.ambientEquatorColor; s.ground=RenderSettings.ambientGroundColor;
                EditorApplication.delayCall+=s.Capture; // after the build has set the classroom lighting
                return s;
            }
            void Capture()
            {
                if(sun!=null) { classroomSun=sun.color; classroomIntensity=sun.intensity; classroomRotation=sun.transform.rotation; }
                classroomMode=RenderSettings.ambientMode; classroomSky=RenderSettings.ambientSkyColor; classroomEquator=RenderSettings.ambientEquatorColor; classroomGround=RenderSettings.ambientGroundColor;
                captured=true;
            }
            public void Restore()
            {
                if(!captured) Capture();
                if(sun!=null) { sun.color=sunColour; sun.intensity=sunIntensity; sun.transform.rotation=sunRotation; }
                RenderSettings.ambientMode=mode; RenderSettings.ambientSkyColor=sky; RenderSettings.ambientEquatorColor=equator; RenderSettings.ambientGroundColor=ground;
            }
            public void ApplyClassroom()
            {
                if(!captured) return;
                if(sun!=null) { sun.color=classroomSun; sun.intensity=classroomIntensity; sun.transform.rotation=classroomRotation; }
                RenderSettings.ambientMode=classroomMode; RenderSettings.ambientSkyColor=classroomSky; RenderSettings.ambientEquatorColor=classroomEquator; RenderSettings.ambientGroundColor=classroomGround;
            }
        }
    }
}
