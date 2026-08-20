using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers <see cref="ControllableGenerator.MirrorClassNameFromText"/> and
    /// <see cref="ControllableGenerator.NamespaceFromText"/>: reading a mirror out of its own source
    /// text rather than out of the compiled type.
    ///
    /// This is what keeps `Update Controllable Script` reachable when it is needed most. A mirror
    /// still referencing a renamed member breaks its whole assembly, and the menu validators used to
    /// answer from <c>MonoScript.GetClass()</c> - which can then be null, hiding Update on exactly the
    /// file that has to be repaired. These readings must therefore work on text that does not compile.
    /// </summary>
    public class MirrorTextReadingTests
    {
        //A mirror whose body no longer compiles: the source script's `speed` was renamed, so the
        //emitted poll references a member that is gone. The reason this class exists.
        const string BrokenMirror =
            "using UnityEngine;\r\n" +
            "\r\n" +
            "public class MyScriptControllable : Controllable\r\n" +
            "{\r\n" +
            "    [OCFProperty]\r\n" +
            "    public float speed;\r\n" +
            "\r\n" +
            "    protected override void PollTargetScript()\r\n" +
            "    {\r\n" +
            "        var target = controllableTargetScript as MyScript;\r\n" +
            "        if (target == null) return;\r\n" +
            "\r\n" +
            "        if (speed != target.speed) { speed = target.speed; RaiseScriptValueChanged(\"speed\"); }\r\n" +
            "    }\r\n" +
            "}\r\n";

        [Test]
        public void AMirrorThatDoesNotCompile_StillReportsItsClassName()
        {
            Assert.AreEqual("MyScriptControllable", ControllableGenerator.MirrorClassNameFromText(BrokenMirror));
        }

        [Test]
        public void ANamespacedMirror_ReportsBothItsClassAndItsNamespace()
        {
            string text = ControllableGenerator.BuildScriptText("MyScriptControllable", "Game.Play", "");

            Assert.AreEqual("MyScriptControllable", ControllableGenerator.MirrorClassNameFromText(text));
            Assert.AreEqual("Game.Play", ControllableGenerator.NamespaceFromText(text));
        }

        [Test]
        public void AMirrorWithNoNamespace_ReportsNone()
        {
            Assert.IsNull(ControllableGenerator.NamespaceFromText(BrokenMirror));
        }

        //Generate is hidden on whatever this reports, so a source script must report nothing: the menu
        //would otherwise offer to write a FooControllableControllable.
        [Test]
        public void ASourceScript_IsNotAMirror()
        {
            const string source =
                "using UnityEngine;\r\n\r\npublic class MyScript : MonoBehaviour\r\n{\r\n"
                + "    [OCFExposed]\r\n    public float speed;\r\n}\r\n";

            Assert.IsNull(ControllableGenerator.MirrorClassNameFromText(source));
        }

        //The base class has to end the name, the same rule IsMirrorName follows: a script deriving from
        //ControllableSettings derives from something else entirely.
        [Test]
        public void ABaseTypeMerelyStartingWithTheSuffix_IsNotAMirror()
        {
            Assert.IsNull(ControllableGenerator.MirrorClassNameFromText(
                "public class MyScript : ControllableSettings\r\n{\r\n}\r\n"));
        }

        [Test]
        public void AMirrorImplementingInterfaces_IsStillAMirror()
        {
            Assert.AreEqual("MyScriptControllable", ControllableGenerator.MirrorClassNameFromText(
                "public class MyScriptControllable : Controllable, ISomething\r\n{\r\n}\r\n"));
        }

        [Test]
        public void NoText_ReportsNothing()
        {
            Assert.IsNull(ControllableGenerator.MirrorClassNameFromText(null));
            Assert.IsNull(ControllableGenerator.MirrorClassNameFromText(""));
            Assert.IsNull(ControllableGenerator.NamespaceFromText(null));
            Assert.IsNull(ControllableGenerator.NamespaceFromText(""));
        }

        //What repairing writes: an empty body, under the file's own name and namespace. It has to
        //compile whatever the source script now looks like, and it has to still read as a mirror so a
        //second Update finds it.
        [Test]
        public void AnEmptiedMirror_CompilesToAnEmptyClassAndStillReadsAsAMirror()
        {
            string repaired = ControllableGenerator.BuildScriptText(
                "MyScriptControllable",
                ControllableGenerator.NamespaceFromText(BrokenMirror),
                "");

            StringAssert.DoesNotContain("PollTargetScript", repaired);
            StringAssert.DoesNotContain("target", repaired);
            StringAssert.Contains("public class MyScriptControllable : Controllable\r\n{\r\n", repaired);
            Assert.AreEqual("MyScriptControllable", ControllableGenerator.MirrorClassNameFromText(repaired));
        }
    }
}
