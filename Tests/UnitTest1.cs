using ColorUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void RRGGBB()
        {
            string hex = "#a1234F"; //10100001_00100011_01001111

            UInt32 val = (UInt32)ColorUtil.RRGGBBHexToARGB32(hex.AsSpan(1));
            IsTrue(val == 0b11111111_10100001_00100011_01001111);
        }

        [TestMethod]
        public void RRGGBBAA()
        {
            string hex = "#a1234F11"; //10100001_00100011_01001111_00010001

            UInt32 val = (UInt32)ColorUtil.RRGGBBHexToARGB32(hex.AsSpan(1));
            IsTrue(val == 0b00010001_10100001_00100011_01001111);
        }
    }
}