using System;
using System.Collections.Generic;
using System.Linq;
namespace MoleculeLab
{
    // Pure state: neither input devices nor physics can bypass these rules.
    public sealed class MoleculeState
    {
        public MoleculeDefinition Definition { get; }
        readonly bool[] occupied;
        readonly HashSet<int> broken = new HashSet<int>();
        public bool Consumed { get; private set; }
        public bool Complete => !Consumed && occupied.All(x => x) && broken.Count == 0;
        public event Action Completed;
        public event Action BondChanged;
        public MoleculeState(MoleculeDefinition definition, bool complete = false)
        {
            if (definition == null || !definition.IsValid(out var error)) throw new ArgumentException("Invalid molecule definition");
            Definition = definition;
            occupied = Enumerable.Repeat(complete, definition.atoms.Length).ToArray();
            occupied[definition.seedIndex] = true;
        }
        public bool IsOccupied(int index) => index >= 0 && index < occupied.Length && occupied[index];
        public bool CanPlace(int site, ElementDefinition element, out string reason)
        {
            reason = "";
            if (Consumed) reason = "This molecule has been consumed.";
            else if (site < 0 || site >= occupied.Length) reason = "No bonding site here.";
            else if (occupied[site]) reason = "This bonding site is occupied.";
            else if (Definition.atoms[site].element != element) reason = "This site needs " + Definition.atoms[site].element.symbol + ".";
            else if (!Definition.bonds.Any(b => b.a == site && occupied[b.b] || b.b == site && occupied[b.a])) reason = "Build the adjoining atom first.";
            return reason.Length == 0;
        }
        public bool TryPlace(int site, ElementDefinition element, out string reason)
        {
            if (!CanPlace(site, element, out reason)) return false;
            occupied[site] = true;
            if (Complete) Completed?.Invoke();
            return true;
        }
        public bool TryBreak(int bond)
        {
            if (!Complete || bond != Definition.rotatableBond || bond < 0) return false;
            broken.Add(bond); BondChanged?.Invoke(); return true;
        }
        public bool TryRejoin(int bond)
        {
            if (Consumed || !broken.Remove(bond)) return false;
            BondChanged?.Invoke(); if (Complete) Completed?.Invoke(); return true;
        }
        public int[][] Fragments()
        {
            var unseen = new HashSet<int>(Enumerable.Range(0, occupied.Length).Where(IsOccupied));
            var result = new List<int[]>();
            while (unseen.Count > 0)
            {
                var group = new List<int>(); var queue = new Queue<int>();
                var first = unseen.First(); unseen.Remove(first); queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue(); group.Add(node);
                    for (var i = 0; i < Definition.bonds.Length; i++)
                    {
                        if (broken.Contains(i)) continue;
                        var b = Definition.bonds[i]; var next = b.a == node ? b.b : b.b == node ? b.a : -1;
                        if (unseen.Remove(next)) queue.Enqueue(next);
                    }
                }
                result.Add(group.ToArray());
            }
            return result.ToArray();
        }
        internal void Consume() { Consumed = true; }
    }

    public static class ReactionEngine
    {
        static Dictionary<string,int> Atoms(ReactionAmount[] amounts)
        {
            var result = new Dictionary<string,int>();
            foreach (var part in amounts)
                foreach (var atom in part.molecule.atoms)
                { result.TryGetValue(atom.element.symbol, out var n); result[atom.element.symbol] = n + part.count; }
            return result;
        }
        public static bool IsBalanced(ReactionDefinition recipe)
        {
            if (recipe == null || recipe.inputs == null || recipe.outputs == null || recipe.inputs.Length == 0 || recipe.outputs.Length == 0) return false;
            if (recipe.inputs.Concat(recipe.outputs).Any(p => p.count <= 0 || p.molecule == null || !p.molecule.IsValid(out _))) return false;
            var a = Atoms(recipe.inputs); var b = Atoms(recipe.outputs);
            return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var count) && count == pair.Value);
        }
        public static bool CanReact(ReactionDefinition recipe, IReadOnlyList<MoleculeState> states, out string reason, ReactionCondition condition = ReactionCondition.None)
        {
            reason = "";
            if (!IsBalanced(recipe)) reason = "Reaction definition is not balanced.";
            else if (condition != recipe.requiredCondition) reason = "Select conditions: " + recipe.conditions;
            else if (states == null || states.Count != recipe.inputs.Sum(p => p.count)) reason = "The tray needs exactly " + string.Join(" + ",recipe.inputs.Select(p=>p.count+" "+p.molecule.formula)) + ".";
            else if (states.Distinct().Count() != states.Count || states.Any(s => s == null || !s.Complete)) reason = "Use distinct, complete, unconsumed molecules.";
            else
            {
                var expected = recipe.inputs.GroupBy(p => p.molecule).ToDictionary(g => g.Key, g => g.Sum(p => p.count));
                if (states.Any(s => !expected.ContainsKey(s.Definition)) || expected.Any(p => states.Count(s => s.Definition == p.Key) != p.Value))
                    reason = "These molecules do not match the reaction recipe.";
            }
            return reason.Length == 0;
        }
        public static bool TryReact(ReactionDefinition recipe, IReadOnlyList<MoleculeState> states, out MoleculeState[] products, out string reason, ReactionCondition condition = ReactionCondition.None)
        {
            products = Array.Empty<MoleculeState>();
            if (!CanReact(recipe, states, out reason, condition)) return false;
            products = recipe.outputs.SelectMany(p => Enumerable.Range(0, p.count).Select(_ => new MoleculeState(p.molecule, true))).ToArray();
            foreach (var state in states) state.Consume();
            return true;
        }
    }
}
