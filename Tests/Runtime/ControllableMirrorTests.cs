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
    /// whose name matches a public field on the controllableTargetScript.
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

            var target = controllableTargetScript as MirrorTarget;
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
    /// Target with an enum field whose members carry explicit, non-sequential values, and a list a
    /// string member is chosen from - the two dropdown routes, in the shape a generated mirror
    /// produces them.
    /// </summary>
    public class DropdownMirrorTarget : MonoBehaviour
    {
        public enum LightMode { None = 0, Spot = 5, Wash = 12 }

        public LightMode lightMode = LightMode.None;

        public System.Collections.Generic.List<string> palettes = new System.Collections.Generic.List<string> { "warm", "cool" };
        public string palette = "warm";
    }

    /// <summary>
    /// Mirror exposing both, as ControllableGenerator emits them: the enum is declared with its real
    /// type, and the list stays on the target script rather than being duplicated here.
    /// </summary>
    public class DropdownMirrorControllable : Controllable
    {
        [OSCProperty] public DropdownMirrorTarget.LightMode lightMode;

        [OSCProperty(targetList = "palettes")] public string palette;
    }

    /// <summary>
    /// Mirror that declares its own list under the same name the target script uses, the way a
    /// hand-written mirror and controllablePresetList do. The mirror's list is the one that must win.
    /// </summary>
    public class MirrorOwnedListControllable : Controllable
    {
        public System.Collections.Generic.List<string> palettes = new System.Collections.Generic.List<string> { "mirror-a", "mirror-b" };

        [OSCProperty(targetList = "palettes")] public string palette;
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
            // Build inactive so controllableTargetScript can be wired before Controllable.Awake() binds.
            var go = new GameObject("mirror-test");
            go.SetActive(false);

            var target = go.AddComponent<MirrorTarget>();
            target.myValue = initial;

            var ctrl = go.AddComponent<MirrorControllable>();
            ctrl.controllableTargetScript = target;
            ctrl.controllableUsePresets = false; // avoid preset filesystem I/O in OnEnable/OnDisable

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
            ctrl.controllableTargetScript = target;
            ctrl.controllableUsePresets = false;
            go.SetActive(true); // Awake() binds the mirror
            yield return null;

            var expected = new Vector4(1.5f, -2.25f, 3f, 4.75f);
            ctrl.vec = expected;

            var data = (ControllableData)ctrl.GetData();
            ctrl.vec = Vector4.zero;
            ctrl.LoadData(data);

            Assert.AreEqual(expected, ctrl.vec,
                "A Vector4 [OSCProperty] should survive a GetData/LoadData round-trip.");

            Object.Destroy(go);
            yield return null;
        }

        static (GameObject go, MirrorTarget target, TypedPollControllable ctrl) BuildTypedPollMirror()
        {
            var go = new GameObject("typed-poll-test");
            go.SetActive(false);

            var target = go.AddComponent<MirrorTarget>();
            var ctrl = go.AddComponent<TypedPollControllable>();
            ctrl.controllableTargetScript = target;
            ctrl.controllableUsePresets = false;

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
        /// OSC and preset writes both go through SetFieldProp, and they leave the mirror and the
        /// target agreeing - so no poll can detect them. SetFieldProp has to notify the UI itself
        /// or the widgets never refresh.
        /// </summary>
        [UnityTest]
        public IEnumerator OscWrite_RaisesControllableValueChanged()
        {
            var (go, target, ctrl) = BuildMirror(initial: 0f);
            yield return null;

            int raised = 0;
            ctrl.controllableValueChanged += _ => raised++;

            ctrl.SetFieldProp(ctrl.controllableFields["myValue"], new List<object> { 4f });

            Assert.AreEqual(1, raised, "SetFieldProp must tell the UI the value moved.");
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

        static (GameObject go, DropdownMirrorTarget target, DropdownMirrorControllable ctrl) BuildDropdownMirror()
        {
            var go = new GameObject("dropdown-test");
            go.SetActive(false);

            var target = go.AddComponent<DropdownMirrorTarget>();
            var ctrl = go.AddComponent<DropdownMirrorControllable>();
            ctrl.controllableTargetScript = target;
            ctrl.controllableUsePresets = false;

            go.SetActive(true);
            return (go, target, ctrl);
        }

        /// <summary>
        /// An enum is set from its member name, which is what OSC carries and what saveData writes.
        /// </summary>
        [UnityTest]
        public IEnumerator EnumProperty_IsSetByName_AndReachesTheTargetScript()
        {
            var (go, target, ctrl) = BuildDropdownMirror();
            yield return null;

            int raised = 0;
            ctrl.controllableValueChanged += _ => raised++;

            ctrl.SetFieldProp(ctrl.controllableFields["lightMode"], new List<object> { "Wash" });

            Assert.AreEqual(DropdownMirrorTarget.LightMode.Wash, ctrl.lightMode, "The mirror should hold the named member.");
            Assert.AreEqual(DropdownMirrorTarget.LightMode.Wash, target.lightMode, "The write should reach the target script.");
            Assert.AreEqual(1, raised, "SetFieldProp must tell the UI the value moved.");

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// Wash is the third member but its declared value is 12. The mechanism this replaced stored
        /// the member's position, so it wrote 2 here and every explicitly-valued enum was wrong.
        /// </summary>
        [UnityTest]
        public IEnumerator EnumProperty_IsSetByDeclaredValue_NotByIndex()
        {
            var (go, target, ctrl) = BuildDropdownMirror();
            yield return null;

            ctrl.SetFieldProp(ctrl.controllableFields["lightMode"], new List<object> { 12 });

            Assert.AreEqual(DropdownMirrorTarget.LightMode.Wash, target.lightMode,
                "12 is Wash's declared value; resolving it as an index would give Spot.");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnumProperty_RoundTripsThrough_GetData_LoadData()
        {
            var (go, _, ctrl) = BuildDropdownMirror();
            yield return null;

            ctrl.lightMode = DropdownMirrorTarget.LightMode.Wash;

            var data = (ControllableData)ctrl.GetData();
            ctrl.lightMode = DropdownMirrorTarget.LightMode.None;
            ctrl.LoadData(data);

            Assert.AreEqual(DropdownMirrorTarget.LightMode.Wash, ctrl.lightMode,
                "An enum [OSCProperty] should survive a GetData/LoadData round-trip.");

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// A generated mirror declares no list of its own, so the entries have to come off the target
        /// script. This is what lets [OSCExposed(targetList = ...)] work without hand editing.
        /// </summary>
        [UnityTest]
        public IEnumerator GetTargetList_FindsAListOnTheTargetScript()
        {
            var (go, target, ctrl) = BuildDropdownMirror();
            yield return null;

            CollectionAssert.AreEqual(target.palettes, ctrl.GetTargetList("palettes"));

            //Read live, so entries added at runtime are the ones a dropdown shows on its next refresh.
            target.palettes.Add("neutral");
            CollectionAssert.Contains(ctrl.GetTargetList("palettes"), "neutral");

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// The mirror is searched first. controllablePresetList is declared on Controllable itself, so losing this
        /// order would take the preset dropdown with it.
        /// </summary>
        [UnityTest]
        public IEnumerator GetTargetList_PrefersTheMirrorsOwnList()
        {
            var go = new GameObject("mirror-list-test");
            go.SetActive(false);

            var target = go.AddComponent<DropdownMirrorTarget>();
            var ctrl = go.AddComponent<MirrorOwnedListControllable>();
            ctrl.controllableTargetScript = target;
            ctrl.controllableUsePresets = false;

            go.SetActive(true);
            yield return null;

            CollectionAssert.AreEqual(ctrl.palettes, ctrl.GetTargetList("palettes"),
                "A list declared on the mirror must win over one of the same name on the target script.");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetTargetList_ReturnsNull_ForANameThatResolvesToNothing()
        {
            var (go, _, ctrl) = BuildDropdownMirror();
            yield return null;

            Assert.IsNull(ctrl.GetTargetList("nope"));
            Assert.IsNull(ctrl.GetTargetList(""));
            //'palette' exists but is a string, not a list.
            Assert.IsNull(ctrl.GetTargetList("palette"));

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
