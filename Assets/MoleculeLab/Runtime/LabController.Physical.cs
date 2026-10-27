using UnityEngine;

namespace MoleculeLab
{
    // Physical controls instead of menu buttons: a red reset button on the bench and two bins beside it.
    public sealed partial class LabController
    {
        public ResetButton ResetControl { get; private set; }
        public MoleculeBin GalleryBin { get; private set; }
        public MoleculeBin TrashBin { get; private set; }

        void BuildPhysicalControls()
        {
            ResetControl=ResetButton.Create(bench,this);
            // Floor bins either side of the bench, at the player's end; they stay put when the bench is adjusted.
            GalleryBin=MoleculeBin.Create(Environment.transform,this,true,new Vector3(-1.4f,0,.9f));
            TrashBin=MoleculeBin.Create(Environment.transform,this,false,new Vector3(1.4f,0,.9f));
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
        int GalleryCount(string id)
        {
            if(Gallery==null) return 0;
            foreach(var entry in Gallery.Entries) if(entry.id==id) return entry.timesBuilt;
            return 0;
        }
    }
}
