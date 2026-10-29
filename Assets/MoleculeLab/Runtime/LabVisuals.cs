using System.Collections.Generic;
using TMPro;
using UnityEngine;
namespace MoleculeLab
{
    public static class LabVisuals
    {
        /// <summary>Visual size of atoms relative to ElementDefinition.radius (smaller balls, same bond lengths).</summary>
        public const float AtomScale = .6f;
        static readonly Dictionary<Color,Material> materials = new Dictionary<Color,Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { materials.Clear(); }
        public static Material Material(Color color)
        {
            if (!materials.TryGetValue(color, out var material) || material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color; material.SetFloat("_Smoothness", .35f);
                materials[color] = material;
            }
            return material;
        }
        public static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            go.GetComponent<Collider>().enabled = collider;
            return go;
        }
        /// <summary>Destroy at runtime; DestroyImmediate in the editor's edit-mode preview of the classroom.</summary>
        public static void Discard(Object target)
        {
            if(target==null) return;
            if(Application.isPlaying) Object.Destroy(target); else Object.DestroyImmediate(target);
        }
        /// <summary>The renderer's own material copies (renderer.materials), without leaking them into the scene in edit mode.</summary>
        public static Material[] OwnMaterials(Renderer renderer)
        {
            if(Application.isPlaying) return renderer.materials;
            var shared=renderer.sharedMaterials; var copies=new Material[shared.Length];
            for(var i=0;i<shared.Length;i++) if(shared[i]!=null) copies[i]=new Material(shared[i]){hideFlags=HideFlags.DontSave};
            renderer.sharedMaterials=copies; return copies;
        }
        public static TextMeshPro Label(Transform parent, string text, Vector3 position, float size = .04f)
        {
            var go = new GameObject("Label", typeof(TextMeshPro)); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.GetComponent<TextMeshPro>(); label.text = text; label.fontSize = size * 10;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(1.1f, .22f);
            label.textWrappingMode = TextWrappingModes.Normal;
            go.AddComponent<FaceViewer>();
            return label;
        }
    }
    public sealed class FaceViewer : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main != null) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
