using System;
using System.Drawing;
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
            const string Yes = "a1234F";
            
            Console.WriteLine(int.Parse(Yes, NumberStyles.HexNumber));
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32());
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32_AVX2());
            
            // BenchmarkRunner.Run<Bench>();
            //
            // while (true);
        }
    }

    [MemoryDiagnoser]
    [DisassemblyDiagnoser(exportCombinedDisassemblyReport: true)]
    public class Bench
    {
        private const string Hex = "a1234F";

        //[Benchmark]
        public int HexToIntNaive()
        {
            return int.Parse(Hex.AsSpan(), System.Globalization.NumberStyles.HexNumber);
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD()
        {
            return Hex.AsSpan().RRGGBBHexToRGB32();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2()
        {
            return Hex.AsSpan().RRGGBBHexToRGB32_AVX2();
        }
    }
    
    public static unsafe class HexHelpers
    {
        //https://www.asciitable.com/
        private const char StartingPoint = '0';

        private const int UpperToLowerCaseOffset = 'a' - 'A'; //This is a positive num

        private const int NumNextToFirstLetterOffset = 'A' - ('9' + 1); //This is a positive num

        private static readonly int* HexTable;

        static HexHelpers()
        {
            const int TotalEntries = 127 + 1;

            HexTable = (int*) NativeMemory.AlignedAlloc((nuint) TotalEntries * sizeof(int), 64);

            //This is important - For we set garbage data to pull from an index of 0!
            //Naturally, we also want its underlying to be 0, negating the effect of
            //our horizontal adds against garbage data
            *HexTable = 0;

            byte HexVal = 0;

            for (var Current = '0'; Current <= '9'; Current++, HexVal++)
            {
                HexTable[Current] = HexVal;
            }

            for (var Current = 'A'; Current <= 'F'; Current++, HexVal++)
            {
                HexTable[Current] = HexVal;
                
                HexTable[Current + UpperToLowerCaseOffset] = HexVal;
            }
        }

        public static int RRGGBBHexToRGB32(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);
            
            var _0 = HexCharToInt(FirstChar) << 20;

            var _1 = HexCharToInt(Unsafe.Add(ref FirstChar, 1)) << 16;

            var _2 = HexCharToInt(Unsafe.Add(ref FirstChar, 2)) << 12;

            var _3 = HexCharToInt(Unsafe.Add(ref FirstChar, 3)) << 8;

            var _4 = HexCharToInt(Unsafe.Add(ref FirstChar, 4)) << 4;

            var _5 = HexCharToInt(Unsafe.Add(ref FirstChar, 5));

            return _0 | _1 | _2 | _3 | _4 | _5;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexCharToInt(char Val)
        {
            var IsLowerCaseMask = unchecked(('Z' - Val) >> 31);
            
            var IsNonNumericMask = unchecked(('9' - Val) >> 31);
            
            return (Val - (UpperToLowerCaseOffset & IsLowerCaseMask) -
                    (NumNextToFirstLetterOffset & IsNonNumericMask)) - StartingPoint;
        }

        public static int RRGGBBHexToRGB32_AVX2(this ReadOnlySpan<char> HexSpan)
        {
            var HexVec = //Back-tracking should prevent AV-ing
                Vector128.LoadUnsafe(ref Unsafe.Subtract(ref Unsafe.As<char, short>(ref MemoryMarshal.GetReference(HexSpan)), 2));

            HexVec = Vector128.BitwiseAnd(HexVec,
                Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue));

            var RHexVec = Avx2.ConvertToVector256Int32(HexVec);

            RHexVec = Avx2.GatherVector256(HexTable, RHexVec, 4);

            RHexVec = Avx2.ShiftLeftLogicalVariable(RHexVec, Vector256.Create((uint) 0, 0, 20, 16, 12, 8, 4, 0));

            return Vector256.Sum(RHexVec);
        }
        
        public static int RRGGBBAAHexToARGB32(this ReadOnlySpan<char> HexSpan)
        {
            //https://en.wikipedia.org/wiki/RGBA_color_model
            var HexVec = //Back-tracking should prevent AV-ing
                Vector128.LoadUnsafe(ref Unsafe.As<char, short>(ref MemoryMarshal.GetReference(HexSpan)));

            var RHexVec = Avx2.ConvertToVector256Int32(HexVec);

            RHexVec = Avx2.GatherVector256(HexTable, RHexVec, 4);

            RHexVec = Avx2.ShiftLeftLogicalVariable(RHexVec, Vector256.Create((uint) 20, 16, 12, 8, 4, 0, 28, 24));

            //RHexVec = Avx2.ShiftLeftLogicalVariable(RHexVec, Vector256.Create((uint) 28, 24, 20, 16, 12, 8, 4, 0));

            return Vector256.Sum(RHexVec);
        }
    }
}

