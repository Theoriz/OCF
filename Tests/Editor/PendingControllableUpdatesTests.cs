using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers the serialization of the queue that carries "regenerate this repaired mirror" requests
    /// across the domain reload that emptying a mirror triggers.
    ///
    /// This is the whole of what a test can reach: the resume itself needs a real reload, so the
    /// behaviour that matters - completing the regeneration, and never retrying a failed one - is
    /// verified by hand in the Editor. Do not read a green run here as the feature being covered.
    /// </summary>
    public class PendingControllableUpdatesTests
    {
        const string EntryA = "Assets/Scripts/PlayerControllable.cs|Player";
        const string EntryB = "Assets/Scripts/EnemyControllable.cs|Enemy";

        [Test]
        public void RoundTrip_PreservesEveryEntryAndOrder()
        {
            var entries = new[] { EntryA, EntryB };

            var result = PendingControllableUpdates.Deserialize(PendingControllableUpdates.Serialize(entries));

            CollectionAssert.AreEqual(entries, result);
        }

        [Test]
        public void EmptyAndNullInputs_GiveAnEmptyQueue()
        {
            Assert.IsEmpty(PendingControllableUpdates.Deserialize(""));
            Assert.IsEmpty(PendingControllableUpdates.Deserialize(null));
            Assert.AreEqual("", PendingControllableUpdates.Serialize(null));
            Assert.AreEqual("", PendingControllableUpdates.Serialize(new string[0]));
        }

        /// <summary>
        /// SessionState round-trips through string storage, so blank and trailing lines are worth
        /// tolerating rather than turning into empty entries that fail to parse later.
        /// </summary>
        [Test]
        public void BlankAndTrailingLines_AreIgnored()
        {
            var result = PendingControllableUpdates.Deserialize("\n" + EntryA + "\n\n" + EntryB + "\n");

            CollectionAssert.AreEqual(new[] { EntryA, EntryB }, result);
        }

        [Test]
        public void AnEntry_ReportsItsPathAndSourceName()
        {
            Assert.AreEqual("Assets/Scripts/PlayerControllable.cs", PendingControllableUpdates.PathOf(EntryA));
            Assert.AreEqual("Player", PendingControllableUpdates.SourceNameOf(EntryA));
        }

        //A source script name is an identifier, so the separator is split on from the right and a path
        //containing one still reads back whole.
        [Test]
        public void APathContainingTheSeparator_StillReadsBack()
        {
            const string entry = "Assets/Odd|Folder/PlayerControllable.cs|Player";

            Assert.AreEqual("Assets/Odd|Folder/PlayerControllable.cs", PendingControllableUpdates.PathOf(entry));
            Assert.AreEqual("Player", PendingControllableUpdates.SourceNameOf(entry));
        }

        //An entry that lost its source name reports none, so the resume reports it instead of
        //regenerating from an empty type name.
        [Test]
        public void AnEntryWithNoSeparator_ReportsNoSourceName()
        {
            Assert.IsNull(PendingControllableUpdates.SourceNameOf("Assets/Scripts/PlayerControllable.cs"));
            Assert.IsNull(PendingControllableUpdates.SourceNameOf(null));
        }
    }
}
