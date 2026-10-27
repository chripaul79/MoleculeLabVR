using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEditor.Build;

namespace MoleculeLab.Editor
{
    // Check the generated output, not just the serialized feature toggles: other
    // feature validation rules can re-enable Android XR dependencies during build.
    public sealed class QuestManifestValidation : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            XNamespace android="http://schemas.android.com/apk/res/android";
            var manifests=new[]{Path.Combine(path,"src/main/AndroidManifest.xml"),Path.Combine(path,"xrmanifest.androidlib/AndroidManifest.xml")};
            var docs=manifests.Select(XDocument.Load).ToArray();
            var entries=docs.SelectMany(d=>d.Descendants()).ToArray();
            if(entries.Any(e=>e.Name.LocalName=="uses-feature" && ((string)e.Attribute(android+"name")??"").StartsWith("android.software.xr.api.") && (string)e.Attribute(android+"required")=="true"))
                throw new BuildFailedException("Quest build accidentally requires Android XR hardware. Disable all Unity.XR.AndroidOpenXR features.");
            if(!entries.Any(e=>(string)e.Attribute(android+"name")=="com.oculus.supportedDevices" && ((string)e.Attribute(android+"value")??"").Contains("eureka")))
                throw new BuildFailedException("Quest 3 is missing from the supported-device manifest.");
            if(!entries.Any(e=>(string)e.Attribute(android+"name")=="com.oculus.permission.HAND_TRACKING"))
                throw new BuildFailedException("Quest hand-tracking permission is missing.");
            // Free build's PubChem check needs network access (Player Settings > Internet Access: Require).
            var internet=entries.Any(e=>e.Name.LocalName=="uses-permission" && (string)e.Attribute(android+"name")=="android.permission.INTERNET");
            if(!internet) UnityEngine.Debug.LogWarning("INTERNET permission not found in the Unity library manifest. Free build's PubChem check needs Internet Access set to Require.");
            File.WriteAllText("Documentation/QuestManifestValidation.txt","PASS: Quest 3 (eureka) supported.\nPASS: Meta hand-tracking permission included.\nPASS: No required Android XR software features.\n"+
                (internet?"PASS: INTERNET permission included (PubChem free build).\n":"WARN: INTERNET permission not seen in the library manifest; verify Internet Access = Require.\n"));
        }
    }
}
