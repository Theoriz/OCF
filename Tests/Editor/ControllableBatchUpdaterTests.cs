using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers the discovery behind `Theoriz ▸ OCF ▸ Update All Controllables`.
    ///
    /// The entry rewrites every script it reports, so what it reports is the whole safety of it: a
    /// package file picked up here would be overwritten in a folder the project does not own, and a
    /// non-mirror picked up here would be replaced by a generated mirror.
    /// </summary>
    public class ControllableBatchUpdaterTests
    {
        //The three packages are embedded under Assets/Plugins in this project, so the rule has to be
        //"a folder holding a package.json" rather than "not under Assets".
        [Test]
        public void ScriptsInsideAPackage_AreRecognised()
        {
            Assert.IsTrue(ControllableBatchUpdater.IsPackagePath(
                "Assets/Plugins/OCF/Editor/Scripts/ControllableGenerator.cs"));
            Assert.IsTrue(ControllableBatchUpdater.IsPackagePath(
                "Packages/com.unity.inputsystem/InputSystem/InputManager.cs"));
        }

        [Test]
        public void ScriptsInTheProjectsOwnAssets_AreNotAPackage()
        {
            Assert.IsFalse(ControllableBatchUpdater.IsPackagePath(
                "Assets/GenUIDemo/Scripts/TestScriptControllable.cs"));
            Assert.IsFalse(ControllableBatchUpdater.IsPackagePath(null));
        }

        [Test]
        public void TheDemoMirror_IsFound()
        {
            var paths = ControllableBatchUpdater.FindUpdatableMirrors()
                .Select(AssetDatabase.GetAssetPath)
                .ToList();

            Assert.IsTrue(paths.Any(p => p.EndsWith("/TestScriptControllable.cs")),
                "The demo's TestScriptControllable should be updatable.");
        }

        //Nothing shipped by a package is rewritten, and nothing that is not a mirror.
        [Test]
        public void OnlyProjectOwnedMirrors_AreFound()
        {
            foreach (var script in ControllableBatchUpdater.FindUpdatableMirrors())
            {
                string path = AssetDatabase.GetAssetPath(script);

                Assert.IsFalse(ControllableBatchUpdater.IsPackagePath(path), path);
                Assert.IsTrue(ControllableGenerator.IsMirrorType(script.GetClass()), path);
                Assert.IsTrue(ControllableGenerator.IsMirrorName(script.GetClass().Name), path);
            }
        }
    }
}
