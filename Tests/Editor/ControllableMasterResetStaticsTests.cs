using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// EditMode tests pinning what ControllableMaster.ResetStatics clears.
    ///
    /// With "Enter Play Mode Options ▸ Reload Domain" turned off, static state survives between play
    /// sessions. Everything ResetStatics misses therefore leaks: a stale subscriber on the two static
    /// events keeps receiving callbacks after the object that registered it is gone. GenUI's UIMaster
    /// unsubscribes in OnDestroy and so is safe either way, but a Controllable in a user's project can
    /// subscribe too, and nothing makes it as careful — so the events are cleared at the source.
    ///
    /// Dropping a line from ResetStatics fails nothing else, and only on the second play session, in
    /// a configuration not everyone enables. These tests exist to fail loudly instead.
    /// </summary>
    public class ControllableMasterResetStaticsTests
    {
        static void InvokeResetStatics()
        {
            var method = typeof(ControllableMaster)
                .GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method,
                "ControllableMaster.ResetStatics is gone. Unity calls it by attribute, so nothing " +
                "else references it by name and its removal is silent.");

            method.Invoke(null, null);
        }

        /// <summary>
        /// A field-like event is a private static delegate field of the same name, which is the only
        /// way to observe from outside the declaring type whether it still holds subscribers.
        /// </summary>
        static object EventBackingField(string name)
        {
            var field = typeof(ControllableMaster)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "ControllableMaster has no static event named '" + name + "'.");

            return field.GetValue(null);
        }

        [Test]
        public void ResetStatics_ClearsControllableAdded()
        {
            ControllableMaster.controllableAdded += OnAdded;

            InvokeResetStatics();

            Assert.IsNull(EventBackingField("controllableAdded"),
                "A subscriber survived ResetStatics and would keep firing in the next play session.");
        }

        [Test]
        public void ResetStatics_ClearsControllableRemoved()
        {
            ControllableMaster.controllableRemoved += OnRemoved;

            InvokeResetStatics();

            Assert.IsNull(EventBackingField("controllableRemoved"),
                "A subscriber survived ResetStatics and would keep firing in the next play session.");
        }

        /// <summary>
        /// The registry and the cached preset root were already cleared before the events were, and
        /// the preset root especially matters: controllables read presets from OnEnable, which can
        /// precede ControllableMaster.Awake.
        /// </summary>
        [Test]
        public void ResetStatics_ClearsRegistryAndInstance()
        {
            InvokeResetStatics();

            Assert.IsEmpty(ControllableMaster.RegisteredControllables);
            Assert.IsNull(ControllableMaster.instance);
        }

        [Test]
        public void ResetStatics_RunsBeforeAnySceneObjectAwakes()
        {
            var method = typeof(ControllableMaster)
                .GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);

            var attribute = (RuntimeInitializeOnLoadMethodAttribute)
                Attribute.GetCustomAttribute(method, typeof(RuntimeInitializeOnLoadMethodAttribute));

            Assert.IsNotNull(attribute, "Unity only calls ResetStatics because of this attribute.");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType,
                "Subscribers register from Awake, so the clear has to happen before that.");
        }

        static void OnAdded(Controllable controllable) { }

        static void OnRemoved(Controllable controllable) { }
    }
}
