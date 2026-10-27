using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace MoleculeLab
{
    public enum LookupStatus { Found, NotFound, Rejected, Error }

    [Serializable] public sealed class MoleculeLookupResult
    {
        public LookupStatus status;
        public int cid;
        public string smiles, title, iupacName, molecularFormula, message;
        /// <summary>PubChem 3D conformer as an MDL molfile (Ångström); empty when PubChem has none.</summary>
        public string sdf3d;
        public bool fromCache;
    }

    /// <summary>Identifies a structure from its SMILES. Swappable so tests and offline builds need no network.</summary>
    public interface IMoleculeLookup
    {
        IEnumerator Lookup(string smiles,Action<MoleculeLookupResult> done);
    }

    /// <summary>
    /// PubChem PUG REST client: one identity/property query and, when the compound exists, one 3D conformer download.
    /// Requests are spaced to stay inside PubChem's 5 requests/second limit; definitive answers are cached on disk.
    /// </summary>
    public sealed partial class PubChemClient : IMoleculeLookup
    {
        public const string BaseUrl="https://pubchem.ncbi.nlm.nih.gov/rest/pug/";
        public int timeoutSeconds=12;
        readonly string cacheDirectory;
        float lastRequest=-10;

        public PubChemClient(string cacheDirectory) { this.cacheDirectory=cacheDirectory; }

        public static string PropertyUrl(string smiles) =>
            BaseUrl+"compound/smiles/property/Title,IUPACName,MolecularFormula/JSON?smiles="+Uri.EscapeDataString(smiles);
        public static string ConformerUrl(int cid) =>
            BaseUrl+"compound/cid/"+cid.ToString(CultureInfo.InvariantCulture)+"/SDF?record_type=3d";

        public IEnumerator Lookup(string smiles,Action<MoleculeLookupResult> done)
        {
            var cached=LoadCache(smiles);
            if(cached!=null) { cached.fromCache=true; done(cached); yield break; }
            yield return Throttle();
            MoleculeLookupResult result;
            using(var request=UnityWebRequest.Get(PropertyUrl(smiles)))
            {
                request.timeout=timeoutSeconds;
                yield return request.SendWebRequest();
                var code=request.result==UnityWebRequest.Result.ConnectionError?0:request.responseCode;
                result=InterpretProperties(code,request.downloadHandler!=null?request.downloadHandler.text:null,request.error);
            }
            result.smiles=smiles;
            if(result.status==LookupStatus.Found)
            {
                yield return Throttle();
                using(var request=UnityWebRequest.Get(ConformerUrl(result.cid)))
                {
                    request.timeout=timeoutSeconds;
                    yield return request.SendWebRequest();
                    if(request.responseCode==200) result.sdf3d=request.downloadHandler.text;
                    // 404: PubChem has no 3D conformer (too large, flexible or stereo-undefined). Anything else may be transient: don't cache.
                    else if(request.responseCode!=404) { done(result); yield break; }
                }
            }
            if(result.status==LookupStatus.Found || result.status==LookupStatus.NotFound) SaveCache(result);
            done(result);
        }

        IEnumerator Throttle()
        {
            var wait=lastRequest+.25f-Time.realtimeSinceStartup;
            if(wait>0) yield return new WaitForSecondsRealtime(wait);
            lastRequest=Time.realtimeSinceStartup;
        }

        [Serializable] internal sealed class PropertyReply { public PropertyTable PropertyTable; }
        [Serializable] internal sealed class PropertyTable { public PropertyRow[] Properties; }
        [Serializable] internal sealed class PropertyRow { public int CID; public string MolecularFormula, IUPACName, Title; }
        [Serializable] internal sealed class FaultReply { public Fault Fault; }
        [Serializable] internal sealed class Fault { public string Code, Message; }

        /// <summary>Pure interpretation of the property response (HTTP status 0 = no connection).</summary>
        public static MoleculeLookupResult InterpretProperties(long code,string body,string error)
        {
            var result=new MoleculeLookupResult();
            if(code==200)
            {
                PropertyRow row=null;
                try { row=JsonUtility.FromJson<PropertyReply>(body??"")?.PropertyTable?.Properties?.FirstOrDefault(); } catch(ArgumentException) { }
                if(row==null) { result.status=LookupStatus.Error; result.message="PubChem sent a reply this version cannot read."; }
                else if(row.CID<=0) { result.status=LookupStatus.NotFound; result.message="PubChem has no compound with this structure."; }
                else
                {
                    result.status=LookupStatus.Found; result.cid=row.CID;
                    result.title=string.IsNullOrEmpty(row.Title)?row.IUPACName:row.Title;
                    result.iupacName=row.IUPACName; result.molecularFormula=row.MolecularFormula;
                }
                return result;
            }
            string fault=null;
            try { fault=JsonUtility.FromJson<FaultReply>(body??"")?.Fault?.Message; } catch(ArgumentException) { }
            if(code==404) { result.status=LookupStatus.NotFound; result.message="PubChem has no compound with this structure."; }
            else if(code==400) { result.status=LookupStatus.Rejected; result.message="PubChem could not read this structure"+(string.IsNullOrEmpty(fault)?".":": "+fault); }
            else if(code==503 || code==429) { result.status=LookupStatus.Error; result.message="PubChem is busy. Wait a moment and check again."; }
            else if(code==0) { result.status=LookupStatus.Error; result.message="No connection to PubChem"+(string.IsNullOrEmpty(error)?".":" ("+error+")."); }
            else { result.status=LookupStatus.Error; result.message="PubChem answered "+code+(string.IsNullOrEmpty(fault)?".":": "+fault); }
            return result;
        }

        string CachePath(string smiles)
        {
            using(var sha=SHA256.Create())
            {
                var hash=sha.ComputeHash(Encoding.UTF8.GetBytes(smiles));
                return Path.Combine(cacheDirectory,string.Concat(hash.Select(b=>b.ToString("x2")))+".json");
            }
        }
        MoleculeLookupResult LoadCache(string smiles)
        {
            if(string.IsNullOrEmpty(cacheDirectory)) return null;
            try
            {
                var path=CachePath(smiles); if(!File.Exists(path)) return null;
                var result=JsonUtility.FromJson<MoleculeLookupResult>(File.ReadAllText(path));
                return result!=null && result.smiles==smiles?result:null;
            }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException) { return null; }
        }
        void SaveCache(MoleculeLookupResult result)
        {
            if(string.IsNullOrEmpty(cacheDirectory)) return;
            try { Directory.CreateDirectory(cacheDirectory); File.WriteAllText(CachePath(result.smiles),JsonUtility.ToJson(result)); }
            catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException) { Debug.LogWarning("PubChem cache not written: "+exception.Message); }
        }
    }

    /// <summary>Reads the connection table and coordinates of an MDL V2000 molfile/SDF record, such as PubChem's 3D conformers.</summary>
    public static class MolfileParser
    {
        public static MolGraph Parse(string text,out bool charged)
        {
            charged=false;
            if(string.IsNullOrEmpty(text)) throw new FormatException("Empty molfile.");
            var lines=text.Replace("\r","").Split('\n');
            if(lines.Length<4 || !lines[3].Contains("V2000")) throw new FormatException("Only V2000 molfiles are supported.");
            var atoms=Int(lines[3],0); var bonds=Int(lines[3],3);
            if(atoms<1 || lines.Length<4+atoms+bonds) throw new FormatException("Truncated molfile.");
            var graph=new MolGraph();
            for(var i=0;i<atoms;i++)
            {
                var line=lines[4+i];
                if(line.Length<34) throw new FormatException("Short atom line "+(i+1)+".");
                graph.Add(line.Substring(31,3).Trim(),new Vector3(Float(line,0),Float(line,10),Float(line,20)));
                if(line.Length>=39 && Int(line,36)!=0) charged=true;
            }
            for(var i=0;i<bonds;i++)
            {
                var line=lines[4+atoms+i];
                var a=Int(line,0)-1; var b=Int(line,3)-1; var order=Int(line,6);
                if(a<0 || b<0 || a>=atoms || b>=atoms || a==b) throw new FormatException("Bad bond line "+(i+1)+".");
                if(order<1 || order>3) throw new FormatException("Unsupported bond type "+order+".");
                graph.Bonds.Add(new GraphBond(a,b,order));
            }
            foreach(var line in lines.Skip(4+atoms+bonds))
            {
                if(line.StartsWith("M  END")) break;
                if(line.StartsWith("M  CHG") || line.StartsWith("M  RAD")) charged=true;
            }
            return graph;
        }
        static string Field(string line,int start,int length) => line.Length<=start?"":line.Substring(start,Math.Min(length,line.Length-start)).Trim();
        static int Int(string line,int start)
        {
            var field=Field(line,start,3);
            if(field.Length==0) return 0;
            if(!int.TryParse(field,NumberStyles.Integer,CultureInfo.InvariantCulture,out var value)) throw new FormatException("Bad integer '"+field+"'.");
            return value;
        }
        static float Float(string line,int start)
        {
            if(!float.TryParse(Field(line,start,10),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)) throw new FormatException("Bad coordinate.");
            return value;
        }
    }

    /// <summary>Turns a verified free structure into an ordinary runtime MoleculeDefinition.</summary>
    public static class DiscoveredMolecule
    {
        /// <summary>Teaching-model scale used by the curated recipes (C-C 1.54 Å ≈ 0.2 m).</summary>
        public const float MetresPerAngstrom=.13f;

        public static MoleculeDefinition Create(MolGraph built,MolGraph conformer,int[] map,MoleculeLookupResult info,ElementDefinition[] elements,out bool usedConformer)
        {
            usedConformer=conformer!=null && map!=null && map.Length==built.Count;
            var positions=new Vector3[built.Count];
            for(var i=0;i<built.Count;i++)
            {
                if(usedConformer) { var q=conformer.Positions[map[i]]; positions[i]=new Vector3(q.x,q.y,-q.z)*MetresPerAngstrom; } // right- to left-handed axes keeps chirality
                else positions[i]=built.Positions[i];
            }
            var centre=Vector3.zero; foreach(var position in positions) centre+=position; centre/=Mathf.Max(1,positions.Length);
            var definition=ScriptableObject.CreateInstance<MoleculeDefinition>();
            var formula=built.Formula();
            var title=info!=null && !string.IsNullOrEmpty(info.title)?info.title:formula;
            if(title.Length>40) title=title.Substring(0,37)+"...";
            definition.name=title;
            definition.id=info!=null && info.cid>0?"pubchem-"+info.cid:"free-"+Guid.NewGuid().ToString("N");
            definition.displayName=title;
            definition.formula=formula; definition.molecularFormula=formula;
            definition.atoms=built.Symbols.Select((symbol,i)=>new AtomSite(elements.First(e=>e!=null && e.symbol==symbol),positions[i]-centre)).ToArray();
            definition.bonds=built.Bonds.Select(b=>new BondDefinition(b.A,b.B,b.Order)).ToArray();
            definition.seedIndex=Mathf.Max(0,built.Symbols.FindIndex(s=>s!="H"));
            var iupac=info!=null && !string.IsNullOrEmpty(info.iupacName)?"IUPAC: "+info.iupacName:"";
            definition.geometry=(info!=null && info.cid>0?"PubChem CID "+info.cid+"  "+iupac+"\n":"")+
                (usedConformer?"3D conformer from PubChem, scaled as a teaching model":"Idealised in-lab geometry; PubChem has no 3D conformer");
            definition.electronicState="Built freely and identified in PubChem from its bonding. Bond orders are shown as you built them.";
            return definition;
        }
    }
}
