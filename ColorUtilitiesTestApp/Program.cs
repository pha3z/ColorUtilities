using System;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using Color.Encoding;

namespace ColorUtilitiesTestApp // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            #if DEBUG
            const string Yes = "a1234F";

            var Span = Yes.AsSpan();
            
            Console.WriteLine(int.Parse("FF000000", NumberStyles.HexNumber));
            
            Console.WriteLine(int.Parse(Span, NumberStyles.HexNumber));
            
            Console.WriteLine(ColorEncoding.RRGGBBHexToARGB32_Scalar(Span));
            
            Console.WriteLine(ColorEncoding.RRGGBBHexToARGB32_AVX2(Span));
            #else

            try
            {
                BenchmarkRunner.Run<Bench>();
            }

            finally
            {
                while (true);
            }
            #endif
        }
    }

    [MemoryDiagnoser]
    [DisassemblyDiagnoser(exportCombinedDisassemblyReport: true)]
    public class Bench
    {
        private const string Hex = "123456";

        [Benchmark]
        public int HexToIntNaive()
        {
            return int.Parse(Hex.AsSpan(), NumberStyles.HexNumber);
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD()
        {
            return ColorEncoding.RRGGBBHexToARGB32_Scalar(Hex.AsSpan());
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2()
        {
            return ColorEncoding.RRGGBBHexToARGB32_AVX2(Hex.AsSpan());
        }
    }
    
    
}

