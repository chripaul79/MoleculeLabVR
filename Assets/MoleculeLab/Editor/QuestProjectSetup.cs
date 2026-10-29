using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace MoleculeLab.Editor
{
    public static class QuestProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/MoleculeLab.unity";
        public const string RigPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";

        [MenuItem("Molecule Lab/Configure Quest")]
        public static void Configure()
        {
            PlayerSettings.companyName = "Molecule Lab";
            PlayerSettings.productName = "Molecule Lab VR";
            PlayerSettings.bundleVersion="1.1.1"; PlayerSettings.Android.bundleVersionCode=3;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.moleculelab.vr");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
            // The VR template can force SampleScene on entering Play, regardless
            // of which scene is open. Make the lab the explicit startup scene.
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var androidSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, "com.unity.openxr.featureset.android");
                if (androidSet != null) androidSet.isEnabled = false;
                var metaSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, "com.unity.openxr.featureset.meta");
                if (metaSet != null) metaSet.isEnabled = true;
                OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
            }
            var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach (var feature in settings.GetFeatures<OpenXRFeature>())
            {
                if (feature == null) continue;
                var type = feature.GetType().Name;
                if (type.StartsWith("AndroidXR", StringComparison.Ordinal) || feature.GetType().Assembly.GetName().Name == "Unity.XR.AndroidOpenXR") feature.enabled = false;
                if (type == "MetaQuestFeature" || type == "HandTracking" || type == "MetaHandTrackingAim" ||
                    type == "MetaQuestTouchPlusControllerProfile" || type == "OculusTouchControllerProfile") feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(settings);
            Directory.CreateDirectory("Logs");
            AssetDatabase.SaveAssets();
            Validate();
            var profile = AssetDatabase.FindAssets("Quest Development t:BuildProfile")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BuildProfile>).FirstOrDefault();
            if (profile == null)
            {
                var platforms = BuildProfile.GetInstalledPlatformModules().ToArray();
                Debug.Log("Installed build platforms: " + string.Join(", ", platforms.Select(p => p.displayName)));
                var platform = platforms.FirstOrDefault(p => p.displayName.Contains("Meta Quest"));
                if (string.IsNullOrEmpty(platform.displayName)) platform = platforms.First(p => p.displayName.Contains("Android"));
                BuildProfile.CreateBuildProfile(platform.platformGuid, "Quest Development", ProfileReady);
            }
            else ProfileReady(profile);
            Debug.Log("MOLECULE_LAB_QUEST_CONFIGURED");
        }

        static void ProfileReady(BuildProfile profile)
        {
            profile.overrideGlobalScenes = true;
            profile.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            var serialized = new SerializedObject(profile);
            var development = serialized.FindProperty("m_PlatformBuildProfile.m_Development");
            if (development != null) development.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Molecule Lab/Validate Quest")]
        public static void Validate()
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            // Only fix required rules that explicitly provide an automatic fix.
            foreach (var rule in issues.Where(r => r.error && r.fixItAutomatic && r.fixIt != null)) rule.fixIt();
            issues.Clear();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            File.WriteAllLines("Logs/QuestValidation.txt", issues.Count == 0 ? new[] { "PASS: No OpenXR project validation issues." } : issues.Select(r => (r.error ? "ERROR: " : "NOTE: ") + r.message));
            foreach (var issue in issues) Debug.LogWarning(issue.message);
            if (issues.Any(r => r.error)) throw new Exception("Quest OpenXR validation has required errors; see Logs/QuestValidation.txt");
        }

        public static void CreateFoundation()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/BasicScene.unity");
            foreach (var origin in UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>())
                UnityEngine.Object.DestroyImmediate(origin.gameObject);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
            rig.name = "Molecule Lab XR Origin";
            // Stationary workbench: retain tracking and interaction, disable locomotion providers.
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace?.Contains("Locomotion") == true) behaviour.enabled = false;
            var modality = rig.GetComponentInChildren<XRInputModalityManager>(true);
            if (modality == null || modality.leftHand == null || modality.rightHand == null || modality.leftController == null || modality.rightController == null)
                throw new Exception("Hands rig is missing modality references");
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "Foundation Workbench";
            bench.transform.position = new Vector3(0, .8f, .85f);
            bench.transform.localScale = new Vector3(1.6f, .08f, .65f);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Foundation Grab Test";
            ball.transform.position = new Vector3(0, 1, .75f);
            ball.transform.localScale = Vector3.one * .1f;
            ball.AddComponent<Rigidbody>();
            ball.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Configure();
        }

        [MenuItem("Molecule Lab/Build Quest APK")]
        public static void BuildQuest()
        {
            Configure();
            Directory.CreateDirectory("Builds");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "Builds/MoleculeLab-Quest3.apk", options = BuildOptions.Development
            });
            File.WriteAllText("Logs/LastBuild.txt", $"Result: {result.summary.result}\nSize: {result.summary.totalSize}\nDuration: {result.summary.totalTime}\nErrors: {result.summary.totalErrors}\nWarnings: {result.summary.totalWarnings}\n");
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Quest APK build failed");
        }
    }
}
