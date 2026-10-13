using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoleculeLab.Editor
{
    [InitializeOnLoad]
    public static class LabPreviewCapture
    {
        const string Key="MoleculeLab.CapturePreview";
        static double readyAt;
        static double timeoutAt;
        static int readyFrames;
        static LabPreviewCapture() { if(SessionState.GetBool(Key,false)) Register(); }
        static void Register() { readyFrames=0; readyAt=EditorApplication.timeSinceStartup+3; timeoutAt=EditorApplication.timeSinceStartup+90; EditorApplication.update-=Tick; EditorApplication.update+=Tick; }
        public static void CaptureFinal() { AcademicLabBuilder.FinalizeEnvironment(); Capture(); }
        public static void Capture()
        {
            QuestProjectSetup.Configure();
            EditorSceneManager.OpenScene(QuestProjectSetup.ScenePath);
            SessionState.SetBool(Key,true); Register(); EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>timeoutAt) { SessionState.SetBool(Key,false); Debug.LogError("Lab preview did not initialize within 90 seconds."); EditorApplication.Exit(1); return; }
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<readyAt) return;
            if(GameObject.Find("Lab control panel")==null || ++readyFrames<10) return;
            EditorApplication.update-=Tick; SessionState.SetBool(Key,false);
            try
            {
                if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new Exception("Preview capture requires graphics; omit -nographics.");
                var camera=Camera.main;
                if(camera==null) throw new Exception("The lab has no main camera.");
                camera.transform.position=new Vector3(0,1.8f,-1.25f);
                camera.transform.LookAt(new Vector3(0,1.25f,1.15f));
                camera.fieldOfView=65; camera.nearClipPlane=.03f; camera.farClipPlane=30;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.045f,.075f);
                Save(camera,"Documentation/LabPreview.png");
                var lab=UnityEngine.Object.FindAnyObjectByType<LabController>(); lab.seedTransferDuration=0;
                lab.StartMolecule(lab.molecules.First(m=>m.id=="water"));
                var atom=UnityEngine.Object.FindObjectsByType<LooseAtom>().First(a=>a.Element.symbol=="H" && a.OnShelf);
                atom.TakeFromShelf(); atom.transform.position=lab.Selected.SitePosition(1)+new Vector3(0,.012f,-.012f);
                camera.transform.position=new Vector3(-.05f,1.42f,.04f); camera.transform.LookAt(new Vector3(-.14f,1.19f,.7f)); camera.fieldOfView=55;
                var preview=UnityEngine.Object.FindAnyObjectByType<BondPreview>(); preview.SendMessage("LateUpdate"); lab.PreviewAtom(atom);
                Save(camera,"Documentation/BondPreview.png");
                lab.ResetLab(); lab.SetMode(LabMode.React); lab.SelectReaction(lab.reactions[3]);
                camera.transform.position=new Vector3(.05f,1.55f,-.3f); camera.transform.LookAt(lab.Environment.panelMount.position); camera.fieldOfView=49;
                foreach(var tool in UnityEngine.Object.FindObjectsByType<EnergyTool>()) tool.SendMessage("Update");
                Save(camera,"Documentation/ReactionMenuPreview.png");
                lab.ResetLab(); lab.StartMolecule(lab.molecules.First(m=>m.id=="benzene"));
                for(var i=1;i<lab.Selected.Definition.atoms.Length;i++)
                {
                    var piece=UnityEngine.Object.FindObjectsByType<LooseAtom>().First(a=>a.OnShelf && a.Element==lab.Selected.Definition.atoms[i].element);
                    piece.transform.position=lab.Selected.SitePosition(i); lab.TryAttach(piece);
                }
                lab.Selected.SendMessage("UpdateGeometry");
                foreach(var tool in UnityEngine.Object.FindObjectsByType<EnergyTool>()) tool.SendMessage("Update");
                camera.transform.position=new Vector3(-.1f,1.3f,-.35f); camera.transform.LookAt(lab.Selected.transform.position); camera.fieldOfView=54;
                Save(camera,"Documentation/BenzenePreview.png");
                Debug.Log("MOLECULE_LAB_PREVIEW_CAPTURED"); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static void Save(Camera camera,string path)
        {
            foreach(var atom in UnityEngine.Object.FindObjectsByType<LooseAtom>()) atom.FaceSymbol(camera);
            foreach(var label in UnityEngine.Object.FindObjectsByType<FaceViewer>()) label.transform.rotation=Quaternion.LookRotation(label.transform.position-camera.transform.position);
            Canvas.ForceUpdateCanvases();
            var texture=new RenderTexture(1800,1200,24); texture.Create(); camera.aspect=1.5f;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=texture });
            var previous=RenderTexture.active; RenderTexture.active=texture;
            var image=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); image.Apply();
            Directory.CreateDirectory("Documentation"); File.WriteAllBytes(path,image.EncodeToPNG());
            RenderTexture.active=previous; texture.Release(); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(texture);
        }
    }
}
