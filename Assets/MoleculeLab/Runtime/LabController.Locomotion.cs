using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace MoleculeLab
{
    // Controller teleport. The rig was built with all locomotion switched off (stationary bench); this switches
    // teleport and snap turn back on at runtime, keeps smooth movement off, and makes only the floors valid targets.
    public sealed partial class LabController
    {
        [Tooltip("Controllers: push a thumbstick forward, aim at the floor, release to teleport. Left/right snap-turns.")]
        public bool enableTeleport=true;
        public bool TeleportReady { get; private set; }
        public IReadOnlyList<TeleportationArea> TeleportAreas => teleportAreas;
        readonly List<TeleportationArea> teleportAreas=new List<TeleportationArea>();

        void EnableTeleport()
        {
            if(!enableTeleport) return;
            var inactive=FindObjectsInactive.Include;
            var mediator=FindAnyObjectByType<LocomotionMediator>(inactive);
            var provider=FindAnyObjectByType<TeleportationProvider>(inactive);
            if(mediator==null || provider==null) { Debug.LogWarning("Teleport not enabled: the XR rig has no LocomotionMediator or TeleportationProvider."); return; }
            mediator.enabled=true;
            foreach(var body in FindObjectsByType<XRBodyTransformer>(inactive,FindObjectsSortMode.None)) body.enabled=true;
            provider.enabled=true;
            foreach(var turn in FindObjectsByType<SnapTurnProvider>(inactive,FindObjectsSortMode.None)) turn.enabled=true;
            // No smooth movement or smooth turning: teleport is far more comfortable in a headset.
            foreach(var move in FindObjectsByType<ContinuousMoveProvider>(inactive,FindObjectsSortMode.None)) move.enabled=false;
            foreach(var turn in FindObjectsByType<ContinuousTurnProvider>(inactive,FindObjectsSortMode.None)) turn.enabled=false;
            // The Starter Assets input manager (sample assembly, so set by name) must route the sticks to teleport.
            foreach(var behaviour in FindObjectsByType<MonoBehaviour>(inactive,FindObjectsSortMode.None).Where(b=>b!=null && b.GetType().Name=="ControllerInputActionManager"))
                foreach(var property in new[]{"smoothMotionEnabled","smoothTurnEnabled"})
                {
                    var p=behaviour.GetType().GetProperty(property);
                    if(p!=null && p.CanWrite && p.PropertyType==typeof(bool)) p.SetValue(behaviour,false);
                }
            // Only the controllers' teleport rays may select teleport targets, so a hand pinch at the floor never teleports.
            var teleportLayer=InteractionLayerMask.GetMask("Teleport");
            if(teleportLayer==0) { Debug.LogWarning("Teleport not enabled: no 'Teleport' interaction layer."); return; }
            foreach(var interactor in FindObjectsByType<XRBaseInteractor>(inactive,FindObjectsSortMode.None))
                if(!interactor.name.Contains("Teleport")) interactor.interactionLayers=interactor.interactionLayers.value & ~teleportLayer;
            foreach(var floor in Environment.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled && (c.name.StartsWith("Floor 6") || c.name=="Gallery floor")))
            {
                if(!floor.TryGetComponent<TeleportationArea>(out var area)) area=floor.gameObject.AddComponent<TeleportationArea>();
                area.interactionLayers=teleportLayer; area.teleportationProvider=provider;
                teleportAreas.Add(area);
            }
            TeleportReady=teleportAreas.Count>0;
        }
    }
}
