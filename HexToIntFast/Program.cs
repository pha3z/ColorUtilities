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
    
    public static unsafe class ColorUtilities
    {
        //https://www.asciitable.com/
        private const char StartingPoint = '0';

        private const int UpperToLowerCaseOffset = 'a' - 'A'; //This is a positive num

        private const int NumNextToFirstLetterOffset = 'A' - ('9' + 1); //This is a positive num

        private static readonly int* HexTable;

        static ColorUtilities()
        {
            //Don't allocate HexTable if there's AVX2 support!
            if (!Avx2.IsSupported)
            {
                const int TotalEntries = 127 + 1;

                HexTable = (int*) NativeMemory.AllocZeroed((nuint) TotalEntries * sizeof(int) + 64);

                //Get the original addr of '0'
                var ZeroPos = (nint) (HexTable + '0');
            
                //Get next aligned boundary
                var NewZeroPos = (ZeroPos + (64 - 1)) & ~(64 - 1);

                var ByteOffset = NewZeroPos - ZeroPos;

                //We will never deallocate this, so don't bother storing original start
                HexTable += ByteOffset;

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
        }
        
        public static int RRGGBBHexToARGB32(this ReadOnlySpan<char> HexSpan)
        {
            if (Avx2.IsSupported)
            {
                return RRGGBBHexToARGB32_AVX2(HexSpan);
            }

            else
            {
                return RRGGBBHexToARGB32_Scalar(HexSpan);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static int RRGGBBHexToARGB32_AVX2(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);

            var Sm0lVec = Vector128.LoadUnsafe(ref Unsafe.Subtract(ref Unsafe.As<char, short>(ref FirstChar), 2));

            var IsLowerCaseVec = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) 'F'));
            
            var IsAlphabet  = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) '9'));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) UpperToLowerCaseOffset),IsLowerCaseVec));
            
            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) NumNextToFirstLetterOffset),IsAlphabet));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Vector128.Create((short) StartingPoint));

            //Sm0lVec = Avx2.And(Sm0lVec, Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue));
            
            Sm0lVec = Avx2.Or(Sm0lVec, Vector128.Create(15, 15, 0, 0, 0, 0, 0, 0));
            
            var Vec = Avx2.ConvertToVector256Int32(Sm0lVec);
            
            Vec = Avx2.ShiftLeftLogicalVariable(Vec, Vector256.Create((uint) 28, 24, 20, 16, 12, 8, 4, 0));

            return Vector256.Sum(Vec);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int RRGGBBHexToARGB32_Scalar(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);

            var TablePtr = HexTable;
            
            var _0 = TablePtr[FirstChar] << 20;

            var _1 = TablePtr[Unsafe.Add(ref FirstChar, 1)] << 16;

            var _2 = TablePtr[Unsafe.Add(ref FirstChar, 2)] << 12;

            var _3 = TablePtr[Unsafe.Add(ref FirstChar, 3)] << 8;

            var _4 = TablePtr[Unsafe.Add(ref FirstChar, 4)] << 4;

            var _5 = TablePtr[Unsafe.Add(ref FirstChar, 5)];

            return -16777216 | _0 | _1 | _2 | _3 | _4 | _5;
        }

        public static int RRGGBBAAHexToARGB32(this ReadOnlySpan<char> HexSpan)
        {
            if (Avx2.IsSupported)
            {
                return RRGGBBAAHexToARGB32_AVX2(HexSpan);
            }

            else
            {
                return RRGGBBAAHexToARGB32_Scalar(HexSpan);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static int RRGGBBAAHexToARGB32_AVX2(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);

            var Sm0lVec = Vector128.LoadUnsafe(ref Unsafe.As<char, short>(ref FirstChar));

            var IsLowerCaseVec = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) 'F'));
            
            var IsAlphabet  = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) '9'));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) UpperToLowerCaseOffset),IsLowerCaseVec));
            
            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) NumNextToFirstLetterOffset),IsAlphabet));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Vector128.Create((short) StartingPoint));

            var Vec = Avx2.ConvertToVector256Int32(Sm0lVec);
            
            Vec = Avx2.ShiftLeftLogicalVariable(Vec, Vector256.Create((uint) 20, 16, 12, 8, 4, 0, 28, 24));

            return Vector256.Sum(Vec);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int RRGGBBAAHexToARGB32_Scalar(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);
            
            var _0 = HexTable[FirstChar] << 20;

            var _1 = HexTable[Unsafe.Add(ref FirstChar, 1)] << 16;

            var _2 = HexTable[Unsafe.Add(ref FirstChar, 2)] << 12;

            var _3 = HexTable[Unsafe.Add(ref FirstChar, 3)] << 8;

            var _4 = HexTable[Unsafe.Add(ref FirstChar, 4)] << 4;

            var _5 = HexTable[Unsafe.Add(ref FirstChar, 5)];
            
            var _6 = HexTable[Unsafe.Add(ref FirstChar, 6)] << 28;

            var _7 = HexTable[Unsafe.Add(ref FirstChar, 7)] << 24;
            
            return _0 | _1 | _2 | _3 | _4 | _5 | _6 | _7;
        }
    }
}

