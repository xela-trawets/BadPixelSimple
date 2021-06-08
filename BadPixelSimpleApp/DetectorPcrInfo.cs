using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BadPixelSimpleApp
{
    public record DetectorPcrDescriptor(int AsicWidth, int AsicHeight, int AsicCount)
    {
        public int DetWidth => AsicCount * AsicWidth;
        //! note Folded double detctors need unfolded coordinated
        public int BitsPerPixelPcr => 17;
        //int PcrDisableBitPos = 14;
        public Int64 PixelsPerAsic => AsicHeight * AsicWidth;// 256 * 128;
        public int BitsPerRow => AsicWidth * BitsPerPixelPcr;//128 * 17;
        public int ExtraBitsPerColumn => 1; // May be added by FPGA ?
        public int FinalClockBitsPerAsic => 0;//1; //if the final Pcr Clock bit per Aisc is not added by hardware - then Add it here

        public int ExtraBitsPerAsic => AsicWidth * ExtraBitsPerColumn + FinalClockBitsPerAsic;
        Int64 RealBitsPerAsic => BitsPerPixelPcr * PixelsPerAsic;
        Int64 BitsPerAsic => BitsPerPixelPcr * PixelsPerAsic + ExtraBitsPerAsic;

        //int BitsPerAsic => AsicWidth * AsicHeight * BitsPerPixelPcr + AsicWidth;// 557184;
        public int BytesPerAsic => (int)((BitsPerAsic + 31) / 32) * 4;
        public int PcrByteSize => BytesPerAsic * AsicCount;
        public (int byteIndex, int bitInByte) IndexOfBadPixeInPCR(int badPixelX, int badPixelY)
        {
            int pcrBit = 14;
            Int64 finalBitPos = IndexOfBadPixeInPCR(badPixelX, badPixelY, pcrBit);
            return ((int)(finalBitPos / 8), (int)(finalBitPos % 8));
            //Set_bit(pcrBitOffset + DisableBitPosOffset, 1);//Assume digital register order- not little endian
        }
        public Int64 IndexOfBadPixeInPCR(int badPixelX, int badPixelY, int pcrBit = 14)
        {
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

            return finalBitPos;
            //Set_bit(pcrBitOffset + DisableBitPosOffset, 1);//Assume digital register order- not little endian
        }
    };
}
