using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;
namespace MoleculeLab
{
    public sealed class MoleculeScaleTransformer : XRBaseGrabTransformer
    {
        Vector3 startMidpoint, startVector, startPosition, startScale;
        Quaternion startRotation;
        bool initialized;
        public static float ClampScale(float value) => Mathf.Clamp(value, .25f, 6f);
        public override void OnGrabCountChanged(XRGrabInteractable grab, Pose pose, Vector3 scale)
        {
            initialized = grab.interactorsSelecting.Count >= 2;
            if (!initialized) return;
            var a = grab.interactorsSelecting[0].GetAttachTransform(grab).position;
            var b = grab.interactorsSelecting[1].GetAttachTransform(grab).position;
            startMidpoint = (a + b) * .5f; startVector = b - a;
            startPosition = pose.position; startRotation = pose.rotation; startScale = scale;
        }
        public override void Process(XRGrabInteractable grab, XRInteractionUpdateOrder.UpdatePhase phase, ref Pose pose, ref Vector3 scale)
        {
            if (!initialized || grab.interactorsSelecting.Count < 2 || startVector.magnitude < .025f) return;
            var a = grab.interactorsSelecting[0].GetAttachTransform(grab).position;
            var b = grab.interactorsSelecting[1].GetAttachTransform(grab).position;
            if ((b-a).magnitude < .01f) return;
            var value = ClampScale(startScale.x * (b-a).magnitude / startVector.magnitude);
            var ratio = value / startScale.x;
            var rotation = Quaternion.FromToRotation(startVector, b-a);
            pose.position = (a+b) * .5f + rotation * (startPosition-startMidpoint) * ratio;
            pose.rotation = rotation * startRotation;
            scale = Vector3.one * value;
        }
    }
}
