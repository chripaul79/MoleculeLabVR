using System;
using System.Linq;
using UnityEngine;
namespace MoleculeLab
{
    [Serializable] public struct AtomSite
    {
        public ElementDefinition element;
        public Vector3 position;
        public AtomSite(ElementDefinition e, Vector3 p) { element = e; position = p; }
    }
    [Serializable] public struct BondDefinition
    {
        public int a, b, order;
        public BondDefinition(int start, int end, int count = 1) { a = start; b = end; order = count; }
    }
    [CreateAssetMenu(menuName = "Molecule Lab/Molecule")]
    public sealed class MoleculeDefinition : ScriptableObject
    {
        public string id, displayName, formula;
        [TextArea] public string geometry;
        public AtomSite[] atoms = Array.Empty<AtomSite>();
        public BondDefinition[] bonds = Array.Empty<BondDefinition>();
        public int seedIndex;
        public int rotatableBond = -1;
        public int[] rotatingSites = Array.Empty<int>();
        public bool isRadical;
        public string molecularFormula;
        [TextArea] public string electronicState;
        public int[] radicalSites = Array.Empty<int>();
        public int[] aromaticRing = Array.Empty<int>();
        public bool IsAromaticBond(BondDefinition bond) => aromaticRing.Length > 0 &&
            Enumerable.Range(0, aromaticRing.Length).Any(i =>
                (aromaticRing[i] == bond.a && aromaticRing[(i+1)%aromaticRing.Length] == bond.b) ||
                (aromaticRing[i] == bond.b && aromaticRing[(i+1)%aromaticRing.Length] == bond.a));

        public bool IsValid(out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(id) || atoms.Length == 0 || seedIndex < 0 || seedIndex >= atoms.Length)
                error = "A molecule needs an ID, atoms and a valid seed.";
            else if (atoms.Any(a => a.element == null)) error = "Every site needs an element.";
            else if (bonds.Any(b => b.a < 0 || b.b < 0 || b.a >= atoms.Length || b.b >= atoms.Length || b.a == b.b || b.order < 1 || b.order > 3))
                error = "Invalid bond endpoint or order.";
            else if (bonds.Select(b => Math.Min(b.a,b.b) + ":" + Math.Max(b.a,b.b)).Distinct().Count() != bonds.Length)
                error = "Duplicate bonds.";
            else
            {
                for (var i = 0; i < atoms.Length; i++)
                    if (bonds.Where(b => b.a == i || b.b == i).Sum(b => b.order) > atoms[i].element.maximumBondOrder)
                        error = "Bond order exceeds an element's allowed valence.";
                var reached = new System.Collections.Generic.HashSet<int> { seedIndex };
                for (var i = 0; i < atoms.Length; i++) foreach (var b in bonds)
                { if (reached.Contains(b.a)) reached.Add(b.b); if (reached.Contains(b.b)) reached.Add(b.a); }
                if (reached.Count != atoms.Length) error = "Every atom must be connected to the seed.";
                if (rotatableBond >= bonds.Length || rotatableBond < -1 || rotatingSites.Any(i => i < 0 || i >= atoms.Length))
                    error = "Invalid rotation group.";
            }
            if(error.Length == 0)
            {
                if(radicalSites.Any(i=>i<0 || i>=atoms.Length) || radicalSites.Distinct().Count()!=radicalSites.Length || isRadical!=(radicalSites.Length>0))
                    error="Declare each radical site explicitly.";
                for(var i=0;i<atoms.Length;i++)
                {
                    var valence=bonds.Where(b=>b.a==i || b.b==i).Sum(b=>b.order);
                    var expected=atoms[i].element.maximumBondOrder-(radicalSites.Contains(i)?1:0);
                    if(valence!=expected) error="Incomplete valence at atom "+i;
                }
                var formulaCounts=new System.Collections.Generic.Dictionary<string,int>();
                var formulaText=string.IsNullOrEmpty(molecularFormula)?formula:molecularFormula;
                var matches=System.Text.RegularExpressions.Regex.Matches(formulaText,@"([A-Z][a-z]?)([0-9]*)");
                if(string.Concat(matches.Cast<System.Text.RegularExpressions.Match>().Select(m=>m.Value))!=formulaText)
                    error="Invalid molecular formula.";
                foreach(System.Text.RegularExpressions.Match match in matches)
                {
                    var symbol=match.Groups[1].Value; var count=match.Groups[2].Value.Length==0?1:int.Parse(match.Groups[2].Value);
                    formulaCounts.TryGetValue(symbol,out var previous); formulaCounts[symbol]=previous+count;
                }
                var actual=atoms.GroupBy(a=>a.element.symbol).ToDictionary(g=>g.Key,g=>g.Count());
                if(actual.Count!=formulaCounts.Count || actual.Any(k=>!formulaCounts.TryGetValue(k.Key,out var n)||n!=k.Value)) error="Formula does not match atom inventory.";
                if(aromaticRing.Length>0)
                {
                    if(aromaticRing.Length!=6 || aromaticRing.Distinct().Count()!=6 || aromaticRing.Any(i=>i<0||i>=atoms.Length)) error="Supported aromatic ring must contain six distinct atoms.";
                    else
                    {
                        var edges=bonds.Where(IsAromaticBond).ToArray();
                        if(aromaticRing.Any(i=>atoms[i].element.symbol!="C") || edges.Length!=6 || edges.Count(b=>b.order==2)!=3 ||
                           aromaticRing.Any(i=>edges.Where(b=>b.a==i||b.b==i).Sum(b=>b.order)!=3)) error="Invalid benzene Kekule bookkeeping.";
                        var normal=Vector3.Cross(atoms[aromaticRing[1]].position-atoms[aromaticRing[0]].position,atoms[aromaticRing[2]].position-atoms[aromaticRing[0]].position).normalized;
                        if(normal.sqrMagnitude<.9f || atoms.Any(a=>Mathf.Abs(Vector3.Dot(a.position-atoms[aromaticRing[0]].position,normal))>.0001f)) error="Benzene must be planar.";
                        if(edges.Length>0 && edges.Any(b=>Mathf.Abs(Vector3.Distance(atoms[b.a].position,atoms[b.b].position)-Vector3.Distance(atoms[edges[0].a].position,atoms[edges[0].b].position))>.0001f)) error="Aromatic C-C lengths must be equivalent.";
                    }
                }
                if(rotatableBond>=0)
                {
                    var rotatingBond=bonds[rotatableBond];
                    if(rotatingBond.order!=1 || IsAromaticBond(rotatingBond)) error="Only supported single bonds can rotate.";
                    var component=new System.Collections.Generic.HashSet<int>{rotatingBond.b};
                    for(var pass=0;pass<atoms.Length;pass++) for(var j=0;j<bonds.Length;j++)
                    {
                        if(j==rotatableBond) continue; var edge=bonds[j];
                        if(component.Contains(edge.a)) component.Add(edge.b);
                        if(component.Contains(edge.b)) component.Add(edge.a);
                    }
                    if(component.Contains(rotatingBond.a) || !component.SetEquals(rotatingSites.Concat(new[]{rotatingBond.b}))) error="Rotation must preserve one whole side of an acyclic bond.";
                }
            }
            return error.Length == 0;
        }
    }
}
