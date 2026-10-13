using UnityEngine;
namespace MoleculeLab
{
    public readonly struct PlacementResult
    {
        public readonly bool Preview, Ready;
        public readonly float Distance, Angle;
        public PlacementResult(bool preview,bool ready,float distance,float angle) { Preview=preview; Ready=ready; Distance=distance; Angle=angle; }
    }
    public static class BondPlacement
    {
        public static PlacementResult Evaluate(bool chemicallyValid,Vector3 anchor,Vector3 target,Vector3 atom,float previewDistance=.18f,float snapDistance=.05f,float maxAngle=15)
        {
            var distance=Vector3.Distance(target,atom);
            var direction=atom-anchor;
            var angle=direction.sqrMagnitude<.000001f?180:Vector3.Angle(target-anchor,direction);
            return new PlacementResult(chemicallyValid && distance<=previewDistance,chemicallyValid && distance<=snapDistance && angle<=maxAngle,distance,angle);
        }
    }
}
