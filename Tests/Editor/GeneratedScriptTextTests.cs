using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers <see cref="ControllableGenerator.BuildScriptText"/>: the text a generated mirror file is
    /// made of. A mirror is compiled by Unity the moment it is written, so a defect in this shape does
    /// not surface as a failed assertion but as a compile error in the user's project - which is why
    /// the pure text builder is separated from the Editor work around it and pinned here.
    ///
    /// The namespace is the part worth guarding: the mirror follows the namespace of the script it
    /// mirrors, and wrapping it means indenting a body that is already written with its own
    /// indentation.
    /// </summary>
    public class GeneratedScriptTextTests
    {
        const string MirrorName = "MyScriptControllable";

        // What ExtractOCFExposedMembers emits for a single exposed field: already indented one level,
        // and blank-line separated.
        const string Members = "    [OCFProperty]\r\n    public float speed;\r\n\r\n";

        [Test]
        public void NoNamespace_LeavesTheClassAtTopLevel()
        {
            string text = ControllableGenerator.BuildScriptText(MirrorName, null, Members);

            StringAssert.DoesNotContain("namespace", text);
            StringAssert.Contains($"using UnityEngine;\r\n\r\npublic class {MirrorName} : Controllable\r\n{{\r\n", text);
            StringAssert.Contains("\r\n    [OCFProperty]\r\n    public float speed;\r\n", text);
        }

        //A type in the global namespace reports its namespace as null, but nothing stops a caller from
        //passing the empty string, and an emitted `namespace ` would not compile.
        [Test]
        public void EmptyNamespace_IsTreatedAsNoNamespace()
        {
            string withNull = ControllableGenerator.BuildScriptText(MirrorName, null, Members);
            string withEmpty = ControllableGenerator.BuildScriptText(MirrorName, "", Members);

            Assert.AreEqual(withNull, withEmpty);
        }

        [Test]
        public void Namespace_WrapsTheClassAndKeepsTheUsingOutside()
        {
            string text = ControllableGenerator.BuildScriptText(MirrorName, "My.Game", Members);

            StringAssert.StartsWith("using UnityEngine;\r\n\r\nnamespace My.Game\r\n{\r\n", text);
            StringAssert.Contains($"\r\n    public class {MirrorName} : Controllable\r\n    {{\r\n", text);
            StringAssert.EndsWith("    }\r\n}\r\n", text);
        }

        //Wrapping indents the class by one level, members included - the body arrives already indented
        //and has to end up one level deeper, not left where it was.
        [Test]
        public void Namespace_IndentsTheMembersWithTheClass()
        {
            string text = ControllableGenerator.BuildScriptText(MirrorName, "My.Game", Members);

            StringAssert.Contains("\r\n        [OCFProperty]\r\n        public float speed;\r\n", text);
        }

        //Indenting line by line must skip the blank ones: trailing whitespace is not a compile error,
        //so nothing else would report it.
        [Test]
        public void Namespace_LeavesBlankLinesBlank()
        {
            string text = ControllableGenerator.BuildScriptText(MirrorName, "My.Game", Members);

            foreach (string line in text.Split('\n'))
            {
                string content = line.TrimEnd('\r');
                Assert.AreEqual(content.TrimEnd(), content, $"'{content}' has trailing whitespace.");
            }
        }

        //The file is written to disk as-is, so both paths have to come out with the CRLF endings the
        //rest of the package uses - wrapping rebuilds the text line by line and could drop them.
        [Test]
        public void EveryLineEnding_IsWindowsStyle()
        {
            foreach (string ns in new[] { null, "My.Game" })
            {
                string text = ControllableGenerator.BuildScriptText(MirrorName, ns, Members);

                StringAssert.DoesNotContain("\n", text.Replace("\r\n", ""));
            }
        }
    }
}
