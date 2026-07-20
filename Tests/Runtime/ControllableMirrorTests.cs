using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests
{
    /// <summary>Plain target script whose public field is mirrored by a Controllable.</summary>
    public class MirrorTarget : MonoBehaviour
    {
        public float myValue;
    }

    /// <summary>
    /// Hand-written mirror (what ControllableGenerator would emit): an [OSCProperty] field
    /// whose name matches a public field on the TargetScript.
    /// </summary>
    public class MirrorControllable : Controllable
    {
        [OSCProperty] public float myValue;
    }

    /// <summary>Target with a Vector4 field, to exercise the Vector4 preset round-trip.</summary>
    public class Vector4MirrorTarget : MonoBehaviour
    {
        public Vector4 vec;
    }

    /// <summary>Mirror exposing a Vector4 [OSCProperty].</summary>
    public class Vector4MirrorControllable : Controllable
    {
        [OSCProperty] public Vector4 vec;
    }

    /// <summary>
    /// PlayMode tests for the Controllable "mirror" pattern.
    ///
    /// NOTE: <see cref="ScriptChange_RaisesControllableValueChanged_ExactlyOnce"/> is an
    /// intentional RED test. Controllable.Update() calls RaiseEventValueChanged twice per
    /// change (once directly, once via scriptValueChanged → OnScriptValueChanged); see
    /// docs/PackageAudit-2026-07-16.md, OCF P1 "controllableValueChanged fires twice".
    /// It passes once the duplicate raise is removed.
    /// </summary>
    public class ControllableMirrorTests
    {
        static (GameObject go, MirrorTarget target, MirrorControllable ctrl) BuildMirror(float initial = 0f)
        {
            // Build inactive so TargetScript can be wired before Controllable.Awake() binds.
            var go = new GameObject("mirror-test");
            go.SetActive(false);

            var target = go.AddComponent<MirrorTarget>();
            target.myValue = initial;

            var ctrl = go.AddComponent<MirrorControllable>();
            ctrl.TargetScript = target;
            ctrl.usePresets = false; // avoid preset filesystem I/O in OnEnable/OnDisable

            go.SetActive(true); // Awake() runs here and binds the mirror by field name
            return (go, target, ctrl);
        }

        [UnityTest]
        public IEnumerator Mirror_IsInitializedFromTargetValue_OnBind()
        {
            var (go, _, ctrl) = BuildMirror(initial: 7.5f);
            yield return null;

            Assert.AreEqual(7.5f, ctrl.myValue, 1e-6f,
                "The mirror field should be seeded from the target's value during Awake binding.");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScriptChange_RaisesControllableValueChanged_ExactlyOnce()
        {
            var (go, target, ctrl) = BuildMirror(initial: 0f);
            yield return null;

            int raised = 0;
            ctrl.controllableValueChanged += _ => raised++;

            // Change the underlying target field, then run one poll deterministically.
            target.myValue = 5f;
            ctrl.Update();

            Assert.AreEqual(1, raised,
                "controllableValueChanged should fire exactly once per target change " +
                "(known bug: it currently fires twice because Update() raises it directly " +
                "and again via scriptValueChanged → OnScriptValueChanged).");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Vector4Property_RoundTripsThrough_GetData_LoadData()
        {
            var go = new GameObject("vec4-test");
            go.SetActive(false);
            var target = go.AddComponent<Vector4MirrorTarget>();
            var ctrl = go.AddComponent<Vector4MirrorControllable>();
            ctrl.TargetScript = target;
            ctrl.usePresets = false;
            go.SetActive(true); // Awake() binds the mirror
            yield return null;

            var expected = new Vector4(1.5f, -2.25f, 3f, 4.75f);
            ctrl.vec = expected;

            var data = (ControllableData)ctrl.getData();
            ctrl.vec = Vector4.zero;
            ctrl.loadData(data);

            Assert.AreEqual(expected, ctrl.vec,
                "A Vector4 [OSCProperty] should survive a getData/loadData round-trip.");

            Object.Destroy(go);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            // Controllable registration is static and survives between tests.
            ControllableMaster.RegisteredControllables.Clear();
        }
    }
}
