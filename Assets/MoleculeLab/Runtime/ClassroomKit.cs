using UnityEngine;

namespace MoleculeLab
{
    /// <summary>
    /// Models from the imported asset packs used to dress the lab as a bright classroom. Filled automatically by
    /// Molecule Lab > Set up imported asset packs (Assets/MoleculeLab/Resources/ClassroomKit.asset); any field can be
    /// swapped in the Inspector. Empty fields are simply skipped.
    /// </summary>
    [CreateAssetMenu(menuName="Molecule Lab/Classroom kit",fileName="ClassroomKit")]
    public sealed class ClassroomKit : ScriptableObject
    {
        [Header("Atom beakers")]
        public GameObject beaker;
        [Header("Classroom furniture (3D Laboratory Environment)")]
        public GameObject labCounter;
        public GameObject desk, chair, wallRack, storageBox, instrument, tap;
        [Header("Glassware on the counters")]
        public GameObject erlenmeyer;
        public GameObject florence, beakerWater, cylinder, spiritLamp, boilingStand, funnel, chinaDish, testTube, erlenmeyerEmpty;
        [Header("Gallery (Minimal Sci-fi Pack, 3D Scifi Kit)")]
        public GameObject wallPanelWide;
        public GameObject wallPanelNarrow, canister, scifiBox, sideLight, labSign;
        [Header("Room shell and extra furniture (3D Laboratory Environment)")]
        public GameObject labWall;
        public GameObject labFloor, labRoof, labShelf, labWaterShelf;
        public Material woodMaterial, metalMaterial;
        [Header("Unused: earlier sci-fi shell")]
        public GameObject scifiWall;
        public GameObject scifiWallAlt, scifiFloor, scifiRoof;
        [Header("Surfaces")]
        public Texture2D floorTiles;
        public Texture2D floorNormal;
    }
}
