using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace MoleculeLab
{
    /// <summary>What PubChem says a compound is and what it is used for, shown on the gallery hologram.</summary>
    [Serializable] public sealed class MoleculeInfo
    {
        public LookupStatus status;
        public int cid;
        public string description, descriptionSource, message;
        public string[] uses=Array.Empty<string>();
    }

    /// <summary>Describes a PubChem compound. Swappable so tests need no network.</summary>
    public interface IMoleculeInfoSource
    {
        IEnumerator Describe(int cid,Action<MoleculeInfo> done);
    }

    public sealed partial class PubChemClient : IMoleculeInfoSource
    {
        public static string DescriptionUrl(int cid) => BaseUrl+"compound/cid/"+cid.ToString(CultureInfo.InvariantCulture)+"/description/JSON";
        public static string UsesUrl(int cid) => "https://pubchem.ncbi.nlm.nih.gov/rest/pug_view/data/compound/"+cid.ToString(CultureInfo.InvariantCulture)+"/JSON?heading=Uses";

        /// <summary>Two requests (description, then the "Uses" section of the compound record); cached on disk per CID.</summary>
        public IEnumerator Describe(int cid,Action<MoleculeInfo> done)
        {
            var path=string.IsNullOrEmpty(cacheDirectory)?null:Path.Combine(cacheDirectory,"info-"+cid.ToString(CultureInfo.InvariantCulture)+".json");
            if(path!=null)
            {
                MoleculeInfo cached=null;
                try { if(File.Exists(path)) cached=JsonUtility.FromJson<MoleculeInfo>(File.ReadAllText(path)); }
                catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException) { }
                if(cached!=null && cached.cid==cid) { done(cached); yield break; }
            }
            var info=new MoleculeInfo{cid=cid,status=LookupStatus.Found};
            yield return Throttle();
            using(var request=UnityWebRequest.Get(DescriptionUrl(cid)))
            {
                request.timeout=timeoutSeconds; yield return request.SendWebRequest();
                if(request.responseCode==200) { var parsed=ParseDescription(request.downloadHandler.text); info.description=parsed.description; info.descriptionSource=parsed.source; }
                else if(request.responseCode!=404) { done(Failed(cid,request)); yield break; }
            }
            yield return Throttle();
            using(var request=UnityWebRequest.Get(UsesUrl(cid)))
            {
                request.timeout=timeoutSeconds; yield return request.SendWebRequest();
                if(request.responseCode==200) info.uses=ParseUses(request.downloadHandler.text);
                else if(request.responseCode!=404) { done(Failed(cid,request)); yield break; }
            }
            if(path!=null)
                try { Directory.CreateDirectory(cacheDirectory); File.WriteAllText(path,JsonUtility.ToJson(info)); }
                catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException) { }
            done(info);
        }
        static MoleculeInfo Failed(int cid,UnityWebRequest request) => new MoleculeInfo{cid=cid,status=LookupStatus.Error,
            message=request.result==UnityWebRequest.Result.ConnectionError?"No connection to PubChem.":"PubChem answered "+request.responseCode+"."};

        [Serializable] internal sealed class DescriptionReply { public InformationList InformationList; }
        [Serializable] internal sealed class InformationList { public DescriptionItem[] Information; }
        [Serializable] internal sealed class DescriptionItem { public int CID; public string Title, Description, DescriptionSourceName; }

        /// <summary>Prefers the ChEBI definition ("X is a ... It has a role as ..."), else PubChem's first description.</summary>
        public static (string description,string source) ParseDescription(string json)
        {
            DescriptionReply reply=null;
            try { reply=JsonUtility.FromJson<DescriptionReply>(json??""); } catch(ArgumentException) { }
            var items=reply?.InformationList?.Information?.Where(i=>i!=null && !string.IsNullOrWhiteSpace(i.Description)).ToArray()??Array.Empty<DescriptionItem>();
            var best=items.FirstOrDefault(i=>i.DescriptionSourceName=="ChEBI")??items.FirstOrDefault();
            if(best==null) return (null,null);
            return (Shorten(best.Description,330),best.DescriptionSourceName);
        }

        // PUG View records nest sections; JsonUtility needs explicit, non-recursive levels.
        [Serializable] internal sealed class ViewReply { public ViewRecord Record; }
        [Serializable] internal sealed class ViewRecord { public ViewSection1[] Section; }
        [Serializable] internal sealed class ViewSection1 { public string TOCHeading; public ViewSection2[] Section; }
        [Serializable] internal sealed class ViewSection2 { public string TOCHeading; public ViewInformation[] Information; public ViewSection3[] Section; }
        [Serializable] internal sealed class ViewSection3 { public string TOCHeading; public ViewInformation[] Information; }
        [Serializable] internal sealed class ViewInformation { public string Name; public ViewValue Value; }
        [Serializable] internal sealed class ViewValue { public ViewString[] StringWithMarkup; }
        [Serializable] internal sealed class ViewString { public string String; }

        static readonly string[] NoiseStarts={"For more","For ","CIR ingredient","Use (kg","Estimated","Information on","Not Known"};
        static readonly string[] NoiseContains={"[Category:","/SRP:","USEPA/OPP"};
        /// <summary>
        /// Picks up to two readable "what it is used for" sentences from the compound record's Uses section
        /// (HSDB "Sources/Uses" first), skipping database links and statistics; falls back to the use classification.
        /// </summary>
        public static string[] ParseUses(string json,int maximum=2)
        {
            ViewReply reply=null;
            try { reply=JsonUtility.FromJson<ViewReply>(json??""); } catch(ArgumentException) { }
            var uses=reply?.Record?.Section?.Where(s=>s!=null && s.Section!=null).SelectMany(s=>s.Section).FirstOrDefault(s=>s!=null && s.TOCHeading=="Uses");
            if(uses==null) return Array.Empty<string>();
            string First(ViewInformation i) => i?.Value?.StringWithMarkup?.FirstOrDefault(v=>v!=null && !string.IsNullOrWhiteSpace(v.String))?.String;
            var information=uses.Information??Array.Empty<ViewInformation>();
            var ordered=information.Where(i=>i!=null && i.Name=="Sources/Uses").Concat(information.Where(i=>i!=null && string.IsNullOrEmpty(i.Name)));
            var result=new List<string>();
            foreach(var text in ordered.Select(First))
            {
                if(text==null || text.Length<25 || NoiseStarts.Any(n=>text.StartsWith(n)) || NoiseContains.Any(n=>text.Contains(n))) continue;
                var clean=Shorten(Clean(text),200);
                if(clean.Length<25 || result.Any(r=>r.StartsWith(clean.Substring(0,20),StringComparison.OrdinalIgnoreCase))) continue;
                result.Add(clean); if(result.Count>=maximum) return result.ToArray();
            }
            if(result.Count>0) return result.ToArray();
            var classes=(uses.Section??Array.Empty<ViewSection3>()).Where(s=>s!=null && s.TOCHeading=="Use Classification")
                .SelectMany(s=>s.Information??Array.Empty<ViewInformation>()).Select(First).Where(t=>t!=null)
                .Select(t=>t.Split(new[]{"->"},StringSplitOptions.None).Last().Trim().TrimEnd('.'))
                .Where(t=>t.Length>2 && t.Length<40 && !t.Contains("Green circle") && !t.Contains(";"))
                .Select(t=>t.ToLowerInvariant()).Distinct().Take(4).ToArray();
            return classes.Length==0?Array.Empty<string>():new[]{"Classified as: "+string.Join(", ",classes)+"."};
        }
        static string Clean(string text)
        {
            text=Regex.Replace(text,@"\[[^\]]*\]","");            // [Merck Index]
            text=Regex.Replace(text,@"/[A-Z][A-Z ,]*:?/","");      // /SRP:/ style editorial markers
            text=text.Replace("...","").Replace(" ;",";");
            return Regex.Replace(text,@"\s+"," ").Trim().TrimStart(';',',',' ');
        }
        /// <summary>Keeps whole sentences up to the limit; otherwise cuts at a word and adds "...".</summary>
        public static string Shorten(string text,int limit)
        {
            if(string.IsNullOrEmpty(text) || text.Length<=limit) return text;
            var cut=text.LastIndexOf(". ",limit,StringComparison.Ordinal);
            if(cut>limit/3) return text.Substring(0,cut+1);
            var space=text.LastIndexOf(' ',limit); if(space<limit/2) space=limit;
            return text.Substring(0,space).TrimEnd(',',';',' ')+"...";
        }
    }
}
