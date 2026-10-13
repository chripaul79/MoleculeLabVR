using UnityEngine;
namespace MoleculeLab
{
    [CreateAssetMenu(menuName = "Molecule Lab/Element")]
    public sealed class ElementDefinition : ScriptableObject
    {
        public string symbol;
        public string displayName;
        public Color color = Color.white;
        [Range(.025f, .08f)] public float radius = .045f;
        public int maximumBondOrder = 1;
    }
}
