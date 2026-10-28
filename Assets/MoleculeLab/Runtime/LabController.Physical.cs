using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoleculeLab
{
    // Physical controls instead of menu buttons: a red reset button on the bench and two bins beside it.
    public sealed partial class LabController
    {
        public ResetButton ResetControl { get; private set; }
        public MoleculeBin GalleryBin { get; private set; }
        public MoleculeBin TrashBin { get; private set; }
        public ReactionStation Station { get; private set; }

        void BuildPhysicalControls()
        {
            // Reset button front left and the gallery hole back left, both at the far left end of the wider bench.
            var end=BenchWidth*.5f;
            ResetControl=ResetButton.Create(bench,this,new Vector3(-(end-.2f),.04f,-.3f));
            GalleryBin=MoleculeBin.CreateHole(bench,this,new Vector3(-(end-.3f),0,.2f));
            TrashBin=MoleculeBin.Create(Environment.transform,this,false,new Vector3(end+.45f,0,.9f));
        }
        /// <summary>
        /// The reaction corner: a pack table against the right wall, under the reactions whiteboard, with the vessel and tools.
        /// The old tray on the workbench is retired; the vessel's anchor becomes the lab's "Reaction tray".
        /// </summary>
        void BuildReactionCorner()
        {
            var wall=Classroom!=null?Classroom.SideWall:2.94f;
            var local=new Vector3(wall-.36f,0,ClassroomBuilder.ReactionCornerZ);
            var table=brightClassroom && classroomKit!=null?classroomKit.desk:null;
            Station=ReactionStation.Create(this,Environment.transform,local,90,table,classroomKit);
            var oldTray=Environment.tray;
            if(oldTray!=null && oldTray!=Station.Anchor)
            {
                oldTray.name="Reaction tray (retired)"; oldTray.gameObject.SetActive(false);
                foreach(var t in bench.GetComponentsInChildren<TMPro.TMP_Text>(true)) if(t.text.Trim()=="REACTION TRAY" || t.text.Trim()=="REACTION VESSEL") t.gameObject.SetActive(false);
            }
            tray=Station.Anchor; Environment.tray=Station.Anchor;
        }
        /// <summary>The workbench is widened to this (it was 2.05 m), so finished molecules have room away from the gallery hole.</summary>
        public const float BenchWidth=2.6f;
        void WidenBench()
        {
            var stretch=BenchWidth/2.05f;
            foreach(var t in bench.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="Workbench" || t.name=="Worktable timber edge") { var s=t.localScale; s.x*=stretch; t.localScale=s; }
                else if(t.name=="Table support") { var p=t.localPosition; p.x=Mathf.Sign(p.x)*(BenchWidth*.5f-.17f); t.localPosition=p; }
            }
        }
        /// <summary>Everything resets except the gallery, which is saved between sessions.</summary>
        public void ResetFromButton()
        {
            ResetLab();
            if(audioSource!=null && completeTone!=null) audioSource.PlayOneShot(completeTone);
            Notify("Lab reset. Your gallery is kept.");
        }
        /// <summary>A finished molecule dropped into a bin: keep it in the gallery, or discard it. Either way it leaves the bench.</summary>
        public void MoleculeIntoBin(MoleculeView view,bool gallery)
        {
            if(view==null || !views.Contains(view) || !view.State.Complete || view.Grab.isSelected) return;
            var name=view.Definition.displayName; string message;
            if(gallery)
            {
                var isNew=RecordInGallery(view.Definition);
                message=isNew?name+" is now in the gallery behind you.":name+" was already in the gallery; its count went up.";
                GalleryBin.Flash(isNew?"Added: "+name:name+" x"+GalleryCount(view.Definition.id));
            }
            else { message=name+" discarded."; TrashBin.Flash("Discarded"); }
            Recycle(view); UpdateTray(); Notify(message,true);
        }
        // ---------- the physical reaction station ----------
        public static string ToolName(ReactionCondition condition) => condition==ReactionCondition.Ignition?"FLAME":condition==ReactionCondition.PalladiumCatalyst?"Pd CATALYST":condition==ReactionCondition.AcidCatalyst?"ACID":"no tool";
        /// <summary>A tool held over the vessel (or, for tool-free reactions, nothing at all): run the reaction it makes possible, or say what is missing.</summary>
        public void ApplyCondition(ReactionCondition condition)
        {
            if(reacting) return;
            var docked=DockedViews();
            if(docked.Length==0) { Station?.Flash("Put finished molecules in the vessel first."); return; }
            var reaction=FindReaction(docked,condition,out var plan,out var problem);
            if(reaction==null)
            {
                ReactionCondition? other=null;
                foreach(var tool in ReactionRules.Tools) if(tool!=condition && CanRunWith(docked,tool)) { other=tool; break; }
                var message=problem!=null?problem:
                    other!=null?"The "+ToolName(condition)+" does nothing here. This reaction needs the "+ToolName(other.Value)+".":
                    plan!=null && plan.State==PlanState.Missing?ToolName(condition)+": "+plan.Hint+".":
                    "The "+ToolName(condition)+" does nothing with these molecules. It "+(plan!=null?plan.Hint:"")+".";
                Station?.Flash(message); Notify(message); return;
            }
            ActiveReaction=reaction; SelectedCondition=condition; UpdateTray();
            ActivateReaction();
        }
        /// <summary>The curated recipe that can use everything in the vessel (the current one if it still fits); null if none does.</summary>
        ReactionDefinition ChooseReaction(IEnumerable<MoleculeDefinition> contents)
        {
            var list=contents.ToList();
            bool Fits(ReactionDefinition r)=>r!=null && list.GroupBy(d=>d).All(g=>r.inputs.Any(p=>p.molecule==g.Key && p.count>=g.Count()));
            if(Curated(ActiveReaction) && Fits(ActiveReaction)) return ActiveReaction;
            return reactions?.FirstOrDefault(Fits);
        }
        /// <summary>What the vessel label shows: what is in it and, for each tool, what it would make or what is still missing.</summary>
        public string ReactionStatus()
        {
            if(reacting) return "<b>Reacting...</b>\nAtoms rearrange; nothing is lost.";
            var docked=DockedViews();
            if(docked.Length==0) return "<b>REACTION VESSEL</b>\nDrop finished molecules in here";
            // The label refreshes four times a second; only work the rules out again when the vessel's contents change.
            if(statusText!=null && statusReaction==ActiveReaction && statusViews!=null && statusViews.SequenceEqual(docked)) return statusText;
            statusViews=docked; statusReaction=ActiveReaction; statusText=WorkOutStatus(docked); return statusText;
        }
        string statusText; MoleculeView[] statusViews; ReactionDefinition statusReaction;
        string WorkOutStatus(MoleculeView[] docked)
        {
            var r=ActiveReaction;
            if(Curated(r) && ChooseReaction(docked.Select(v=>v.Definition))==r)
            {
                var checklist=string.Join("   ",r.inputs.Select(p=>$"{Mathf.Min(p.count,docked.Count(v=>v.Definition==p.molecule))}/{p.count} {p.molecule.formula}"));
                var missing=r.inputs.Where(p=>docked.Count(v=>v.Definition==p.molecule)<p.count).Select(p=>$"{p.count-docked.Count(v=>v.Definition==p.molecule)} more {p.molecule.formula}").ToArray();
                var next=missing.Length>0?"Add "+string.Join(" and ",missing):"Now hold the <b>"+ToolName(r.requiredCondition)+"</b> over the vessel";
                return "<b>"+r.title+"</b>\n"+r.equation+"\n"+checklist+"\n"+next;
            }
            var graphs=Graphs(docked); var names=Names(docked); var lines=new List<string>();
            foreach(var tool in ReactionRules.Tools)
            {
                var curated=CuratedFor(docked,tool); var plan=ReactionRules.Plan(graphs,names,tool);
                if(curated!=null) lines.Add("<b>"+ToolName(tool)+"</b>: "+curated.title+" - ready");
                else if(plan.Ready) lines.Add("<b>"+ToolName(tool)+"</b>: "+plan.Title+" - ready ("+plan.Equation+")");
                else if(plan.State==PlanState.Missing) lines.Add("<b>"+ToolName(tool)+"</b>: "+plan.Hint);
            }
            if(lines.Count==0) lines.Add("Nothing here reacts yet. Fuels burn with O2 (FLAME); H2 adds to C=C (Pd); water + alkene or acid + alcohol (ACID).");
            var contents=string.Join(" + ",docked.GroupBy(v=>v.Definition.formula).Select(g=>(g.Count()>1?g.Count()+" ":"")+g.Key));
            return "<b>In the vessel:</b> "+contents+"\n"+string.Join("\n",lines);
        }

        int GalleryCount(string id)
        {
            if(Gallery==null) return 0;
            foreach(var entry in Gallery.Entries) if(entry.id==id) return entry.timesBuilt;
            return 0;
        }
    }
}
