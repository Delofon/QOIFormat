using System;
using System.IO;

using SixLabors.ImageSharp; // used only for decoded QOI image encoding to a usable format
using SixLabors.ImageSharp.PixelFormats;

using QOIShared;

namespace QOIDecoder
{
    class DecoderProgram
    {
        static bool verbose = false;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            if (args.Length == 1)
            {
                throw new ArgumentException("At least two arguments are required.");
            }

            string[] path = new string[2];
            {
                int pathIndex = 0;
                foreach (string arg in args)
                {
                    if (arg.StartsWith('-'))
                    {
                        foreach (char opt in args[0].Substring(1))
                        {
                            if (opt == 'h')
                            {
                                PrintUsage();
                                return;
                            }
                            else if (opt == 'v')
                            {
                                verbose = true;
                            }
                        }
                        continue;
                    }

                    Path.GetFullPath(arg);
                    path[pathIndex++] = arg;
                }
            }

            FileStream fs = new FileStream(path[0], FileMode.Open, FileAccess.Read);
            BinaryReader reader = new BinaryReader(fs);

            // Check for the "qoif" chars at the beginning
            {
                uint magic = reader.ReadUInt32();

                // "qoif" in LE
                if(magic != 0x66696F71)
                {
                    Console.WriteLine(magic);
                    throw new InvalidDataException("The source path is not a QOIF image.");
                }
            }

            // The next 64 bytes are the dimensions of the image.
            uint width, height;

            // Read in BE
            {
                byte[] size = reader.ReadBytes(8);

                width = (uint)(size[0] << 24 | size[1] << 16 | size[2] << 8 | size[3]);
                height = (uint)(size[4] << 24 | size[5] << 16 | size[6] << 8 | size[7]);
            }

            reader.ReadBytes(2); // Next two bytes are the channel and the colourspace codes. Useless junk lol

            byte[] byteData = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

            reader.Close();
            fs.Close();
            reader.Dispose();
            fs.Dispose();

            Colour[] img = new Colour[width * height];
            Colour[] array = new Colour[64];
            Colour previous = new Colour(0, 0, 0, 255);

            //Verbose(previous.a.ToString("x") + "\n\n\n\n\n\n\n");

            int imgIndex = 0;
            for(int i = 0; i < byteData.Length - 8; i++)
            {
                byte b1 = byteData[i];

                byte leading_bits = (byte)(b1 >> 6);
                //Verbose($"Image Index: {imgIndex}\n4 Bytes: {b1:x} {byteData[i + 1]:x} {byteData[i + 2]:x} {byteData[i + 3]:x}\nByte1 leading_bits: {leading_bits:x}");
                if (b1 == (byte)Tags.QOI_OP_RGBA)
                {
                    byte r = byteData[++i];
                    byte g = byteData[++i];
                    byte b = byteData[++i];
                    byte a = byteData[++i];
                    img[imgIndex] = new Colour(r, g, b, a);
                    Verbose($"QOI_OP_RGBA: {r} {g} {b} {a}");
                }
                else if (b1 == (byte)Tags.QOI_OP_RGB)
                {
                    byte r = byteData[++i];
                    byte g = byteData[++i];
                    byte b = byteData[++i];
                    img[imgIndex] = new Colour(r, g, b, previous.a);
                    Verbose($"QOI_OP_RGB: {r} {g} {b}");
                }
                else
                {
                    //Verbose($"Leading bits: {leading_bits}");
                    switch (leading_bits)
                    {
                        case (byte)Tags.QOI_OP_INDEX >> 6:
                            byte index = (byte)((b1 << 2) >> 2);
                            img[imgIndex] = array[index];
                            Verbose($"QOI_OP_INDEX: {index}");
                            break;
                        case (byte)Tags.QOI_OP_DIFF >> 6:
                        {
                            // current - previous
                            //Verbose($"b1: {b1}");
                            int dr = ((byte)(b1 << 2) >> 6) - 2;
                            int dg = ((byte)(b1 << 4) >> 6) - 2;
                            int db = ((byte)(b1 << 6) >> 6) - 2;
                            Colour newCol = new Colour((byte)((previous.r + dr) % 256), (byte)((previous.g + dg) % 256), (byte)((previous.b + db) % 256), previous.a);
                            img[imgIndex] = newCol;
                            Verbose($"QOI_OP_DIFF: {dr} {dg} {db}");
                            break;
                        }
                        case (byte)Tags.QOI_OP_LUMA >> 6:
                        {
                            byte b2 = byteData[++i];
                            int diff_green = ((byte)(b1 << 2) >> 6) - 32;
                            int dr_dg      =  (byte)(b2 >> 4) - 8;
                            int db_dg      = ((byte)(b2 << 4) >> 4) - 8;
                            int cur_g = (byte)(previous.g + diff_green) % 256;
                            int cur_r = (byte)(dr_dg + diff_green + previous.r) % 256;
                            int cur_b = (byte)(db_dg + diff_green + previous.b) % 256;
                            Colour newCol = new Colour((byte)cur_r, (byte)cur_g, (byte)cur_b, previous.a);
                            img[imgIndex] = newCol;
                            Verbose($"QOI_OP_LUMA: {diff_green} {dr_dg} {db_dg}");
                            break;
                        }
                        case (byte)Tags.QOI_OP_RUN >> 6:
                            int run = ((byte)(b1 << 2) >> 2) + 1;
                            Verbose($"QOI_OP_RUN: {run}");
                            while (run > 0)
                            {
                                //Verbose($"imgIndex: {imgIndex} arrSize: {img.Length}");
                                img[imgIndex++] = previous;
                                run--;
                            }
                            imgIndex--;
                            break;
                    }
                }

                
                previous = img[imgIndex];
                array[QOIChunks.getArrayIndex(previous)] = previous;
                imgIndex++;

                //Console.WriteLine($"Pixel: {previous.r} {previous.g} {previous.b} {previous.a}\n");
            }

            byte[] imgBytes = new byte[width * height * 4];
            for(int i = 0; i < img.Length; i++)
            {
                imgBytes[4 * i]     = img[i].r;
                imgBytes[4 * i + 1] = img[i].g;
                imgBytes[4 * i + 2] = img[i].b;
                imgBytes[4 * i + 3] = img[i].a;
            }

            FileStream dest = new FileStream(path[1], FileMode.Create, FileAccess.Write);
            Image<Rgba32> image = Image.LoadPixelData<Rgba32>(imgBytes, (int)width, (int)height);
            image.SaveAsPng(dest);

            dest.Close();
            dest.Dispose();

            if(verbose)
            {
                FileStream file_bytes = new FileStream("./file_bytes.txt", FileMode.Create, FileAccess.Write);
                StreamWriter writer = new StreamWriter(file_bytes);
                for(int i = 0; i < imgBytes.Length; i++)
                {
                    writer.Write($"{imgBytes[i]} ");
                }
                writer.Close();
                file_bytes.Close();

                writer.Dispose();
                file_bytes.Dispose();
            }
        }

        static void Verbose(string str) { if (verbose) Console.WriteLine(str); }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: qoidecode [options] <source> <destination>\nOptions:\n-h  Print usage and exit.\n-v   Verbose logging.");
        }
    }
}
