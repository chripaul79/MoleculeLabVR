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
            ResetControl=ResetButton.Create(bench,this);
            // Gallery: a hole in the back left of the workbench (moves with the bench). Trash: a floor bin to the right.
            GalleryBin=MoleculeBin.CreateHole(bench,this,new Vector3(-.78f,0,.22f));
            TrashBin=MoleculeBin.Create(Environment.transform,this,false,new Vector3(1.4f,0,.9f));
            Station=ReactionStation.Create(this,bench,tray,classroomKit);
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
        /// <summary>A tool held over the vessel: supply its condition and, if everything is there, run the reaction.</summary>
        public void ApplyCondition(ReactionCondition condition)
        {
            if(reacting) return;
            var docked=views.Where(v=>v!=null && v.Docked && !v.Grab.isSelected).ToArray();
            if(docked.Length==0) { Station?.Flash("Put finished molecules in the vessel first."); return; }
            SelectedCondition=condition; UpdateTray();
            if(!CanActivate(out var reason))
            {
                var needed=ActiveReaction!=null?ActiveReaction.requiredCondition:ReactionCondition.None;
                var message=ActiveReaction!=null && InputsComplete() && needed!=condition?"The "+ToolName(condition)+" does nothing here. This reaction needs the "+ToolName(needed)+".":reason;
                Station?.Flash(message); Notify(message); return;
            }
            ActivateReaction();
        }
        bool InputsComplete()
        {
            if(ActiveReaction==null) return false;
            var docked=views.Where(v=>v!=null && v.Docked && !v.Grab.isSelected).ToArray();
            return ActiveReaction.inputs.All(p=>docked.Count(v=>v.Definition==p.molecule)>=p.count);
        }
        /// <summary>The reaction that can use everything already in the vessel plus <paramref name="adding"/> (the current one if it still fits).</summary>
        ReactionDefinition ChooseReaction(IEnumerable<MoleculeDefinition> contents)
        {
            var list=contents.ToList();
            bool Fits(ReactionDefinition r)=>r!=null && list.GroupBy(d=>d).All(g=>r.inputs.Any(p=>p.molecule==g.Key && p.count>=g.Count()));
            if(Fits(ActiveReaction)) return ActiveReaction;
            return reactions?.FirstOrDefault(Fits);
        }
        /// <summary>What the vessel label shows: contents, what is missing, and which tool to use.</summary>
        public string ReactionStatus()
        {
            if(reacting) return "<b>Reacting...</b>\nAtoms rearrange; nothing is lost.";
            var docked=views.Where(v=>v!=null && v.Docked && !v.Grab.isSelected).ToArray();
            if(docked.Length==0) return "<b>REACTION VESSEL</b>\nDrop finished molecules in here";
            var r=ActiveReaction; if(r==null) return "No known reaction for these molecules.";
            var checklist=string.Join("   ",r.inputs.Select(p=>$"{Mathf.Min(p.count,docked.Count(v=>v.Definition==p.molecule))}/{p.count} {p.molecule.formula}"));
            var missing=r.inputs.Where(p=>docked.Count(v=>v.Definition==p.molecule)<p.count).Select(p=>$"{p.count-docked.Count(v=>v.Definition==p.molecule)} more {p.molecule.formula}").ToArray();
            var next=missing.Length>0?"Add "+string.Join(" and ",missing):"Now hold the <b>"+ToolName(r.requiredCondition)+"</b> over the vessel";
            return "<b>"+r.title+"</b>\n"+r.equation+"\n"+checklist+"\n"+next;
        }

        int GalleryCount(string id)
        {
            if(Gallery==null) return 0;
            foreach(var entry in Gallery.Entries) if(entry.id==id) return entry.timesBuilt;
            return 0;
        }
    }
}
