using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// EditMode tests pinning Controllable's name constants and reserved-name query to the members
    /// they describe.
    ///
    /// Consumers (notably GenUI's UIMaster.CleanGeneratedUI) group the preset buttons by matching
    /// these constants against the reflected method name, and ControllableGenerator refuses
    /// [OSCExposed] members whose name is reserved. A rename on one side and not the other would
    /// break either silently at runtime, so these tests exist to fail loudly instead.
    /// </summary>
    public class PresetMethodNamesTests
    {
        static string[] ParameterlessOSCMethodNames(Type type)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => Attribute.GetCustomAttribute(m, typeof(OSCMethod)) != null)
                .Where(m => m.GetParameters().Length == 0)
                .Select(m => m.Name)
                .ToArray();
        }

        [Test]
        public void PresetMethodNames_AllExist_OnControllable()
        {
            var declared = ParameterlessOSCMethodNames(typeof(Controllable));

            foreach (var name in Controllable.PresetMethodNames)
            {
                Assert.Contains(name, declared,
                    "Controllable.PresetMethodNames lists '" + name +
                    "', but no parameterless [OSCMethod] by that name exists on Controllable.");
            }
        }

        [Test]
        public void AllPresetMethodNames_AllExist_OnControllableMasterControllable()
        {
            var declared = ParameterlessOSCMethodNames(typeof(ControllableMasterControllable));

            foreach (var name in ControllableMasterControllable.AllPresetMethodNames)
            {
                Assert.Contains(name, declared,
                    "ControllableMasterControllable.AllPresetMethodNames lists '" + name +
                    "', but no parameterless [OSCMethod] by that name exists on that class.");
            }
        }

        /// <summary>
        /// The two sets must stay disjoint: CleanGeneratedUI checks the per-controllable set first,
        /// so an overlapping name would land in PresetHolder and never reach AllPresetHolder.
        /// </summary>
        [Test]
        public void PresetMethodNameSets_DoNotOverlap()
        {
            CollectionAssert.IsEmpty(
                Controllable.PresetMethodNames.Intersect(ControllableMasterControllable.AllPresetMethodNames));
        }

        /// <summary>
        /// Guards the usePresets gate in Controllable.Awake, which skips exactly these methods
        /// when presets are off.
        /// </summary>
        [Test]
        public void PresetMethodNames_MatchesTheDocumentedSet()
        {
            CollectionAssert.AreEquivalent(
                new[] { "Save", "SaveAs", "Load", "Show" }, Controllable.PresetMethodNames);
        }

        [Test]
        public void IsReservedMemberName_CoversControllablesOwnMembers()
        {
            Assert.IsTrue(Controllable.IsReservedMemberName("Save"), "Save is an [OSCMethod] on Controllable.");
            Assert.IsTrue(Controllable.IsReservedMemberName("LoadWithName"),
                "LoadWithName is an [OSCMethod] on Controllable that PresetMethodNames does not list.");
            Assert.IsTrue(Controllable.IsReservedMemberName("id"),
                "id is a public field on Controllable, and the key ControllableMaster registers under.");
            Assert.IsTrue(Controllable.IsReservedMemberName("debug"), "debug is a public field on Controllable.");
        }

        /// <summary>
        /// Controllable is a MonoBehaviour, so Unity's own members are shadowable too and must be
        /// reserved: an [OSCExposed] field named 'name' would hide UnityEngine.Object.name.
        /// </summary>
        [Test]
        public void IsReservedMemberName_CoversInheritedUnityMembers()
        {
            Assert.IsTrue(Controllable.IsReservedMemberName("name"));
            Assert.IsTrue(Controllable.IsReservedMemberName("enabled"));
            Assert.IsTrue(Controllable.IsReservedMemberName("transform"));
        }

        [Test]
        public void IsReservedMemberName_IsFalse_ForNamesControllableDoesNotDeclare()
        {
            Assert.IsFalse(Controllable.IsReservedMemberName("myValue"));
            Assert.IsFalse(Controllable.IsReservedMemberName("RandomizeColor"));
        }

        /// <summary>
        /// The three name sets nest: preset methods are built-in [OSCMethod]s, and every built-in is
        /// a member Controllable declares. Pins them together so one can't drift from another.
        /// </summary>
        [Test]
        public void PresetMethodNames_AreASubset_OfTheReservedMembers()
        {
            foreach (var name in Controllable.PresetMethodNames)
                Assert.IsTrue(Controllable.IsReservedMemberName(name),
                    "'" + name + "' is a preset method but is not reported as a reserved member name.");
        }
    }
}
