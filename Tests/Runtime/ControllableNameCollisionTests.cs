using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests
{
    /// <summary>
    /// Target script declaring a method whose name collides with one of Controllable's own
    /// [OSCMethod] members. LoadWithName is used rather than Save because it is not gated by
    /// usePresets, so the fixture can keep presets off and avoid filesystem I/O.
    /// </summary>
    public class CollidingTarget : MonoBehaviour
    {
        public bool loadWithNameCalled;

        public void LoadWithName(string fileName, float duration, string tweenStyle)
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
        public new void LoadWithName(string fileName, float duration, string tweenStyle)
        {
        }
    }

    /// <summary>Mirror whose method overloads Controllable's built-in (different signature).</summary>
    public class OverloadingMirror : Controllable
    {
        [OSCMethod]
        public void LoadWithName(int slot)
        {
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
            // Build inactive so TargetScript can be wired before Controllable.Awake() binds.
            var go = new GameObject("collision-test");
            go.SetActive(false);

            var target = go.AddComponent<CollidingTarget>();

            var ctrl = go.AddComponent<T>();
            ctrl.TargetScript = target;
            ctrl.usePresets = false; // avoid preset filesystem I/O; LoadWithName is registered regardless

            go.SetActive(true); // Awake() runs here
            return (go, target, ctrl);
        }

        static void AssertBoundToBuiltIn(Controllable ctrl)
        {
            Assert.IsTrue(ctrl.Methods.ContainsKey("LoadWithName"),
                "Controllable's built-in LoadWithName should always be registered.");

            var info = ctrl.Methods["LoadWithName"];

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

        [TearDown]
        public void TearDown()
        {
            // Controllable registration is static and survives between tests.
            ControllableMaster.RegisteredControllables.Clear();
        }
    }
}
