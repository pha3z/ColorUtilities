using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Color.Encoding;
using static Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public static void RRGGBB()
        {
            var Hex = "#a1234F"; //10100001_00100011_01001111

            var Val = (uint) ColorEncoding.RRGGBBHexToARGB32(Hex.AsSpan(1));
            
            IsTrue(Val == 0b11111111_10100001_00100011_01001111);
        }

        [TestMethod]
        public static void RRGGBBAA()
        {
            var Hex = "#a1234F11"; //10100001_00100011_01001111_00010001

            var Val = (uint) ColorEncoding.RRGGBBHexToARGB32(Hex.AsSpan(1));
            
            IsTrue(Val == 0b00010001_10100001_00100011_01001111);
        }
    }
}