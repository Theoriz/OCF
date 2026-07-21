using System.Collections;
using System.Collections.Generic;
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

    /// <summary>
    /// Hand-written stand-in for what ControllableGenerator now emits: a typed PollTargetScript
    /// override that compares the mirror against the target directly instead of going through
    /// Controllable's reflection poll, which boxes every exposed value every frame.
    /// </summary>
    public class TypedPollControllable : Controllable
    {
        [OSCProperty] public float myValue;

        public int pollCount;

        protected override void PollTargetScript()
        {
            pollCount++;

            var target = TargetScript as MirrorTarget;
            if (target == null) return;

            if (myValue != target.myValue)
            {
                myValue = target.myValue;
                RaiseScriptValueChanged("myValue");
            }
        }
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

        static (GameObject go, MirrorTarget target, TypedPollControllable ctrl) BuildTypedPollMirror()
        {
            var go = new GameObject("typed-poll-test");
            go.SetActive(false);

            var target = go.AddComponent<MirrorTarget>();
            var ctrl = go.AddComponent<TypedPollControllable>();
            ctrl.TargetScript = target;
            ctrl.usePresets = false;

            go.SetActive(true);
            return (go, target, ctrl);
        }

        [UnityTest]
        public IEnumerator TypedPoll_RaisesControllableValueChanged_ExactlyOnce()
        {
            var (go, target, ctrl) = BuildTypedPollMirror();
            yield return null;

            int raised = 0;
            ctrl.controllableValueChanged += _ => raised++;

            target.myValue = 5f;
            ctrl.Update();

            Assert.AreEqual(1, raised,
                "A mirror overriding PollTargetScript must match the reflection path: exactly one " +
                "controllableValueChanged per target change.");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TypedPoll_IsUsedInsteadOfTheReflectionPoll()
        {
            var (go, target, ctrl) = BuildTypedPollMirror();
            yield return null;

            ctrl.pollCount = 0;
            target.myValue = 3f;
            ctrl.Update();

            Assert.AreEqual(1, ctrl.pollCount, "Update() must route through PollTargetScript.");
            Assert.AreEqual(3f, ctrl.myValue, 1e-6f, "The override must write the mirror field.");

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// A value written by OCF itself - UI, OSC or a preset, all of which funnel through
        /// uiValueChanged - is not a script-side change. The poll used to read it back as one and
        /// report it a second time on the next frame.
        /// </summary>
        [UnityTest]
        public IEnumerator OcfWrite_IsNotReportedAsAScriptChange_OnTheNextPoll()
        {
            var (go, _, ctrl) = BuildMirror(initial: 0f);
            yield return null;

            ctrl.myValue = 9f;
            ctrl.OnUiValueChanged("myValue");

            int raised = 0;
            ctrl.controllableValueChanged += _ => raised++;

            ctrl.Update();

            Assert.AreEqual(0, raised,
                "The poll must not report a value OCF has just written as a script-side change.");

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// OSC and preset writes both go through setFieldProp, and they leave the mirror and the
        /// target agreeing - so no poll can detect them. setFieldProp has to notify the UI itself
        /// or the widgets never refresh.
        /// </summary>
        [UnityTest]
        public IEnumerator OscWrite_RaisesControllableValueChanged()
        {
            var (go, target, ctrl) = BuildMirror(initial: 0f);
            yield return null;

            int raised = 0;
            ctrl.controllableValueChanged += _ => raised++;

            ctrl.setFieldProp(ctrl.Fields["myValue"], new List<object> { 4f });

            Assert.AreEqual(1, raised, "setFieldProp must tell the UI the value moved.");
            Assert.AreEqual(4f, ctrl.myValue, 1e-6f, "The mirror should hold the written value.");
            Assert.AreEqual(4f, target.myValue, 1e-6f, "The write should reach the target script.");

            ctrl.Update();

            Assert.AreEqual(1, raised, "The following poll must not report it a second time.");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MirrorWithoutOverride_StillPollsByReflection()
        {
            var (go, target, ctrl) = BuildMirror(initial: 0f);
            yield return null;

            target.myValue = 2f;
            ctrl.Update();

            Assert.AreEqual(2f, ctrl.myValue, 1e-6f,
                "Mirrors that do not override PollTargetScript must keep working on the reflection path.");

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
