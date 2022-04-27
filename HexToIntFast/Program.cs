using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace HexToIntFast // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // const string Yes = "a1234F";
            //
            // Console.WriteLine(int.Parse(Yes, NumberStyles.HexNumber));
            //
            // Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32());
            //
            // Console.WriteLine(Yes.AsSpan().RRGGBBHexToRGB32_CacheOptimized());
            //
            // return;
             
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
            return int.Parse(Hex.AsSpan(), NumberStyles.HexNumber);
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD()
        {
            return Hex.AsSpan().RRGGBBHexToRGB32();
        }
        
        [Benchmark]
        public int HexToIntTrumpMcD_CacheOptimized()
        {
            return Hex.AsSpan().RRGGBBHexToRGB32_CacheOptimized();
        }
    }
    
    public static unsafe class HexHelpers
    {
        //https://www.asciitable.com/
        private const int UpperToLowerCaseOffset = 'a' - 'A'; //This is a positive num

        private static readonly int* HexTable;

        private static readonly byte* HexTable2;

        static HexHelpers()
        {
            const int TotalEntries = 127 + 1;

            HexTable = (int*) NativeMemory.AlignedAlloc((nuint) TotalEntries * sizeof(int), 64);
            
            HexTable2 = (byte*) NativeMemory.AllocZeroed((nuint) TotalEntries + 64);
            
            //Get the original addr of '0'
            var ZeroPos = (nint) (HexTable2 + '0');
            
            //Get next aligned boundary
            var NewZeroPos = (ZeroPos + (64 - 1)) & ~(64 - 1);

            var ByteOffset = NewZeroPos - ZeroPos;

            //We will never deallocate this, so don't bother storing original start
            HexTable2 += ByteOffset;
            
            //This is important - For we set garbage data to pull from an index of 0!
            //Naturally, we also want its underlying to be 0, negating the effect of
            //our horizontal adds against garbage data
            *HexTable = 0;
            *HexTable2 = 0;
            
            byte HexVal = 0;

            for (var Current = '0'; Current <= '9'; Current++, HexVal++)
            {
                HexTable[Current] = HexVal;
                HexTable2[Current] = HexVal;
            }

            for (var Current = 'A'; Current <= 'F'; Current++, HexVal++)
            {
                HexTable[Current] = HexVal;
                HexTable[Current + UpperToLowerCaseOffset] = HexVal;
                
                HexTable2[Current] = HexVal;
            }
        }
        
        
        public static int RRGGBBHexToRGB32(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);
            
            var _0 = HexTable[FirstChar] << 20;

            var _1 = HexTable[Unsafe.Add(ref FirstChar, 1)] << 16;

            var _2 = HexTable[Unsafe.Add(ref FirstChar, 2)] << 12;

            var _3 = HexTable[Unsafe.Add(ref FirstChar, 3)] << 8;

            var _4 = HexTable[Unsafe.Add(ref FirstChar, 4)] << 4;

            var _5 = HexTable[Unsafe.Add(ref FirstChar, 5)];

            return _0 | _1 | _2 | _3 | _4 | _5;
        }
        

        public static int RRGGBBHexToRGB32_CacheOptimized(this ReadOnlySpan<char> HexSpan)
        {
            ref var FirstChar = ref MemoryMarshal.GetReference(HexSpan);
            
            var _0 = GetIntFromChar(FirstChar) << 20;

            var _1 = GetIntFromChar(Unsafe.Add(ref FirstChar, 1)) << 16;

            var _2 = GetIntFromChar(Unsafe.Add(ref FirstChar, 2)) << 12;

            var _3 = GetIntFromChar(Unsafe.Add(ref FirstChar, 3)) << 8;

            var _4 = GetIntFromChar(Unsafe.Add(ref FirstChar, 4)) << 4;

            var _5 = GetIntFromChar(Unsafe.Add(ref FirstChar, 5));

            return _0 | _1 | _2 | _3 | _4 | _5;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int GetIntFromChar(char Char)
            {
                var LowerCaseMask = ('Z' - Char) >> 31;

                return HexTable2[Char - (UpperToLowerCaseOffset & LowerCaseMask)];
            }
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

