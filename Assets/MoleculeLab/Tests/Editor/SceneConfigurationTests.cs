using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
namespace MoleculeLab.Tests
{
    public sealed class SceneConfigurationTests
    {
        [Test] public void BothHandsUseNearFarObjectsAndMenus()
        {
            var rig=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoleculeLab/Prefabs/MoleculeLabRig.prefab");
            var gates=rig.GetComponentsInChildren<HandMenuRay>(true); Assert.That(gates.Length,Is.EqualTo(2));
            foreach(var gate in gates)
            {
                Assert.That(gate.direct,Is.Not.Null); Assert.That(gate.aim,Is.Not.Null);
                Assert.That(gate.direct.enableNearCasting,Is.True); Assert.That(gate.direct.enableFarCasting,Is.True);
                Assert.That(gate.direct.enableUIInteraction,Is.True);
            }
            Assert.That(rig.GetComponentsInChildren<XRPokeInteractor>(true).All(p=>!p.enableUIInteraction && !p.enabled),Is.True);
        }
        [Test] public void FixedShelfClearsWorkbenchAndGrabTargetsDoNotOverlap()
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoleculeLab/Environment/AcademicLab.prefab");
            var instance=Object.Instantiate(root);
            try
            {
                var env=instance.GetComponent<LabEnvironment>(); var shelf=env.workstation.Find("Reachable atom shelf");
                Assert.That(shelf.localPosition.x,Is.EqualTo(-.92f).Within(.001));
                var back=shelf.GetComponentsInChildren<MeshRenderer>().Max(r=>r.bounds.max.z);
                var front=env.bench.Find("Workbench").GetComponent<Renderer>().bounds.min.z;
                Assert.That(front-back,Is.GreaterThanOrEqualTo(.0499f));
                for(var i=0;i<48;i++) for(var j=i+1;j<48;j++)
                {
                    float Radius(int id)=>id<8?.055f:id<16?.052f:.04f;
                    Assert.That(Vector3.Distance(env.SocketPosition(i),env.SocketPosition(j)),Is.GreaterThan(Radius(i)+Radius(j)));
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
