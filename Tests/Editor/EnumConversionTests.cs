using System;
using NUnit.Framework;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// EditMode unit tests for TypeConverter.TryGetEnumValue, the single place OSC messages, presets
    /// and the dropdown all resolve an enum through.
    ///
    /// The enums here declare explicit, non-sequential values on purpose: the mechanism this replaced
    /// stored the member's *position* in the names array, so every one of those tests passed against
    /// a sequential enum and silently stored the wrong member against any other.
    /// </summary>
    public class EnumConversionTests
    {
        enum LightMode { None = 0, Spot = 5, Wash = 12 }

        enum ByteBacked : byte { Off = 0, On = 200 }

        [Flags]
        enum Channels { None = 0, Red = 1, Green = 2, Blue = 4 }

        [Test]
        public void Name_ResolvesToMember()
        {
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(LightMode), "Wash", out var result));
            Assert.AreEqual(LightMode.Wash, result);
        }

        [Test]
        public void Number_ResolvesToDeclaredValue_NotIndex()
        {
            //Wash is the third member but its declared value is 12. Resolving 12 must give Wash, and
            //resolving 2 - its index - must give nothing.
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(LightMode), 12, out var byValue));
            Assert.AreEqual(LightMode.Wash, byValue);

            Assert.IsFalse(TypeConverter.TryGetEnumValue(typeof(LightMode), 2, out _));
        }

        [Test]
        public void Name_IsCaseInsensitive()
        {
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(LightMode), "wash", out var result));
            Assert.AreEqual(LightMode.Wash, result);
        }

        [Test]
        public void UnknownName_IsRefused()
        {
            Assert.IsFalse(TypeConverter.TryGetEnumValue(typeof(LightMode), "Nope", out var result));
            Assert.IsNull(result);
        }

        [Test]
        public void UndefinedNumber_IsRefused_WhenNotFlags()
        {
            Assert.IsFalse(TypeConverter.TryGetEnumValue(typeof(LightMode), 7, out _));

            //A number written as text has to be refused on the same terms, or the string path would
            //be a way around the check.
            Assert.IsFalse(TypeConverter.TryGetEnumValue(typeof(LightMode), "7", out _));
        }

        [Test]
        public void NumericString_ResolvesToDeclaredValue()
        {
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(LightMode), "12", out var result));
            Assert.AreEqual(LightMode.Wash, result);
        }

        [Test]
        public void BoxedMember_PassesThrough()
        {
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(LightMode), LightMode.Spot, out var result));
            Assert.AreEqual(LightMode.Spot, result);
        }

        [Test]
        public void Float_ResolvesToMember()
        {
            //OSC carries numbers as floats as readily as ints.
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(LightMode), 12f, out var result));
            Assert.AreEqual(LightMode.Wash, result);
        }

        [Test]
        public void ByteBackedEnum_ResolvesWithoutCast()
        {
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(ByteBacked), 200, out var byValue));
            Assert.AreEqual(ByteBacked.On, byValue);

            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(ByteBacked), "On", out var byName));
            Assert.AreEqual(ByteBacked.On, byName);
        }

        [Test]
        public void FlagsCombination_RoundTripsThroughItsOwnToString()
        {
            //This is the shape saveData writes for a combined value, so a preset has to read it back.
            var combined = Channels.Red | Channels.Blue;

            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(Channels), combined.ToString(), out var result));
            Assert.AreEqual(combined, result);
        }

        [Test]
        public void FlagsCombination_AcceptsUndefinedNumber()
        {
            //5 names no single member, but Red | Blue is a meaningful value of a [Flags] enum.
            Assert.IsTrue(TypeConverter.TryGetEnumValue(typeof(Channels), 5, out var result));
            Assert.AreEqual(Channels.Red | Channels.Blue, result);
        }

        [Test]
        public void NonEnumTypeAndNullValue_AreRefused()
        {
            Assert.IsFalse(TypeConverter.TryGetEnumValue(typeof(int), "Wash", out _));
            Assert.IsFalse(TypeConverter.TryGetEnumValue(typeof(LightMode), null, out _));
            Assert.IsFalse(TypeConverter.TryGetEnumValue(null, "Wash", out _));
        }

        [Test]
        public void DescribeEnumValues_ListsEveryMember()
        {
            Assert.AreEqual("None, Spot, Wash", TypeConverter.DescribeEnumValues(typeof(LightMode)));
        }

        [Test]
        public void IsFlags_DistinguishesTheTwo()
        {
            Assert.IsTrue(TypeConverter.IsFlags(typeof(Channels)));
            Assert.IsFalse(TypeConverter.IsFlags(typeof(LightMode)));
        }
    }
}
