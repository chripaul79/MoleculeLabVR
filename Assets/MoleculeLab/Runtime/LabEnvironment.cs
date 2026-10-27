using UnityEngine;
using TMPro;
namespace MoleculeLab
{
    public sealed class LabEnvironment : MonoBehaviour
    {
        public Transform workstation, bench, tray, panelMount;
        public Transform[] sockets;
        public TextMeshPro[] stockLabels;
        /// <summary>Sockets added at runtime (the extra element shelf), numbered after the prefab's own sockets.</summary>
        [System.NonSerialized] public Transform[] extraSockets=new Transform[0];
        [System.NonSerialized] public System.Action<AtomInventory> extraCounts;
        public Vector3 SocketPosition(int socket) => socket<sockets.Length?sockets[socket].position:extraSockets[socket-sockets.Length].position;
        public void UpdateCounts(AtomInventory inventory)
        {
            for(var basket=0;basket<4;basket++)
            {
                var first=basket==0?0:basket==1?8:basket==2?16:32; var count=basket<2?8:16; var n=0;
                foreach(var token in inventory.Tokens) if(token.Owner==AtomInventory.Shelf && token.Socket>=first && token.Socket<first+count) n++;
                stockLabels[basket].text=$"{(basket==0?"CARBON / C":basket==1?"OXYGEN / O":"HYDROGEN / H")}   {(inventory.Unlimited?"unlimited":n+"/"+count)}";
            }
            extraCounts?.Invoke(inventory);
        }
    }
}
