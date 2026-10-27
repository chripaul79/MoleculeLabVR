using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
namespace MoleculeLab
{
    public sealed partial class LabController
    {
        RectTransform menuRoot;
        GameObject pageObject;
        bool settingsPage;
        int moleculePage;
        Button activateButton;
        float reachOffset;
        public static string ConditionName(ReactionCondition c) => c==ReactionCondition.Ignition?"Ignition":c==ReactionCondition.PalladiumCatalyst?"Pd catalyst":c==ReactionCondition.AcidCatalyst?"Acid catalyst":"Not selected";
        RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); r.gameObject.layer=HandMenuRay.MenuLayer;
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f); r.anchoredPosition=position; r.sizeDelta=size; return r;
        }
        TextMeshProUGUI Text(Transform parent,string value,Vector2 p,Vector2 size,float fontSize,Color color)
        {
            var t=Rect(value,parent,p,size).gameObject.AddComponent<TextMeshProUGUI>(); t.text=value; if(font!=null)t.font=font;
            t.fontSize=fontSize; t.color=color; t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false; return t;
        }
        Button Button(Transform parent,string title,Vector2 p,Vector2 size,Action action)
        {
            var r=Rect(title,parent,p,size); var image=r.gameObject.AddComponent<Image>(); image.color=new Color(.12f,.24f,.3f);
            var button=r.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(.5f,1,.85f); colors.pressedColor=new Color(.2f,.7f,.65f); button.colors=colors;
            button.navigation=new Navigation { mode=Navigation.Mode.None };
            button.onClick.AddListener(()=>action()); Text(r,title,Vector2.zero,size-new Vector2(18,8),28,Color.white); return button;
        }
        void CreateUI()
        {
            var go=new GameObject("Lab control panel",typeof(RectTransform),typeof(Canvas),typeof(TrackedDeviceGraphicRaycaster));
            go.layer=HandMenuRay.MenuLayer;
            go.transform.SetParent(Environment.panelMount,false); go.transform.localPosition=Vector3.back*.03f; go.transform.localScale=Vector3.one*.00065f;
            var canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=Camera.main;
            menuRoot=(RectTransform)go.transform; menuRoot.sizeDelta=new Vector2(1800,1240);
            var panel=Rect("Panel",menuRoot,Vector2.zero,menuRoot.sizeDelta).gameObject.AddComponent<Image>(); panel.color=new Color(.025f,.055f,.085f); panel.raycastTarget=true;
            Text(menuRoot,"MOLECULE LAB",new Vector2(-470,555),new Vector2(800,80),48,new Color(.25f,.95f,.85f));
            modeText=Text(menuRoot,"BUILD MODE",new Vector2(480,555),new Vector2(650,60),28,Color.white);
            string[] names={"BUILD","FREE BUILD","INSPECT","REACT","SETTINGS"};
            LabMode[] tabModes={LabMode.Build,LabMode.Free,LabMode.Inspect,LabMode.React,LabMode.Build};
            for(var i=0;i<names.Length;i++) { var tab=i; Button(menuRoot,names[i],new Vector2(-704+i*352,455),new Vector2(330,80),()=> { if(tab==names.Length-1) { settingsPage=true; RebuildMenu(); } else SetMode(tabModes[tab]); }); }
            status=Text(menuRoot,"",new Vector2(0,-320),new Vector2(1670,130),27,Color.white);
            // Physical controls replace the old bottom-row buttons: red button = reset, bins = keep or discard.
            Text(menuRoot,"KEEP a molecule: drop it in the GALLERY bin.   DISCARD: drop it in the TRASH bin.   RESET: press the red button on the bench.",new Vector2(0,-440),new Vector2(1690,70),25,Color.white);
            Text(menuRoot,"Grab: reach or point + pinch.  Menu: point, pinch, release.  Teleport: controller thumbstick.",new Vector2(0,-550),new Vector2(1690,70),23,new Color(.75f,.8f,.82f));
            RebuildMenu();
        }
        void RebuildMenu()
        {
            if(menuRoot==null) return;
            if(pageObject!=null) { pageObject.SetActive(false); Destroy(pageObject); }
            pageObject=Rect("Current menu page",menuRoot,Vector2.zero,new Vector2(1800,700)).gameObject;
            var page=pageObject.transform; activateButton=null;
            lesson=Text(page,hints?instructions[lessonStep]:"Hints hidden. Essential placement feedback stays enabled.",new Vector2(430,265),new Vector2(780,170),26,Color.white);
            trayText=Text(page,"",new Vector2(430,60),new Vector2(780,160),26,Color.white);
            energyText=Text(page,"",new Vector2(430,-160),new Vector2(760,95),24,new Color(.8f,.85f,.88f));
            if(settingsPage)
            {
                var labels=new[]{"Bench lower","Bench higher","Bench nearer","Bench farther","Sound on/off","Effects full/low","Hints on/off","Clear gallery"};
                Action[] actions={()=>AdjustHeight(-.05f),()=>AdjustHeight(.05f),()=>AdjustReach(-.05f),()=>AdjustReach(.05f),ToggleSound,()=>{ReducedEffects=!ReducedEffects;Notify(ReducedEffects?"Reduced effects enabled.":"Full effects enabled.");},()=>{hints=!hints;RebuildMenu();},ClearGallery};
                for(var i=0;i<labels.Length;i++) Button(page,labels[i],new Vector2(-650+(i%2)*410,300-(i/2)*125),new Vector2(375,80),actions[i]);
                lesson.text="Fit the workstation to your seated or standing reach. Both hands support direct pinch grabs. Point and pinch also grabs distant objects. Release before changing targets.";
            }
            else if(Mode==LabMode.Build)
            {
                moleculePage=Mathf.Clamp(moleculePage,0,(molecules.Length-1)/4);
                for(var i=0;i<4;i++)
                {
                    var index=moleculePage*4+i; if(index>=molecules.Length) break; var m=molecules[index];
                    Button(page,m.displayName+"  "+m.formula,new Vector2(-450,300-i*125),new Vector2(760,80),()=>StartMolecule(m));
                }
                Button(page,"< Previous",new Vector2(-650,-200),new Vector2(350,80),()=>{moleculePage=Mathf.Max(0,moleculePage-1);RebuildMenu();});
                Button(page,"Next >",new Vector2(-250,-200),new Vector2(350,80),()=>{moleculePage=Mathf.Min((molecules.Length-1)/4,moleculePage+1);RebuildMenu();});
                energyText.text=$"Molecules: page {moleculePage+1}/{(molecules.Length+3)/4}\nChoose REACT for conditions and activation.";
            }
            else if(Mode==LabMode.Free) BuildFreePage(page);
            else if(Mode==LabMode.Inspect)
            {
                Text(page,Selected==null?"Grab a completed molecule to inspect it.":Selected.Definition.displayName+"  "+Selected.Definition.formula+"\n"+Selected.Definition.geometry+"\n"+Selected.Definition.electronicState,new Vector2(-450,160),new Vector2(780,360),27,Color.white);
                Button(page,"Ethane: twist +30 deg",new Vector2(-450,-120),new Vector2(760,80),TwistStep).interactable=Selected!=null && Selected.Definition.rotatableBond>=0;
                energyText.text="Ethane energy tool: illustrative homolysis only.\nOther structures do not support this break tool.";
            }
            else
            {
                for(var i=0;i<reactions.Length;i++) { var r=reactions[i]; Button(page,(ActiveReaction==r?"> ":"")+r.title,new Vector2(-450,300-i*125),new Vector2(760,80),()=>SelectReaction(r)); }
                lesson.text=ActiveReaction==null?"Choose a reaction.":"1 Build inputs   2 Release into tray\n3 Choose conditions   4 Activate\n"+ActiveReaction.conditions;
                var conditions=new[]{ReactionCondition.Ignition,ReactionCondition.PalladiumCatalyst,ReactionCondition.AcidCatalyst};
                for(var i=0;i<3;i++) { var c=conditions[i]; Button(page,ConditionName(c),new Vector2(165+i*265,-85),new Vector2(245,80),()=>SelectCondition(c)); }
                activateButton=Button(page,"Activate reaction",new Vector2(430,-200),new Vector2(760,80),ActivateReaction);
                energyText.gameObject.SetActive(false);
            }
            UpdateTray();
        }
        public void AdjustReach(float change)
        {
            if(reacting || views.Any(v=>v.PlacementLocked)) { Notify("Wait for the animation to finish."); return; }
            CancelInteractions(); var next=Mathf.Clamp(reachOffset+change,-.25f,.25f); var delta=Vector3.forward*(next-reachOffset); reachOffset=next;
            Environment.workstation.position+=delta; foreach(var view in views)view.transform.position+=delta; foreach(var atom in loose)if(atom!=null)atom.transform.position+=delta;
            if(freeView!=null) freeView.transform.position+=delta;
            energyTool.ReturnHome(); Notify("Workstation reach adjusted.");
        }
    }
}
