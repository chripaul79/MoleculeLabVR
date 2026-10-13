using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Meta;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace MoleculeLab
{
    public enum LabMode { Build, Inspect, React }
    public sealed partial class LabController : MonoBehaviour
    {
        public ElementDefinition[] elements;
        public MoleculeDefinition[] molecules;
        public MoleculeDefinition methylRadical;
        public ReactionDefinition waterReaction;
        public ReactionDefinition[] reactions;
        public ReactionDefinition ActiveReaction { get; private set; }
        public ReactionCondition SelectedCondition { get; private set; }
        public bool IsReacting => reacting;
        public TMP_FontAsset font;
        public LabMode Mode { get; private set; }
        public bool Resetting { get; private set; }
        public Vector3 BuildPosition => bench.position + new Vector3(-.12f,.46f,-.15f);
        public LabEnvironment Environment { get; private set; }
        public AtomInventory Inventory { get; private set; }
        [Range(.08f,.3f)] public float previewDistance=.18f;
        [Range(.02f,.12f)] public float snapDistance=.05f;
        [Range(5,45)] public float snapAngle=15;
        [Min(0)] public float seedTransferDuration=.33f;
        public bool ReducedEffects { get; private set; }
        bool muted, hints=true;
        BondPreview preview;
        AudioClip readyTone, completeTone;
        AudioSource ambience;
        public MoleculeView Selected { get; private set; }
        public event Action<MoleculeView> Completed;
        public event Action ReactionCompleted;
        public event Action BondChanged;
        readonly List<MoleculeView> views = new List<MoleculeView>();
        readonly List<LooseAtom> loose = new List<LooseAtom>();
        readonly HashSet<string> achievements = new HashSet<string>();
        readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        Transform bench, dynamicRoot, tray;
        TextMeshProUGUI status, lesson, modeText, trayText, energyText;
        AudioSource audioSource;
        AudioClip successTone;
        InspectionHandle twistHandle, bendHandle;
        EnergyTool energyTool;
        bool reacting;
        float height = .82f;
        int lastEnergyPercent = -1;
        int lessonStep;
        static readonly string[] lessonKeys = { "build", "inspect", "scale", "twist", "bend", "break", "rejoin", "react" };
        static readonly string[] instructions = {
            "01 / PRACTICE\nReach or point and pinch an atom; Return atoms restores it. Point, pinch and release to click the menu. Choose Water: its seed appears automatically. Add H at each guide.",
            "02 / INSPECT\nSwitch to Inspect and select a finished molecule to see its geometry.",
            "03 / SCALE\nHold a finished molecule with both hands; spread or close your hands.",
            "04 / TWIST\nBuild ethane, select Inspect, then grip the gold handle and turn your wrist.",
            "05 / DEFORM\nGrip the blue handle and move it up/down. Release to restore geometry.",
            "06 / BREAK\nHold the energy wand tip at ethane's central bond for two seconds.",
            "07 / REJOIN\nMove the two CH3 radicals together and release to restore ethane.",
            "08 / REACT\nChoose a reaction. Build the listed inputs and release them into the tray. Select its conditions and activate.",
            "LESSON COMPLETE\nBuild, inspect and react again. Reset bench starts a fresh session." };

        void Start()
        {
            Application.targetFrameRate = 72;
            if (font != null) TMP_Settings.defaultFontAsset = font;
            BuildEnvironment();
            CreateUI();
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0; audioSource.volume = .16f;
            successTone = AudioClip.Create("Bond confirmation", 4800, 1, 24000, false);
            var samples = new float[4800];
            for (var i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i*2*Mathf.PI*660/24000) * (1f-i/(float)samples.Length) * .3f;
            successTone.SetData(samples,0); ownedResources.Add(successTone);
            readyTone=Tone("Ready",880,.06f); completeTone=Tone("Molecule complete",990,.35f);
            ambience=gameObject.AddComponent<AudioSource>(); ambience.loop=true; ambience.volume=.018f;
            var air=AudioClip.Create("Quiet ventilation",24000,1,24000,false); var noise=new float[24000]; var random=new System.Random(17); float smooth=0;
            for(var i=0;i<noise.Length;i++) { smooth=Mathf.Lerp(smooth,(float)random.NextDouble()*2-1,.08f); noise[i]=smooth; }
            air.SetData(noise,0); ownedResources.Add(air); ambience.clip=air; ambience.Play();
            ResetLab();
            StartCoroutine(RequestRefreshRate());
        }
        AudioClip Tone(string name,float hz,float seconds)
        {
            var clip=AudioClip.Create(name,(int)(24000*seconds),1,24000,false); var data=new float[clip.samples];
            for(var i=0;i<data.Length;i++) data[i]=Mathf.Sin(i*2*Mathf.PI*hz/24000)*(1-i/(float)data.Length)*.25f;
            clip.SetData(data,0); ownedResources.Add(clip); return clip;
        }
        public void ReadyCue() { audioSource.PlayOneShot(readyTone); }
        void ToggleSound() { muted=!muted; audioSource.mute=muted; ambience.mute=muted; Notify(muted?"Sound muted.":"Sound enabled."); }
        IEnumerator RequestRefreshRate()
        {
            for (var i=0; i<20; i++)
            {
                var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
                if (displays.Any(d=>d.running)) { XRDisplaySubsystem display = displays.First(d=>d.running); display.TryRequestDisplayRefreshRate(72); yield break; }
                yield return new WaitForSeconds(1);
            }
        }
        void BuildEnvironment()
        {
            Environment=FindAnyObjectByType<LabEnvironment>();
            if(Environment==null) throw new InvalidOperationException("Install the academic lab environment using Molecule Lab > Install academic environment.");
            bench=Environment.bench; tray=Environment.tray; height=Environment.workstation.position.y;
            preview=new GameObject("Bond placement feedback").AddComponent<BondPreview>(); preview.Initialize(this);
            dynamicRoot = new GameObject("Session objects").transform; dynamicRoot.SetParent(transform);
            twistHandle = new GameObject("Twist handle").AddComponent<InspectionHandle>(); twistHandle.Initialize(this,false);
            bendHandle = new GameObject("Deformation handle").AddComponent<InspectionHandle>(); bendHandle.Initialize(this,true);
            energyTool = new GameObject("Energy wand").AddComponent<EnergyTool>(); energyTool.Initialize(this);
        }

        public void Notify(string message,bool success=false)
        {
            if (status != null) status.text=message;
            if(success && audioSource != null) audioSource.PlayOneShot(successTone);
        }
        public void Achieve(string key)
        {
            if(!achievements.Add(key)) return;
            while(lessonStep<lessonKeys.Length && achievements.Contains(lessonKeys[lessonStep])) lessonStep++;
            if(lesson!=null && hints) lesson.text=instructions[lessonStep];
        }
        public void SetMode(LabMode mode)
        {
            if(reacting) { Notify("Wait for the reaction to finish, or reset the bench."); return; }
            CancelInteractions(); Mode=mode;
            modeText.text=mode.ToString().ToUpperInvariant()+" MODE"; settingsPage=false; RebuildMenu();
            if(Selected!=null) { Selected.ShowGeometry(mode==LabMode.Inspect); Selected.Bend(0); }
            if(mode==LabMode.Inspect && Selected!=null && Selected.State.Complete) Achieve("inspect");
            Notify(mode==LabMode.Build?"Choose a molecule. Direction guides show valid bonding sites.":mode==LabMode.Inspect?"Grab a molecule to inspect. Ethane supports twisting and the energy wand.":"Choose a reaction, build its inputs, dock them, select conditions, then activate.");
        }
        public void Select(MoleculeView view)
        {
            if(Selected!=null) Selected.ShowGeometry(false);
            Selected=view; view.ShowGeometry(Mode==LabMode.Inspect); if(Mode==LabMode.Inspect) RebuildMenu();
            UpdateTray();
            if(Mode==LabMode.Inspect) Achieve("inspect");
        }
        public void StartMolecule(MoleculeDefinition definition)
        {
            if(reacting) return;
            SetMode(LabMode.Build);
            foreach(var v in views.Where(v=>!v.State.Complete).ToArray()) Recycle(v);
            if(views.Count>=12) { Notify("Recycle a molecule to make space on the bench."); return; }
            var seed=Inventory.FirstAvailable(definition.atoms[definition.seedIndex].element.symbol);
            if(seed<0) { Notify("No seed atom available. Return loose atoms or recycle a molecule."); return; }
            if(views.Any(v=>Vector3.Distance(v.transform.position,BuildPosition)<.25f)) ReturnMolecules();
            var atom=loose.First(a=>a.TokenId==seed); var start=atom.transform.position;
            Selected=Spawn(new MoleculeState(definition),BuildPosition);
            Inventory.Transfer(seed,AtomInventory.Shelf,Selected.InventoryOwner); Selected.TokenIds[definition.seedIndex]=seed;
            loose.Remove(atom); atom.gameObject.SetActive(false); Destroy(atom.gameObject);
            StartCoroutine(SeedTransfer(Selected,start)); RefreshStock();
            Notify("Build "+definition.displayName+". Electrical guides are teaching cues. Release when the ring is ready.");
        }
        IEnumerator SeedTransfer(MoleculeView view,Vector3 start)
        {
            view.PlacementLocked=true; var end=BuildPosition; view.transform.position=start;
            for(float t=0;seedTransferDuration>0 && t<1;t+=Time.deltaTime/seedTransferDuration) { if(view==null) yield break; view.transform.position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,t)); yield return null; }
            if(view!=null) { view.transform.position=end; view.PlacementLocked=false; }
        }
        MoleculeView Spawn(MoleculeState state,Vector3 position)
        {
            var go=new GameObject(state.Definition.displayName); go.transform.SetParent(dynamicRoot); go.transform.position=position;
            var view=go.AddComponent<MoleculeView>(); view.Initialize(this,state); views.Add(view); return view;
        }
        void SpawnShelfAtom(int id)
        {
            var element=elements.First(e=>e.symbol==Inventory.Tokens[id].Symbol);
            var go=LabVisuals.Shape(element.symbol+" atom "+id,PrimitiveType.Sphere,dynamicRoot,Vector3.zero,Vector3.one*element.radius*2,element.color);
            go.transform.position=Environment.SocketPosition(id); var atom=go.AddComponent<LooseAtom>(); atom.Initialize(this,element,id); loose.Add(atom);
        }
        public void RefreshStock() { if(Inventory!=null) Environment.UpdateCounts(Inventory); }
        public void AtomReleased(LooseAtom atom)
        {
            ClearPreview(atom);
            if(Vector3.Distance(atom.transform.position,Environment.SocketPosition(atom.TokenId))<.18f) atom.ReturnHome();
        }
        public void ClearPreview(LooseAtom atom=null) { if(preview!=null) preview.Clear(atom); }
        public void PreviewAtom(LooseAtom atom) { if(preview!=null) preview.Show(atom); }
        public bool FindPlacement(LooseAtom atom,MoleculeView preferred,int preferredSite,out MoleculeView view,out int site,out PlacementResult result)
        {
            view=null; site=-1; result=default;
            if(Mode!=LabMode.Build || atom==null) return false;
            if(preferred!=null && !preferred.PlacementLocked && preferred.State.CanPlace(preferredSite,atom.Element,out _))
            {
                var test=EvaluatePlacement(preferred,preferredSite,atom);
                if(test.Preview) { view=preferred; site=preferredSite; result=test; return true; }
            }
            var closest=float.MaxValue;
            foreach(var candidate in views.Where(v=>!v.State.Complete && !v.PlacementLocked))
                for(var i=0;i<candidate.Definition.atoms.Length;i++)
                {
                    var test=EvaluatePlacement(candidate,i,atom);
                    if(test.Preview && test.Distance<closest) { closest=test.Distance; view=candidate; site=i; result=test; }
                }
            return view!=null;
        }
        public PlacementResult EvaluatePlacement(MoleculeView view,int site,LooseAtom atom)
        {
            var valid=view.State.CanPlace(site,atom.Element,out _); var anchor=view.AnchorFor(site);
            return BondPlacement.Evaluate(valid && anchor>=0,anchor<0?view.transform.position:view.SitePosition(anchor),view.SitePosition(site),atom.transform.position,previewDistance,snapDistance,snapAngle);
        }
        public bool TryAttach(LooseAtom atom)
        {
            if(atom==null || !loose.Contains(atom) || atom.Grab.isSelected) return false;
            var preferred=preview!=null && preview.Atom==atom?preview.Target:null; var preferredSite=preview!=null?preview.Site:-1;
            if(!FindPlacement(atom,preferred,preferredSite,out var candidate,out var site,out var result) || !result.Ready)
            { ClearPreview(atom); Notify("Match the element and move into the guide until it says Release to bond."); return false; }
            if(!atom.TakeFromShelf() || !Inventory.Transfer(atom.TokenId,atom.Owner,candidate.InventoryOwner)) return false;
            candidate.TokenIds[site]=atom.TokenId;
            if(!candidate.State.TryPlace(site,atom.Element,out var reason)) throw new InvalidOperationException(reason);
            var from=atom.transform.position; loose.Remove(atom); atom.gameObject.SetActive(false); Destroy(atom.gameObject); candidate.Refresh();
            candidate.AnimateSite(site,from); ClearPreview(atom); RefreshStock();
            if(!candidate.State.Complete) Notify("Bond formed. Follow the next guide.",true);
            return true;
        }
        public void ReturnAtoms() { if(reacting) return; CancelInteractions(); foreach(var atom in loose) atom.ReturnHome(); Notify("Loose atoms returned to their own sockets."); }
        public void RecycleSelected()
        {
            if(reacting || Selected==null) { Notify("Select a molecule to recycle."); return; }
            CancelInteractions(); Recycle(Selected); UpdateTray(); Notify("Atoms returned to the shelf.");
        }
        void Recycle(MoleculeView view)
        {
            // Recycling either radical also recycles its partner, preventing stale rejoin references.
            var group=view.FragmentPair==null?new[]{view}:views.Where(v=>v.FragmentPair==view.FragmentPair).ToArray();
            foreach(var item in group)
            {
                foreach(var id in item.TokenIds.Where(id=>id>=0)) { if(Inventory.Transfer(id,item.InventoryOwner,AtomInventory.Shelf)) SpawnShelfAtom(id); }
                Remove(item);
            }
            RefreshStock();
        }
        void TransferTokens(MoleculeView[] sources,MoleculeView[] targets)
        {
            var ids=sources.SelectMany(v=>v.TokenIds.Where(id=>id>=0).Select(id=>(id,v.InventoryOwner))).ToList();
            foreach(var target in targets) for(var i=0;i<target.TokenIds.Length;i++)
            {
                if(!target.State.IsOccupied(i)) continue;
                var n=ids.FindIndex(t=>Inventory.Tokens[t.id].Symbol==target.Definition.atoms[i].element.symbol);
                if(n<0) throw new InvalidOperationException("Atom conservation failure");
                var token=ids[n]; ids.RemoveAt(n);
                if(!Inventory.Transfer(token.id,token.InventoryOwner,target.InventoryOwner)) throw new InvalidOperationException("Duplicate atom transfer");
                target.TokenIds[i]=token.id;
            }
            if(ids.Count!=0) throw new InvalidOperationException("Unassigned atoms after transformation");
            foreach(var source in sources) Array.Fill(source.TokenIds,-1);
        }
        public void MoleculeCompleted(MoleculeView view)
        {
            Select(view); Notify(view.Definition.displayName+" complete. Grab it with one or two hands."); audioSource.PlayOneShot(completeTone); Achieve("build"); Completed?.Invoke(view);
        }
        public void MoleculeReleased(MoleculeView view)
        {
            if(view.FragmentPair!=null)
            {
                var other=views.FirstOrDefault(v=>v!=view && v.FragmentPair==view.FragmentPair && !v.Grab.isSelected && Vector3.Distance(v.transform.position,view.transform.position)<.22f);
                if(other!=null && view.ParentState.TryRejoin(view.ParentState.Definition.rotatableBond))
                {
                    var state=view.ParentState; var position=(view.transform.position+other.transform.position)*.5f;
                    var scale=view.transform.localScale; var rotation=view.transform.rotation;
                    Selected=Spawn(state,position); TransferTokens(new[]{view,other},new[]{Selected}); Remove(view); Remove(other); Selected.transform.localScale=scale; Selected.transform.rotation=rotation;
                    Achieve("rejoin"); BondChanged?.Invoke(); Notify("Ethane restored: the C-C bond is rejoined.",true); return;
                }
            }
            if(reacting || ActiveReaction==null || !view.State.Complete || view.Grab.isSelected) return;
            var delta=view.transform.position-tray.position;
            if(Mathf.Abs(delta.x)>.34f || Mathf.Abs(delta.z)>.35f || Mathf.Abs(delta.y)>.5f) return;
            var requirement=ActiveReaction.inputs.FirstOrDefault(p=>p.molecule==view.Definition);
            if(requirement.molecule==null) { Notify("This reaction does not use "+view.Definition.displayName+". Check its input list."); return; }
            var docked=views.Where(v=>v.Docked && v!=view).ToArray();
            if(docked.Count(v=>v.Definition==view.Definition)>=requirement.count) { Notify("Enough "+view.Definition.formula+" is already docked."); return; }
            var count=ActiveReaction.inputs.Sum(p=>p.count);
            var slot=Enumerable.Range(0,count).First(i=>!docked.Any(v=>Vector3.Distance(v.transform.position,TraySlot(i))<.03f));
            view.transform.localScale=Vector3.one*.4f; view.transform.position=TraySlot(slot); view.Docked=true; UpdateTray();
            Notify("Docked "+view.Definition.formula+". Follow the reaction checklist.");
        }
        Vector3 TraySlot(int index) => tray.position+new Vector3(0,.15f,-.19f+index*.19f);
        public void SelectReaction(ReactionDefinition reaction)
        {
            if(reacting || reaction==null || reactions==null || !reactions.Contains(reaction)) return;
            ReturnMolecules(); ActiveReaction=reaction; SelectedCondition=ReactionCondition.None;
            UpdateTray(); RebuildMenu(); Notify(reaction.explanation);
        }
        public void SelectCondition(ReactionCondition condition)
        {
            if(reacting) return;
            SelectedCondition=condition; UpdateTray(); Notify("Selected condition: "+ConditionName(condition));
        }
        public bool CanActivate(out string reason)
        {
            if(reacting) { reason="Reaction is already running."; return false; }
            if(ActiveReaction==null) { reason="Choose a reaction first."; return false; }
            return ReactionEngine.CanReact(ActiveReaction,views.Where(v=>v.Docked && !v.Grab.isSelected).Select(v=>v.State).ToArray(),out reason,SelectedCondition);
        }
        void UpdateTray()
        {
            if(trayText==null) return;
            if(ActiveReaction==null) { trayText.text="REACT: choose a reaction to see its input checklist."; return; }
            var docked=views.Where(v=>v.Docked && !v.Grab.isSelected).ToArray();
            trayText.text=ActiveReaction.equation+"\n"+string.Join("   ",ActiveReaction.inputs.Select(p=>$"{docked.Count(v=>v.Definition==p.molecule)}/{p.count} {p.molecule.formula}"))+"\nCondition: "+ConditionName(SelectedCondition);
            if(activateButton!=null) activateButton.interactable=CanActivate(out _);
        }
        public void ActivateReaction()
        {
            if(Mode!=LabMode.React || reacting) { Notify("Switch to React mode to activate the tray."); return; }
            var input=views.Where(v=>v.Docked && !v.Grab.isSelected).ToArray();
            if(ActiveReaction==null) { Notify("Choose a reaction first."); return; }
            if(!ReactionEngine.TryReact(ActiveReaction,input.Select(v=>v.State).ToArray(),out var products,out var reason,SelectedCondition)) { Notify(reason); return; }
            reacting=true;
            foreach(var v in input) { v.CancelSelection(); v.Grab.enabled=false; }
            UpdateTray(); StartCoroutine(AnimateReaction(input,products));
        }
        IEnumerator AnimateReaction(MoleculeView[] input,MoleculeState[] products)
        {
            Notify("Activation supplied. Atoms rearrange; atom counts stay constant.");
            var starts=input.Select(v=>v.transform.position).ToArray();
            for(float t=0;t<1;t+=Time.deltaTime*.6f)
            {
                for(var i=0;i<input.Length;i++) { input[i].transform.position=Vector3.Lerp(starts[i],tray.position+Vector3.up*.25f,t); input[i].transform.Rotate(0,Time.deltaTime*90,0); }
                yield return null;
            }
            var outputs=new List<MoleculeView>();
            for(var i=0;i<products.Length;i++) { Selected=Spawn(products[i],bench.position+new Vector3(-.4f+i*.38f,.35f,.08f)); outputs.Add(Selected); }
            TransferTokens(input,outputs.ToArray()); foreach(var v in input) Remove(v);
            reacting=false; UpdateTray(); Achieve("react"); ReactionCompleted?.Invoke(); Notify(string.Join(" + ",ActiveReaction.outputs.Select(p=>p.count+" "+p.molecule.formula))+" formed. Products can be inspected or reused. "+(ActiveReaction.reversible?"Forward illustration only; not an equilibrium yield.":""),true);
        }
        void Remove(MoleculeView view)
        {
            view.CancelSelection(); views.Remove(view); if(Selected==view) Selected=null; view.gameObject.SetActive(false); Destroy(view.gameObject);
        }
        public void BreakSelected()
        {
            var original=Selected;
            if(Mode!=LabMode.Inspect || original==null || original.Definition.id!="ethane" || original.Grab.isSelected) return;
            if(views.Count>=12) { Notify("The bench is full. Reset before creating more fragments."); return; }
            if(!original.State.TryBreak(original.Definition.rotatableBond)) return;
            var id=Guid.NewGuid().ToString(); var position=original.transform.position; var state=original.State;
            var scale=original.transform.localScale; var rotation=original.transform.rotation; var fragments=new List<MoleculeView>();
            for(var i=0;i<2;i++)
            {
                var fragment=Spawn(new MoleculeState(methylRadical,true),position+rotation*Vector3.right*(i==0?-.15f:.15f)*scale.x);
                fragment.transform.localScale=scale; fragment.transform.rotation=rotation;
                fragment.FragmentPair=id; fragment.ParentState=state; fragments.Add(fragment);
            }
            TransferTokens(new[]{original},fragments.ToArray()); Remove(original); Selected=fragments[0];
            Achieve("break"); BondChanged?.Invoke(); Notify("Two CH3 radicals. Bring them together and release to rejoin. Energy is illustrative.",true);
        }
        void TwistStep()
        {
            if(Mode!=LabMode.Inspect || Selected==null || Selected.Definition.rotatableBond<0) { Notify("Inspect a completed ethane molecule first."); return; }
            Selected.Twist(Selected.Torsion+30); Achieve("twist");
        }
        public void EnergyProgress(float fraction)
        {
            var percent=Mathf.RoundToInt(fraction*100);
            if(percent==lastEnergyPercent) return;
            lastEnergyPercent=percent; energyText.text="Energy: "+percent+"%\nIllustrative threshold";
        }
        public Vector3 WandHome => bench.position+new Vector3(-.75f,.15f,-.15f);
        void AdjustHeight(float change)
        {
            if(reacting || views.Any(v=>v.PlacementLocked)) { Notify("Wait for the animation to finish before moving the workstation."); return; }
            CancelInteractions(); var next=Mathf.Clamp(height+change,.6f,1.05f); var delta=Vector3.up*(next-height); height=next;
            Environment.workstation.position+=delta; foreach(var view in views) view.transform.position+=delta; foreach(var atom in loose) if(atom!=null) atom.transform.position+=delta;
            energyTool.ReturnHome();
        }
        public void ReturnMolecules()
        {
            if(reacting) return; CancelInteractions();
            for(var i=0;i<views.Count;i++)
            {
                if(!views[i].State.Complete || views[i].PlacementLocked) continue;
                views[i].Docked=false; views[i].transform.localScale=Vector3.one*.65f;
                views[i].transform.position=bench.position+new Vector3(-.4f+(i%4)*.28f,.27f+(i/4)*.24f,.18f);
            }
            UpdateTray(); Notify("Molecules returned to the bench.");
        }
        void CancelInteractions()
        {
            ClearPreview();
            foreach(var view in views) if(view!=null) view.CancelSelection();
            foreach(var atom in loose) if(atom!=null && atom.Grab!=null && atom.Grab.interactionManager!=null)
                foreach(var interactor in atom.Grab.interactorsSelecting.ToArray()) atom.Grab.interactionManager.SelectCancel(interactor,atom.Grab);
            if(twistHandle!=null) twistHandle.Cancel(); if(bendHandle!=null) bendHandle.Cancel(); if(energyTool!=null) energyTool.Cancel();
        }
        public void ResetLab()
        {
            Resetting=true; StopAllCoroutines(); CancelInteractions(); reacting=false;
            foreach(Transform child in dynamicRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            views.Clear(); loose.Clear(); Selected=null; achievements.Clear(); lessonStep=0; lesson.text=instructions[0];
            Inventory=new AtomInventory(); Resetting=false; for(var id=0;id<Inventory.Tokens.Count;id++) SpawnShelfAtom(id); RefreshStock();
            ActiveReaction=waterReaction; SelectedCondition=ReactionCondition.None;
            energyTool.ReturnHome(); EnergyProgress(0); UpdateTray(); Mode=LabMode.Build; settingsPage=false; moleculePage=0; modeText.text="BUILD MODE"; RebuildMenu(); Notify("Choose a recipe. All 48 atoms are available on the shelf.");
        }
        void Update()
        {
            if(bench==null) return;
            if(Selected!=null && Selected.Grab.isSelected && Selected.Grab.interactorsSelecting.Count>=2 && Mathf.Abs(Selected.transform.localScale.x-1)>.1f) Achieve("scale");
        }
        void OnApplicationPause(bool paused) { if(paused) CancelInteractions(); }
        void OnApplicationFocus(bool focused) { if(!focused) CancelInteractions(); }
        void OnDestroy()
        {
            if(preview!=null) Destroy(preview.gameObject);
            foreach(var resource in ownedResources) if(resource!=null) Destroy(resource);
            if(twistHandle!=null) Destroy(twistHandle.gameObject); if(bendHandle!=null) Destroy(bendHandle.gameObject); if(energyTool!=null) Destroy(energyTool.gameObject);
        }
    }
}
