#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace SizeScanner {
    [TestFixture]
    public class BytesToDirSizeConverterTests {
        BytesToDirSizeConverter BytesToDirSizeConverter { get; set; }
        [SetUp]
        public void SetUp() {
            BytesToDirSizeConverter = new BytesToDirSizeConverter();
        }

        [TearDown]
        public void TearDown() {
            BytesToDirSizeConverter = null;
        }

        string GetDirSizeHelper(long length) => (string) BytesToDirSizeConverter.Convert(length, typeof(string), null, CultureInfo.CurrentCulture);

        [Test]
        public void BytesToDirSizeConverterTest() {
            Assert.AreEqual("0 Б", GetDirSizeHelper(0));
            Assert.AreEqual("8 ЭБ", GetDirSizeHelper(long.MaxValue));
            Assert.AreEqual("0 Б", GetDirSizeHelper(long.MinValue));
            Assert.AreEqual("100 Б", GetDirSizeHelper(100));
            Assert.AreEqual("1,5 КБ", GetDirSizeHelper(1536));
            Assert.AreEqual("1,44 МБ", GetDirSizeHelper(1509950));
        }
    }
}
#endif