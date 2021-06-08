using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BadPixelSimpleApp
{
    //public struct BadPixel { public int RawX; public int RawY; };
    public enum BadPixelFixCategory
    {
        DontFix = 0,
        PCRFix = 1,
        SoftwareFix = 2
    }
    public enum DetectorModelClass
    {
        Missing = 0,
        Thor = 1,
        Hydra = 2
    }
    public record BadPixelRec(int RawX, int RawY, BadPixelFixCategory Category = BadPixelFixCategory.PCRFix);
    public record BadPixelList(string DetectorId, DetectorModelClass ThorOrHydra, int AsicCount, string JsonVersionName = "Joe",int JsonVersion = CurrentJsonVersion)
    {
        public const int CurrentJsonVersion = 90;
        //public List<List<int>> BadPixels { set; get; } = new();
        //public List<(int RawX, int RawY)> BadPixels { set; get; } = new();
        static DetectorPcrDescriptor Thor = new DetectorPcrDescriptor(128, 256, 0);//set width before use
        static DetectorPcrDescriptor Hydra = new DetectorPcrDescriptor(256, 64, 0);//set width before use
        public DetectorPcrDescriptor DetectorPcrInfo => ThorOrHydra switch
        {
            DetectorModelClass.Thor => Thor with { AsicCount = AsicCount },
            DetectorModelClass.Hydra => Hydra with { AsicCount = AsicCount },
            _ => throw new("unknown detector/asic model type ")
        };

        public List<BadPixelRec> BadPixels { set; get; } = new();
        public List<int> BadRows { set; get; } = new();
        public List<int> BadColumns { set; get; } = new();
    }
}
