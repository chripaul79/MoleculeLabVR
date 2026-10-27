using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MoleculeLab
{
    public readonly struct GraphBond
    {
        public readonly int A, B, Order;
        public GraphBond(int a,int b,int order) { A=a; B=b; Order=order; }
        public int Other(int atom) => A==atom?B:B==atom?A:-1;
        public bool Joins(int a,int b) => A==a && B==b || A==b && B==a;
    }

    /// <summary>Element-agnostic molecular graph shared by free building, PubChem records and catalogue comparison.</summary>
    public sealed class MolGraph
    {
        public readonly List<string> Symbols=new List<string>();
        public readonly List<Vector3> Positions=new List<Vector3>();
        public readonly List<GraphBond> Bonds=new List<GraphBond>();
        public int Count => Symbols.Count;
        public int Add(string symbol,Vector3 position) { Symbols.Add(symbol); Positions.Add(position); return Symbols.Count-1; }
        public IEnumerable<int> Neighbors(int atom) { foreach(var bond in Bonds) { var other=bond.Other(atom); if(other>=0) yield return other; } }
        public int Degree(int atom) => Bonds.Count(b=>b.A==atom || b.B==atom);
        public int BondSum(int atom) => Bonds.Where(b=>b.A==atom || b.B==atom).Sum(b=>b.Order);
        public int HydrogenCount(int atom) => Neighbors(atom).Count(n=>Symbols[n]=="H");
        public int BondIndex(int a,int b) => Bonds.FindIndex(x=>x.Joins(a,b));
        public string Formula() => FreeChemistry.HillFormula(Symbols);
        public MolGraph Clone()
        {
            var copy=new MolGraph(); copy.Symbols.AddRange(Symbols); copy.Positions.AddRange(Positions); copy.Bonds.AddRange(Bonds); return copy;
        }
        public static MolGraph From(MoleculeDefinition definition)
        {
            var graph=new MolGraph();
            foreach(var atom in definition.atoms) graph.Add(atom.element!=null?atom.element.symbol:"?",atom.position);
            foreach(var bond in definition.bonds) graph.Bonds.Add(new GraphBond(bond.a,bond.b,bond.order));
            return graph;
        }
    }

    public static class FreeChemistry
    {
        /// <summary>Non-bonding electron pairs used only for VSEPR-style guide directions.</summary>
        public static int LonePairs(string symbol)
        {
            switch(symbol)
            {
                case "O": case "S": case "Se": return 2;
                case "N": case "P": return 1;
                case "F": case "Cl": case "Br": case "I": return 3;
                default: return 0;
            }
        }
        /// <summary>Hill order (C, H, then alphabetical; alphabetical when there is no carbon), as PubChem reports formulas.</summary>
        public static string HillFormula(IEnumerable<string> symbols)
        {
            var counts=symbols.GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count());
            var order=new List<string>();
            if(counts.ContainsKey("C")) { order.Add("C"); if(counts.ContainsKey("H")) order.Add("H"); }
            order.AddRange(counts.Keys.Where(k=>!order.Contains(k)).OrderBy(k=>k,StringComparer.Ordinal));
            var text=new StringBuilder();
            foreach(var symbol in order) { text.Append(symbol); if(counts[symbol]>1) text.Append(counts[symbol]); }
            return text.ToString();
        }
    }

    /// <summary>
    /// A molecule the player assembles without a recipe. Pure state: valence rules are enforced here,
    /// independently of hands, controllers and physics.
    /// </summary>
    public sealed class FreeStructure
    {
        public const float HeavyBond=.19f, HydrogenBond=.15f, HydrogenPair=.12f;
        public MolGraph Graph { get; } = new MolGraph();
        readonly List<int> valences=new List<int>();
        readonly List<int> tokens=new List<int>();
        /// <summary>Increments whenever atoms, bonds or bond orders change.</summary>
        public int Version { get; private set; }
        public int Count => Graph.Count;
        public IReadOnlyList<int> TokenIds => tokens;
        public string Symbol(int atom) => Graph.Symbols[atom];
        public Vector3 Position(int atom) => Graph.Positions[atom];
        public void SetPosition(int atom,Vector3 position) { Graph.Positions[atom]=position; }
        public int Valence(int atom) => valences[atom];
        public int OpenValence(int atom) => valences[atom]-Graph.BondSum(atom);
        public string Label(int atom) => Symbol(atom)+(atom+1);
        public int TotalOpenValence => Enumerable.Range(0,Count).Sum(i=>OpenValence(i));

        public static float BondLength(string a,string b,int order)
        {
            var length=a=="H" && b=="H"?HydrogenPair:a=="H" || b=="H"?HydrogenBond:HeavyBond;
            return length*(order==3?.8f:order==2?.88f:1f);
        }

        public int AddAtom(string symbol,int valence,Vector3 position,int token)
        {
            if(string.IsNullOrEmpty(symbol) || valence<1) throw new ArgumentException("A free-build atom needs a symbol and at least one bond.");
            valences.Add(valence); tokens.Add(token); Version++;
            return Graph.Add(symbol,position);
        }
        public bool CanBond(int a,int b,out string reason)
        {
            reason="";
            if(a<0 || b<0 || a>=Count || b>=Count || a==b) reason="Choose two different atoms.";
            else if(Graph.BondIndex(a,b)>=0) reason=Label(a)+" and "+Label(b)+" are already bonded.";
            else if(OpenValence(a)<1) reason=Label(a)+" has no free bond left.";
            else if(OpenValence(b)<1) reason=Label(b)+" has no free bond left.";
            return reason.Length==0;
        }
        public int AddBond(int a,int b)
        {
            if(!CanBond(a,b,out var reason)) throw new InvalidOperationException(reason);
            Graph.Bonds.Add(new GraphBond(a,b,1)); Version++; return Graph.Bonds.Count-1;
        }
        public bool CanSetOrder(int bond,int order,out string reason)
        {
            reason="";
            if(bond<0 || bond>=Graph.Bonds.Count) { reason="Select a bond first."; return false; }
            if(order<1 || order>3) { reason="Bonds can be single, double or triple."; return false; }
            var current=Graph.Bonds[bond]; var extra=order-current.Order;
            if(extra<=0) return true;
            foreach(var atom in new[]{current.A,current.B})
                if(OpenValence(atom)<extra)
                { reason=Label(atom)+" has no spare bond. Pull a hydrogen off it first."; return false; }
            return true;
        }
        public void SetOrder(int bond,int order)
        {
            if(!CanSetOrder(bond,order,out var reason)) throw new InvalidOperationException(reason);
            var current=Graph.Bonds[bond]; if(current.Order==order) return;
            Graph.Bonds[bond]=new GraphBond(current.A,current.B,order); Version++;
        }
        /// <summary>Removes the most recently added atom and its bonds; returns its inventory token. The rest always stays connected.</summary>
        public int RemoveLastAtom() => Count==0?-1:RemoveAtom(Count-1);
        /// <summary>An atom can be pulled off only if the remaining atoms stay one connected structure.</summary>
        public bool CanRemove(int atom,out string reason)
        {
            reason="";
            if(atom<0 || atom>=Count) { reason="No such atom."; return false; }
            if(Count<=2) return true;
            var start=atom==0?1:0; var seen=new HashSet<int>{start}; var queue=new Queue<int>(); queue.Enqueue(start);
            while(queue.Count>0) { var current=queue.Dequeue(); foreach(var next in Graph.Neighbors(current)) if(next!=atom && seen.Add(next)) queue.Enqueue(next); }
            if(seen.Count!=Count-1) { reason="Pull off the atoms attached to "+Label(atom)+" first."; return false; }
            return true;
        }
        /// <summary>Removes an atom and its bonds, renumbering later atoms; returns its inventory token.</summary>
        public int RemoveAtom(int atom)
        {
            if(atom<0 || atom>=Count) return -1;
            Graph.Bonds.RemoveAll(b=>b.A==atom || b.B==atom);
            for(var i=0;i<Graph.Bonds.Count;i++)
            {
                var bond=Graph.Bonds[i];
                Graph.Bonds[i]=new GraphBond(bond.A>atom?bond.A-1:bond.A,bond.B>atom?bond.B-1:bond.B,bond.Order);
            }
            var token=tokens[atom];
            Graph.Symbols.RemoveAt(atom); Graph.Positions.RemoveAt(atom); valences.RemoveAt(atom); tokens.RemoveAt(atom);
            Version++; return token;
        }
        /// <summary>Short description of what is still missing, e.g. "C1 needs 2, O3 needs 1".</summary>
        public string MissingText(int maximum=3)
        {
            var open=Enumerable.Range(0,Count).Where(i=>OpenValence(i)>0).ToList();
            var parts=open.Take(maximum).Select(i=>Label(i)+" needs "+OpenValence(i));
            return string.Join(", ",parts)+(open.Count>maximum?", ...":"");
        }
        public bool IsComplete(out string reason)
        {
            reason="";
            if(Count<2) { reason="Add at least two atoms before checking."; return false; }
            for(var i=0;i<Count;i++)
            {
                var open=OpenValence(i);
                if(open>0) { reason=Label(i)+" still has "+open+" open bond"+(open>1?"s":"")+". Add atoms, raise a bond order, or use Fill hydrogens."; return false; }
            }
            return true;
        }
        public int GraphDistance(int a,int b)
        {
            var distance=Enumerable.Repeat(-1,Count).ToArray(); var queue=new Queue<int>(); distance[a]=0; queue.Enqueue(a);
            while(queue.Count>0) { var atom=queue.Dequeue(); foreach(var next in Graph.Neighbors(atom)) if(distance[next]<0) { distance[next]=distance[atom]+1; queue.Enqueue(next); } }
            return distance[b];
        }

        int Domains(int atom,int neighbors) => Mathf.Clamp(neighbors+FreeChemistry.LonePairs(Symbol(atom))+Mathf.Max(0,OpenValence(atom)),1,4);

        /// <summary>Ideal directions (unit vectors, local space) for this atom's open bonds.</summary>
        public Vector3[] OpenDirections(int atom)
        {
            var open=OpenValence(atom); if(open<=0) return Array.Empty<Vector3>();
            var neighbors=Graph.Neighbors(atom).ToArray();
            var used=neighbors.Select(n=>Position(n)-Position(atom)).Where(v=>v.sqrMagnitude>1e-10f).Select(v=>v.normalized).ToArray();
            var free=FreeDirections(used,Domains(atom,neighbors.Length),ZigzagHint(atom,neighbors));
            return free.Take(open).ToArray();
        }
        Vector3 ZigzagHint(int atom,int[] neighbors)
        {
            // A new bond parallel to the bond two steps back gives a planar zig-zag (anti) chain.
            if(neighbors.Length!=1) return Vector3.zero;
            var neighbor=neighbors[0]; var other=-1;
            foreach(var n in Graph.Neighbors(neighbor)) if(n!=atom && (other<0 || Symbol(other)=="H" && Symbol(n)!="H")) other=n;
            return other<0?Vector3.zero:Position(neighbor)-Position(other);
        }
        static Vector3 Safe(Vector3 value,Vector3 fallback) => value.sqrMagnitude>1e-6f?value.normalized:fallback.normalized;
        static Vector3 Perpendicular(Vector3 axis,Vector3 hint)
        {
            var p=hint-axis*Vector3.Dot(hint,axis);
            if(p.sqrMagnitude<1e-6f) p=Vector3.Cross(axis,Vector3.up);
            if(p.sqrMagnitude<1e-6f) p=Vector3.Cross(axis,Vector3.forward);
            return p.normalized;
        }
        static Vector3[] Canonical(int domains)
        {
            switch(domains)
            {
                case 1: return new[]{Vector3.right};
                case 2: return new[]{Vector3.right,Vector3.left};
                case 3: return new[]{Vector3.right,new Vector3(-.5f,.8660254f,0),new Vector3(-.5f,-.8660254f,0)};
                default: return new[]{Vector3.right,new Vector3(-1f/3,.942809f,0),new Vector3(-1f/3,-.4714045f,.8164966f),new Vector3(-1f/3,-.4714045f,-.8164966f)};
            }
        }
        /// <summary>Unoccupied VSEPR directions given occupied unit directions and the total electron-domain count (1-4).</summary>
        public static Vector3[] FreeDirections(Vector3[] used,int domains,Vector3 hint)
        {
            var count=used.Length;
            if(count>=domains) return Array.Empty<Vector3>();
            if(count==0) return Canonical(domains);
            var n=used[0];
            if(domains==2) return new[]{-n};
            if(domains==3)
            {
                if(count==1) { var p=Perpendicular(n,hint); return new[]{(-.5f*n+.8660254f*p).normalized,(-.5f*n-.8660254f*p).normalized}; }
                return new[]{Safe(-(used[0]+used[1]),Perpendicular(n,hint))};
            }
            if(count==1)
            {
                var p=Perpendicular(n,hint); var q=Vector3.Cross(n,p).normalized; var result=new Vector3[3];
                for(var k=0;k<3;k++) { var angle=k*2*Mathf.PI/3; result[k]=(n*(-1f/3)+(p*Mathf.Cos(angle)+q*Mathf.Sin(angle))*.942809f).normalized; }
                return result;
            }
            if(count==2)
            {
                var bisector=Safe(-(used[0]+used[1]),Perpendicular(n,hint));
                var normal=Vector3.Cross(used[0],used[1]); if(normal.sqrMagnitude<1e-6f) normal=Perpendicular(bisector,hint);
                normal.Normalize();
                return new[]{(bisector*.5773503f+normal*.8164966f).normalized,(bisector*.5773503f-normal*.8164966f).normalized};
            }
            var sum=used[0]+used[1]+used[2];
            var plane=Vector3.Cross(used[1]-used[0],used[2]-used[0]); if(plane.sqrMagnitude<1e-6f) plane=Perpendicular(n,hint);
            plane.Normalize(); if(Vector3.Dot(plane,sum)>0) plane=-plane;
            return new[]{Safe(-sum,plane)};
        }

        /// <summary>
        /// Position-based relaxation towards idealised teaching geometry: bond lengths, VSEPR angles
        /// (as 1-3 distances) and non-bonded clearance. Keeps the centroid fixed unless an atom is pinned.
        /// centroidExclude: an atom the player just dragged; the centre is taken from the other atoms, so releasing
        /// a dragged atom springs it back instead of sliding the whole structure towards the hand.
        /// </summary>
        public void Relax(int iterations=140,int pinned=-1,int centroidExclude=-1)
        {
            var n=Count; if(n<2) return;
            if(centroidExclude>=n) centroidExclude=-1;
            var p=Graph.Positions; var before=Centroid(centroidExclude);
            // A tiny deterministic jitter breaks planar symmetry, so a zig-zag chain can fold into a puckered ring.
            for(var i=0;i<n;i++) if(i!=pinned) p[i]+=new Vector3(Mathf.Sin(i*12.9898f+1),Mathf.Sin(i*78.233f+2),Mathf.Sin(i*37.719f+3))*.004f;
            var constraints=new List<(int a,int b,float length,float weight,bool minimum)>();
            var related=new HashSet<long>();
            long Key(int a,int b) => Math.Min(a,b)*4096L+Math.Max(a,b);
            foreach(var bond in Graph.Bonds) { constraints.Add((bond.A,bond.B,BondLength(Symbol(bond.A),Symbol(bond.B),bond.Order),1f,false)); related.Add(Key(bond.A,bond.B)); }
            for(var c=0;c<n;c++)
            {
                var neighbors=Graph.Neighbors(c).ToArray(); if(neighbors.Length<2) continue;
                var domains=Mathf.Clamp(Domains(c,neighbors.Length),2,4);
                var cos=domains==2?-1f:domains==3?-.5f:-1f/3;
                for(var j=0;j<neighbors.Length;j++) for(var k=j+1;k<neighbors.Length;k++)
                {
                    var a=neighbors[j]; var b=neighbors[k];
                    var la=BondLength(Symbol(c),Symbol(a),Graph.Bonds[Graph.BondIndex(c,a)].Order);
                    var lb=BondLength(Symbol(c),Symbol(b),Graph.Bonds[Graph.BondIndex(c,b)].Order);
                    constraints.Add((a,b,Mathf.Sqrt(Mathf.Max(1e-6f,la*la+lb*lb-2*la*lb*cos)),.6f,false)); related.Add(Key(a,b));
                }
            }
            for(var a=0;a<n;a++) for(var b=a+1;b<n;b++)
            {
                if(related.Contains(Key(a,b))) continue;
                var hydrogens=(Symbol(a)=="H"?1:0)+(Symbol(b)=="H"?1:0);
                constraints.Add((a,b,hydrogens==0?.3f:hydrogens==1?.24f:.2f,.4f,true));
            }
            for(var iteration=0;iteration<iterations;iteration++)
                foreach(var c in constraints)
                {
                    var delta=p[c.b]-p[c.a]; var length=delta.magnitude;
                    if(length<1e-5f) { delta=new Vector3(.001f*(c.a+1),.0007f*(c.b+1),.0003f*(c.a+c.b+1)); length=delta.magnitude; }
                    if(c.minimum && length>=c.length) continue;
                    var correction=delta*((length-c.length)/length*c.weight);
                    var wa=c.a==pinned?0f:c.b==pinned?1f:.5f; var wb=c.b==pinned?0f:c.a==pinned?1f:.5f;
                    p[c.a]+=correction*wa; p[c.b]-=correction*wb;
                }
            if(pinned<0) { var shift=before-Centroid(centroidExclude); for(var i=0;i<n;i++) p[i]+=shift; }
        }
        /// <summary>Mean position, optionally leaving one atom out.</summary>
        public Vector3 Centroid(int exclude=-1)
        {
            var sum=Vector3.zero; var count=0;
            for(var i=0;i<Count;i++) if(i!=exclude) { sum+=Graph.Positions[i]; count++; }
            return count==0?Vector3.zero:sum/count;
        }
    }

    /// <summary>Writes an unambiguous SMILES string (explicit bracket atoms, Kekulé bond orders) for a PubChem identity query.</summary>
    public static class SmilesWriter
    {
        public static string Write(MolGraph graph)
        {
            var heavy=Enumerable.Range(0,graph.Count).Where(i=>graph.Symbols[i]!="H").ToArray();
            if(heavy.Length==0) return string.Concat(Enumerable.Repeat("[H]",graph.Count));
            var adjacency=new List<(int other,int order)>[graph.Count];
            for(var i=0;i<graph.Count;i++) adjacency[i]=new List<(int,int)>();
            foreach(var bond in graph.Bonds)
                if(graph.Symbols[bond.A]!="H" && graph.Symbols[bond.B]!="H")
                { adjacency[bond.A].Add((bond.B,bond.Order)); adjacency[bond.B].Add((bond.A,bond.Order)); }
            foreach(var list in adjacency) list.Sort((x,y)=>x.other.CompareTo(y.other));
            var visited=new bool[graph.Count];
            var children=new List<(int other,int order)>[graph.Count];
            var rings=new List<(int other,int order)>[graph.Count];
            for(var i=0;i<graph.Count;i++) { children[i]=new List<(int,int)>(); rings[i]=new List<(int,int)>(); }
            var ringKeys=new HashSet<long>();
            long Key(int a,int b) => Math.Min(a,b)*4096L+Math.Max(a,b);
            void Visit(int atom,int from)
            {
                visited[atom]=true;
                foreach(var (other,order) in adjacency[atom])
                {
                    if(other==from) continue;
                    if(visited[other]) { if(ringKeys.Add(Key(atom,other))) { rings[atom].Add((other,order)); rings[other].Add((atom,order)); } }
                    else { children[atom].Add((other,order)); Visit(other,atom); }
                }
            }
            var text=new StringBuilder(); var open=new Dictionary<long,int>(); var freeDigits=new SortedSet<int>(); var nextDigit=1;
            void Emit(int atom)
            {
                var h=graph.HydrogenCount(atom);
                text.Append('[').Append(graph.Symbols[atom]); if(h>0) { text.Append('H'); if(h>1) text.Append(h); } text.Append(']');
                foreach(var (other,order) in rings[atom])
                {
                    var key=Key(atom,other);
                    if(open.TryGetValue(key,out var digit)) { text.Append(Digit(digit)); open.Remove(key); freeDigits.Add(digit); }
                    else
                    {
                        if(freeDigits.Count>0) { digit=freeDigits.Min; freeDigits.Remove(digit); } else digit=nextDigit++;
                        open[key]=digit; text.Append(BondSymbol(order)).Append(Digit(digit));
                    }
                }
                for(var i=0;i<children[atom].Count;i++)
                {
                    var (child,order)=children[atom][i]; var last=i==children[atom].Count-1;
                    if(!last) text.Append('(');
                    text.Append(BondSymbol(order)); Emit(child);
                    if(!last) text.Append(')');
                }
            }
            // A free structure is always one connected piece; emit every heavy component defensively.
            foreach(var start in heavy)
            {
                if(visited[start]) continue;
                if(text.Length>0) text.Append('.');
                Visit(start,-1); Emit(start);
            }
            return text.ToString();
        }
        static string BondSymbol(int order) => order==2?"=":order==3?"#":"";
        static string Digit(int digit) => digit<10?digit.ToString():"%"+digit;
    }

    /// <summary>Constitution matching: same atoms, same connectivity, same hydrogen counts. Bond orders are not compared, so alternative Kekulé forms still match.</summary>
    public static class StructureMatcher
    {
        public static int[] Match(MolGraph a,MolGraph b)
        {
            if(a==null || b==null || a.Count!=b.Count || a.Bonds.Count!=b.Bonds.Count || a.Formula()!=b.Formula()) return null;
            var n=a.Count; var map=Enumerable.Repeat(-1,n).ToArray();
            var heavyA=Enumerable.Range(0,n).Where(i=>a.Symbols[i]!="H").ToList();
            var heavyB=Enumerable.Range(0,n).Where(i=>b.Symbols[i]!="H").ToList();
            if(heavyA.Count==0)
            {
                for(var i=0;i<n;i++) map[i]=i;
                return a.Bonds.All(x=>b.BondIndex(map[x.A],map[x.B])>=0)?map:null;
            }
            var adjacencyA=Adjacency(a); var adjacencyB=Adjacency(b);
            string Label(MolGraph g,int i) => g.Symbols[i]+":"+g.HydrogenCount(i)+":"+g.Neighbors(i).Count(x=>g.Symbols[x]!="H");
            var labelA=heavyA.ToDictionary(i=>i,i=>Label(a,i)); var labelB=heavyB.ToDictionary(i=>i,i=>Label(b,i));
            // Breadth-first order so every atom after the first has a mapped neighbour constraining it.
            var order=new List<int>(); var seen=new HashSet<int>();
            foreach(var root in heavyA)
            {
                if(!seen.Add(root)) continue;
                var queue=new Queue<int>(); queue.Enqueue(root);
                while(queue.Count>0) { var atom=queue.Dequeue(); order.Add(atom); foreach(var next in a.Neighbors(atom)) if(a.Symbols[next]!="H" && seen.Add(next)) queue.Enqueue(next); }
            }
            var used=new bool[n];
            bool Assign(int k)
            {
                if(k==order.Count) return true;
                var u=order[k];
                foreach(var v in heavyB)
                {
                    if(used[v] || labelB[v]!=labelA[u]) continue;
                    var consistent=true;
                    for(var j=0;j<k && consistent;j++) { var w=order[j]; consistent=adjacencyA[u,w]==adjacencyB[v,map[w]]; }
                    if(!consistent) continue;
                    map[u]=v; used[v]=true;
                    if(Assign(k+1)) return true;
                    map[u]=-1; used[v]=false;
                }
                return false;
            }
            if(!Assign(0)) return null;
            foreach(var u in heavyA)
            {
                var hydrogensA=a.Neighbors(u).Where(x=>a.Symbols[x]=="H").ToList();
                var hydrogensB=b.Neighbors(map[u]).Where(x=>b.Symbols[x]=="H").ToList();
                if(hydrogensA.Count!=hydrogensB.Count) return null;
                for(var i=0;i<hydrogensA.Count;i++) map[hydrogensA[i]]=hydrogensB[i];
            }
            return map.All(x=>x>=0) && map.Distinct().Count()==n?map:null;
        }
        static bool[,] Adjacency(MolGraph graph)
        {
            var result=new bool[graph.Count,graph.Count];
            foreach(var bond in graph.Bonds) { result[bond.A,bond.B]=true; result[bond.B,bond.A]=true; }
            return result;
        }
    }
}
