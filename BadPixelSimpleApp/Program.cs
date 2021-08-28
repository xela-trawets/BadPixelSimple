using DetConfigReader;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadPixelSimpleApp
{
    class Program
    {
        public static class PcrTestImplementation
        {
            const int bitsPerAsic = 557184;
            const int bytesPerAsic = ((bitsPerAsic + 31) / 32) * 4;
            const int pcrByteSize = bytesPerAsic * asicCount;
            const int asicCount = 8;
            static void setbit(Span<byte> bytes, int bitPos)
            {
                bytes[bitPos / 8] |= (byte)(128 >> (bitPos % 8));
            }
            public static int getbit(Span<byte> bytes, int bitPos)
            {
                int t = bytes[bitPos / 8] & (byte)(128 >> (bitPos % 8));
                return t == 0 ? 0 : 1;
            }
            public static int PixBitPos(int nBit, int x, int y) =>
                (x / 128) * bitsPerAsic +
                (255 - y) * 17 * 128 +
                nBit * 128 + (x % 128);
            public static int KillPixBitPos(int x, int y) =>
                PixBitPos(14, x, y);
            //(x / 128) * bitsPerAsic +
            //(255 - y) * 17 * 128 +
            //14 * 128 + (x % 128);
            public static void KillPix(byte[] pcrBytes, int x, int y)
            {
                int bitPos = KillPixBitPos(x, y);
                setbit(pcrBytes, bitPos);
            }
            public static int GetPcrXY(byte[] pcrBytes, int x, int y)
            {
                int result = 0;
                for (int nBit = 0; nBit < 17; nBit++)
                {
                    int bitPos = PixBitPos(nBit, x, y);
                    int b = getbit(pcrBytes, bitPos) << nBit;
                    result = result | b;
                }
                return result;
            }
        }
        static void ReadPcrTest()
        {
            var detcfgLines = File.ReadAllLines(@"C:\Users\AlexA\Downloads\mca604\media\card\apps\detector.config", Encoding.ASCII)
                .Select(lx => lx.Trim().Split('=').Select(wx => wx.Trim()))
                .Where(wxl => wxl.Count() == 2)
                //.Where(wxl => int.TryParse(wxl.ElementAt(1),out int tmp))
                .Select(wxl => new KeyValuePair<string, string>(wxl.ElementAt(0), wxl.ElementAt(1)))
                .ToList();

            var detCfg = new Dictionary<string, string>(detcfgLines);
            var intValuePairs = detCfg.Where(kvp => int.TryParse(kvp.Value, out int tmp))
                .Select(kvp => new KeyValuePair<string, int>(kvp.Key, int.Parse(kvp.Value)));
            var detIntCfg = new Dictionary<string, int>(intValuePairs, StringComparer.OrdinalIgnoreCase);

            int w = detIntCfg["number_of_imaging_units"]
                * detIntCfg["number_of_asics_per_imaging_unit"]
                * detIntCfg["number_of_asic_cols"];
            int h = detIntCfg["number_of_asic_rows"];
            var pcrBytes = File.ReadAllBytes(@"C:\Users\AlexA\Downloads\mca604\media\card\apps\pcr.config");

            //(x / 128) * bitsPerAsic +
            //(255 - y) * 17 * 128 +
            //14 * 128 + (x % 128);
            var bitsSet = Enumerable.Range(557184 * 11, 557184)
                .Where (bitPos => PcrTestImplementation.getbit(pcrBytes, bitPos)!=0)
                .Select(t => (
                a: t / 557184,
                b: ((t % 557184) % (17 * 128)) / 128,
                x: (t % 557184) % 128,
                y: 255 - ((t % 557184) / (17 * 128))
                ))
                .ToList();

            //var allZero = pcrBytes.SelectMany((t, n) => Enumerable.Range(0,8).Where(b=>)).Where(t => t.v != 0)
            //    .Select(t => (a: (t.i * 8) / 557184,
            //    b: (((t.i * 8) % 557184) % (17 * 128)) / 128,
            //    x: (((t.i * 8) % 557184) % (17 * 128)) % 128,
            //    y: 255 - ((t.i * 8) % 557184) / (17 * 128)
            //    ))
            //    .ToList();
            for (int nAsic = 0; nAsic < 16; nAsic++)
            {
                for (int y = 80; y < 88; y++)
                    for (int x = 1240; x < 1243; x++)
                    {
                        int t = PcrTestImplementation.GetPcrXY(pcrBytes, x, y);
                        Console.Write($"{t:D6} ");
                    }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// This program reads a file specifying bad pixels
        /// The output file is the binary bitstream to be stored on the detector
        /// </summary>
        /// <param name="args">Json input file See Test.Json for example</param>
        static void Main(string[] args)
        {
            ReadPcrTest();
            Environment.Exit(0);
            var di = DetConfigReader.DetConfigReader.TelnetDetInfo("192.168.184.130").Result;
            Console.WriteLine(di);

            //var rem = "scp -o "StrictHostKeyChecking = no" -F "NUL" -P 22  "C: \Users\AlexA\test.ias" root@192.168.184.130:/media/card/user/";
            //Test();
            //Sort out parameters
            Console.WriteLine("Hello World!");
            var jsonPath = (args is { Length: > 0 }) ? args[0] ?? null : null;
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"Cannot read Input json File {jsonPath} ");
                Console.WriteLine($"Usage {Path.GetFileName(Environment.ProcessPath)} BadPixelFile.json");
                // comment for testing
                //Environment.Exit(-1);
                jsonPath = @"Test.json";
            }
            string pcrOutputPath = Path.Combine(Path.GetDirectoryName(jsonPath), $"pcrmask.config");

            //load the input
            var bpl = LoadJson(jsonPath);

            Console.WriteLine($"Detector {bpl.DetectorId}; {bpl.ThorOrHydra} with {bpl.AsicCount} Asics ");
            Console.WriteLine($"Calculations follow: ");
            Console.WriteLine(JsonSerializer.Serialize(bpl.DetectorPcrInfo, JsonOptions));

            var detAsicInfo = bpl.DetectorPcrInfo; //Thor with { AsicCount = 8 };
            var pcrData = new byte[detAsicInfo.PcrByteSize];

            //Loop over the bad pixels in list setting the disable bit (bit 14 on the asic pixel pcr)
            foreach (var badpix in bpl.BadPixels)
            {
                if (badpix.Category != BadPixelFixCategory.PCRFix) continue;
                //transform to bit stream position and flip the bit
                var badPixBitpos = detAsicInfo.IndexOfBadPixeInPCR(badpix.RawX, badpix.RawY, pcrBit: 14);
                setbit(pcrData, badPixBitpos);
            }

            // Write the PCR bit stream as a file
            File.WriteAllBytes(pcrOutputPath, pcrData);
            Console.WriteLine($"Input  {jsonPath} ");
            Console.WriteLine($"Output  {pcrOutputPath} ");
            Console.WriteLine($@"Please Copy output from {pcrOutputPath} to the detector {'\n'} at PCR_user_file=/media/card/user/pcrmask.config ");
            Console.WriteLine($@"(The name is set in detector.config) ");
            Console.WriteLine($@" ");
        }
        static public JsonSerializerOptions JsonOptions
        {
            get
            {
                var jso = new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    IncludeFields = true,
                };
                jso.Converters.Add(new JsonStringEnumConverter());
                return jso;
            }
        }
        static public BadPixelList LoadJson(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            var bpl = JsonSerializer.Deserialize<BadPixelList>(json, JsonOptions);
            LintBadPixelList(bpl);
            return bpl;
        }
        static public void LintBadPixelList(BadPixelList bpl)
        {
            Debug.Assert(bpl.JsonVersion <= Global.CurrentJsonVersion);
            var detAsicInfo = bpl.DetectorPcrInfo; //Thor with { AsicCount = 8 };
            foreach (var badpix in bpl.BadPixels)
            {
                Debug.Assert((uint)badpix.RawX < detAsicInfo.DetWidth);
                Debug.Assert((uint)badpix.RawY < detAsicInfo.AsicHeight);
            }
        }
        static void setbit(Span<byte> bytes, long bitPos)
        {
            bytes[(int)(bitPos / 8)] |= (byte)(128 >> (int)(bitPos % 8));
        }
        static void Test()
        {
            var di = DetConfigReader.DetConfigReader.TelnetDetInfo("192.168.184.130").Result;
            Console.WriteLine(di);
            var detectorId = "001";
            var bpl = new BadPixelList(detectorId, DetectorModelClass.Thor, 8);
            var swFix = BadPixelFixCategory.SoftwareFix;
            bpl.BadPixels.AddRange(new List<BadPixelRec>() { new(12, 21, swFix), new(18, 81) });
            bpl.BadColumns.Add(57);
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
            bpl = JsonSerializer.Deserialize<BadPixelList>(json, jso);
            var detAsicInfo = bpl.DetectorPcrInfo; //Thor with { AsicCount = 8 };
            foreach (var badpix in bpl.BadPixels)
            {
                Debug.Assert((uint)badpix.RawX < detAsicInfo.DetWidth);
                Debug.Assert((uint)badpix.RawY < detAsicInfo.AsicHeight);
                if (badpix.Category != BadPixelFixCategory.PCRFix) continue;
                int kpbp = PcrTestImplementation.KillPixBitPos(badpix.RawX, badpix.RawY);
                var badPixBitpos = detAsicInfo.IndexOfBadPixeInPCR(badpix.RawX, badpix.RawY, pcrBit: 14);
                Console.WriteLine($"{kpbp} ==? {badPixBitpos} ");
            }
        }
    }
}