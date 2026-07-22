using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests
{
    /// <summary>
    /// PlayMode tests for ControllableMaster's teardown when it never connected.
    ///
    /// OSCReceiverName is a serialized field, so a ControllableMaster added from script has none
    /// until one is assigned. Every receiver lookup is keyed by it, and keying a dictionary by null
    /// throws - out of OnDisable, which would also skip the Zeroconf teardown that follows it. The
    /// test framework fails a test on an unhandled exception, so these are the guard.
    /// </summary>
    public class ControllableMasterLifecycleTests
    {
        [UnityTest]
        public IEnumerator DisablingAMasterWithNoReceiverName_DoesNotThrow()
        {
            var go = new GameObject("master-lifecycle-test");
            var master = go.AddComponent<ControllableMaster>();

            //Disabled before the first frame, so Start never runs and no socket is ever opened -
            //exactly the state a master is in when it is added and torn down in the same frame.
            master.enabled = false;
            yield return null;

            Object.DestroyImmediate(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingAMasterWithNoReceiverName_DoesNotThrow()
        {
            var go = new GameObject("master-lifecycle-test");
            go.AddComponent<ControllableMaster>();

            //Destroyed while still enabled: OnDisable runs as part of teardown rather than from a
            //deliberate disable, which is the other way into the same lookup.
            Object.DestroyImmediate(go);
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
