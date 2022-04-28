using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace HexToIntFast // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            #if DEBUG
            const string Yes = "a1234F";
            
            Console.WriteLine(int.Parse("FF000000", NumberStyles.HexNumber));
            
            Console.WriteLine(int.Parse(Yes.AsSpan(), NumberStyles.HexNumber));
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToARGB32());
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToARGB32_AVX2());
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

        //[Benchmark]
        public int HexToIntNaive()
        {
            return int.Parse(Hex.AsSpan(), NumberStyles.HexNumber);
        }
        
        //[Benchmark]
        public int HexToIntTrumpMcD()
        {
            return Hex.AsSpan().RRGGBBHexToARGB32_Scalar();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2()
        {
            return Hex.AsSpan().RRGGBBHexToARGB32_AVX2();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2_V256Sum()
        {
            return Hex.AsSpan().RRGGBBHexToARGB32_AVX2();
        }
    }
    
    
}

