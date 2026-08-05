using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers the naming rule that pairs a mirror with the script it mirrors:
    /// <see cref="ControllableGenerator.IsMirrorName"/> and
    /// <see cref="ControllableGenerator.SourceNameFor"/>.
    ///
    /// Both menus read it - the Project-window entries decide between Generate and Update on it, and
    /// `Update Controllable` uses it to work from either end of the pair. Getting it wrong writes a
    /// file under the wrong name rather than failing, which is why the edges are pinned here.
    /// </summary>
    public class MirrorNamingTests
    {
        [Test]
        public void MirrorName_ReportsTheSourceName()
        {
            Assert.IsTrue(ControllableGenerator.IsMirrorName("PlayerControllable"));
            Assert.AreEqual("Player", ControllableGenerator.SourceNameFor("PlayerControllable"));
        }

        //A source script maps to itself, which is what lets a caller holding either end ask without
        //checking which one it has.
        [Test]
        public void SourceName_MapsToItself()
        {
            Assert.IsFalse(ControllableGenerator.IsMirrorName("Player"));
            Assert.AreEqual("Player", ControllableGenerator.SourceNameFor("Player"));
        }

        //The suffix on its own leaves no source name; stripping it would give "" and generate a file
        //called "Controllable.cs" over the base class.
        [Test]
        public void TheSuffixAlone_IsNotAMirrorName()
        {
            Assert.IsFalse(ControllableGenerator.IsMirrorName(ControllableGenerator.MirrorSuffix));
            Assert.AreEqual(ControllableGenerator.MirrorSuffix,
                ControllableGenerator.SourceNameFor(ControllableGenerator.MirrorSuffix));
        }

        //The suffix has to end the name: a script called ControllableSettings is not a mirror of
        //"Settings".
        [Test]
        public void TheSuffixElsewhereInTheName_IsNotAMirrorName()
        {
            Assert.IsFalse(ControllableGenerator.IsMirrorName("ControllableSettings"));
            Assert.AreEqual("ControllableSettings", ControllableGenerator.SourceNameFor("ControllableSettings"));
        }

        [Test]
        public void NoName_IsNotAMirrorName()
        {
            Assert.IsFalse(ControllableGenerator.IsMirrorName(null));
            Assert.IsFalse(ControllableGenerator.IsMirrorName(""));
        }

        //Round trip: appending the suffix to a reported source name gets the mirror's name back, which
        //is the assumption Update Controllable makes when it pairs the two.
        [Test]
        public void SourceName_PlusTheSuffix_IsTheMirrorName()
        {
            const string mirror = "TestScriptControllable";

            string rebuilt = ControllableGenerator.SourceNameFor(mirror) + ControllableGenerator.MirrorSuffix;

            Assert.AreEqual(mirror, rebuilt);
        }
    }
}
