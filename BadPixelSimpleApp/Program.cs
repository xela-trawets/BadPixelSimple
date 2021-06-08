using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadPixelSimpleApp
{
    class Program
    {
        public AsicDescriptor();
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine($"Usage {Path.GetFileName(Environment.ProcessPath)} BadPixelFile.json");
            var bpl = new BadPixelList("001");
            var swFix = BadPixelFixCategory.SoftwareFix;
            bpl.BadPixels.AddRange(new List<BadPixelRec>() { new(12, 21, swFix), new(18, 81) });
            bpl.BadColumns.Add(17);
            bpl.BadRows.Add(0);
            bpl.BadRows.Add(1);
            bpl.BadRows.Add(254);
            bpl.BadRows.Add(255);
            var jso = new JsonSerializerOptions()
            {
                WriteIndented = true,
                IncludeFields = true,
            };
            jso.Converters.Add(new JsonStringEnumConverter());
            string json = JsonSerializer.Serialize(bpl, jso);
            File.WriteAllText(@"C:\tmp\bpl.json", json);
            bpl = JsonSerializer.Deserialize<BadPixelList>(json);
            const AsicWidth = 128;
            foreach (var badpix in bpl.BadPixels)
            {
                if (badpix.Category!=BadPixelFixCategory.PCRFix) continue;
                int asicIndex = badpix.RawX / AsicWidth;
            }
        }
        static (int byteIndex, int bitInByte) IndexOfBadPixeInPCR(int badPixelX, int badPixelY, int pcrBit = 14)
        //Hard coded for Stanley Thor 
        // 256 and 128
        {
            int BitsPerPixelPcr = 17;
            //int PcrDisableBitPos = 14;
            Int64 PixelsPerAsic = 256 * 128;
            int BitsPerRow = 128 * 17;
            int ExtraBitsPerColumn = 1; // May be added by FPGA ?
            int FinalClockBitsPerAsic = 0;//1; //if the final Pcr Clock bit per Aisc is not added by hardware - then Add it here

            int ExtraBitsPerAsic = 128 * ExtraBitsPerColumn + FinalClockBitsPerAsic;
            Int64 RealBitsPerAsic = BitsPerPixelPcr * PixelsPerAsic;
            Int64 BitsPerAsic = BitsPerPixelPcr * PixelsPerAsic + ExtraBitsPerAsic;

            int AsicIndex = badPixelX / 128;
            int AsicPixelX = badPixelX % 128;
            int AsicPixelY = badPixelY;
            AsicPixelY = 256 - 1 - badPixelY;

            Int64 Asic_Start_Bit = AsicIndex * BitsPerAsic;
            Int64 RowStartBit = AsicPixelY * BitsPerRow;
            Int64 pcrBitOffset = Asic_Start_Bit + RowStartBit + AsicPixelX;
            Int64 DisableBitPosOffset = pcrBit * 128;// PcrDisableBitPos * 128;

            Int64 finalBitPos = Asic_Start_Bit + pcrBitOffset + DisableBitPosOffset;
            finalBitPos = Asic_Start_Bit + RowStartBit + DisableBitPosOffset + AsicPixelX;

            return ((int)(finalBitPos / 8), (int)(finalBitPos % 8));
            //Set_bit(pcrBitOffset + DisableBitPosOffset, 1);//Assume digital register order- not little endian
        }
        public static IEnumerable<int> BitsFrom((byte, int) a)
        {
            var (value, index) = a;
            for (int n = 0; n < 8; n++)
            {
                int t = 128 >> n;
                if ((value & t) != 0) yield return index * 8 + n;
            }
        }
        public static void MainOriginal()
        {
            Console.WriteLine("Hello Ally World!");
            Random rand = new Random();
            int x = 900; int y = 43; int pcrBit = 14;
            int errors = 0;
            for (int n = 0; n < 1000000; n++)
            {
                int bitPos = (x / 128) * bitsPerAsic +
                (255 - y) * 17 * 128 +
                pcrBit * 128 + (x % 128);
                var bp = IndexOfBadPixeInPCR(x, y, pcrBit);
                int npix = bp.byteIndex * 8 + bp.bitInByte;
                if (npix != bitPos)
                {
                    errors++;
                    Console.WriteLine($" {n} {npix == bitPos} ");
                }
                if ((true, x, y, pcrBit) != PcrXyFromBitPos(bitPos))
                {
                    errors++;
                    Console.WriteLine($" {n} Bad Inverse ");
                }

                x = rand.Next(1024);
                y = rand.Next(256);
                pcrBit = rand.Next(17);
            }
            Console.WriteLine($" Done {errors} ");
            Console.ReadKey();
            //var bp =IndexOfBadPixeInPCR(900, 43);
            ////bp = IndexOfBadPixeInPCR(1024, 0);
            //Console.WriteLine($"{bp.Item1} : {1 << (7 - bp.Item2)}");
            // @"C:\Users\Xela\Downloads\thor_462.pcr");

            // Read bitstream
            var pcrFileBytes = File.ReadAllBytes(@"c:\tmp\pcr.config");
            // Create List of all set bits
            var list = pcrFileBytes
                .Select((value, Index) => (value, Index)).Where(x => x.value != 0)
                .SelectMany(a => BitsFrom(a))
                //.Select(a => a.Index * 8 + (7 - BitOperations.Log2(a.value)))//many
                .Select(bitIndex => new { asicIndex = bitIndex / 557184, bitInAsic = bitIndex % 557184 })
                .Select(abit => new { abit.asicIndex, rowInAsic = abit.bitInAsic / (128 * 17), bitInRow = abit.bitInAsic % (128 * 17) })
                .Select(rpos => new { rpos.asicIndex, rpos.rowInAsic, rpos.bitInRow, bitInPcr = rpos.bitInRow / 128, pixelInRow = rpos.bitInRow % 128 })
                .ToList();
            var image = new int[1024 * 256];
            // put bits into image
            foreach (var p in list) { image[p.rowInAsic * 1024 + p.asicIndex * 128 + p.pixelInRow] |= 1 << p.bitInPcr; }
            // write image to raw file
            using (var fs = new FileStream(@"C:\tmp\pcr.raw", FileMode.Create))
            { fs.Write(MemoryMarshal.AsBytes<int>(image)); }



            foreach (var p in image.Where(a => a != 0).Where(a => a != 32700))
            { Console.WriteLine($"{p} \t: disabled ?"); }

            foreach (var p in list)
            {
                Console.WriteLine($"{p.asicIndex} {p.rowInAsic} {p.pixelInRow} {p.bitInPcr}\t: disabled ");
            }

            byte[] pcrBytes = new byte[pcrByteSize];

            for (y = 0; y < 255; y++)
            {
                x = 900;
                KillPix(pcrBytes, x, y);
            }
            File.WriteAllBytes(@"c:\tmp\462_B.pcr", pcrBytes);
        }

        const int bitsPerAsic = 557184;
        const int bytesPerAsic = ((bitsPerAsic + 31) / 32) * 4;
        const int pcrByteSize = bytesPerAsic * asicCount;
        const int asicCount = 8;

        static void setbit(Span<byte> bytes, int bitPos)
        {
            bytes[bitPos / 8] |= (byte)(128 >> (bitPos % 8));
        }
        static void KillPix(byte[] pcrBytes, int x, int y)
        {
            int bitPos = (x / 128) * bitsPerAsic +
                (255 - y) * 17 * 128 +
                14 * 128 + (x % 128);
            setbit(pcrBytes, bitPos);
        }
        static (bool isRealBitNotExtraclock, int x, int y, int pcrBit) PcrXyFromBitPos(int bitPos)
        {
            int nAsic = bitPos / bitsPerAsic;
            int bitPosInAsic = bitPos % bitsPerAsic;
            int bitPosInRow = bitPosInAsic % (17 * 128);
            int y = 255 - (bitPosInAsic / (17 * 128));
            int pcrBit = bitPosInRow / 128;
            int xInAsic = bitPosInRow % 128;
            int x = xInAsic + nAsic * 128;
            bool isRealBitNotExtraclock = y >= 0;
            return (isRealBitNotExtraclock, x, y, pcrBit);
        }
        static IEnumerable<uint> PcrWalk(uint[] PcrImageAs32BitPixels)
        {
            //Some questions if the asic dimensions are some odd number)
            const int AsicCount = 8;
            const int ImageWdith = 1024;
            int bitPos = 0;
            while (bitPos < bitsPerAsic * AsicCount)
            {
                uint next = 0;
                for (int bitInDWord = 0; bitInDWord < 32; bitInDWord++)
                {
                    var bit = PcrXyFromBitPos(bitPos);
                    if (bit.isRealBitNotExtraclock)
                    {
                        int byteInRGB24 = ImageWdith * bit.y + bit.x;
                        if (byteInRGB24 >= PcrImageAs32BitPixels.Length) break;
                        uint nextBitValue = (PcrImageAs32BitPixels[byteInRGB24] >> bit.pcrBit) & 1u;//simple bit peel
                        uint mask = nextBitValue << (31 - bitInDWord);//Atukem bit order
                        next |= mask;
                    }
                    bitPos++;
                }
                //Atukem Byte order
                next = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(next);
                yield return next;
            }
        }
        static uint[] Image32BitToPcr(uint[] PcrImageAs32BitPixels)
        {
            var result = PcrWalk(PcrImageAs32BitPixels).ToArray();
            return result;
        }
    }
    //Stuff used to crack the PCR format
    //    foreach (var p in list)
    //{
    //    var bitInByteIntel = BitOperations.Log2(byteValue);
    //    var bitInByteLogic = 7 - bitInByteIntel;
    //    var bitIndex = byteIndex * 8 + bitInByteLogic;
    //    int bitInAsic = bitIndex % 557184;
    //    int rowInAsic = bitInAsic / (128 * 17);
    //    int bitInRow = bitInAsic % (128 * 17);
    //    int bitInPcr = bitInRow / 128;
    //    int pixelInRow = bitInRow % 128;

    //    //Console.WriteLine($"{bitIndex}\t: bitIndex in file");
    //    //Console.WriteLine($"{bitInAsic} \t: bitIndex in Asic");
    //    Console.WriteLine($"{rowInAsic} \t: rowInAsic ");
    //    Console.WriteLine($"{bitInPcr} \t: bitInPcr ");
    //    Console.WriteLine($"{pixelInRow} \t: pixelInRow ");
    //    Console.WriteLine($"");
    //    //Console.WriteLine($"{pcrFileBytes.Length * 8 - bitIndex} \t: bitIndex from end of file");
    //    Console.WriteLine($"");
    //}
    //        Console.WriteLine($"{-(bp.Item1 * 8 + bp.Item2 - 557184 * 8)} \t: bitIndex from end of file");
}