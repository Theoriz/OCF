using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Theoriz.OCF.Tests
{
    /// <summary>
    /// Imports a prefab written in the pre-2.0.0 format and checks every field arrives under its new
    /// name. <see cref="FormerlySerializedAsTests"/> only proves the attributes are declared; this is
    /// the one that proves Unity actually applies them, which is what a user upgrading an existing
    /// project depends on.
    ///
    /// The prefab is generated here rather than checked in so it carries no hand-written .meta and
    /// cannot drift from Controllable's real script GUID.
    /// </summary>
    public class LegacySerializationMigrationTests
    {
        const string TestFolder = "Assets/__OCFLegacyMigrationTest__";
        const string PrefabPath = TestFolder + "/LegacyControllable.prefab";

        static string ControllableScriptGuid()
        {
            var path = AssetDatabase.FindAssets("Controllable t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("/Controllable.cs"));

            Assert.IsNotNull(path, "Could not locate Controllable.cs in the AssetDatabase.");
            return AssetDatabase.AssetPathToGUID(path);
        }

        /// <summary>
        /// A prefab holding one Controllable serialised with the 1.x field names. Every value is
        /// deliberately *not* the field's default, so a passing assert can only mean the value came
        /// out of the file; were they left at the defaults, a migration that silently dropped
        /// everything would still pass.
        ///
        /// 'hasPresets', 'BarColor', 'usePanel' and 'closePanelAtStart' are included even though the
        /// fields are gone in 2.0.0 - the first deleted, the last three moved to GenUI's
        /// GenUIPanelSettings, which no attribute can migrate to. An unknown key must be ignored on
        /// import, not throw.
        /// </summary>
        static string LegacyPrefabYaml(string scriptGuid)
        {
            return @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &1000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000}
  - component: {fileID: 114000}
  m_Layer: 0
  m_Name: LegacyControllable
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &114000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: " + scriptGuid + @", type: 3}
  m_Name:
  m_EditorClassIdentifier:
  TargetScript: {fileID: 114000}
  BarColor: {r: 0.25, g: 0.5, b: 0.75, a: 1}
  id: LegacyId
  folder: LegacyFolder
  debug: 1
  targetDirectory: /legacy/dir/
  sourceScene: LegacyScene
  usePanel: 0
  usePresets: 0
  hasPresets: 1
  closePanelAtStart: 0
  currentPreset: legacy.pst
  presetList:
  - legacy.pst
";
        }

        [SetUp]
        public void WriteLegacyPrefab()
        {
            Directory.CreateDirectory(TestFolder);
            File.WriteAllText(PrefabPath, LegacyPrefabYaml(ControllableScriptGuid()));
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void DeleteLegacyPrefab()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        static Controllable LoadMigrated()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "The generated legacy prefab failed to import.");

            var controllable = prefab.GetComponent<Controllable>();
            Assert.IsNotNull(controllable,
                "The legacy prefab imported without a Controllable - the script GUID is wrong.");

            return controllable;
        }

        /// <summary>
        /// The one that matters most. TargetScript is the link to the real script, and a missing
        /// [FormerlySerializedAs] would bring every Controllable in every user project back unlinked
        /// - in a scene the user only has to re-save to lose the wiring for good.
        /// </summary>
        [Test]
        public void TargetScript_SurvivesTheRename()
        {
            Assert.IsNotNull(LoadMigrated().controllableTargetScript,
                "controllableTargetScript came back null, so the object reference stored under "
                + "'TargetScript' was not migrated.");
        }

        [Test]
        public void EveryValueField_SurvivesTheRename()
        {
            var c = LoadMigrated();

            Assert.AreEqual("LegacyId", c.controllableId);
            Assert.AreEqual("LegacyFolder", c.controllableFolder);
            Assert.AreEqual("/legacy/dir/", c.controllableTargetDirectory);
            Assert.AreEqual("LegacyScene", c.controllableSourceScene);
            Assert.AreEqual("legacy.pst", c.controllableCurrentPreset);
            CollectionAssert.AreEqual(new[] { "legacy.pst" }, c.controllablePresetList);
        }

        /// <summary>
        /// Separated from the rest because booleans are where a failed migration hides: every one of
        /// these is stored as the opposite of its field initialiser, so a field left at its default
        /// reads as the value the file did not contain.
        /// </summary>
        [Test]
        public void BooleanFields_KeepTheirStoredValue_NotTheirDefault()
        {
            var c = LoadMigrated();

            Assert.IsTrue(c.controllableDebug, "debug was stored as true; the field defaults to false.");
            Assert.IsFalse(c.controllableUsePresets, "usePresets was stored as false; the field defaults to true.");
        }
    }
}
