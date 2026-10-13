using System;
using UnityEngine;
namespace MoleculeLab
{
    [Serializable] public struct ReactionAmount
    {
        public MoleculeDefinition molecule;
        [Min(1)] public int count;
        public ReactionAmount(MoleculeDefinition m, int n) { molecule = m; count = n; }
    }
    public enum ReactionCondition { None, Ignition, PalladiumCatalyst, AcidCatalyst }
    [CreateAssetMenu(menuName = "Molecule Lab/Reaction")]
    public sealed class ReactionDefinition : ScriptableObject
    {
        public string id, title, equation;
        public ReactionCondition requiredCondition;
        public bool reversible;
        [TextArea] public string conditions;
        public string[] sources = Array.Empty<string>();
        [TextArea] public string explanation;
        public ReactionAmount[] inputs, outputs;
    }
}
