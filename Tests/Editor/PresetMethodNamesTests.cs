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
    /// [OCFExposed] members whose name is reserved. A rename on one side and not the other would
    /// break either silently at runtime, so these tests exist to fail loudly instead.
    /// </summary>
    public class PresetMethodNamesTests
    {
        static string[] ParameterlessOCFMethodNames(Type type)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => Attribute.GetCustomAttribute(m, typeof(OCFMethod)) != null)
                .Where(m => m.GetParameters().Length == 0)
                .Select(m => m.Name)
                .ToArray();
        }

        [Test]
        public void PresetMethodNames_AllExist_OnControllable()
        {
            var declared = ParameterlessOCFMethodNames(typeof(Controllable));

            foreach (var name in Controllable.PresetMethodNames)
            {
                Assert.Contains(name, declared,
                    "Controllable.PresetMethodNames lists '" + name +
                    "', but no parameterless [OCFMethod] by that name exists on Controllable.");
            }
        }

        [Test]
        public void AllPresetMethodNames_AllExist_OnControllableMasterControllable()
        {
            var declared = ParameterlessOCFMethodNames(typeof(ControllableMasterControllable));

            foreach (var name in ControllableMasterControllable.AllPresetMethodNames)
            {
                Assert.Contains(name, declared,
                    "ControllableMasterControllable.AllPresetMethodNames lists '" + name +
                    "', but no parameterless [OCFMethod] by that name exists on that class.");
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
                new[] { "ControllableSave", "ControllableSaveAs", "ControllableLoad", "ControllableShow" },
                Controllable.PresetMethodNames);
        }

        /// <summary>
        /// Dropping a name from either global set does not fail anywhere else — the button simply
        /// stops being reparented into its row at the top of the panel and reappears in the body,
        /// which is easy to miss.
        /// </summary>
        [Test]
        public void GlobalMethodNames_MatchTheDocumentedSets()
        {
            CollectionAssert.AreEquivalent(
                new[] { "ControllableSaveAll", "ControllableSaveAsAll", "ControllableLoadAll" },
                ControllableMasterControllable.AllPresetMethodNames);

            CollectionAssert.AreEquivalent(
                new[] { "ControllableOpenPresetsFolder" },
                ControllableMasterControllable.GlobalActionMethodNames);
        }

        [Test]
        public void GlobalActionMethodNames_AllExist_OnControllableMasterControllable()
        {
            var declared = ParameterlessOCFMethodNames(typeof(ControllableMasterControllable));

            foreach (var name in ControllableMasterControllable.GlobalActionMethodNames)
            {
                Assert.Contains(name, declared,
                    "ControllableMasterControllable.GlobalActionMethodNames lists '" + name +
                    "', but no parameterless [OCFMethod] by that name exists on that class.");
            }
        }

        /// <summary>
        /// CleanGeneratedUI tests each set in turn and reparents on every match, so a name in both
        /// would be moved twice and land in whichever row is checked last.
        /// </summary>
        [Test]
        public void GlobalMethodNameSets_DoNotOverlap()
        {
            CollectionAssert.IsEmpty(
                ControllableMasterControllable.AllPresetMethodNames
                    .Intersect(ControllableMasterControllable.GlobalActionMethodNames));

            CollectionAssert.IsEmpty(
                Controllable.PresetMethodNames
                    .Intersect(ControllableMasterControllable.GlobalActionMethodNames));
        }

        [Test]
        public void IsReservedMemberName_CoversControllablesOwnMembers()
        {
            Assert.IsTrue(Controllable.IsReservedMemberName("ControllableSave"),
                "ControllableSave is an [OCFMethod] on Controllable.");
            Assert.IsTrue(Controllable.IsReservedMemberName("ControllableLoadWithName"),
                "ControllableLoadWithName is an [OCFMethod] on Controllable that PresetMethodNames does not list.");
            Assert.IsTrue(Controllable.IsReservedMemberName("controllableId"),
                "controllableId is a public field on Controllable, and the key ControllableMaster registers under.");
            Assert.IsTrue(Controllable.IsReservedMemberName("controllableDebug"),
                "controllableDebug is a public field on Controllable.");
        }

        /// <summary>
        /// The whole point of the prefix: the unprefixed spellings are the names users most often want
        /// to expose, and every one of them used to be refused by the generator. If any of these comes
        /// back true, a member was missed by the rename and is still occupying the name.
        /// </summary>
        [Test]
        public void IsReservedMemberName_IsFalse_ForTheNamesThePrefixFreed()
        {
            foreach (var freed in new[]
            {
                "id", "debug", "folder", "targetDirectory", "sourceScene",
                "usePanel", "usePresets", "hasPresets", "closePanelAtStart",
                "BarColor", "TargetScript", "currentPreset", "presetList",
                "Fields", "TargetFields", "Methods", "PreviousFieldsValues",
                "uiValueChanged", "scriptValueChanged",
                "Save", "SaveAs", "Load", "Show", "LoadWithName",
            })
            {
                Assert.IsFalse(Controllable.IsReservedMemberName(freed),
                    "'" + freed + "' should have been freed by the controllable prefix, but Controllable "
                    + "still declares a member by that name.");
            }
        }

        /// <summary>
        /// Controllable is a MonoBehaviour, so Unity's own members are shadowable too and must be
        /// reserved: an [OCFExposed] field named 'name' would hide UnityEngine.Object.name.
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
        /// The three name sets nest: preset methods are built-in [OCFMethod]s, and every built-in is
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
