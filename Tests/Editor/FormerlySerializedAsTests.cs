using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.Serialization;

namespace Theoriz.OCF.Tests
{
    /// <summary>
    /// Guards the 2.0.0 rename, where every public member Controllable declares took a
    /// <c>controllable</c> prefix. Renaming a serialized field without
    /// <see cref="FormerlySerializedAsAttribute"/> does not fail to compile and does not warn: Unity
    /// simply cannot find the old key and leaves the field at its default. For
    /// <c>controllableTargetScript</c> that means every Controllable in every user project comes back
    /// unlinked, in a scene the user only has to re-save to lose permanently.
    ///
    /// These attributes are removed in 3.0.0, at which point this fixture goes with them.
    /// </summary>
    public class FormerlySerializedAsTests
    {
        /// <summary>Every serialized field the prefix renamed, as new name → the name it had in 1.x.</summary>
        static readonly object[] RenamedFields =
        {
            new object[] { "controllableTargetScript",     "TargetScript" },
            new object[] { "controllableBarColor",         "BarColor" },
            new object[] { "controllableId",               "id" },
            new object[] { "controllableDebug",            "debug" },
            new object[] { "controllableFolder",           "folder" },
            new object[] { "controllableTargetDirectory",  "targetDirectory" },
            new object[] { "controllableSourceScene",      "sourceScene" },
            new object[] { "controllableUsePanel",         "usePanel" },
            new object[] { "controllableUsePresets",       "usePresets" },
            new object[] { "controllableClosePanelAtStart","closePanelAtStart" },
            new object[] { "controllableCurrentPreset",    "currentPreset" },
            new object[] { "controllablePresetList",       "presetList" },
        };

        [TestCaseSource(nameof(RenamedFields))]
        public void EveryRenamedField_CarriesFormerlySerializedAs(string current, string legacy)
        {
            var field = typeof(Controllable).GetField(current,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(field,
                "Controllable no longer declares '" + current + "'. If it was renamed again, "
                + "update this table and add a [FormerlySerializedAs] for every old spelling.");

            var names = field.GetCustomAttributes<FormerlySerializedAsAttribute>()
                .Select(a => a.oldName)
                .ToArray();

            CollectionAssert.Contains(names, legacy,
                "'" + current + "' is missing [FormerlySerializedAs(\"" + legacy
                + "\")]. Every scene and prefab saved before 2.0.0 stores it under the old name, and "
                + "without the attribute Unity silently resets it to its default on load.");
        }

        /// <summary>
        /// The table above is only as good as its coverage. Every serialized field on Controllable
        /// that carries the prefix must appear in it, so adding a prefixed field later without a
        /// migration entry fails here rather than in a user's scene.
        /// </summary>
        [Test]
        public void TheRenameTable_CoversEveryPrefixedSerializedField()
        {
            var serialized = typeof(Controllable)
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => f.DeclaringType == typeof(Controllable))
                .Where(f => f.GetCustomAttribute<System.NonSerializedAttribute>() == null)
                .Where(f => f.Name.StartsWith("controllable"))
                .Select(f => f.Name);

            var tabled = RenamedFields.Cast<object[]>().Select(r => (string)r[0]);

            CollectionAssert.AreEquivalent(tabled, serialized,
                "Controllable's serialized prefixed fields and the migration table have drifted apart. "
                + "A field here without a [FormerlySerializedAs] entry loses its value on upgrade.");
        }
    }
}
