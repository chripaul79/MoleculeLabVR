using NUnit.Framework;
using UnityEngine;
namespace MoleculeLab.Tests
{
    public sealed class InventoryPlacementTests
    {
        [Test] public void InitialInventoryMatchesFiniteShelf()
        { var stock=new AtomInventory(); Assert.That(stock.Tokens.Count,Is.EqualTo(48)); Assert.That(stock.Available("C"),Is.EqualTo(8)); Assert.That(stock.Available("O"),Is.EqualTo(8)); Assert.That(stock.Available("H"),Is.EqualTo(32)); }
        [Test] public void DuplicateConsumptionCannotTransferAgain()
        { var stock=new AtomInventory(); var id=stock.FirstAvailable("C"); Assert.That(stock.Transfer(id,"shelf","molecule"),Is.True); Assert.That(stock.Transfer(id,"shelf","duplicate"),Is.False); Assert.That(stock.Available("C"),Is.EqualTo(7)); }
        [Test] public void ExhaustionAndRecyclingRestoreExactlyOriginalStock()
        { var stock=new AtomInventory(); for(var i=0;i<8;i++) Assert.That(stock.Transfer(stock.FirstAvailable("C"),"shelf","build"),Is.True); Assert.That(stock.FirstAvailable("C"),Is.EqualTo(-1)); stock.ReturnOwner("build"); stock.ReturnOwner("build"); Assert.That(stock.Available("C"),Is.EqualTo(8)); }
        [Test] public void InvalidOwnersAndIdsCannotMutateInventory()
        { var stock=new AtomInventory(); Assert.That(stock.Transfer(-1,"shelf","x"),Is.False); Assert.That(stock.Transfer(48,"shelf","x"),Is.False); Assert.That(stock.Transfer(0,"shelf",null),Is.False); Assert.That(stock.Available("C"),Is.EqualTo(8)); }
        [Test] public void CorrectTargetIsReady()
        { var p=BondPlacement.Evaluate(true,Vector3.zero,Vector3.right*.16f,Vector3.right*.16f); Assert.That(p.Preview && p.Ready,Is.True); }
        [Test] public void WrongElementNeverPreviewsOrCommits()
        { var p=BondPlacement.Evaluate(false,Vector3.zero,Vector3.right*.16f,Vector3.right*.16f); Assert.That(p.Preview || p.Ready,Is.False); }
        [Test] public void NearbyButWrongDirectionIsNotReady()
        { var p=BondPlacement.Evaluate(true,Vector3.zero,Vector3.right*.16f,new Vector3(.14f,.07f,0)); Assert.That(p.Preview,Is.True); Assert.That(p.Ready,Is.False); }
        [TestCase(.049f,true)] [TestCase(.051f,false)]
        public void DistanceBoundary(float offset,bool ready)
        { Assert.That(BondPlacement.Evaluate(true,Vector3.zero,Vector3.right*.16f,Vector3.right*(.16f+offset)).Ready,Is.EqualTo(ready)); }
        [TestCase(14.9f,true)] [TestCase(15.1f,false)]
        public void AngleBoundary(float degrees,bool ready)
        { var point=Quaternion.Euler(0,0,degrees)*Vector3.right*.16f; Assert.That(BondPlacement.Evaluate(true,Vector3.zero,Vector3.right*.16f,point).Ready,Is.EqualTo(ready)); }
        [Test] public void CoincidentAnchorCannotSnap()
        { Assert.That(BondPlacement.Evaluate(true,Vector3.zero,Vector3.right*.05f,Vector3.zero).Ready,Is.False); }
    }
}
