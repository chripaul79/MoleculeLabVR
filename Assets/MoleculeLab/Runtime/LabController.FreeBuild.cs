using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace MoleculeLab
{
    // Free build: assemble any structure the finite stock allows, by hand only. Every change updates the
    // structure's status; the moment every valence is satisfied it is checked in PubChem and, if known, locked.
    public sealed partial class LabController
    {
        [Header("Free build")]
        [Range(.03f,.12f)] public float freeSnapDistance=.07f;
        [Range(.06f,.2f)] public float ringSnapDistance=.13f;
        [Tooltip("Releasing a beaker atom this close to the build spot starts a free structure.")]
        [Range(.1f,.6f)] public float freeSeedDistance=.3f;
        [Min(1)] public float freeRetrySeconds=8;
        /// <summary>Structure identification service. Defaults to PubChem with an on-device cache; tests may replace it.</summary>
        public IMoleculeLookup MoleculeLookup { get; set; }
        public FreeBuildView FreeView => freeView;
        public bool IsCheckingStructure => checkingStructure;
        public MoleculeLookupResult LastLookup { get; private set; }
        public IReadOnlyList<string> Discoveries => discoveries;
        FreeBuildView freeView;
        bool checkingStructure;
        int lookupSession, freeRetries;
        readonly List<string> discoveries=new List<string>();
        LineRenderer dropZone;
        TextMeshPro dropLabel;
        const int MaximumFreeRetries=3;
        /// <summary>Extra goals without a recipe (no guide spots): name, formula as written, Hill formula as the structure reports it.</summary>
        static readonly (string name,string formula,string hill)[] FreeGoals=
        {
            ("Ammonia","NH3","H3N"),("Hydrogen peroxide","H2O2","H2O2"),("Formaldehyde","CH2O","CH2O"),("Hydrogen cyanide","HCN","CHN"),
            ("Methylamine","CH3NH2","CH5N"),("Methanethiol","CH3SH","CH4S"),("Chloromethane","CH3Cl","CH3Cl"),("Acetic acid","CH3COOH","C2H4O2"),
            ("Ethyl acetate","CH3COOC2H5","C4H8O2"),("Phosphine","PH3","H3P"),("Hydrogen fluoride","HF","FH"),("Propane","C3H8","C3H8"),
        };
        /// <summary>A BUILD goal: a recipe (guide spots available) or a formula to reach freely.</summary>
        public sealed class BuildGoal { public string Name, Formula, Hill; public MoleculeDefinition Recipe; }
        BuildGoal[] goals;
        /// <summary>The goal list: every recipe, then the extra formula-only goals.</summary>
        public IReadOnlyList<BuildGoal> Goals
        {
            get
            {
                if(goals!=null) return goals;
                var recipes=molecules.Where(m=>m!=null).Select(m=>new BuildGoal{Name=m.displayName,Formula=m.formula,Hill=MolGraph.From(m).Formula(),Recipe=m}).ToList();
                goals=recipes.Concat(FreeGoals.Where(g=>recipes.All(r=>r.Hill!=g.hill)).Select(g=>new BuildGoal{Name=g.name,Formula=g.formula,Hill=g.hill})).ToArray();
                return goals;
            }
        }
        /// <summary>Hint level for goals: true shows guide spots for every atom (recipes); false shows only the formula.</summary>
        public bool GuidedGoals { get; private set; } = true;
        public string FreeGoal { get; private set; }
        string freeGoalFormula; MoleculeDefinition freeGoalRecipe; int goalPage;
        string goalNote="";
        string TakeGoalNote() { var note=goalNote; goalNote=""; return note; }
        const string FreeInstructions="BUILDING FREELY\nTake an atom from a beaker and drop it on the ring. Release more atoms on the dots to bond them. Push two atoms together for a double bond or a ring; pull an end atom off to remove it.";

        /// <summary>Abandons a running PubChem check (its answer is ignored) and unlocks the structure.</summary>
        void CancelFreeCheck()
        {
            if(!checkingStructure) return;
            lookupSession++; checkingStructure=false;
            if(freeView!=null) { freeView.SetLocked(false); freeView.SetStatus("Check cancelled.",FreeBuildView.IncompleteColor); }
        }
        void EnsureMoleculeLookup()
        {
            if(MoleculeLookup==null) MoleculeLookup=new PubChemClient(Path.Combine(Application.persistentDataPath,"PubChemCache"));
        }
        ElementDefinition ElementFor(string symbol) => elements.FirstOrDefault(e=>e!=null && e.symbol==symbol);
        void ConsumeLoose(LooseAtom atom)
        {
            ClearPreview(atom); loose.Remove(atom); atom.gameObject.SetActive(false); LabVisuals.Discard(atom.gameObject);
        }
        bool FreeEditable()
        {
            if(reacting) { Notify("Wait for the reaction to finish."); return false; }
            if(checkingStructure) { Notify("Wait for the PubChem check to finish."); return false; }
            if(freeView==null || freeView.Structure.Count==0) { Notify("Take an atom from a beaker and drop it on the teal ring to start."); return false; }
            return true;
        }
        void CreateFreeView()
        {
            var go=new GameObject("Free structure"); go.transform.SetParent(dynamicRoot); go.transform.position=FreeBuildPosition;
            freeView=go.AddComponent<FreeBuildView>(); freeView.Initialize(this);
        }
        void DestroyFreeView()
        {
            if(freeView==null) return;
            freeView.CancelGrabs(); freeView.gameObject.SetActive(false); LabVisuals.Discard(freeView.gameObject); freeView=null;
        }
        void PrepareFreeBench()
        {
            foreach(var view in views.Where(v=>!v.State.Complete).ToArray()) Recycle(view);
            ClearBuildSpot();
        }
        void ClearBuildSpot()
        {
            if(views.Any(v=>!v.Grab.isSelected && Vector3.Distance(v.transform.position,FreeBuildPosition)<.3f)) ReturnMolecules();
        }

        /// <summary>
        /// Called after every change to the free structure. Incomplete: say what is missing.
        /// Complete: check PubChem automatically (the structure locks while it is checked).
        /// </summary>
        void FreeStructureChanged()
        {
            freeRetries=0;
            if(freeView!=null && freeView.Structure.Count==0) DestroyFreeView();
            UpdateTray();
            if(freeView==null) return;
            var structure=freeView.Structure;
            if(!structure.IsComplete(out _))
            {
                var open=structure.TotalOpenValence;
                freeView.SetStatus("Not complete: "+open+" open bond"+(open==1?"":"s")+"\n"+structure.MissingText(),FreeBuildView.IncompleteColor);
                return;
            }
            if(views.Count>=12) { freeView.SetStatus("Complete, but the bench is full.\nRecycle a molecule to identify it.",FreeBuildView.ProblemColor); return; }
            StartCoroutine(CheckFreeStructureRoutine());
        }

        /// <summary>Starts a structure from code (tests, or a future menu). In the headset, dropping an atom on the ring does this.</summary>
        public void StartFreeStructure(string symbol)
        {
            if(reacting || checkingStructure) { Notify("Wait for the current activity to finish."); return; }
            if(Mode!=LabMode.Free) SetMode(LabMode.Free);
            ClearFreeStructure(false);
            var element=ElementFor(symbol);
            var token=element==null?-1:Inventory.FirstAvailable(symbol);
            var atom=token<0?null:loose.FirstOrDefault(a=>a.TokenId==token);
            if(atom==null) { Notify("No "+symbol+" atoms left on the shelf. Return atoms or recycle a molecule."); return; }
            ClearBuildSpot(); CreateFreeView();
            if(!Inventory.Transfer(token,AtomInventory.Shelf,freeView.Owner)) { DestroyFreeView(); return; }
            freeView.AddAtom(element,token,Vector3.zero,atom.transform.position);
            ConsumeLoose(atom); RefreshStock();
            Notify("Started with "+symbol+". Release atoms on the teal dots to bond them.");
            FreeStructureChanged();
        }

        bool TryAttachFree(LooseAtom atom)
        {
            if(atom==null || !loose.Contains(atom) || atom.Grab.isSelected) return false;
            if(checkingStructure) { ClearPreview(atom); Notify("Wait for the PubChem check to finish."); return false; }
            if(freeView==null)
            {
                // Releasing a basket atom over the build spot starts a structure; releasing it at its own socket still puts it back.
                if(Vector3.Distance(atom.transform.position,FreeBuildPosition)>freeSeedDistance) return false;
                if(Vector3.Distance(atom.transform.position,Environment.SocketPosition(Inventory.Tokens[atom.TokenId].Socket))<.18f) return false;
                if(!atom.TakeFromShelf()) return false;
                ClearBuildSpot(); CreateFreeView();
                if(!Inventory.Transfer(atom.TokenId,atom.Owner,freeView.Owner)) { DestroyFreeView(); return false; }
                freeView.AddAtom(atom.Element,atom.TokenId,Vector3.zero,atom.transform.position);
                var symbol=atom.Element.symbol;
                ConsumeLoose(atom); RefreshStock();
                Notify("Started with "+symbol+". Release more atoms on the teal dots.",true);
                FreeStructureChanged();
                return true;
            }
            if(!freeView.FindPlacement(atom,out var placement) || !placement.Ready)
            { freeView.ClearPreview(atom); Notify("Release the atom on a teal dot next to an atom with a free bond."); return false; }
            if(!atom.TakeFromShelf() || !Inventory.Transfer(atom.TokenId,atom.Owner,freeView.Owner)) return false;
            var anchor=freeView.Structure.Label(placement.Anchor);
            freeView.Attach(atom.Element,atom.TokenId,placement,atom.transform.position);
            ConsumeLoose(atom); RefreshStock();
            Notify("Bonded to "+anchor+".",true);
            FreeStructureChanged();
            return true;
        }

        /// <summary>A grabbed structure atom was released against another: add a ring bond or raise the bond order.</summary>
        public void FreeBondRequested(FreeBuildView view,int atom,int partner)
        {
            if(view==null) return;
            if(view!=freeView || checkingStructure) { view.Relax(); return; }
            if(!view.CanAddBond(atom,partner,out var reason)) { view.Relax(); Notify(reason); return; }
            var raised=view.Structure.Graph.BondIndex(atom,partner)>=0;
            view.AddBond(atom,partner);
            Notify((raised?"Bond order raised: ":"Ring closed: ")+view.BondText()+".",true);
            FreeStructureChanged();
        }
        /// <summary>A grabbed end atom was pulled off: it returns to its basket.</summary>
        public void FreeAtomPulled(FreeBuildView view,int atom)
        {
            if(view==null) return;
            if(view!=freeView || checkingStructure) { view.Relax(); return; }
            if(!view.Structure.CanRemove(atom,out var reason)) { view.Relax(); Notify(reason); return; }
            var name=view.Structure.Label(atom);
            var token=view.RemoveAtom(atom);
            if(token>=0 && Inventory.Transfer(token,view.Owner,AtomInventory.Shelf)) SpawnShelfAtom(token);
            RefreshStock(); Notify(name+" was put back.");
            FreeStructureChanged();
        }

        public void FillHydrogens()
        {
            if(!FreeEditable()) return;
            var structure=freeView.Structure; var hydrogen=ElementFor("H"); var added=0; var shortage=false;
            for(var i=0;i<structure.Count && !shortage;i++)
            {
                if(structure.Symbol(i)=="H") continue;
                while(structure.OpenValence(i)>0)
                {
                    var token=Inventory.FirstAvailable("H");
                    var atom=token<0?null:loose.FirstOrDefault(a=>a.TokenId==token);
                    if(hydrogen==null || atom==null) { shortage=true; break; }
                    var directions=structure.OpenDirections(i);
                    var direction=directions.Length>0?directions[0]:Vector3.up;
                    if(!Inventory.Transfer(token,AtomInventory.Shelf,freeView.Owner)) { shortage=true; break; }
                    var local=structure.Position(i)+direction*FreeStructure.BondLength(structure.Symbol(i),"H",1);
                    freeView.AddAtom(hydrogen,token,local,atom.transform.position,i,false);
                    ConsumeLoose(atom); added++;
                }
            }
            freeView.Relax(); freeView.LiftClear(); RefreshStock(); // hydrogens added underneath must not end up inside the bench
            if(shortage) Notify("Added "+added+" H, but the shelf ran out of hydrogen. Recycle a molecule to free more.");
            else Notify(added==0?"No open bonds to fill.":"Added "+added+" hydrogen atom"+(added==1?"":"s")+".",added>0);
            if(added>0) FreeStructureChanged();
        }

        public void UndoFreeAtom()
        {
            if(!FreeEditable()) return;
            var token=freeView.RemoveLastAtom();
            if(token>=0 && Inventory.Transfer(token,freeView.Owner,AtomInventory.Shelf)) SpawnShelfAtom(token);
            RefreshStock(); Notify("Last atom returned to the shelf.");
            FreeStructureChanged();
        }

        public void ClearFreeStructure(bool notify)
        {
            if(freeView==null) return;
            CancelFreeCheck();
            foreach(var token in freeView.Structure.TokenIds.ToArray())
                if(token>=0 && Inventory.Transfer(token,freeView.Owner,AtomInventory.Shelf)) SpawnShelfAtom(token);
            DestroyFreeView(); RefreshStock(); UpdateTray();
            if(notify) Notify("Free structure cleared. Its atoms are back on the shelf.");
        }

        public void SelectFreeBond(int delta)
        {
            if(!FreeEditable()) return;
            if(freeView.Structure.Graph.Bonds.Count==0) { Notify("No bonds yet."); return; }
            freeView.SelectBond(delta); UpdateTray(); Notify("Selected bond: "+freeView.BondText());
        }
        public void SetFreeBondOrder(int order)
        {
            if(!FreeEditable()) return;
            if(!freeView.TrySetOrder(order,out var reason)) { Notify(reason); return; }
            Notify("Bond "+freeView.BondText()+".",true);
            FreeStructureChanged();
        }
        /// <summary>Menu fallback for the push-together gesture: single, double, triple, then back to single.</summary>
        public void CycleFreeBondOrder()
        {
            if(!FreeEditable()) return;
            var bond=freeView.SelectedBond;
            if(bond<0) { Notify("No bond selected."); return; }
            SetFreeBondOrder(freeView.Structure.Graph.Bonds[bond].Order%3+1);
        }
        public void TidyFreeStructure()
        {
            if(!FreeEditable()) return;
            freeView.Relax(); Notify("Shape tidied using ideal bond lengths and angles.");
        }

        /// <summary>Manual re-check (for example after Wi-Fi returns). Normally checks start automatically.</summary>
        public void CheckFreeStructure()
        {
            if(checkingStructure) return;
            if(freeView==null || freeView.Structure.Count==0) { Notify("Build a structure first."); return; }
            if(!freeView.Structure.IsComplete(out var reason)) { Notify(reason); return; }
            if(views.Count>=12) { Notify("The bench is full. Recycle a molecule before checking."); return; }
            freeRetries=0;
            StartCoroutine(CheckFreeStructureRoutine());
        }
        IEnumerator CheckFreeStructureRoutine()
        {
            var session=++lookupSession; var view=freeView;
            var graph=view.Structure.Graph.Clone(); var smiles=SmilesWriter.Write(graph); var version=view.Structure.Version;
            var catalogue=molecules.FirstOrDefault(m=>m!=null && StructureMatcher.Match(graph,MolGraph.From(m))!=null);
            checkingStructure=true; view.SetLocked(true);
            view.SetStatus(graph.Formula()+" complete!\nChecking PubChem...",FreeBuildView.CheckingColor);
            RebuildMenu(); Notify("Complete structure. Checking it with PubChem...");
            EnsureMoleculeLookup();
            MoleculeLookupResult result=null;
            yield return MoleculeLookup.Lookup(smiles,r=>result=r);
            if(session!=lookupSession || view==null || view!=freeView) yield break;
            checkingStructure=false; view.SetLocked(false);
            if(result==null) result=new MoleculeLookupResult{status=LookupStatus.Error,smiles=smiles,message="No lookup service answered."};
            LastLookup=result;
            switch(result.status)
            {
                case LookupStatus.Found:
                {
                    MolGraph conformer=null; int[] map=null;
                    if(!string.IsNullOrEmpty(result.sdf3d))
                        try { conformer=MolfileParser.Parse(result.sdf3d,out var charged); map=charged?null:StructureMatcher.Match(graph,conformer); }
                        catch(FormatException exception) { Debug.LogWarning("PubChem conformer ignored: "+exception.Message); }
                    var usedConformer=false; var definition=catalogue;
                    if(definition==null)
                    {
                        definition=DiscoveredMolecule.Create(graph,conformer,map,result,elements,out usedConformer);
                        if(!definition.IsValid(out var error))
                        {
                            LabVisuals.Discard(definition);
                            view.SetStatus("PubChem: "+result.title+"\nbut the lab cannot model it.",FreeBuildView.ProblemColor);
                            Notify("PubChem knows this as "+result.title+", but the lab could not build its model: "+error);
                            break;
                        }
                        ownedResources.Add(definition); madeDefinitions.Add(definition); // reactions reuse its PubChem name
                    }
                    var title=string.IsNullOrEmpty(result.title)?definition.displayName:result.title;
                    if(!discoveries.Contains(title)) discoveries.Add(title);
                    ConvertFreeStructure(definition);
                    const string galleryNote=" Drop it in the GALLERY hole on the bench to keep it, or the TRASH bin to discard it.";
                    var detail=catalogue!=null?"It matches the lab's "+catalogue.displayName+" recipe, so it works in reactions."
                        :usedConformer?"Now showing PubChem's 3D shape.":"PubChem has no 3D shape for it, so your geometry is kept.";
                    Notify(TakeGoalNote()+"PubChem CID "+result.cid+": "+title+(string.IsNullOrEmpty(result.iupacName) || result.iupacName.Equals(title,StringComparison.OrdinalIgnoreCase)?"":" ("+result.iupacName+")")+", "+(result.molecularFormula??graph.Formula())+". "+detail+galleryNote,true);
                    break;
                }
                case LookupStatus.NotFound:
                    view.SetStatus(graph.Formula()+" is complete, but not in PubChem.\nPull an atom off or push two together to change it.",FreeBuildView.ProblemColor);
                    Notify("Valence rules are satisfied, but PubChem has no compound with this bonding. It may be unstable or simply undocumented.");
                    break;
                case LookupStatus.Rejected:
                    view.SetStatus("PubChem could not read this structure.",FreeBuildView.ProblemColor);
                    Notify(result.message);
                    break;
                default:
                    if(catalogue!=null)
                    {
                        if(!discoveries.Contains(catalogue.displayName)) discoveries.Add(catalogue.displayName);
                        ConvertFreeStructure(catalogue);
                        Notify(TakeGoalNote()+"PubChem is unreachable, but this matches the lab's "+catalogue.displayName+" recipe. Drop it in the GALLERY hole on the bench to keep it.",true);
                    }
                    else if(freeRetries<MaximumFreeRetries)
                    {
                        view.SetStatus(graph.Formula()+" complete.\nPubChem unreachable, retrying...",FreeBuildView.ProblemColor);
                        Notify(result.message+" Retrying automatically.");
                        StartCoroutine(RetryFreeCheck(view,version));
                    }
                    else
                    {
                        view.SetStatus(graph.Formula()+" complete.\nPubChem unreachable: check Wi-Fi, then Check now.",FreeBuildView.ProblemColor);
                        Notify(result.message+" Check the headset's Wi-Fi, then press Check now.");
                    }
                    break;
            }
            UpdateTray(); RebuildMenu();
        }
        IEnumerator RetryFreeCheck(FreeBuildView view,int version)
        {
            yield return new WaitForSecondsRealtime(freeRetrySeconds);
            if(view==null || view!=freeView || checkingStructure || Mode!=LabMode.Free || view.Structure.Version!=version || views.Count>=12) yield break;
            freeRetries++;
            StartCoroutine(CheckFreeStructureRoutine());
        }

        MoleculeView ConvertFreeStructure(MoleculeDefinition definition)
        {
            var source=freeView; var position=source.transform.position;
            var built=source.Structure.Graph;
            var rightFormula=FreeGoal!=null && built.Formula()==freeGoalFormula;
            // A recipe goal also needs the right bonding: ethanol and dimethyl ether share C2H6O.
            var reachedGoal=rightFormula && (freeGoalRecipe==null || StructureMatcher.Match(built,MolGraph.From(freeGoalRecipe))!=null);
            var isomer=rightFormula && !reachedGoal;
            var molecule=Spawn(new MoleculeState(definition,true),position);
            var tokens=source.Structure.TokenIds.ToList();
            for(var i=0;i<definition.atoms.Length;i++)
            {
                var n=tokens.FindIndex(t=>Inventory.Tokens[t].Symbol==definition.atoms[i].element.symbol);
                if(n<0) throw new InvalidOperationException("Atom conservation failure");
                if(!Inventory.Transfer(tokens[n],source.Owner,molecule.InventoryOwner)) throw new InvalidOperationException("Duplicate atom transfer");
                molecule.TokenIds[i]=tokens[n]; tokens.RemoveAt(n);
            }
            if(tokens.Count!=0) throw new InvalidOperationException("Unassigned atoms after identification");
            DestroyFreeView();
            var extent=definition.atoms.Max(a=>a.position.magnitude+a.element.radius*LabVisuals.AtomScale);
            if(extent>.35f) molecule.transform.localScale=Vector3.one*MoleculeScaleTransformer.ClampScale(.35f/extent);
            Select(molecule); audioSource.PlayOneShot(completeTone); Achieve("build"); Completed?.Invoke(molecule); UpdateTray();
            // Said at the start of the identification message that follows.
            if(reachedGoal) { goalNote="Goal reached: "+FreeGoal+"! "; FreeGoal=null; freeGoalRecipe=null; RebuildMenu(); }
            else if(isomer) goalNote="Right formula, but not "+FreeGoal+": this is an isomer (same atoms, different bonding). ";
            return molecule;
        }

        /// <summary>The teal ring that shows where to drop the first atom.</summary>
        void UpdateFreeDropZone()
        {
            var show=(Mode==LabMode.Free || Mode==LabMode.Build && !views.Any(v=>v!=null && !v.State.Complete)) && freeView==null && !reacting && bench!=null;
            if(dropZone==null)
            {
                if(!show) return;
                var go=new GameObject("Free build drop zone"); go.transform.SetParent(transform,false);
                dropZone=go.AddComponent<LineRenderer>(); dropZone.useWorldSpace=false; dropZone.loop=true; dropZone.positionCount=48;
                dropZone.sharedMaterial=LabVisuals.Material(new Color(.2f,.85f,.75f));
                for(var i=0;i<48;i++) { var angle=i*Mathf.PI*2/48; dropZone.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.12f); }
                // A paper build mat taped to the bench: the ring is drawn on it and the instruction written underneath.
                dropLabel=LabelCraft.PaperNote(go.transform,"Build mat",new Vector3(0,-.003f,-.035f),LabelCraft.Flat,new Vector2(.32f,.4f),"",.22f);
                dropLabel.transform.localPosition=new Vector3(0,-.165f,-.0012f); dropLabel.rectTransform.sizeDelta=new Vector2(.3f,.06f);
            }
            if(dropZone.gameObject.activeSelf!=show) dropZone.gameObject.SetActive(show);
            if(!show) return;
            dropZone.transform.position=new Vector3(FreeBuildPosition.x,BenchTop+.004f,FreeBuildPosition.z); // drawn on the tabletop
            var ready=loose.Any(a=>a!=null && a.Grab!=null && a.Grab.isSelected && Vector3.Distance(a.transform.position,FreeBuildPosition)<=freeSeedDistance);
            dropZone.widthMultiplier=ready?.008f:.004f+(ReducedEffects?0:.0015f*Mathf.Sin(Time.time*3));
            dropLabel.text=ready?"Release to start building":"Drop any atom here\nto start building";
        }

        string FreeStatusText()
        {
            if(freeView==null || freeView.Structure.Count==0)
                return "No structure yet.\nTake any atom from a beaker and drop it on the teal ring.";
            var s=freeView.Structure; var open=s.TotalOpenValence;
            var state=freeView.StatusText; var newline=state.IndexOf('\n');
            return (FreeGoal!=null?"Goal: "+FreeGoal+" "+freeGoalFormula+"\n":"")+s.Graph.Formula()+"   "+s.Count+" atoms   "+(open==0?"complete":open+" open bond"+(open==1?"":"s"))+
                "\nSelected bond: "+freeView.BondText()+"\n"+(newline>=0?state.Substring(0,newline):state);
        }
        string FreeDiscoveryText()
        {
            if(discoveries.Count==0) return "PubChem discoveries: none yet.\nNeeds Wi-Fi; answers are cached on the headset.";
            return "PubChem discoveries: "+discoveries.Count+"\n"+string.Join(", ",discoveries.Skip(Mathf.Max(0,discoveries.Count-3)));
        }
        /// <summary>
        /// The one BUILD page. Top row: how much help a goal gives (guide spots / formula only) or no goal at all.
        /// Below: the goals. With guide spots a recipe starts with its seed and teal sites; otherwise the player builds
        /// freely on the teal ring and the structure is checked against the goal when it is complete.
        /// </summary>
        void BuildPage(Transform page)
        {
            Button(page,(GuidedGoals?"> ":"")+"Guide spots",new Vector2(-707,320),new Vector2(245,80),()=>SetGuidedGoals(true),26);
            Button(page,(!GuidedGoals?"> ":"")+"Formula only",new Vector2(-450,320),new Vector2(245,80),()=>SetGuidedGoals(false),26);
            Button(page,(Mode==LabMode.Free && FreeGoal==null?"> ":"")+"No goal",new Vector2(-193,320),new Vector2(245,80),BuildAnything,26);
            var list=Goals; var pages=(list.Count+3)/4; goalPage=Mathf.Clamp(goalPage,0,pages-1);
            for(var i=0;i<4;i++)
            {
                var index=goalPage*4+i; if(index>=list.Count) break; var goal=list[index];
                var current=Mode==LabMode.Free?FreeGoal==goal.Name:Selected!=null && !Selected.State.Complete && Selected.Definition==goal.Recipe;
                var note=GuidedGoals && goal.Recipe==null?"  (formula only)":"";
                Button(page,(current?"> ":"")+goal.Name+"  "+goal.Formula+note,new Vector2(-450,210-i*105),new Vector2(760,80),()=>ChooseGoal(goal));
            }
            Button(page,"< Previous",new Vector2(-650,-210),new Vector2(350,80),()=>{goalPage=Mathf.Max(0,goalPage-1);RebuildMenu();});
            Button(page,"Next >",new Vector2(-250,-210),new Vector2(350,80),()=>{goalPage=Mathf.Min(pages-1,goalPage+1);RebuildMenu();});
            lesson.text=!hints?"Hints hidden. Placement feedback stays enabled.":Mode==LabMode.Free?FreeInstructions:instructions[lessonStep];
            energyText.text=$"Goals: page {goalPage+1}/{pages}\n"+(Mode==LabMode.Free?(FreeGoal!=null?"Goal: "+FreeGoal+" "+freeGoalFormula:"No goal: anything you finish gets named."):"Or drop any atom on the teal ring to build freely.");
            ShortcutRow(page);
        }
        void SetGuidedGoals(bool guided)
        {
            GuidedGoals=guided; RebuildMenu();
            Notify(guided?"Goals show guide spots for every atom.":"Goals show only the formula: work out the structure yourself.");
        }
        /// <summary>Picks a goal: a guided recipe (seed and guide spots), or a formula to build freely on the teal ring.</summary>
        public void ChooseGoal(BuildGoal goal)
        {
            if(goal==null || reacting) return;
            if(GuidedGoals && goal.Recipe!=null) { FreeGoal=null; freeGoalRecipe=null; StartMolecule(goal.Recipe); return; }
            if(Mode!=LabMode.Free) SetMode(LabMode.Free);
            FreeGoal=goal.Name; freeGoalFormula=goal.Hill; freeGoalRecipe=goal.Recipe; RebuildMenu();
            Notify("Goal: "+goal.Name+" ("+goal.Formula+"). No guide spots: drop an atom on the teal ring and work out the bonds yourself.");
        }
        /// <summary>No goal: build anything; the finished structure is named (recipe match or PubChem).</summary>
        public void BuildAnything()
        {
            if(reacting) return;
            FreeGoal=null; freeGoalRecipe=null;
            if(Mode!=LabMode.Free) SetMode(LabMode.Free);
            RebuildMenu(); Notify("No goal: build anything. Drop an atom on the teal ring; finished structures are named automatically.");
        }
        /// <summary>The same three large shortcuts in BUILD and FREE BUILD, in the same place.</summary>
        void ShortcutRow(Transform page)
        {
            Button(page,"Fill hydrogens",new Vector2(-575,-360),new Vector2(540,130),FillHydrogensAnyMode,34);
            Button(page,"Undo last atom",new Vector2(0,-360),new Vector2(540,130),UndoAnyMode,34);
            Button(page,"Clear structure",new Vector2(575,-360),new Vector2(540,130),ClearAnyMode,34);
        }
        public void FillHydrogensAnyMode() { if(Mode==LabMode.Free) FillHydrogens(); else FillRecipeHydrogens(); }
        public void UndoAnyMode() { if(Mode==LabMode.Free) UndoFreeAtom(); else UndoRecipeAtom(); }
        public void ClearAnyMode()
        {
            if(Mode==LabMode.Free) { ClearFreeStructure(true); return; }
            var unfinished=Selected!=null && !Selected.State.Complete?Selected:views.LastOrDefault(v=>!v.State.Complete);
            if(unfinished==null) { Notify("Nothing to clear: finished molecules go in the GALLERY hole or the TRASH bin."); return; }
            CancelInteractions(); Recycle(unfinished); UpdateTray(); Notify("Structure cleared.");
        }
        /// <summary>
        /// BUILD: puts a hydrogen on every open hydrogen site of the recipe being built (repeats while new sites open up).
        /// Atoms are placed on their sites directly (no hand-placement snap test), and a molecule resting on the bench is first
        /// lifted so the new hydrogens underneath do not appear inside the bench.
        /// </summary>
        public void FillRecipeHydrogens()
        {
            var view=Selected!=null && !Selected.State.Complete?Selected:views.LastOrDefault(v=>v!=null && !v.State.Complete);
            if(view==null) { Notify("Choose a goal first, or start building on the teal ring; Fill hydrogens completes the hydrogens."); return; }
            if(reacting) { Notify("Wait for the reaction to finish."); return; }
            if(view.PlacementLocked) { Notify("One moment: the first atom is still landing."); return; }
            bool Open(int site) => view.Definition.atoms[site].element!=null && view.Definition.atoms[site].element.symbol=="H" && view.State.CanPlace(site,view.Definition.atoms[site].element,out _);
            var sites=Enumerable.Range(0,view.Definition.atoms.Length).ToArray();
            if(!sites.Any(Open)) { Notify("No hydrogen fits yet: place the heavier atoms first."); return; }
            if(!view.Grab.isSelected)
            {
                // Every site that will be filled must end up above whatever the molecule is resting on.
                var support=SupportHeight(sites.Where(view.State.IsOccupied).Select(view.RestPosition),view.transform);
                if(support.HasValue) view.Lift(support.Value+.005f-sites.Min(i=>view.RestPosition(i).y-view.SiteRadius(i)));
            }
            var added=0; var shortage=false;
            for(var pass=0;pass<8 && !shortage;pass++)
            {
                var progress=false;
                foreach(var site in sites)
                {
                    if(!Open(site)) continue;
                    var atom=loose.FirstOrDefault(a=>a!=null && a.OnShelf && a.Element!=null && a.Element.symbol=="H");
                    if(atom==null) { shortage=true; break; }
                    if(PlaceDirectly(view,site,atom)) { added++; progress=true; }
                }
                if(!progress || view==null || view.State.Complete) break;
            }
            if(view!=null && !view.State.Complete) Notify(shortage?"Added "+added+" H, but the beakers ran out of hydrogen.":added+" hydrogen"+(added==1?"":"s")+" added. Place the next heavy atom, then fill again.",added>0);
            else if(added>0 && Mode!=LabMode.Free) audioSource.PlayOneShot(successTone);
        }
        bool PlaceDirectly(MoleculeView view,int site,LooseAtom atom)
        {
            if(!atom.TakeFromShelf()) return false;
            if(!Inventory.Transfer(atom.TokenId,atom.Owner,view.InventoryOwner)) { atom.ReturnHome(); return false; }
            view.TokenIds[site]=atom.TokenId;
            if(!view.State.TryPlace(site,atom.Element,out var reason)) throw new InvalidOperationException(reason);
            var from=atom.transform.position; ClearPreview(atom); loose.Remove(atom); atom.gameObject.SetActive(false); LabVisuals.Discard(atom.gameObject);
            if(view!=null) { view.Refresh(); view.AnimateSite(site,from); }
            RefreshStock(); return true;
        }
        static readonly RaycastHit[] supportHits=new RaycastHit[16];
        /// <summary>Height of the surface under these points (the bench, a table...), ignoring molecules and atoms; null if nothing is below.</summary>
        static float? SupportHeight(IEnumerable<Vector3> points,Transform self)
        {
            float? best=null;
            foreach(var point in points)
            {
                var count=Physics.RaycastNonAlloc(point,Vector3.down,supportHits,1.2f,~0,QueryTriggerInteraction.Ignore);
                for(var k=0;k<count;k++)
                {
                    var t=supportHits[k].collider!=null?supportHits[k].collider.transform:null;
                    if(t==null || t.IsChildOf(self) || t.GetComponentInParent<LooseAtom>()!=null || t.GetComponentInParent<MoleculeView>()!=null || t.GetComponentInParent<FreeAtomHandle>()!=null) continue;
                    if(best==null || supportHits[k].point.y>best.Value) best=supportHits[k].point.y;
                }
            }
            return best;
        }
        /// <summary>A beaker atom being held passes through structures that are being built, so it can be brought right up to them without shoving them about.</summary>
        public void IgnoreBuildCollisions(Collider own)
        {
            if(own==null || !own.enabled) return;
            foreach(var view in views)
                if(view!=null && !view.State.Complete)
                    foreach(var c in view.GetComponentsInChildren<Collider>()) if(c.enabled) Physics.IgnoreCollision(own,c);
            if(freeView!=null) foreach(var c in freeView.GetComponentsInChildren<Collider>()) if(c.enabled) Physics.IgnoreCollision(own,c);
        }
        /// <summary>BUILD: takes the most recently placed atom off the recipe (the seed atom stays).</summary>
        public void UndoRecipeAtom()
        {
            var view=Selected!=null && !Selected.State.Complete?Selected:views.LastOrDefault(v=>!v.State.Complete);
            if(view==null) { Notify("Nothing to undo."); return; }
            if(!view.TryRemoveLastAtom(out var token)) { Notify("Only the starting atom is left; use Clear structure to remove it."); return; }
            if(token>=0 && Inventory.Transfer(token,view.InventoryOwner,AtomInventory.Shelf)) SpawnShelfAtom(token);
            RefreshStock(); Notify("Last atom removed.");
        }
    }
}
