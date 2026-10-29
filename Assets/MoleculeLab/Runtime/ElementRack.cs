using System.Linq;
using TMPro;
using UnityEngine;
namespace MoleculeLab
{
    /// <summary>Elements beyond the original H/C/O set. Created at runtime unless the scene already assigns an asset with that symbol.</summary>
    public static class ElementCatalog
    {
        public readonly struct Spec
        {
            public readonly string Symbol, Name; public readonly Color Color; public readonly float Radius; public readonly int Valence;
            public Spec(string symbol,string name,Color color,float radius,int valence) { Symbol=symbol; Name=name; Color=color; Radius=radius; Valence=valence; }
        }
        // CPK-style colours; valence = usual neutral bond count used by the free-build rules.
        public static readonly Spec[] Extra={
            new Spec("N","Nitrogen",new Color(.19f,.36f,.97f),.05f,3),
            new Spec("S","Sulfur",new Color(.95f,.82f,.18f),.06f,2),
            new Spec("P","Phosphorus",new Color(1f,.5f,.05f),.058f,3),
            new Spec("F","Fluorine",new Color(.56f,.88f,.31f),.044f,1),
            new Spec("Cl","Chlorine",new Color(.12f,.78f,.18f),.056f,1),
            new Spec("Br","Bromine",new Color(.62f,.16f,.14f),.062f,1),
        };
        public static ElementDefinition Create(Spec spec)
        {
            var element=ScriptableObject.CreateInstance<ElementDefinition>();
            element.name="Element_"+spec.Name; element.symbol=spec.Symbol; element.displayName=spec.Name;
            element.color=spec.Color; element.radius=spec.Radius; element.maximumBondOrder=spec.Valence;
            return element;
        }
        public static bool IsLight(Color color) => color.r*.3f+color.g*.59f+color.b*.11f>.6f;
    }

    /// <summary>A second atom shelf, mirroring the original one on the right of the bench, with one basket per extra element.</summary>
    public sealed class ElementRack : MonoBehaviour
    {
        public const int SocketsPerBasket=4;
        public Transform[] Sockets { get; private set; }
        public string[] SocketSymbols { get; private set; }
        TextMeshPro[] labels;
        ElementDefinition[] baskets;
        static readonly Color Teal=new Color(.21f,.37f,.38f), Metal=new Color(.48f,.55f,.56f), Dark=new Color(.09f,.13f,.15f);

        public static ElementRack Create(Transform workstation,ElementDefinition[] elements)
        {
            var go=new GameObject("Extra element shelf (runtime)"); go.transform.SetParent(workstation,false);
            go.transform.localPosition=new Vector3(.92f,0,-.26f); go.transform.localRotation=Quaternion.Euler(0,65,0);
            var rack=go.AddComponent<ElementRack>(); rack.Build(elements); return rack;
        }
        void Build(ElementDefinition[] elements)
        {
            baskets=elements; labels=new TextMeshPro[elements.Length];
            Sockets=new Transform[elements.Length*SocketsPerBasket]; SocketSymbols=new string[Sockets.Length];
            foreach(var x in new[]{-.6f,.6f}) LabVisuals.Shape("Shelf upright",PrimitiveType.Cube,transform,new Vector3(x,-.16f,.2f),new Vector3(.035f,1.3f,.035f),Metal,true);
            for(var b=0;b<elements.Length;b++)
            {
                var tier=b/3; var column=b%3;
                var tray=new GameObject("Element basket "+elements[b].symbol).transform; tray.SetParent(transform,false);
                tray.localPosition=new Vector3((column-1)*.38f,-.02f+tier*.34f,tier*.12f);
                LabVisuals.Shape("Basket base",PrimitiveType.Cube,tray,Vector3.zero,new Vector3(.36f,.025f,.3f),Teal,true);
                LabVisuals.Shape("Basket left lip",PrimitiveType.Cube,tray,new Vector3(-.175f,.035f,0),new Vector3(.018f,.065f,.3f),Metal,true);
                LabVisuals.Shape("Basket right lip",PrimitiveType.Cube,tray,new Vector3(.175f,.035f,0),new Vector3(.018f,.065f,.3f),Metal,true);
                LabVisuals.Shape("Basket rear lip",PrimitiveType.Cube,tray,new Vector3(0,.035f,.145f),new Vector3(.36f,.065f,.018f),Metal,true);
                for(var i=0;i<SocketsPerBasket;i++)
                {
                    var p=new Vector3(i%2==0?-.08f:.08f,.075f,i<2?-.07f:.07f);
                    var socket=new GameObject("Atom socket "+elements[b].symbol+" "+i).transform; socket.SetParent(tray,false); socket.localPosition=p;
                    Sockets[b*SocketsPerBasket+i]=socket; SocketSymbols[b*SocketsPerBasket+i]=elements[b].symbol;
                    LabVisuals.Shape("Empty socket marker",PrimitiveType.Cylinder,tray,new Vector3(p.x,.016f,p.z),new Vector3(.065f,.002f,.065f),Dark,false);
                }
                var label=LabVisuals.Label(tray,"",new Vector3(0,-.025f,-.165f),.02f);
                var face=label.GetComponent<FaceViewer>(); if(face!=null) { face.enabled=false; LabVisuals.Discard(face); }
                label.rectTransform.sizeDelta=new Vector2(.36f,.065f); labels[b]=label;
            }
        }
        /// <summary>Updates basket labels; firstSocket is this rack's first socket index in the inventory.</summary>
        public void UpdateCounts(AtomInventory inventory,int firstSocket)
        {
            for(var b=0;b<baskets.Length;b++)
            {
                var start=firstSocket+b*SocketsPerBasket;
                var n=inventory.Tokens.Count(t=>t.Owner==AtomInventory.Shelf && t.Socket>=start && t.Socket<start+SocketsPerBasket);
                labels[b].text=baskets[b].displayName.ToUpperInvariant()+" / "+baskets[b].symbol+"   "+(inventory.Unlimited?"unlimited":n+"/"+SocketsPerBasket);
            }
        }
    }
}
