using UnityEngine;
using TMPro;
namespace MoleculeLab
{
    public sealed class LabEnvironment : MonoBehaviour
    {
        public Transform workstation, bench, tray, panelMount;
        public Transform[] sockets;
        public TextMeshPro[] stockLabels;
        public Vector3 SocketPosition(int id) => sockets[id].position;
        public void UpdateCounts(AtomInventory inventory)
        {
            for(var basket=0;basket<4;basket++)
            {
                var first=basket==0?0:basket==1?8:basket==2?16:32; var count=basket<2?8:16; var n=0;
                for(var i=first;i<first+count;i++) if(inventory.Tokens[i].Owner==AtomInventory.Shelf) n++;
                stockLabels[basket].text=$"{(basket==0?"CARBON / C":basket==1?"OXYGEN / O":"HYDROGEN / H")}   {n}/{count}";
            }
        }
    }
}
