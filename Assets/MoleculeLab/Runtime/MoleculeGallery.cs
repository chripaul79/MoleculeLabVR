using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MoleculeLab
{
    /// <summary>One exhibit: a molecule the player has completed, stored with its own geometry so it can be shown in later sessions.</summary>
    [Serializable] public sealed class GalleryEntry
    {
        public string id, name, formula, detail, firstBuilt;
        public int timesBuilt;
        /// <summary>PubChem compound ID when known (free-build discoveries); 0 for recipes, resolved on demand.</summary>
        public int cid;
        public string[] symbols=Array.Empty<string>();
        public Vector3[] positions=Array.Empty<Vector3>();
        public int[] bondA=Array.Empty<int>(), bondB=Array.Empty<int>(), bondOrder=Array.Empty<int>();
        /// <summary>Aromatic bonds are drawn as single sticks; bondOrder keeps the Kekulé order for chemistry.</summary>
        public bool[] aromatic=Array.Empty<bool>();
    }

    /// <summary>Every distinct molecule the player has completed, saved as JSON so the gallery survives restarts.</summary>
    public sealed class MoleculeGallery
    {
        [Serializable] internal sealed class GalleryFile { public List<GalleryEntry> entries=new List<GalleryEntry>(); }
        readonly List<GalleryEntry> entries=new List<GalleryEntry>();
        /// <summary>File path, or null for an in-memory gallery (tests).</summary>
        public string FilePath { get; }
        public IReadOnlyList<GalleryEntry> Entries => entries;
        public int Version { get; private set; }

        public MoleculeGallery(string filePath)
        {
            FilePath=filePath;
            if(string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
            try
            {
                var file=JsonUtility.FromJson<GalleryFile>(File.ReadAllText(filePath));
                if(file!=null && file.entries!=null) entries.AddRange(file.entries.Where(e=>e!=null && !string.IsNullOrEmpty(e.id) && e.symbols!=null && e.positions!=null && e.symbols.Length==e.positions.Length));
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { Debug.LogWarning("Gallery file ignored: "+exception.Message); }
        }

        public static GalleryEntry FromDefinition(MoleculeDefinition definition)
        {
            var aromatic=definition.bonds.Select(b=>definition.IsAromaticBond(b)).ToArray();
            var firstLine=(definition.geometry??"").Split('\n')[0];
            return new GalleryEntry
            {
                id=definition.id, name=definition.displayName,
                formula=string.IsNullOrEmpty(definition.molecularFormula)?definition.formula:definition.molecularFormula,
                detail=definition.id!=null && definition.id.StartsWith("pubchem-")?firstLine:"Lab recipe",
                symbols=definition.atoms.Select(a=>a.element!=null?a.element.symbol:"?").ToArray(),
                positions=definition.atoms.Select(a=>a.position).ToArray(),
                bondA=definition.bonds.Select(b=>b.a).ToArray(), bondB=definition.bonds.Select(b=>b.b).ToArray(),
                bondOrder=definition.bonds.Select(b=>b.order).ToArray(), aromatic=aromatic,
                timesBuilt=0, firstBuilt=DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                cid=definition.id!=null && definition.id.StartsWith("pubchem-") && int.TryParse(definition.id.Substring(8),out var cid)?cid:0
            };
        }
        /// <summary>The exhibit's bonding graph, e.g. to write its SMILES for a PubChem lookup.</summary>
        public static MolGraph ToGraph(GalleryEntry entry)
        {
            var graph=new MolGraph();
            for(var i=0;i<entry.symbols.Length;i++) graph.Add(entry.symbols[i],i<entry.positions.Length?entry.positions[i]:Vector3.zero);
            var count=Mathf.Min(entry.bondA.Length,Mathf.Min(entry.bondB.Length,entry.bondOrder.Length));
            for(var i=0;i<count;i++) graph.Bonds.Add(new GraphBond(entry.bondA[i],entry.bondB[i],entry.bondOrder[i]));
            return graph;
        }

        /// <summary>Adds the molecule (or counts another build of it). Returns true when it is new to the gallery.</summary>
        public bool Record(MoleculeDefinition definition)
        {
            if(definition==null || string.IsNullOrEmpty(definition.id)) return false;
            var entry=entries.FirstOrDefault(e=>e.id==definition.id);
            var isNew=entry==null;
            if(isNew) { entry=FromDefinition(definition); entries.Add(entry); }
            entry.timesBuilt++; Version++; Save();
            return isNew;
        }
        public void Clear() { entries.Clear(); Version++; Save(); }

        void Save()
        {
            if(string.IsNullOrEmpty(FilePath)) return;
            try
            {
                var directory=Path.GetDirectoryName(FilePath); if(!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(FilePath,JsonUtility.ToJson(new GalleryFile{entries=entries.ToList()},true));
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
            { Debug.LogWarning("Gallery not saved: "+exception.Message); }
        }
    }
}
