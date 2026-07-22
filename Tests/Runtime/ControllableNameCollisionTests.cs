using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests
{
    /// <summary>
    /// Target script declaring a method whose name collides with one of Controllable's own
    /// [OSCMethod] members. ControllableLoadWithName is used rather than ControllableSave because it
    /// is not gated by controllableUsePresets, so the fixture can keep presets off and avoid
    /// filesystem I/O.
    /// </summary>
    public class CollidingTarget : MonoBehaviour
    {
        public bool loadWithNameCalled;

        public void ControllableLoadWithName(string fileName)
        {
            loadWithNameCalled = true;
        }
    }

    /// <summary>Mirror declaring nothing: the accidental-collision case.</summary>
    public class PlainMirror : Controllable
    {
    }

    /// <summary>Mirror whose method hides Controllable's built-in (identical signature).</summary>
    public class HidingMirror : Controllable
    {
        [OSCMethod]
        public new void ControllableLoadWithName(string fileName)
        {
        }
    }

    /// <summary>Mirror whose method overloads Controllable's built-in (different signature).</summary>
    public class OverloadingMirror : Controllable
    {
        [OSCMethod]
        public void ControllableLoadWithName(int slot)
        {
        }
    }

    /// <summary>Mirror that counts DataLoaded calls. Since tweening was removed, LoadData no longer
    /// defers DataLoaded through a coroutine; it must invoke it exactly once, synchronously.</summary>
    public class DataLoadedCountingMirror : Controllable
    {
        public int dataLoadedCount;

        public override void DataLoaded()
        {
            dataLoadedCount++;
        }
    }

    /// <summary>
    /// PlayMode tests for Controllable's rule that its own members always win over a target script
    /// or mirror that reuses their names. Each case below reaches the registration code by a
    /// different route and used to fail differently: silent hijack, silent unregistration, and a
    /// duplicate-key throw respectively.
    /// </summary>
    public class ControllableNameCollisionTests
    {
        static (GameObject go, CollidingTarget target, T ctrl) Build<T>() where T : Controllable
        {
            // Build inactive so controllableTargetScript can be wired before Controllable.Awake() binds.
            var go = new GameObject("collision-test");
            go.SetActive(false);

            var target = go.AddComponent<CollidingTarget>();

            var ctrl = go.AddComponent<T>();
            ctrl.controllableTargetScript = target;
            ctrl.controllableUsePresets = false; // avoid preset filesystem I/O; LoadWithName is registered regardless

            go.SetActive(true); // Awake() runs here
            return (go, target, ctrl);
        }

        static void AssertBoundToBuiltIn(Controllable ctrl)
        {
            Assert.IsTrue(ctrl.controllableMethods.ContainsKey("ControllableLoadWithName"),
                "Controllable's built-in ControllableLoadWithName should always be registered.");

            var info = ctrl.controllableMethods["ControllableLoadWithName"];

            Assert.IsFalse(info.fromTargetScript,
                "A built-in must never be bound to the target script.");
            Assert.AreEqual(typeof(Controllable), info.methodInfo.DeclaringType,
                "The registered method should be Controllable's own, not a colliding declaration.");
        }

        /// <summary>
        /// The headline bug: any public method on the target with a built-in's name used to displace
        /// it, with no attribute needed and no diagnostic anywhere.
        /// </summary>
        [UnityTest]
        public IEnumerator TargetScriptMethod_DoesNotDisplace_BuiltIn()
        {
            var (go, target, ctrl) = Build<PlainMirror>();
            yield return null;

            AssertBoundToBuiltIn(ctrl);
            Assert.IsFalse(target.loadWithNameCalled,
                "Binding must not have invoked the target's colliding method.");

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// A hiding declaration removes the base method from GetMethods entirely, so merely skipping
        /// it would leave nothing registered under that name.
        /// </summary>
        [UnityTest]
        public IEnumerator MirrorHidingBuiltIn_IsIgnored_AndBuiltInSurvives()
        {
            LogAssert.Expect(LogType.Warning, new Regex("reuses the name of a built-in"));

            var (go, _, ctrl) = Build<HidingMirror>();
            yield return null;

            AssertBoundToBuiltIn(ctrl);

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// An overload is not a hide: both declarations surface, and both used to be added under the
        /// same key, throwing ArgumentException out of Awake.
        /// </summary>
        [UnityTest]
        public IEnumerator MirrorOverloadingBuiltIn_IsIgnored_WithoutThrowing()
        {
            LogAssert.Expect(LogType.Warning, new Regex("reuses the name of a built-in"));

            var (go, _, ctrl) = Build<OverloadingMirror>();
            yield return null;

            AssertBoundToBuiltIn(ctrl);

            Object.Destroy(go);
            yield return null;
        }

        /// <summary>
        /// LoadData used to end with StartCoroutine(CallAfterDuration(DataLoaded, duration)); with the
        /// tween path gone it calls DataLoaded() directly. A subclass override must still fire once.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadData_InvokesDataLoaded_ExactlyOnce()
        {
            var (go, _, ctrl) = Build<DataLoadedCountingMirror>();
            yield return null;

            ctrl.LoadData(new ControllableData());

            Assert.AreEqual(1, ctrl.dataLoadedCount,
                "LoadData should invoke DataLoaded exactly once, synchronously.");

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
