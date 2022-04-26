using System;
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
            // Console.WriteLine("a1234F".HexToInt());
            //
            // Console.WriteLine("a1234F".HexToIntAVX2());
            
            BenchmarkRunner.Run<Bench>();
            
            while (true);
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
            return int.Parse(Hex, System.Globalization.NumberStyles.HexNumber);
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD()
        {
            return Hex.HexToInt();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_AVX2()
        {
            return Hex.HexToIntAVX2();
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

#if DEBUG
        var Span = new Span<int>(HexTable, TotalEntries);
        
        Span.Fill(0);
#endif

            byte HexVal = 0;

            for (var Current = '0'; Current <= '9'; Current++, HexVal++)
            {
                HexTable[Current] = HexVal;
            }

            for (var Current = 'A'; Current <= 'F'; Current++, HexVal++)
            {
                HexTable[Current] = HexVal;

                //Console.WriteLine($"{Current} | {(char) (Current + UpperToLowerCaseOffset)}");

                HexTable[Current + UpperToLowerCaseOffset] = HexVal;
            }

#if DEBUG
        foreach (var x in Span)
        {
            Console.WriteLine(x);
        }
#endif
        }

        public static int HexToInt(this string HexString)
        {
            fixed (char* FirstChar = HexString)
            {
                var _0 = HexCharToInt(FirstChar) << 20;

                var _1 = HexCharToInt(FirstChar + 1) << 16;

                var _2 = HexCharToInt(FirstChar + 2) << 12;

                var _3 = HexCharToInt(FirstChar + 3) << 8;

                var _4 = HexCharToInt(FirstChar + 4) << 4;

                var _5 = HexCharToInt(FirstChar + 5);

                return _0 | _1 | _2 | _3 | _4 | _5;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexCharToInt(char* Char)
        {
            var Val = *Char;

            //Console.WriteLine(Val);

            var IsLowerCaseMask = unchecked(('Z' - Val) >> 31);

            //Console.WriteLine($"Is Lower-Case: {IsLowerCaseMask}");

            var IsNonNumericMask = unchecked(('9' - Val) >> 31);

            //Console.WriteLine($"Is Non-Numeric: {IsNonNumericMask}");

            return (Val - (UpperToLowerCaseOffset & IsLowerCaseMask) -
                    (NumNextToFirstLetterOffset & IsNonNumericMask)) - StartingPoint;
        }

        public static int HexToIntAVX2(this string HexString)
        {
            var HexVec = //Back-tracking should prevent AV-ing
                Vector128.LoadUnsafe(ref Unsafe.Subtract(ref Unsafe.As<char, short>(ref MemoryMarshal.GetReference(HexString.AsSpan())), 2));

            HexVec = Vector128.BitwiseAnd(HexVec,
                Vector128.Create(0, 0, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue, short.MaxValue));

            var RHexVec = Avx2.ConvertToVector256Int32(HexVec);

            RHexVec = Avx2.GatherVector256(HexTable, RHexVec, 4);

            RHexVec = Avx2.ShiftLeftLogicalVariable(RHexVec, Vector256.Create((uint) 0, 0, 20, 16, 12, 8, 4, 0));

            var RHexVecUpper = RHexVec.GetUpper();

            var RHexVecLower = RHexVec.GetLower();

            //(1, 2, 3, 4) | (5, 6, 7, 8)
            //([1 + 2], [3 + 4], [5 + 6], [7 + 8])
            RHexVecUpper = Avx2.HorizontalAdd(RHexVecUpper, RHexVecLower);

            //([1 + 2], [3 + 4], [5 + 6], [7 + 8]) | ([1 + 2], [3 + 4], [5 + 6], [7 + 8])
            //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
            RHexVecUpper = Avx2.HorizontalAdd(RHexVecUpper, RHexVecLower);

            //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) | ([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
            //([1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8]) 
            RHexVecUpper = Avx2.HorizontalAdd(RHexVecUpper, RHexVecLower);

            return RHexVecUpper[0];
        }

        // public static int HexToIntSIMD(this string HexString)
        // { 
        //     //I don't this would AV
        //     var HexVec = Vector128.LoadUnsafe(ref Unsafe.As<char, short>(ref MemoryMarshal.GetReference(HexString.AsSpan())));
        //
        //     HexVec = Vector128.ConditionalSelect(
        //         Vector128.Create(0, 0, 0, 0, 0, 0, short.MaxValue, short.MaxValue), HexVec, Vector128<short>.Zero);
        //     
        //     var IsLowerCaseVec = Vector128.GreaterThan(HexVec, Vector128.Create((short) 'Z'));
        //     
        //     var IsNonNumeric  = Vector128.GreaterThan(HexVec, Vector128.Create((short) '9'));
        //
        //     var UpperToLowerCaseOffsetVex = 
        //         Vector128.ConditionalSelect(IsLowerCaseVec, Vector128.Create((short) UpperToLowerCaseOffset), Vector128<short>.Zero);
        //     
        //     var NumNextToFirstLetterOffsetVec = 
        //         Vector128.ConditionalSelect(IsNonNumeric, Vector128.Create((short) NumNextToFirstLetterOffset), Vector128<short>.Zero);
        //
        //     HexVec = Vector128.Subtract(HexVec, UpperToLowerCaseOffsetVex);
        //     
        //     HexVec = Vector128.Subtract(HexVec, NumNextToFirstLetterOffsetVec);
        //
        //     HexVec = Vector128.Subtract(HexVec, Vector128.Create((short) StartingPoint));
        //
        //     //HexVec = Vector128.ShiftLeft(HexVec, Vector128.Create(20, 16, 12, 8, 4, 0, 31, 31));
        //
        //     var RHexVec = Avx2.ConvertToVector256Int32(HexVec);
        //
        //     RHexVec = Avx2.ShiftLeftLogicalVariable(RHexVec, Vector256.Create((uint) 20, 16, 12, 8, 4, 0, 31, 31));
        //
        //     var RHexVecUpper = RHexVec.GetUpper();
        //     
        //     var RHexVecLower = RHexVec.GetLower();
        //     
        //     //(1, 2, 3, 4) | (5, 6, 7, 8)
        //     //([1 + 2], [3 + 4], [5 + 6], [7 + 8])
        //     RHexVecUpper = Avx2.HorizontalAdd(RHexVecUpper, RHexVecLower);
        //     
        //     //([1 + 2], [3 + 4], [5 + 6], [7 + 8]) | ([1 + 2], [3 + 4], [5 + 6], [7 + 8])
        //     //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
        //     RHexVecUpper = Avx2.HorizontalAdd(RHexVecUpper, RHexVecLower);
        //     
        //     //([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) | ([1 + 2 + 3 + 4], [5 + 6 + 7 + 8], [1 + 2 + 3 + 4], [5 + 6 + 7 + 8]) 
        //     //([1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8], [1 + 2 + 3 + 4 + 5 + 6 + 7 + 8]) 
        //     RHexVecUpper = Avx2.HorizontalAdd(RHexVecUpper, RHexVecLower);
        //
        //     return RHexVecUpper[0];
        // }
    }
}

