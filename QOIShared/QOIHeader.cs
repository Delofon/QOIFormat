using System;

namespace QOIShared
{
    public struct QOIHeader
    {
        public uint width, height;
        public byte channels; // 3 = RGB, 4 = RGBA
        public byte colorspace; // 0 - sRGB with linear alpha, 1 - all linear

        public QOIHeader(uint width, uint height, byte channels, byte colorspace)
        {
            if(width <= 0)
            {
                throw new ArgumentException("The width of the image is not positive.", "width");
            }
            this.width = width;

            if(height <= 0)
            {
                throw new ArgumentException("The height of the image is not positive.", "height");
            }
            this.height = height;

            if(!(channels != 3 && channels != 4))
            {
                throw new ArgumentException("The amount of channels is neither 3 nor 4.", "channels");
            }
            this.channels = channels;

            if(!(colorspace != 0 && colorspace != 1))
            {
                throw new ArgumentException("The colour space code is neither 0 nor 1.", "colorspace");
            }
            this.colorspace = colorspace;
        }
    }
}
