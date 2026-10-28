using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoleculeLab.Editor
{
    /// <summary>
    /// Makes the three imported Asset Store packs usable in this URP project and wires them into the classroom:
    /// 1. converts their materials (built-in Standard, HDRP Lit, legacy custom shaders) to URP Lit, which otherwise render pink;
    /// 2. caps their 4K textures at 2048 on Android so the Quest build stays light;
    /// 3. creates/fills Assets/MoleculeLab/Resources/ClassroomKit.asset with the models the classroom uses.
    /// Runs by itself after scripts compile (it only touches what still needs it) and from the Molecule Lab menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ImportedAssetSetup
    {
        public const string LabPack="Assets/3D Laboratory Environment with Appratus", MinimalPack="Assets/Dzeruza", ScifiKit="Assets/Creepy_Cat";
        public const string KitPath="Assets/MoleculeLab/Resources/ClassroomKit.asset";
        const string L=LabPack+"/Prefabs/", D=MinimalPack+"/MinimalScifiPack/3DModels/", C=ScifiKit+"/3D Scifi Kit Starter Kit_HD/Prefabs/";
        static readonly Dictionary<string,string> KitPaths=new Dictionary<string,string>
        {
            {"beaker",L+"Beaker.prefab"},{"labCounter",L+"shelf showcase.prefab"},{"desk",L+"table with drawers.prefab"},{"chair",L+"chair.prefab"},
            {"wallRack",L+"rack.prefab"},{"storageBox",L+"Box.prefab"},{"instrument",L+"analytical_lab_instruments.prefab"},{"tap",L+"tap.prefab"},
            {"erlenmeyer",L+"Erlenmeyer_flask with water.prefab"},{"florence",L+"florence_flask with water.prefab"},{"beakerWater",L+"Beaker water.prefab"},
            {"cylinder",L+"Graduated_Cylinder with liquid.prefab"},{"spiritLamp",L+"Spirit_Lamp with water.prefab"},{"boilingStand",L+"Boiling_Liquid_Stand.prefab"},
            {"funnel",L+"glass_funnel.prefab"},{"chinaDish",L+"china dish.prefab"},{"testTube",L+"Glass_Lab_test_tube with liquid.prefab"},
            {"erlenmeyerEmpty",L+"Erlenmeyer_flask.prefab"},
            {"wallPanelWide",D+"Walls/Prefabs/SM_Trim_Wall_3x4M_B1.prefab"},{"wallPanelNarrow",D+"Walls/Prefabs/SM_Trim_Wall_3x2M_B1.prefab"},
            {"canister",D+"Props/Prefabs/SM_Canister1.prefab"},{"scifiBox",D+"Props/Prefabs/SM_Box1.prefab"},
            {"sideLight",C+"Lights/Light_SideLight_01.prefab"},
            {"labWall",L+"wall01.prefab"},{"labFloor",L+"floor.prefab"},{"labRoof",L+"roof.prefab"},
            {"toolFlame",L+"Spirit_Lamp with water.prefab"},{"toolCatalyst",L+"crucible_and_cover.prefab"},{"toolAcid",LabPack+"/Models/Glass_medical_dropper.fbx"},
            {"labShelf",L+"shelf.prefab"},{"labWaterShelf",L+"water Shelf.prefab"},
            {"woodMaterial",LabPack+"/Materials/Laboratory03_Wood_BaseMap.mat"},{"metalMaterial",LabPack+"/Materials/Laboratory03_Others_BaseMap.mat"},{"labSign",C+"Symbols/Word_Lab.prefab"},
            {"floorTiles",LabPack+"/Textures/Laboratory03_Floor_BaseMap.png"},{"floorNormal",LabPack+"/Textures/Laboratory03_Floor_Normal.png"},
        };
        static ImportedAssetSetup()
        {
            EditorApplication.delayCall+=() => { if(!EditorApplication.isPlayingOrWillChangePlaymode && !Application.isBatchMode) Run(false); };
        }
        [MenuItem("Molecule Lab/Set up imported asset packs")]
        static void Menu() => Run(true);

        public static void Run(bool verbose)
        {
            var packs=new[]{LabPack,MinimalPack,ScifiKit}.Where(AssetDatabase.IsValidFolder).ToArray();
            if(packs.Length==0) { if(verbose) Debug.Log("Molecule Lab: none of the imported asset packs were found."); return; }
            int converted=0, textures=0; var kitChanged=false;
            try
            {
                AssetDatabase.StartAssetEditing();
                converted=ConvertMaterials(packs);
                converted+=RepairLeftoverEmission(packs);
                converted+=RepairPremultipliedGlass(packs);
                textures=LimitTextures(packs);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            kitChanged=FillKit();
            AssetDatabase.SaveAssets();
            if(verbose || converted>0 || textures>0 || kitChanged)
                Debug.Log($"Molecule Lab: imported packs ready. {converted} material(s) converted to URP, {textures} texture(s) capped for Quest, classroom kit {(kitChanged?"updated":"unchanged")} ({KitPath}).");
        }

        // ---------- materials ----------
        static int ConvertMaterials(string[] folders)
        {
            var lit=Shader.Find("Universal Render Pipeline/Lit"); if(lit==null) { Debug.LogError("Molecule Lab: URP Lit shader not found."); return 0; }
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Material",folders))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null || !NeedsConversion(material)) continue;
                try { Convert(material,lit); EditorUtility.SetDirty(material); count++; }
                catch(Exception e) { Debug.LogWarning("Molecule Lab: could not convert "+path+": "+e.Message); }
            }
            return count;
        }
        /// <summary>
        /// Materials converted from HDRP before the fix above glow white (Minimal Sci-fi Pack walls). If the HDRP emission
        /// was black and there is no emission map, switch URP emission off again.
        /// </summary>
        static int RepairLeftoverEmission(string[] folders)
        {
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Material",folders))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(material==null || !material.IsKeywordEnabled("_EMISSION") || material.GetTexture("_EmissionMap")!=null) continue;
                var colors=new SerializedObject(material).FindProperty("m_SavedProperties").FindPropertyRelative("m_Colors");
                Color? hdrpEmission=null;
                for(var i=0;i<colors.arraySize;i++) { var pair=colors.GetArrayElementAtIndex(i); if(pair.FindPropertyRelative("first").stringValue=="_EmissiveColor") hdrpEmission=pair.FindPropertyRelative("second").colorValue; }
                if(hdrpEmission==null || hdrpEmission.Value.maxColorComponent>.01f) continue;
                material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Color.black);
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack; EditorUtility.SetDirty(material); count++;
            }
            return count;
        }
        /// <summary>
        /// Glass converted with premultiplied blending adds its full colour regardless of alpha, so it looks like white
        /// plastic. Switch transparent pack materials to ordinary alpha blending.
        /// </summary>
        static int RepairPremultipliedGlass(string[] folders)
        {
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Material",folders))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if(material==null || !material.HasProperty("_Surface") || material.GetFloat("_Surface")<.5f || material.GetFloat("_Blend")<.5f) continue;
                MakeAlphaBlended(material); EditorUtility.SetDirty(material); count++;
            }
            return count;
        }
        public static void MakeAlphaBlended(Material material)
        {
            material.SetFloat("_Blend",0); material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        static bool NeedsConversion(Material material)
        {
            var shader=material.shader; if(shader==null) return true;
            var name=shader.name;
            if(name.StartsWith("Universal Render Pipeline/") || name.StartsWith("Shader Graphs/") || name.StartsWith("TextMeshPro") || name.StartsWith("UI/") || name.StartsWith("Skybox/")) return false;
            return true; // Standard, HDRP/*, Legacy Shaders/*, InternalErrorShader, pack-specific built-in shaders
        }
        /// <summary>Reads the saved properties directly (they survive even when the original shader is missing) and rebuilds the material on URP Lit.</summary>
        static void Convert(Material material,Shader lit)
        {
            var textures=new Dictionary<string,(Texture texture,Vector2 scale,Vector2 offset)>(); var floats=new Dictionary<string,float>(); var colors=new Dictionary<string,Color>();
            var saved=new SerializedObject(material).FindProperty("m_SavedProperties");
            var texEnvs=saved.FindPropertyRelative("m_TexEnvs");
            for(var i=0;i<texEnvs.arraySize;i++)
            {
                var pair=texEnvs.GetArrayElementAtIndex(i); var value=pair.FindPropertyRelative("second");
                var texture=value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
                if(texture!=null) textures[pair.FindPropertyRelative("first").stringValue]=(texture,value.FindPropertyRelative("m_Scale").vector2Value,value.FindPropertyRelative("m_Offset").vector2Value);
            }
            var floatArray=saved.FindPropertyRelative("m_Floats");
            for(var i=0;i<floatArray.arraySize;i++) { var pair=floatArray.GetArrayElementAtIndex(i); floats[pair.FindPropertyRelative("first").stringValue]=pair.FindPropertyRelative("second").floatValue; }
            var colorArray=saved.FindPropertyRelative("m_Colors");
            for(var i=0;i<colorArray.arraySize;i++) { var pair=colorArray.GetArrayElementAtIndex(i); colors[pair.FindPropertyRelative("first").stringValue]=pair.FindPropertyRelative("second").colorValue; }
            var keywords=new HashSet<string>(material.shaderKeywords);
            var hdrp=material.shader!=null && material.shader.name.StartsWith("HDRP") || floats.ContainsKey("_SurfaceType") || textures.ContainsKey("_MaskMap") || textures.ContainsKey("_BaseColorMap");

            (Texture texture,Vector2 scale,Vector2 offset)? Tex(params string[] names) { foreach(var n in names) if(textures.TryGetValue(n,out var t)) return t; return null; }
            float F(float fallback,params string[] names) { foreach(var n in names) if(floats.TryGetValue(n,out var f)) return f; return fallback; }
            Color? Col(params string[] names) { foreach(var n in names) if(colors.TryGetValue(n,out var c)) return c; return null; }

            material.shader=lit;
            foreach(var k in material.shaderKeywords) material.DisableKeyword(k);
            var baseMap=Tex("_BaseMap","_MainTex","_BaseColorMap","_Albedo","_Diffuse");
            var color=Col("_BaseColor","_Color") ?? Color.white;
            if(baseMap!=null) { material.SetTexture("_BaseMap",baseMap.Value.texture); material.SetTextureScale("_BaseMap",baseMap.Value.scale); material.SetTextureOffset("_BaseMap",baseMap.Value.offset); material.SetTexture("_MainTex",baseMap.Value.texture); }

            var normal=Tex("_BumpMap","_NormalMap");
            if(normal!=null) { material.SetTexture("_BumpMap",normal.Value.texture); material.SetFloat("_BumpScale",F(1,"_BumpScale","_NormalScale")); material.EnableKeyword("_NORMALMAP"); }

            var mask=Tex("_MetallicGlossMap","_MaskMap");
            if(mask!=null)
            {
                // Standard metallic map and HDRP mask map both keep metal in R and smoothness in A, as URP expects.
                material.SetTexture("_MetallicGlossMap",mask.Value.texture); material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness",Mathf.Clamp01(F(1,"_GlossMapScale","_Smoothness")));
                if(hdrp && textures.ContainsKey("_MaskMap")) { material.SetTexture("_OcclusionMap",mask.Value.texture); material.EnableKeyword("_OCCLUSIONMAP"); }
            }
            else { material.SetFloat("_Metallic",Mathf.Clamp01(F(0,"_Metallic"))); material.SetFloat("_Smoothness",Mathf.Clamp01(F(.5f,"_Glossiness","_Smoothness"))); }
            var occlusion=Tex("_OcclusionMap"); if(occlusion!=null) { material.SetTexture("_OcclusionMap",occlusion.Value.texture); material.EnableKeyword("_OCCLUSIONMAP"); }

            var emissionMap=Tex("_EmissionMap","_EmissiveColorMap");
            // HDRP keeps its real emission in _EmissiveColor; its leftover _EmissionColor is often white and would make walls glow.
            var emission=(hdrp?Col("_EmissiveColor","_EmissionColor"):Col("_EmissionColor","_EmissiveColor")) ?? Color.black;
            if(hdrp) { var peak=emission.maxColorComponent; if(peak>4) emission/=peak/4; } // HDRP stores emission in nits
            if(emissionMap!=null || emission.maxColorComponent>.01f)
            {
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",emissionMap!=null && emission.maxColorComponent<.01f?Color.white:emission);
                if(emissionMap!=null) material.SetTexture("_EmissionMap",emissionMap.Value.texture);
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // Surface: Standard _Mode 1 cutout / 2 fade / 3 transparent; HDRP _SurfaceType 1 transparent, alpha clipping flag; keywords as a last resort.
            var mode=(int)F(0,"_Mode");
            var transparent=mode>=2 || F(0,"_SurfaceType")>.5f || keywords.Contains("_ALPHABLEND_ON") || keywords.Contains("_ALPHAPREMULTIPLY_ON") || keywords.Contains("_SURFACE_TYPE_TRANSPARENT");
            var cutout=!transparent && (mode==1 || F(0,"_AlphaCutoffEnable")>.5f || keywords.Contains("_ALPHATEST_ON"));
            const bool premultiply=false; // premultiplied glass renders as opaque white in URP
            if(transparent && color.a<.18f) color.a=.18f; // glass authored at alpha 0 would vanish without Standard's reflections
            material.SetColor("_BaseColor",color); material.SetColor("_Color",color);

            material.SetFloat("_AlphaClip",cutout?1:0); material.SetFloat("_Cutoff",F(.5f,"_Cutoff","_AlphaCutoff"));
            if(cutout) material.EnableKeyword("_ALPHATEST_ON");
            if(transparent)
            {
                material.SetFloat("_Surface",1); material.SetFloat("_Blend",premultiply?1:0);
                material.SetFloat("_SrcBlend",premultiply?(float)BlendMode.One:(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha",(float)BlendMode.One); material.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0); material.SetOverrideTag("RenderType","Transparent"); material.renderQueue=(int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); if(premultiply) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetShaderPassEnabled("DepthOnly",false); material.SetShaderPassEnabled("ShadowCaster",false);
            }
            else
            {
                material.SetFloat("_Surface",0); material.SetFloat("_SrcBlend",(float)BlendMode.One); material.SetFloat("_DstBlend",(float)BlendMode.Zero); material.SetFloat("_ZWrite",1);
                material.SetOverrideTag("RenderType",cutout?"TransparentCutout":"Opaque"); material.renderQueue=cutout?(int)RenderQueue.AlphaTest:-1;
            }
            material.SetFloat("_Cull",F(2,"_Cull","_CullMode"));
            material.SetFloat("_ReceiveShadows",1);
            // Let URP's own editor code finish the keywords when it is available (it is part of the URP editor assembly).
            TrySetUrpKeywords(material);
        }
        static MethodInfo urpKeywords; static bool urpLooked;
        static void TrySetUrpKeywords(Material material)
        {
            if(!urpLooked)
            {
                urpLooked=true;
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitGUI",false)).FirstOrDefault(t=>t!=null);
                urpKeywords=type?.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="SetMaterialKeywords" && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==typeof(Material));
            }
            try { urpKeywords?.Invoke(null,new object[]{material}); } catch { /* keywords set above are enough */ }
        }

        // ---------- textures ----------
        static int LimitTextures(string[] folders)
        {
            var count=0;
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",folders))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);
                if(!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                var android=importer.GetPlatformTextureSettings("Android");
                var current=android.overridden?android.maxTextureSize:importer.maxTextureSize;
                if(current<=2048) continue;
                android.overridden=true; android.maxTextureSize=2048; importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport(); count++;
            }
            return count;
        }

        // ---------- classroom kit ----------
        static bool FillKit()
        {
            var kit=AssetDatabase.LoadAssetAtPath<ClassroomKit>(KitPath); var changed=false;
            if(kit==null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(KitPath));
                AssetDatabase.Refresh();
                kit=ScriptableObject.CreateInstance<ClassroomKit>(); AssetDatabase.CreateAsset(kit,KitPath); changed=true;
            }
            foreach(var entry in KitPaths)
            {
                var field=typeof(ClassroomKit).GetField(entry.Key); if(field==null || field.GetValue(kit) as UnityEngine.Object!=null) continue;
                var asset=AssetDatabase.LoadAssetAtPath(entry.Value,field.FieldType); if(asset==null) continue;
                field.SetValue(kit,asset); changed=true;
            }
            if(changed) EditorUtility.SetDirty(kit);
            return changed;
        }
    }
}
