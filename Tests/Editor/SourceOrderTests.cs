using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers <see cref="ControllableGenerator.DeclarationIndex"/>, the source-text match the generator
    /// sorts reflected members by so the mirror declares them in the order the script does.
    ///
    /// Reflection returns fields and properties in separate blocks, so without this a property declared
    /// between two fields is emitted before both - which reorders the panel and moves [Header]s.
    /// </summary>
    public class SourceOrderTests
    {
        const string Source = @"
public class Fixture : MonoBehaviour
{
    [OCFExposed] public float a;
    [OCFExposed] public bool b => a > 0f;
    [OCFExposed] public int c = 3;

    [OCFExposed]
    public void DoIt()
    {
        a = 1f;
    }
}
";

        //The regression case: a property declared between two fields keeps its place.
        [Test]
        public void MembersSort_InDeclarationOrder()
        {
            int a = ControllableGenerator.DeclarationIndex(Source, "a");
            int b = ControllableGenerator.DeclarationIndex(Source, "b");
            int c = ControllableGenerator.DeclarationIndex(Source, "c");
            int method = ControllableGenerator.DeclarationIndex(Source, "DoIt");

            Assert.Less(a, b);
            Assert.Less(b, c);
            Assert.Less(c, method);
        }

        //A member the text does not match sorts last rather than first, so members from another part of
        //a partial class keep the reflection order at the end instead of jumping to the top.
        [Test]
        public void AnUnmatchedName_SortsLast()
        {
            Assert.AreEqual(int.MaxValue, ControllableGenerator.DeclarationIndex(Source, "elsewhere"));
        }

        //A mention in a comment or a string must not win over the declaration below it.
        [Test]
        public void AMentionThatIsNotADeclaration_DoesNotMatch()
        {
            const string text = @"
    // speed is the interesting one
    [Tooltip(""speed"")] public float other;
    public float speed;
";
            int speed = ControllableGenerator.DeclarationIndex(text, "speed");
            int other = ControllableGenerator.DeclarationIndex(text, "other");

            Assert.Greater(speed, other);
        }

        //A longer name starting with the same characters is a different member.
        [Test]
        public void APrefixOfAnotherName_DoesNotMatchIt()
        {
            const string text = "public float speedMax;\npublic float speed;";

            Assert.AreEqual(text.IndexOf("speed;"), ControllableGenerator.DeclarationIndex(text, "speed"));
        }
    }
}
