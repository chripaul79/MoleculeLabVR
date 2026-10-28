using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoleculeLab
{
    // Gallery: every molecule the player completes gets a display copy on the gallery tables behind the bench.
    public sealed partial class LabController
    {
        public MoleculeGallery Gallery { get; private set; }
        public GalleryRoom GalleryRoom => galleryRoom;
        GalleryRoom galleryRoom;
        float clearGalleryArmedAt=-100;
        [Tooltip("Seconds the gallery hologram stays after an exhibit is let go.")]
        [Min(0)] public float hologramLinger=6;
        /// <summary>Describes compounds for the gallery hologram. Defaults to the PubChem client; tests may replace it.</summary>
        public IMoleculeInfoSource MoleculeInfoSource { get; set; }
        public MoleculeHologram Hologram => hologram;
        MoleculeHologram hologram;
        readonly Dictionary<string,(MoleculeInfo info,string iupac)> exhibitInfo=new Dictionary<string,(MoleculeInfo,string)>();
        readonly HashSet<string> exhibitInfoLoading=new HashSet<string>();

        void BuildGallery()
        {
            galleryRoom=GalleryRoom.Create(Environment,this);
            Gallery=new MoleculeGallery(Path.Combine(Application.persistentDataPath,"gallery.json"));
            galleryRoom.Show(Gallery,elements);
            hologram=MoleculeHologram.Create(this,font);
        }
        /// <summary>Switches to another gallery file; null keeps the gallery in memory only (used by tests).</summary>
        public void UseGalleryFile(string path)
        {
            Gallery=new MoleculeGallery(path);
            if(galleryRoom!=null) galleryRoom.Show(Gallery,elements);
        }
        /// <summary>Records a completed molecule. Returns true when it is new to the gallery.</summary>
        bool RecordInGallery(MoleculeDefinition definition)
        {
            if(Gallery==null || definition==null) return false;
            var isNew=Gallery.Record(definition);
            if(galleryRoom!=null) galleryRoom.Show(Gallery,elements);
            return isNew;
        }
        /// <summary>Picking up an exhibit opens its hologram; PubChem's description and uses fill in when they arrive.</summary>
        public void ExhibitGrabbed(GalleryExhibit exhibit)
        {
            if(exhibit==null || hologram==null) return;
            hologram.Show(exhibit);
            var id=exhibit.Entry.id;
            if(exhibitInfo.TryGetValue(id,out var known)) { hologram.SetInfo(id,known.info,known.iupac); return; }
            if(exhibitInfoLoading.Add(id)) StartCoroutine(LoadExhibitInfo(exhibit.Entry));
        }
        /// <summary>Let go of an exhibit: its hologram fades; let go over the reaction vessel, a copy of it goes in.</summary>
        public void ExhibitReleased(GalleryExhibit exhibit)
        {
            if(hologram!=null) hologram.HideLater(exhibit,hologramLinger);
            if(exhibit!=null && OverVessel(exhibit.transform.position)) CopyExhibitIntoVessel(exhibit.Entry);
        }
        IEnumerator LoadExhibitInfo(GalleryEntry entry)
        {
            EnsureMoleculeLookup();
            if(MoleculeInfoSource==null) MoleculeInfoSource=MoleculeLookup as IMoleculeInfoSource??new PubChemClient(Path.Combine(Application.persistentDataPath,"PubChemCache"));
            var cid=entry.cid; string iupac=null; MoleculeInfo info=null;
            // Recipe molecules have no stored CID: identify them from their bonding first (cached after the first time).
            MoleculeLookupResult identity=null;
            yield return MoleculeLookup.Lookup(SmilesWriter.Write(MoleculeGallery.ToGraph(entry)),r=>identity=r);
            if(identity!=null && identity.status==LookupStatus.Found) { if(cid<=0) cid=identity.cid; iupac=identity.iupacName; }
            if(cid>0) yield return MoleculeInfoSource.Describe(cid,r=>info=r);
            else info=new MoleculeInfo{status=identity!=null && identity.status==LookupStatus.Error?LookupStatus.Error:LookupStatus.NotFound,
                message=identity!=null && identity.status==LookupStatus.Error?"PubChem is unreachable right now. Check the Wi-Fi and pick it up again.":"PubChem has no record of this structure."};
            exhibitInfoLoading.Remove(entry.id);
            // Errors are not remembered, so the next pick-up tries again.
            if(info!=null && info.status!=LookupStatus.Error) exhibitInfo[entry.id]=(info,iupac);
            if(hologram!=null) hologram.SetInfo(entry.id,info,iupac);
        }
        string GalleryNote(bool isNew) => isNew?" New in the gallery behind you!":" Its gallery count went up.";
        /// <summary>Press twice within four seconds: the gallery is shared across sessions, so one click must not wipe it.</summary>
        public void ClearGallery()
        {
            if(Gallery==null) return;
            if(Gallery.Entries.Count==0) { Notify("The gallery is already empty."); return; }
            if(Time.unscaledTime-clearGalleryArmedAt>4) { clearGalleryArmedAt=Time.unscaledTime; Notify("Press Clear gallery again within 4 seconds to remove all "+Gallery.Entries.Count+" exhibits."); return; }
            clearGalleryArmedAt=-100; Gallery.Clear(); galleryRoom.Show(Gallery,elements); Notify("Gallery cleared.");
        }
    }
}
