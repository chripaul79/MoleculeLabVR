using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoleculeLab.Editor
{
    /// <summary>
    /// Molecule Lab > Capture classroom preview: enters Play mode, photographs the classroom from several viewpoints into
    /// Documentation/Classroom/*.png, then leaves Play mode. Used to check the room without a headset.
    /// </summary>
    [InitializeOnLoad]
    public static class ClassroomPreviewCapture
    {
        const string Key="MoleculeLab.CaptureClassroom";
        static double readyAt, timeoutAt;
        const string RequestFile="Temp/MoleculeLab.capture-classroom";
        static double nextPoll;
        static ClassroomPreviewCapture()
        {
            if(SessionState.GetBool(Key,false)) Register();
            // Creating Temp/MoleculeLab.capture-classroom also starts a capture (lets tools trigger it without the menu).
            EditorApplication.update+=() =>
            {
                if(EditorApplication.timeSinceStartup<nextPoll) return; nextPoll=EditorApplication.timeSinceStartup+2;
                if(!File.Exists(RequestFile) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                // Pick up script and asset edits made outside the editor first (Unity only auto-refreshes when focused).
                AssetDatabase.Refresh();
                if(EditorApplication.isCompiling || EditorApplication.isUpdating) return; // the request survives the reload and runs afterwards
                File.Delete(RequestFile); Capture();
            };
        }
        [MenuItem("Molecule Lab/Capture classroom preview")]
        public static void Capture()
        {
            if(EditorApplication.isPlaying) { Register(); return; }
            var active=EditorSceneManager.GetActiveScene();
            if(active.path!="Assets/Scenes/MoleculeLab.unity")
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene("Assets/Scenes/MoleculeLab.unity");
            }
            SessionState.SetBool(Key,true); Register(); EditorApplication.EnterPlaymode();
        }
        static void Register() { readyAt=EditorApplication.timeSinceStartup+4; timeoutAt=EditorApplication.timeSinceStartup+120; EditorApplication.update-=Tick; EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>timeoutAt) { Stop(); Debug.LogError("Classroom preview: Play mode did not start in time."); return; }
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<readyAt) return;
            var lab=UnityEngine.Object.FindAnyObjectByType<LabController>(); if(lab==null || lab.Classroom==null) return;
            Stop();
            try
            {
                var camera=Camera.main; if(camera==null) throw new Exception("No main camera.");
                camera.nearClipPlane=.03f; camera.farClipPlane=40;
                Directory.CreateDirectory("Documentation/Classroom");
                Shot(camera,"1-overview",new Vector3(-2.45f,1.75f,-1.35f),new Vector3(1.4f,1.0f,2.7f),70);
                Shot(camera,"2-player-view",new Vector3(0,1.62f,-.5f),new Vector3(0,1.15f,1.6f),75);
                Shot(camera,"3-windows",new Vector3(-1.6f,1.6f,.4f),new Vector3(2.9f,1.3f,.55f),75);
                Shot(camera,"4-whiteboard",new Vector3(1.6f,1.6f,.4f),new Vector3(-2.9f,1.45f,.6f),75);
                Shot(camera,"5-rear-counters",new Vector3(.2f,1.55f,1.2f),new Vector3(0,1.0f,3.2f),75);
                Shot(camera,"6-gallery",new Vector3(.3f,1.7f,-.9f-GalleryRoom.Lengthen),new Vector3(0,1.2f,GalleryRoom.RoomEnd+.05f),75);
                Shot(camera,"15-gallery-entrance",new Vector3(.4f,1.55f,.9f),new Vector3(0,2.15f,GalleryRoom.RoomStart),70);
                Shot(camera,"16-gallery-panel",new Vector3(0,1.75f,GalleryRoom.RoomEnd+1.85f),new Vector3(0,1.9f,GalleryRoom.RoomEnd+.05f),70);
                var station=GameObject.Find("Reaction station (runtime)");
                if(station!=null) Shot(camera,"8-reaction-station",station.transform.position+new Vector3(-.8f,.55f,.35f),station.transform.position+Vector3.up*.12f,60);
                if(station!=null) Shot(camera,"14-reaction-corner",new Vector3(.3f,1.6f,.4f),new Vector3(3.0f,1.2f,-1.0f),75);
                var bin=GameObject.Find("Trash bin (runtime)");
                if(bin!=null) Shot(camera,"17-trash-bin",bin.transform.position+new Vector3(-.35f,1.35f,-1.0f),bin.transform.position+Vector3.up*.95f,55);
                var beaker=GameObject.Find("Beaker C");
                if(beaker!=null) Shot(camera,"7-beakers",beaker.transform.position+new Vector3(.25f,.25f,-.45f),beaker.transform.position+Vector3.up*.05f,55);
                Debug.Log("Classroom preview saved to Documentation/Classroom. "+lab.Classroom.PlacedModels+" models placed"+
                    (lab.Classroom.Skipped.Count>0?"; skipped: "+string.Join(", ",lab.Classroom.Skipped):"."));
                if(lab.Station!=null) { ScheduleEffects(camera,lab); return; }
            }
            catch(Exception e) { Debug.LogException(e); }
            EditorApplication.ExitPlaymode();
        }
        // ---------- reaction effects, photographed over a few seconds of play ----------
        static readonly System.Collections.Generic.List<(double at,Action act)> steps=new System.Collections.Generic.List<(double,Action)>();
        static void ScheduleEffects(Camera camera,LabController lab)
        {
            var station=lab.Station; var t0=EditorApplication.timeSinceStartup;
            var eye=station.transform.position+new Vector3(-.75f,.42f,.3f); var at=station.transform.position+Vector3.up*.14f;
            void At(double dt,Action a) => steps.Add((t0+dt,a));
            At(0,()=>station.PlayReaction(ReactionCondition.Ignition));
            At(.3,()=>Shot(camera,"9-fire",eye,at,55));
            At(1.4,()=>Shot(camera,"10-steam",eye,at,55));
            At(2.8,()=>station.PlayReaction(ReactionCondition.PalladiumCatalyst));
            At(3.5,()=>Shot(camera,"11-sparkles",eye,at,55));
            At(4.8,()=>station.PlayReaction(ReactionCondition.AcidCatalyst));
            At(5.6,()=>Shot(camera,"12-bubbles",eye,at,55));
            At(6.9,()=>
            {
                var offsets=new[]{new Vector3(-.13f,.02f,0),new Vector3(.13f,.06f,0),new Vector3(0,.1f,.05f)};
                for(var i=0;i<station.Tools.Count;i++) { var tool=station.Tools[i]; tool.previewHeld=true; tool.transform.position=station.Mouth+offsets[i]; }
            });
            At(8.2,()=>Shot(camera,"13-tools",station.transform.position+new Vector3(-.6f,.5f,.3f),station.Mouth+Vector3.down*.05f,55));
            At(8.4,()=>{ Debug.Log("Effect previews saved."); EditorApplication.ExitPlaymode(); });
            EditorApplication.update-=EffectsTick; EditorApplication.update+=EffectsTick;
        }
        static void EffectsTick()
        {
            if(!EditorApplication.isPlaying) { steps.Clear(); EditorApplication.update-=EffectsTick; return; }
            while(steps.Count>0 && EditorApplication.timeSinceStartup>=steps[0].at)
            {
                var step=steps[0]; steps.RemoveAt(0);
                try { step.act(); } catch(Exception e) { Debug.LogException(e); }
            }
            if(steps.Count==0) EditorApplication.update-=EffectsTick;
        }
        static void Stop() { EditorApplication.update-=Tick; SessionState.SetBool(Key,false); }
        static void Shot(Camera camera,string name,Vector3 from,Vector3 to,float fov)
        {
            camera.transform.position=from; camera.transform.LookAt(to); camera.fieldOfView=fov;
            foreach(var label in UnityEngine.Object.FindObjectsByType<FaceViewer>()) label.transform.rotation=Quaternion.LookRotation(label.transform.position-camera.transform.position);
            Canvas.ForceUpdateCanvases();
            var texture=new RenderTexture(1600,1000,24); texture.Create(); camera.aspect=1.6f;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
            var previous=RenderTexture.active; RenderTexture.active=texture;
            var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); image.Apply();
            File.WriteAllBytes("Documentation/Classroom/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=previous; texture.Release(); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(texture);
        }
    }
}
// gallery entrance and panel shots
