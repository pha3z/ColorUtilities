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
            
            Console.WriteLine(int.Parse(Yes, NumberStyles.HexNumber));
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32());
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32_AVX2());
            
            Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32_AVX2_2());
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
            return Hex.AsSpan().RRGGBBHexToRGB32();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2()
        {
            return Hex.AsSpan().RRGGBBHexToRGB32_AVX2();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2_V256Sum()
        {
            return Hex.AsSpan().RRGGBBHexToRGB32_AVX2_V256Sum();
        }
    }
    
    public static unsafe class HexHelpers
    {
        //https://www.asciitable.com/
        //https://www.asciitable.com/
        private const char StartingPoint = '0';

        private const int UpperToLowerCaseOffset = 'a' - 'A'; //This is a positive num

        private const int NumNextToFirstLetterOffset = 'A' - ('9' + 1); //This is a positive num

        private static readonly int* HexTable;

        static HexHelpers()
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

        public static int RRGGBBHexToRGB32_AVX2(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);

            var Sm0lVec = Vector128.LoadUnsafe(ref Unsafe.Subtract(ref Unsafe.As<char, short>(ref FirstChar), 2));

            var IsLowerCaseVec = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) 'F'));
            
            var IsAlphabet  = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) '9'));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) UpperToLowerCaseOffset),IsLowerCaseVec));
            
            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) NumNextToFirstLetterOffset),IsAlphabet));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Vector128.Create((short) StartingPoint));

            Sm0lVec = Avx2.And(Sm0lVec, Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue));
            
            var Vec = Avx2.ConvertToVector256Int32(Sm0lVec);
            
            Vec = Avx2.ShiftLeftLogicalVariable(Vec, Vector256.Create((uint) 0, 0, 20, 16, 12, 8, 4, 0));
            
            var VecUpper = Vec.GetUpper();

            var VecLower = Vec.GetLower();

            //(1, 2, 3, 4) | (5, 6, 7, 8)
            //([1 + 2], [3 + 4], [5 + 6], [7 + 8])
            VecUpper = Avx2.Add(VecUpper, VecLower);

            //([1 + 2], [3 + 4], [5 + 6], [7 + 8]) | ([1 + 2], [3 + 4], [5 + 6], [7 + 8])
            //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
            VecUpper = Avx2.HorizontalAdd(VecUpper, VecUpper);

            //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) | ([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
            //([1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8]) 
            VecUpper = Avx2.HorizontalAdd(VecUpper, VecUpper);

            return VecUpper[0];
        }
        
        public static int RRGGBBHexToRGB32_AVX2_V256Sum(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);

            var Sm0lVec = Vector128.LoadUnsafe(ref Unsafe.Subtract(ref Unsafe.As<char, short>(ref FirstChar), 2));

            var IsLowerCaseVec = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) 'F'));
            
            var IsAlphabet  = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) '9'));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) UpperToLowerCaseOffset),IsLowerCaseVec));
            
            Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) NumNextToFirstLetterOffset),IsAlphabet));

            Sm0lVec = Avx2.Subtract(Sm0lVec, Vector128.Create((short) StartingPoint));

            Sm0lVec = Avx2.And(Sm0lVec, Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue));
            
            var Vec = Avx2.ConvertToVector256Int32(Sm0lVec);
            
            Vec = Avx2.ShiftLeftLogicalVariable(Vec, Vector256.Create((uint) 0, 0, 20, 16, 12, 8, 4, 0));

            return Vector256.Sum(Vec);
        }
        
        // public static int RRGGBBHexToRGB32_AVX2_2(this ReadOnlySpan<char> HexSpan)
        // {
        //     ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);
        //
        //     var Sm0lVec = Vector128.LoadUnsafe(ref Unsafe.Subtract(ref Unsafe.As<char, short>(ref FirstChar), 2));
        //
        //     var IsLowerCaseVec = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) 'F'));
        //     
        //     var IsAlphabet  = Avx2.CompareGreaterThan(Sm0lVec, Vector128.Create((short) '9'));
        //
        //     Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) UpperToLowerCaseOffset),IsLowerCaseVec));
        //     
        //     Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.And(Vector128.Create((short) NumNextToFirstLetterOffset),IsAlphabet));
        //
        //     var SPVec = Vector128.Create((short) StartingPoint);
        //
        //     var GarbageVec = Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue);
        //     
        //     Sm0lVec = Avx2.Subtract(Sm0lVec, Avx2.BlendVariable(Sm0lVec, SPVec, GarbageVec));
        //
        //     //Sm0lVec = Avx2.Subtract(Sm0lVec, Vector128.Create((short) StartingPoint));
        //
        //     //Sm0lVec = Avx2.And(Sm0lVec, Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue));
        //     
        //     var Vec = Avx2.ConvertToVector256Int32(Sm0lVec);
        //     
        //     Vec = Avx2.ShiftLeftLogicalVariable(Vec, Vector256.Create((uint) 0, 0, 20, 16, 12, 8, 4, 0));
        //     
        //     var VecUpper = Vec.GetUpper();
        //
        //     var VecLower = Vec.GetLower();
        //
        //     //(1, 2, 3, 4) | (5, 6, 7, 8)
        //     //([1 + 2], [3 + 4], [5 + 6], [7 + 8])
        //     VecUpper = Avx2.Add(VecUpper, VecLower);
        //
        //     //([1 + 2], [3 + 4], [5 + 6], [7 + 8]) | ([1 + 2], [3 + 4], [5 + 6], [7 + 8])
        //     //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
        //     VecUpper = Avx2.HorizontalAdd(VecUpper, VecUpper);
        //
        //     //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) | ([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
        //     //([1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8]) 
        //     VecUpper = Avx2.HorizontalAdd(VecUpper, VecUpper);
        //
        //     return VecUpper[0];
        // }
        
        public static int RRGGBBHexToRGB32(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);

            var TablePtr = HexTable;
            
            var _0 = TablePtr[FirstChar] << 20;

            var _1 = TablePtr[Unsafe.Add(ref FirstChar, 1)] << 16;

            var _2 = TablePtr[Unsafe.Add(ref FirstChar, 2)] << 12;

            var _3 = TablePtr[Unsafe.Add(ref FirstChar, 3)] << 8;

            var _4 = TablePtr[Unsafe.Add(ref FirstChar, 4)] << 4;

            var _5 = TablePtr[Unsafe.Add(ref FirstChar, 5)];

            return _0 | _1 | _2 | _3 | _4 | _5;
        }

        public static int RRGGBBAAHexToARGB32(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);
            
            var _0 = HexTable[FirstChar] << 20;

            var _1 = HexTable[Unsafe.Add(ref FirstChar, 1)] << 16;

            var _2 = HexTable[Unsafe.Add(ref FirstChar, 2)] << 12;

            var _3 = HexTable[Unsafe.Add(ref FirstChar, 3)] << 8;

            var _4 = HexTable[Unsafe.Add(ref FirstChar, 4)] << 4;

            var _5 = HexTable[Unsafe.Add(ref FirstChar, 5)];
            
            var _6 = HexTable[Unsafe.Add(ref FirstChar, 6)] << 24;

            var _7 = HexTable[Unsafe.Add(ref FirstChar, 7)] << 28;
            
            return _0 | _1 | _2 | _3 | _4 | _5 | _6 | _7;
        }
    }
}

