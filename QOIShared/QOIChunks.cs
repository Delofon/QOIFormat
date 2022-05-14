using System;

namespace QOIShared
{
    public static class QOIChunks
    {
        public static int getArrayIndex(byte r, byte g, byte b, byte a)
        {
            return (r * 3 + g * 5 + b * 7 + a * 11) % 64;
        }
        public static int getArrayIndex(Colour colour)
        {
            return getArrayIndex(colour.r, colour.g, colour.b, colour.a);
        }
    }

    // Chunk tags as defined by the specification
    public enum Tags
    {
        QOI_OP_RGB = 0b11111110,
        QOI_OP_RGBA = 0b11111111,
        QOI_OP_INDEX = 0b00000000, // RSH 6 to get just the significant bits
        QOI_OP_DIFF = 0b01000000,
        QOI_OP_LUMA = 0b10000000,
        QOI_OP_RUN = 0b11000000
    }
}
