using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BadPixelSimpleApp
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
        public static int KillPixBitPos(int x, int y) =>
            (x / 128) * bitsPerAsic +
            (255 - y) * 17 * 128 +
            14 * 128 + (x % 128);
        public static void KillPix(byte[] pcrBytes, int x, int y)
        {
            int bitPos = KillPixBitPos(x, y);
            setbit(pcrBytes, bitPos);
        }
    }
}
