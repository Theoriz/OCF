using NUnit.Framework;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers <see cref="ControllableMaster.ResolvePresetRoot"/>: which layer wins, and the shape of
    /// the path it returns. The trailing slash is the part worth guarding — every consumer builds
    /// file paths as <c>targetDirectory + fileName</c>, so losing it corrupts every preset path
    /// silently rather than failing loudly.
    ///
    /// The file IO around the resolver is deliberately not covered here; the bad-path fallback is an
    /// Editor/build check.
    /// </summary>
    public class PresetRootResolverTests
    {
        const string ArgPath = "C:/Shows/VenueA/Presets";
        const string InspectorPath = "D:/Configured/Presets";

        static string[] ArgsWith(string path)
        {
            return new[] { "MyApp.exe", ControllableMaster.PresetsPathArgument, path };
        }

        [Test]
        public void CommandLineArgument_BeatsInspectorField()
        {
            var root = ControllableMaster.ResolvePresetRoot(ArgsWith(ArgPath), InspectorPath, false);

            Assert.AreEqual(ArgPath + "/", root);
        }

        [Test]
        public void InspectorField_BeatsTheDefault()
        {
            var root = ControllableMaster.ResolvePresetRoot(null, InspectorPath, false);

            Assert.AreEqual(InspectorPath + "/", root);
        }

        [Test]
        public void NoOverride_FallsBackToTheDefault()
        {
            var appFolder = ControllableMaster.ResolvePresetRoot(null, "", false);
            var documents = ControllableMaster.ResolvePresetRoot(null, "", true);

            Assert.AreNotEqual(appFolder, documents, "useDocumentsDirectory must still select a different root.");
            StringAssert.EndsWith("/Presets/", appFolder);
            StringAssert.Contains("/Presets/", documents);
        }

        [Test]
        public void EveryResult_EndsWithASingleSlash()
        {
            var results = new[]
            {
                ControllableMaster.ResolvePresetRoot(ArgsWith("C:/Trailing/"), "", false),
                ControllableMaster.ResolvePresetRoot(null, "D:/Trailing/", false),
                ControllableMaster.ResolvePresetRoot(null, "", false),
                ControllableMaster.ResolvePresetRoot(null, "", true),
            };

            foreach (var root in results)
            {
                StringAssert.EndsWith("/", root);
                Assert.IsFalse(root.EndsWith("//"), "'" + root + "' should not end with a doubled slash.");
            }
        }

        [Test]
        public void BackslashSeparators_AreNormalised()
        {
            var root = ControllableMaster.ResolvePresetRoot(null, @"C:\Shows\VenueA\Presets", false);

            Assert.AreEqual("C:/Shows/VenueA/Presets/", root);
        }

        [Test]
        public void PathWithSpaces_IsKept()
        {
            var root = ControllableMaster.ResolvePresetRoot(ArgsWith("C:/My Shows/Venue A/Presets"), "", false);

            Assert.AreEqual("C:/My Shows/Venue A/Presets/", root);
        }

        [Test]
        public void SurroundingWhitespace_IsTrimmed()
        {
            var root = ControllableMaster.ResolvePresetRoot(null, "  " + InspectorPath + "  ", false);

            Assert.AreEqual(InspectorPath + "/", root);
        }

        [Test]
        public void RelativePath_IsRejectedAndFallsBackToTheDefault()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Presets/VenueA"));

            var root = ControllableMaster.ResolvePresetRoot(null, "Presets/VenueA", false);

            Assert.AreEqual(ControllableMaster.ResolvePresetRoot(null, "", false), root);
        }

        [Test]
        public void ArgumentWithNoValue_IsIgnored()
        {
            var args = new[] { "MyApp.exe", ControllableMaster.PresetsPathArgument };

            var root = ControllableMaster.ResolvePresetRoot(args, InspectorPath, false);

            Assert.AreEqual(InspectorPath + "/", root, "A dangling argument must not swallow the inspector field.");
        }

        [Test]
        public void MissingOrEmptyArguments_AreIgnored()
        {
            var unrelated = new[] { "MyApp.exe", "-someOtherFlag", "value" };

            Assert.AreEqual(InspectorPath + "/", ControllableMaster.ResolvePresetRoot(unrelated, InspectorPath, false));
            Assert.AreEqual(InspectorPath + "/", ControllableMaster.ResolvePresetRoot(new string[0], InspectorPath, false));
            Assert.AreEqual(InspectorPath + "/", ControllableMaster.ResolvePresetRoot(null, InspectorPath, false));
        }
    }
}
