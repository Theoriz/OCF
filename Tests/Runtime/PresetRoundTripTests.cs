using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests
{
    /// <summary>Target script carrying one member of every type OCF supports.</summary>
    public class AllTypesTarget : MonoBehaviour
    {
        public enum Mode { None = 0, Spot = 5, Wash = 12 }

        public bool boolValue;
        public int intValue;
        public float floatValue;
        public float rangedValue;
        public string stringValue;
        public Vector2 vector2Value;
        public Vector2Int vector2IntValue;
        public Vector3 vector3Value;
        public Vector3Int vector3IntValue;
        public Vector4 vector4Value;
        public Color colorValue;
        public Mode mode;

        public List<string> options = new List<string> { "warm", "cool", "neutral" };
        public string selected;

        public float notInPresets;
        public float readOnlyValue;
    }

    /// <summary>
    /// Hand-written mirror for <see cref="AllTypesTarget"/>. It deliberately does not override
    /// PollTargetScript, so the round-trip runs on the reflection poll a hand-written mirror uses.
    /// </summary>
    public class AllTypesControllable : Controllable
    {
        [OSCProperty] public bool boolValue;
        [OSCProperty] public int intValue;
        [OSCProperty] public float floatValue;
        //Qualified: NUnit declares a RangeAttribute of its own, so a bare [Range] is ambiguous here.
        [OSCProperty, UnityEngine.Range(0f, 1f)] public float rangedValue;
        [OSCProperty] public string stringValue;
        [OSCProperty] public Vector2 vector2Value;
        [OSCProperty] public Vector2Int vector2IntValue;
        [OSCProperty] public Vector3 vector3Value;
        [OSCProperty] public Vector3Int vector3IntValue;
        [OSCProperty] public Vector4 vector4Value;
        [OSCProperty] public Color colorValue;
        [OSCProperty] public AllTypesTarget.Mode mode;

        [OSCProperty(targetList = "options")] public string selected;

        [OSCProperty(includeInPresets = false)] public float notInPresets;

        [OSCProperty(readOnly = true)] public float readOnlyValue;
    }

    /// <summary>
    /// PlayMode tests for a full preset round-trip: every exposed member is saved to a real .pst
    /// file, changed on the target script, and restored by loading that file back.
    ///
    /// It goes through the file rather than through getData/loadData directly, so the JSON encoding,
    /// the per-type string formatting in getData and the parsing in loadData are all covered - the
    /// three places a type can be saved in a shape it cannot be read back from.
    ///
    /// Presets are written under a temporary folder, pointed at by ControllableMaster's
    /// customPresetDirectory, so a test run never touches the project's or the user's presets.
    /// </summary>
    public class PresetRoundTripTests
    {
        class Values
        {
            public bool Bool;
            public int Int;
            public float Float;
            public float Ranged;
            public string String;
            public Vector2 V2;
            public Vector2Int V2I;
            public Vector3 V3;
            public Vector3Int V3I;
            public Vector4 V4;
            public Color Color;
            public AllTypesTarget.Mode Mode;
            public string Selected;
        }

        static readonly Values Saved = new Values
        {
            Bool = true,
            Int = 42,
            Float = 1.25f,
            Ranged = 0.25f,
            //Quotes and backslashes have to survive the JSON encoding, and commas must not be read
            //as vector separators.
            String = "quoted \"text\", a backslash \\ and a comma",
            V2 = new Vector2(1.5f, -2.25f),
            V2I = new Vector2Int(3, -4),
            V3 = new Vector3(1.5f, -2.25f, 3.75f),
            V3I = new Vector3Int(5, -6, 7),
            V4 = new Vector4(1.5f, -2.25f, 3.75f, -4.5f),
            Color = new Color(0.25f, 0.5f, 0.75f, 1f),
            //Wash is the third member but its declared value is 12, so a preset storing a position
            //instead of the member would come back as Spot.
            Mode = AllTypesTarget.Mode.Wash,
            Selected = "neutral",
        };

        static readonly Values Overwritten = new Values
        {
            Bool = false,
            Int = -7,
            Float = -99.5f,
            Ranged = 0.75f,
            String = "something else entirely",
            V2 = new Vector2(-8f, 9f),
            V2I = new Vector2Int(-10, 11),
            V3 = new Vector3(-8f, 9f, -10f),
            V3I = new Vector3Int(-12, 13, -14),
            V4 = new Vector4(-8f, 9f, -10f, 11f),
            Color = new Color(1f, 0f, 0.125f, 0.5f),
            Mode = AllTypesTarget.Mode.None,
            Selected = "warm",
        };

        const float Tolerance = 1e-5f;

        string _presetRoot;
        GameObject _masterGo;

        [SetUp]
        public void SetUp()
        {
            _presetRoot = Path.Combine(Path.GetTempPath(), "OCFPresetRoundTrip_" + Guid.NewGuid().ToString("N"));

            //ControllableMaster.Awake only assigns the singleton; disabling the component before the
            //first frame keeps Start - and with it the OSC socket - from running.
            _masterGo = new GameObject("preset-root-master");
            var master = _masterGo.AddComponent<ControllableMaster>();
            //OSCReceiverName is a serialized field, so a master added from script has none. OnDisable
            //looks the receiver up by that name, so it has to be set before the component is disabled.
            master.OSCReceiverName = "PresetRoundTripTests";
            master.enabled = false;
            master.useDocumentsDirectory = false;
            master.customPresetDirectory = _presetRoot;

            ControllableMaster.InvalidatePresetRoot();
        }

        [TearDown]
        public void TearDown()
        {
            // Controllable registration is static and survives between tests.
            ControllableMaster.RegisteredControllables.Clear();

            if (_masterGo != null)
                UnityEngine.Object.DestroyImmediate(_masterGo);

            ControllableMaster.InvalidatePresetRoot();

            try
            {
                if (Directory.Exists(_presetRoot))
                    Directory.Delete(_presetRoot, true);
            }
            catch (Exception)
            {
                //A leftover temp folder is not worth failing a green test over.
            }
        }

        static (GameObject go, AllTypesTarget target, AllTypesControllable ctrl) BuildAllTypes()
        {
            // Build inactive so TargetScript can be wired before Controllable.Awake() binds.
            var go = new GameObject("all-types-test");
            go.SetActive(false);

            var target = go.AddComponent<AllTypesTarget>();
            var ctrl = go.AddComponent<AllTypesControllable>();
            ctrl.TargetScript = target;
            ctrl.usePresets = true;
            //A fixed folder rather than the test scene's name, so the preset path is predictable.
            ctrl.folder = "RoundTrip";

            go.SetActive(true); // Awake() binds the mirror, OnEnable() prepares the preset folder
            return (go, target, ctrl);
        }

        static void WriteToTarget(AllTypesTarget t, Values v)
        {
            t.boolValue = v.Bool;
            t.intValue = v.Int;
            t.floatValue = v.Float;
            t.rangedValue = v.Ranged;
            t.stringValue = v.String;
            t.vector2Value = v.V2;
            t.vector2IntValue = v.V2I;
            t.vector3Value = v.V3;
            t.vector3IntValue = v.V3I;
            t.vector4Value = v.V4;
            t.colorValue = v.Color;
            t.mode = v.Mode;
            t.selected = v.Selected;
        }

        static void AssertMirrorHolds(AllTypesControllable c, Values v, string because)
        {
            Assert.AreEqual(v.Bool, c.boolValue, because + " (bool)");
            Assert.AreEqual(v.Int, c.intValue, because + " (int)");
            Assert.AreEqual(v.Float, c.floatValue, Tolerance, because + " (float)");
            Assert.AreEqual(v.Ranged, c.rangedValue, Tolerance, because + " (ranged float)");
            Assert.AreEqual(v.String, c.stringValue, because + " (string)");
            AssertVector(new[] { v.V2.x, v.V2.y }, new[] { c.vector2Value.x, c.vector2Value.y }, because + " (Vector2)");
            Assert.AreEqual(v.V2I, c.vector2IntValue, because + " (Vector2Int)");
            AssertVector(new[] { v.V3.x, v.V3.y, v.V3.z }, new[] { c.vector3Value.x, c.vector3Value.y, c.vector3Value.z }, because + " (Vector3)");
            Assert.AreEqual(v.V3I, c.vector3IntValue, because + " (Vector3Int)");
            AssertVector(new[] { v.V4.x, v.V4.y, v.V4.z, v.V4.w }, new[] { c.vector4Value.x, c.vector4Value.y, c.vector4Value.z, c.vector4Value.w }, because + " (Vector4)");
            AssertVector(new[] { v.Color.r, v.Color.g, v.Color.b, v.Color.a }, new[] { c.colorValue.r, c.colorValue.g, c.colorValue.b, c.colorValue.a }, because + " (Color)");
            Assert.AreEqual(v.Mode, c.mode, because + " (enum)");
            Assert.AreEqual(v.Selected, c.selected, because + " (list-backed string)");
        }

        static void AssertTargetHolds(AllTypesTarget t, Values v, string because)
        {
            Assert.AreEqual(v.Bool, t.boolValue, because + " (bool)");
            Assert.AreEqual(v.Int, t.intValue, because + " (int)");
            Assert.AreEqual(v.Float, t.floatValue, Tolerance, because + " (float)");
            Assert.AreEqual(v.Ranged, t.rangedValue, Tolerance, because + " (ranged float)");
            Assert.AreEqual(v.String, t.stringValue, because + " (string)");
            AssertVector(new[] { v.V2.x, v.V2.y }, new[] { t.vector2Value.x, t.vector2Value.y }, because + " (Vector2)");
            Assert.AreEqual(v.V2I, t.vector2IntValue, because + " (Vector2Int)");
            AssertVector(new[] { v.V3.x, v.V3.y, v.V3.z }, new[] { t.vector3Value.x, t.vector3Value.y, t.vector3Value.z }, because + " (Vector3)");
            Assert.AreEqual(v.V3I, t.vector3IntValue, because + " (Vector3Int)");
            AssertVector(new[] { v.V4.x, v.V4.y, v.V4.z, v.V4.w }, new[] { t.vector4Value.x, t.vector4Value.y, t.vector4Value.z, t.vector4Value.w }, because + " (Vector4)");
            AssertVector(new[] { v.Color.r, v.Color.g, v.Color.b, v.Color.a }, new[] { t.colorValue.r, t.colorValue.g, t.colorValue.b, t.colorValue.a }, because + " (Color)");
            Assert.AreEqual(v.Mode, t.mode, because + " (enum)");
            Assert.AreEqual(v.Selected, t.selected, because + " (list-backed string)");
        }

        //Component by component: Unity's vector operator== is an approximate compare, so it would
        //pass on a value that did not actually round-trip.
        static void AssertVector(float[] expected, float[] actual, string because)
        {
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], Tolerance, because + " component " + i);
        }

        [UnityTest]
        public IEnumerator EveryExposedType_IsRestoredByLoadingASavedPreset()
        {
            var (go, target, ctrl) = BuildAllTypes();
            yield return null;

            WriteToTarget(target, Saved);
            target.notInPresets = 1f;
            target.readOnlyValue = 1f;
            ctrl.Update(); // poll picks the values up into the mirror
            AssertMirrorHolds(ctrl, Saved, "The poll should have read the target's values");

            ctrl.SaveAs();
            var presetFileName = ctrl.currentPreset;
            Assert.IsNotEmpty(presetFileName, "SaveAs should record the file it wrote as currentPreset.");

            // Change every member on the target script, and let the poll carry them to the mirror.
            WriteToTarget(target, Overwritten);
            target.notInPresets = 2f;
            target.readOnlyValue = 2f;
            ctrl.Update();
            AssertMirrorHolds(ctrl, Overwritten, "The values should have changed before the preset is loaded back");

            ctrl.LoadWithName(presetFileName);

            AssertMirrorHolds(ctrl, Saved, "Loading the preset should restore the mirror");
            AssertTargetHolds(target, Saved, "Loading the preset should write through to the target script");

            Assert.AreEqual(2f, ctrl.notInPresets, Tolerance,
                "A member marked includeInPresets = false should be left alone by a preset load.");
            Assert.AreEqual(2f, target.notInPresets, Tolerance,
                "...and so should the target script's copy of it.");

            Assert.AreEqual(2f, ctrl.readOnlyValue, Tolerance,
                "A read-only member is not written to the preset and not restored from one.");

            // A preset write leaves mirror and target agreeing, so the next poll must find nothing
            // and must not report the restored values as a script-side change.
            ctrl.Update();
            yield return null;

            AssertMirrorHolds(ctrl, Saved, "The poll after a load should leave the restored values alone");
            AssertTargetHolds(target, Saved, "The poll after a load should leave the target alone");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// The preset really is a file on disk, and it really is what the values come back from -
        /// not something still held in memory from the save.
        /// </summary>
        [UnityTest]
        public IEnumerator ASavedPreset_IsReadBackFromDisk()
        {
            var (go, target, ctrl) = BuildAllTypes();
            yield return null;

            WriteToTarget(target, Saved);
            ctrl.Update();
            ctrl.SaveAs();

            var presetPath = ctrl.targetDirectory + ctrl.currentPreset;
            Assert.IsTrue(File.Exists(presetPath), "SaveAs should have written " + presetPath);

            var contents = File.ReadAllText(presetPath);
            StringAssert.Contains("\"mode\"", contents, "The enum member should be recorded by name.");
            StringAssert.Contains("Wash", contents, "The enum should be saved as its member name.");
            StringAssert.Contains("neutral", contents, "The list-backed selection should be saved.");
            StringAssert.DoesNotContain("notInPresets", contents,
                "A member marked includeInPresets = false should not be written at all.");
            StringAssert.DoesNotContain("readOnlyValue", contents,
                "A read-only member cannot be restored by a load, so it should not be written either.");

            // Drop everything the controllable holds, then load: nothing but the file can supply it.
            WriteToTarget(target, Overwritten);
            ctrl.Update();
            ctrl.LoadWithName(ctrl.currentPreset);

            AssertMirrorHolds(ctrl, Saved, "The values should come back from the file");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }
    }
}
