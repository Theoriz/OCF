using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// EditMode tests pinning the preset method-name constants to the methods they name.
    ///
    /// Consumers (notably GenUI's UIMaster.CleanGeneratedUI) group the preset buttons by matching
    /// these constants against the reflected method name. A rename on one side and not the other
    /// would break that grouping silently at runtime, so these tests exist to fail loudly instead.
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
    }
}
