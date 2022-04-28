using System;
using Color.Encoding;
using FluentAssertions;

namespace Tests
{
    public class Tests
    {
        [Test]
        public void Test1()
        {
            var Hex = "#a1234F"; //10100001_00100011_01001111

            var Val = (uint) ColorEncoding.RRGGBBHexToARGB32(Hex.AsSpan(1));

            Val.Should().Be(0b11111111_10100001_00100011_01001111);
        }
    
        [Test]
        public void Test2()
        {
            var Hex = "#a1234F11"; //10100001_00100011_01001111_00010001

            var Val = (uint) ColorEncoding.RRGGBBAAHexToARGB32(Hex.AsSpan(1));
            
            Val.Should().Be(0b00010001_10100001_00100011_01001111);
        }
    }
}