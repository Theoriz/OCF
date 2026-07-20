using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Theoriz.OCF.Tests.Editor
{
    /// <summary>
    /// EditMode unit tests for TypeConverter (pure static conversion helpers).
    ///
    /// NOTE: <see cref="GetFloat_ParsesInvariantly_UnderCommaDecimalCulture"/> is an
    /// intentional RED test. getFloat uses culture-sensitive parsing (see
    /// docs/PackageAudit-2026-07-16.md, OCF P2 "silent sentinel converters"); under a
    /// comma-decimal locale it returns the fallback 0 instead of the parsed number.
    /// It passes once the parse is made culture-invariant.
    /// </summary>
    public class TypeConverterTests
    {
        [Test]
        public void GetFloat_ParsesInteger_And_Bool()
        {
            Assert.AreEqual(2.5f, TypeConverter.getFloat(2.5f), 1e-6f);
            Assert.AreEqual(3f, TypeConverter.getFloat(3), 1e-6f);
            Assert.AreEqual(1f, TypeConverter.getFloat(true), 1e-6f);
            Assert.AreEqual(0f, TypeConverter.getFloat(false), 1e-6f);
        }

        [Test]
        public void GetBool_ParsesCommonStrings()
        {
            Assert.IsTrue(TypeConverter.getBool("true"));
            Assert.IsTrue(TypeConverter.getBool("1"));
            Assert.IsFalse(TypeConverter.getBool("false"));
            Assert.IsFalse(TypeConverter.getBool("0"));
        }

        [Test]
        public void GetIndexInEnum_ReturnsIndex_WhenFound()
        {
            var list = new List<string> { "Alpha", "Beta", "Gamma" };
            Assert.AreEqual(1, TypeConverter.getIndexInEnum(list, "Beta"));
        }

        [Test]
        public void GetIndexInEnum_ReturnsMinusOne_WhenNotFound()
        {
            var list = new List<string> { "Alpha", "Beta" };
            Assert.AreEqual(-1, TypeConverter.getIndexInEnum(list, "Zeta"));
        }

        [Test]
        public void StringToVector3_ParsesInvariantly()
        {
            var v = TypeConverter.StringToVector3("(1.5, -2.25, 3)");
            Assert.AreEqual(new Vector3(1.5f, -2.25f, 3f), v);
        }

        [Test]
        public void StringToVector4_ParsesInvariantly()
        {
            var v = TypeConverter.StringToVector4("(1.5, -2.25, 3, 4.75)");
            Assert.AreEqual(new Vector4(1.5f, -2.25f, 3f, 4.75f), v);
        }

        [Test]
        public void StringToVector4_Malformed_ReturnsZero_AndWarns()
        {
            LogAssert.Expect(LogType.Warning, new Regex("StringToVector4: malformed string"));
            var v = TypeConverter.StringToVector4("(1, 2, 3)");
            Assert.AreEqual(Vector4.zero, v);
        }

        [Test]
        public void StringToColor_ParsesRgbaForm()
        {
            var c = TypeConverter.StringToColor("RGBA(1.000, 0.500, 0.250, 1.000)");
            Assert.AreEqual(1.00f, c.r, 1e-3f);
            Assert.AreEqual(0.50f, c.g, 1e-3f);
            Assert.AreEqual(0.25f, c.b, 1e-3f);
            Assert.AreEqual(1.00f, c.a, 1e-3f);
        }

        // RED (known bug): culture-sensitive parsing of "1.5" fails under comma-decimal
        // locales and getFloat falls back to 0.
        [Test]
        public void GetFloat_ParsesInvariantly_UnderCommaDecimalCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
                Assert.AreEqual(1.5f, TypeConverter.getFloat("1.5"), 1e-6f,
                    "getFloat should parse '1.5' regardless of the current culture " +
                    "(known bug: culture-sensitive TryParse returns 0 under comma-decimal locales).");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        // GREEN characterization: integer string parsing is culture-independent for plain
        // digits; this pins that it stays robust across locales.
        [Test]
        public void GetInt_ParsesString_UnderCommaDecimalCulture()
        {
            var previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
                Assert.AreEqual(7, TypeConverter.getInt("7"),
                    "getInt should parse '7' regardless of the current culture.");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
