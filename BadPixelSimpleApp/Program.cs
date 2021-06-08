using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BadPixelSimpleApp
{
    class Program
    {/// <summary>
     /// This program reads a file specifying bad pixels
     /// The output file is the binary bitstream to be stored on the detector
     /// </summary>
     /// <param name="args">Json input file See Test.Json for example</param>
        static void Main(string[] args)
        {
            Test();
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