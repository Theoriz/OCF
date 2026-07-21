using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// Covers the serialization of the queue that carries "Add Controllable" requests across the
    /// domain reload triggered by generating a mirror script.
    ///
    /// This is the whole of what a test can reach: the resume itself needs a real reload, so the
    /// behaviour that matters - completing the add, and never retrying a failed one - is verified by
    /// hand in the Editor. Do not read a green run here as the feature being covered.
    /// </summary>
    public class PendingControllableAddsTests
    {
        // A realistic GlobalObjectId string: hyphens and hex only, which is why a newline separates entries.
        const string IdA = "GlobalObjectId_V1-2-6b8a2f1c9e4d5a3b7c0d1e2f3a4b5c6d-1234567890123456789-0";
        const string IdB = "GlobalObjectId_V1-2-aabbccddeeff00112233445566778899-9876543210987654321-0";

        [Test]
        public void RoundTrip_PreservesEveryIdAndOrder()
        {
            var ids = new[] { IdA, IdB };

            var result = PendingControllableAdds.Deserialize(PendingControllableAdds.Serialize(ids));

            CollectionAssert.AreEqual(ids, result);
        }

        [Test]
        public void SingleId_RoundTrips()
        {
            var result = PendingControllableAdds.Deserialize(PendingControllableAdds.Serialize(new[] { IdA }));

            CollectionAssert.AreEqual(new[] { IdA }, result);
        }

        [Test]
        public void EmptyAndNullInputs_GiveAnEmptyQueue()
        {
            Assert.IsEmpty(PendingControllableAdds.Deserialize(""));
            Assert.IsEmpty(PendingControllableAdds.Deserialize(null));
            Assert.AreEqual("", PendingControllableAdds.Serialize(null));
            Assert.AreEqual("", PendingControllableAdds.Serialize(new string[0]));
        }

        /// <summary>
        /// SessionState round-trips through string storage, so blank and trailing lines are worth
        /// tolerating rather than turning into empty entries that fail to parse later.
        /// </summary>
        [Test]
        public void BlankAndTrailingLines_AreIgnored()
        {
            var result = PendingControllableAdds.Deserialize("\n" + IdA + "\n\n" + IdB + "\n");

            CollectionAssert.AreEqual(new[] { IdA, IdB }, result);
        }

        [Test]
        public void EmptyEntries_AreNotSerialized()
        {
            var result = PendingControllableAdds.Serialize(new[] { IdA, "", null, IdB });

            CollectionAssert.AreEqual(new[] { IdA, IdB }, PendingControllableAdds.Deserialize(result));
        }
    }
}
