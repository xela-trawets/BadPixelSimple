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
    public record BadPixelRec(int RawX, int RawY, BadPixelFixCategory Category = BadPixelFixCategory.PCRFix);
    public record BadRow(int RawY);
    public record BadColumn(int RawX);
    public record BadPixelList(string DetectorId)
    {
        //public List<List<int>> BadPixels { set; get; } = new();
        //public List<(int RawX, int RawY)> BadPixels { set; get; } = new();
        public List<BadPixelRec> BadPixels { set; get; } = new();
        public List<int> BadRows { set; get; } = new();
        public List<int> BadColumns { set; get; } = new();
    }
}
