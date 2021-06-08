using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BadPixelSimpleApp
{
    public record BadPixel(int RawX, int RawY);
    public record BadRow(int RawY);
    public record BadColumn(int RawX);
    public record BadPixelList(string DetectorId)
    {
        public static List<BadPixel> BadPixels { set; get; } = new();
        public static List<BadRow> BadRows { set; get; } = new();
        public static List<BadColumn> BadColumns { set; get; } = new();
    }
}
