using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MoleculeLab
{
    // Reactions beyond the fixed recipes. ReactionRules reads the bonding of whatever is in the vessel; a rule reaction becomes an
    // ordinary ReactionDefinition made at runtime, so docking, the tools, the animation and the atom bookkeeping stay the same as
    // for the curated reactions. Molecules from the gallery can be copied into the vessel and used too.
    public sealed partial class LabController
    {
        /// <summary>How many molecules the vessel holds at once.</summary>
        public const int VesselCapacity=6;
        /// <summary>Molecules made in reactions, identified in free build or copied from the gallery: one definition per structure.</summary>
        readonly List<MoleculeDefinition> madeDefinitions=new List<MoleculeDefinition>();
        Coroutine autoReaction;

        MoleculeView[] DockedViews() => views.Where(v=>v!=null && v.Docked && !v.Grab.isSelected).ToArray();
        static List<MolGraph> Graphs(MoleculeView[] docked) => docked.Select(v=>MolGraph.From(v.Definition)).ToList();
        static List<string> Names(MoleculeView[] docked) => docked.Select(v=>v.Definition.displayName).ToList();
        bool Curated(ReactionDefinition reaction) => reaction!=null && reactions!=null && reactions.Contains(reaction);

        /// <summary>A curated recipe needing this condition whose inputs are all in the vessel.</summary>
        ReactionDefinition CuratedFor(MoleculeView[] docked,ReactionCondition condition)
        {
            bool Present(ReactionDefinition r) => r!=null && r.requiredCondition==condition && r.inputs.All(p=>docked.Count(v=>v.Definition==p.molecule)>=p.count);
            if(Curated(ActiveReaction) && Present(ActiveReaction)) return ActiveReaction;
            return reactions?.FirstOrDefault(Present);
        }
        /// <summary>What a tool would do with the vessel's contents right now (tests and the vessel label use this).</summary>
        public ReactionPlan PlanFor(ReactionCondition condition) { var docked=DockedViews(); return ReactionRules.Plan(Graphs(docked),Names(docked),condition); }
        bool CanRunWith(MoleculeView[] docked,ReactionCondition condition) =>
            docked.Length>0 && (CuratedFor(docked,condition)!=null || ReactionRules.Plan(Graphs(docked),Names(docked),condition).Ready);

        /// <summary>The reaction a tool starts: a curated recipe if one fits, otherwise a reaction rule.</summary>
        ReactionDefinition FindReaction(MoleculeView[] docked,ReactionCondition condition,out ReactionPlan plan,out string problem)
        {
            plan=null; problem=null;
            var curated=CuratedFor(docked,condition); if(curated!=null) return curated;
            plan=ReactionRules.Plan(Graphs(docked),Names(docked),condition);
            return plan.Ready?FromPlan(plan,docked,out problem):null;
        }
        ReactionDefinition FromPlan(ReactionPlan plan,MoleculeView[] docked,out string problem)
        {
            problem=null;
            var products=new List<MoleculeDefinition>();
            for(var i=0;i<plan.Products.Count;i++)
            {
                var definition=ResolveProduct(plan.Products[i],plan.ProductNames[i]);
                if(definition==null) { problem="The lab cannot model a product of this reaction."; return null; }
                products.Add(definition);
            }
            string Side(IEnumerable<ReactionAmount> side) => string.Join(" + ",side.Select(p=>(p.count>1?p.count+" ":"")+p.molecule.formula));
            var reaction=ScriptableObject.CreateInstance<ReactionDefinition>(); ownedResources.Add(reaction);
            reaction.id="rule-"+plan.Rule; reaction.name=reaction.title=plan.Title; reaction.requiredCondition=plan.Condition;
            reaction.inputs=plan.Inputs.Select(i=>docked[i].Definition).GroupBy(d=>d).Select(g=>new ReactionAmount(g.Key,g.Count())).ToArray();
            reaction.outputs=products.GroupBy(d=>d).Select(g=>new ReactionAmount(g.Key,g.Count())).ToArray();
            reaction.equation=Side(reaction.inputs)+" -> "+Side(reaction.outputs);
            reaction.explanation=plan.Explanation; reaction.conditions=ToolName(plan.Condition);
            if(!ReactionEngine.IsBalanced(reaction)) { problem="That reaction would not keep every atom, so it was not run."; return null; }
            return reaction;
        }

        /// <summary>A product's definition: the lab's recipe if it is one, an earlier product or gallery exhibit with the same bonding, else a new one.</summary>
        MoleculeDefinition ResolveProduct(MolGraph graph,string suggested)
        {
            var known=molecules.FirstOrDefault(m=>m!=null && StructureMatcher.Match(graph,MolGraph.From(m))!=null)
                ??madeDefinitions.FirstOrDefault(m=>m!=null && StructureMatcher.Match(graph,MolGraph.From(m))!=null);
            if(known!=null) return known;
            var entry=Gallery?.Entries.FirstOrDefault(e=>StructureMatcher.Match(graph,MoleculeGallery.ToGraph(e))!=null);
            if(entry!=null) return Made(graph,entry.name,entry.id,"Made in the reaction vessel; the same molecule is in your gallery.");
            return Made(graph,suggested??ReactionRules.ClassName(graph),null,"Made in the reaction vessel from molecules you built. Idealised in-lab geometry.");
        }
        /// <summary>The definition behind a gallery exhibit, so a copy can go into the vessel.</summary>
        MoleculeDefinition DefinitionFor(GalleryEntry entry)
        {
            var graph=MoleculeGallery.ToGraph(entry);
            return molecules.FirstOrDefault(m=>m!=null && m.id==entry.id)
                ??madeDefinitions.FirstOrDefault(m=>m!=null && m.id==entry.id)
                ??molecules.FirstOrDefault(m=>m!=null && StructureMatcher.Match(graph,MolGraph.From(m))!=null)
                ??madeDefinitions.FirstOrDefault(m=>m!=null && StructureMatcher.Match(graph,MolGraph.From(m))!=null)
                ??Made(graph,entry.name,entry.id,"Copied from the gallery. "+(entry.detail??""));
        }
        MoleculeDefinition Made(MolGraph graph,string name,string id,string origin)
        {
            if(graph.Symbols.Any(s=>ElementFor(s)==null)) return null;
            var centre=Vector3.zero; foreach(var p in graph.Positions) centre+=p; centre/=Mathf.Max(1,graph.Count);
            var formula=graph.Formula();
            var definition=ScriptableObject.CreateInstance<MoleculeDefinition>();
            if(name.Length>40) name=name.Substring(0,37)+"...";
            definition.name=name; definition.displayName=name; definition.id=id??StableId(graph);
            definition.formula=formula; definition.molecularFormula=formula;
            definition.atoms=graph.Symbols.Select((symbol,i)=>new AtomSite(ElementFor(symbol),graph.Positions[i]-centre)).ToArray();
            definition.bonds=graph.Bonds.Select(b=>new BondDefinition(b.A,b.B,b.Order)).ToArray();
            definition.seedIndex=Mathf.Max(0,graph.Symbols.FindIndex(s=>s!="H"));
            definition.geometry="Idealised in-lab geometry"; definition.electronicState=origin;
            if(!definition.IsValid(out var error)) { Debug.LogWarning(name+" cannot be modelled: "+error); Destroy(definition); return null; }
            madeDefinitions.Add(definition); ownedResources.Add(definition);
            return definition;
        }
        /// <summary>The same structure built the same way gets the same id, so the gallery counts it instead of adding it twice.</summary>
        static string StableId(MolGraph graph)
        {
            uint hash=2166136261; foreach(var c in SmilesWriter.Write(graph)) { hash^=c; hash*=16777619; }
            return "made-"+graph.Formula()+"-"+hash.ToString("x8");
        }

        // ---------- the vessel ----------
        Vector3 TraySlot(int index) => tray.TransformPoint(new Vector3(-.16f+(index%3)*.16f,.15f,index<3?-.07f:.08f));
        int FreeSlot(MoleculeView[] others)
        {
            for(var i=0;i<VesselCapacity;i++) if(!others.Any(v=>Vector3.Distance(v.transform.position,TraySlot(i))<.03f)) return i;
            return 0;
        }
        /// <summary>Puts a finished molecule in the vessel; the vessel label then says which tool (if any) makes it react.</summary>
        void Dock(MoleculeView view,MoleculeView[] others)
        {
            var chosen=ChooseReaction(others.Select(v=>v.Definition).Append(view.Definition));
            if(chosen!=ActiveReaction) { ActiveReaction=chosen; SelectedCondition=ReactionCondition.None; RebuildMenu(); }
            view.StopMotion(); view.transform.localScale=Vector3.one*.4f; view.transform.SetPositionAndRotation(TraySlot(FreeSlot(others)),Quaternion.identity);
            view.Docked=true; if(view.Body!=null) { view.Body.position=view.transform.position; view.Body.rotation=view.transform.rotation; }
            UpdateTray(); Station?.Refresh(); ScheduleAutoReaction();
        }
        /// <summary>Some reactions need no tool (bromine and an alkene): they start by themselves a moment after both are in.</summary>
        void ScheduleAutoReaction()
        {
            if(reacting || !CanRunWith(DockedViews(),ReactionCondition.None)) return;
            if(autoReaction!=null) StopCoroutine(autoReaction);
            autoReaction=StartCoroutine(AutoReact());
        }
        IEnumerator AutoReact()
        {
            Station?.Flash("These react without any tool...");
            yield return new WaitForSeconds(.8f);
            autoReaction=null;
            if(!reacting && CanRunWith(DockedViews(),ReactionCondition.None)) ApplyCondition(ReactionCondition.None);
        }
        /// <summary>Molecules a reaction uses: the active reaction's inputs, picked from the vessel (other molecules stay).</summary>
        MoleculeView[] ReactionInputs()
        {
            var docked=DockedViews(); var input=new List<MoleculeView>();
            if(ActiveReaction==null || ActiveReaction.inputs==null) return input.ToArray();
            foreach(var p in ActiveReaction.inputs) if(p.molecule!=null) input.AddRange(docked.Where(v=>v.Definition==p.molecule && !input.Contains(v)).Take(p.count));
            return input.ToArray();
        }

        /// <summary>Is this point over the vessel (local to the tray: 0.5 m wide, 0.4 m deep)?</summary>
        public bool OverVessel(Vector3 world)
        {
            if(tray==null) return false;
            var local=tray.InverseTransformPoint(world);
            return Mathf.Abs(local.x)<.3f && Mathf.Abs(local.z)<.25f && local.y>-.05f && local.y<.55f;
        }
        /// <summary>
        /// A gallery exhibit let go over the vessel: a copy goes in and the exhibit floats back to its stand. The copy is made
        /// from beaker atoms, so every atom in play is still accounted for.
        /// </summary>
        public bool CopyExhibitIntoVessel(GalleryEntry entry)
        {
            if(entry==null || reacting || tray==null) return false;
            var docked=DockedViews();
            if(docked.Length>=VesselCapacity) { Station?.Flash("The vessel is full. Take a molecule out first."); return false; }
            if(views.Count>=12) { Station?.Flash("The bench is full. Put a molecule in a bin first."); return false; }
            var definition=DefinitionFor(entry);
            if(definition==null) { Station?.Flash("The lab cannot model "+entry.name+"."); return false; }
            var view=Spawn(new MoleculeState(definition,true),TraySlot(FreeSlot(docked)));
            for(var i=0;i<definition.atoms.Length;i++)
            {
                var symbol=definition.atoms[i].element.symbol; var token=Inventory.FirstAvailable(symbol);
                var atom=token<0?null:loose.FirstOrDefault(a=>a!=null && a.TokenId==token);
                if(atom==null || !Inventory.Transfer(token,AtomInventory.Shelf,view.InventoryOwner))
                {
                    Recycle(view); RefreshStock();
                    Station?.Flash("Not enough "+symbol+" atoms in the beakers to copy "+entry.name+"."); return false;
                }
                view.TokenIds[i]=token; ConsumeLoose(atom);
            }
            RefreshStock(); Dock(view,docked);
            Notify("A copy of "+entry.name+" from the gallery is in the vessel. Its atoms came from the beakers.",true);
            return true;
        }
    }
}
