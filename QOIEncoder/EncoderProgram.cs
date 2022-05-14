using System;
using System.Collections.Generic;
using System.IO;

using SixLabors.ImageSharp; // used only for initial image decoding
using SixLabors.ImageSharp.PixelFormats;

using QOIShared;

namespace QOIEncoder
{
    class EncoderProgram
    {
        static bool verbose = false;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            if(args.Length == 1)
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
                        foreach (char opt in arg.Substring(1))
                        {
                            switch(opt)
                            {
                                case 'h':
                                    PrintUsage();
                                    return;
                                case 'v':
                                    verbose = true;
                                    break;
                            }
                        }
                        continue;
                    }

                    Path.GetFullPath(arg);
                    path[pathIndex++] = arg;
                }
            }

            uint width, height;
            byte[] bitmap;
            {
                FileStream fs = new FileStream(path[0], FileMode.Open, FileAccess.Read);
                Image<Rgba32> image = Image.Load<Rgba32>(fs);

                fs.Close();
                fs.Dispose();

                width = (uint)image.Width;
                height = (uint)image.Height;

                bitmap = new byte[width * height * 4];
                image.CopyPixelDataTo(bitmap);
            }

            List<byte> byteData = new List<byte>();

            // Write header
            byteData.Add(113); byteData.Add(111); byteData.Add(105); byteData.Add(102);
            byteData.Add((byte)(width >> 24)); byteData.Add((byte)(width >> 16)); byteData.Add((byte)(width >> 8)); byteData.Add((byte)width);
            byteData.Add((byte)(height >> 24)); byteData.Add((byte)(height >> 16)); byteData.Add((byte)(height >> 8)); byteData.Add((byte)height);
            byteData.Add(4); byteData.Add(0);

            Colour[] img = new Colour[width * height];

            for(int i = 0; i < img.Length; i++)
            {
                img[i] = new Colour(bitmap[4 * i], bitmap[4 * i + 1], bitmap[4 * i + 2], bitmap[4 * i + 3]);
            }

            Colour[] array = new Colour[64];
            Colour previous = new Colour(0, 0, 0, 255);

            int cur_run_len = 0;
            for (int i = 0; i < img.Length; i++)
            {
                if (cur_run_len < 62 && img[i].r == previous.r && img[i].g == previous.g && img[i].b == previous.b && img[i].a == previous.a)
                {
                    cur_run_len++;
                    continue;
                }

                if (cur_run_len != 0)
                {
                    byteData.Add((byte)((byte)Tags.QOI_OP_RUN | (byte)(cur_run_len - 1)));
                    Verbose($"QOI_OP_RUN: {cur_run_len}");
                    cur_run_len = 0;
                    i--;
                    continue;
                }

                else
                {
                    int index = QOIChunks.getArrayIndex(img[i]);
                    Colour arrcol = array[index];
                    byte arr_r = arrcol.r;
                    byte arr_g = arrcol.g;
                    byte arr_b = arrcol.b;
                    byte arr_a = arrcol.a;

                    if (img[i].r == arr_r && img[i].g == arr_g && img[i].b == arr_b && img[i].a == arr_a)
                    {
                        byteData.Add((byte)((byte)Tags.QOI_OP_INDEX | (byte)index));
                        Verbose($"QOI_OP_INDEX: {index}");
                    }

                    else if(img[i].a == previous.a)
                    {
                        sbyte dr = (sbyte)(img[i].r - previous.r);
                        sbyte dg = (sbyte)(img[i].g - previous.g);
                        sbyte db = (sbyte)(img[i].b - previous.b);

                        if (dr >= -2 && dr <= 1 && dg >= -2 && dg <= 1 && db >= -2 && db <= 1)
                        {
                            //Verbose($"byte: {((byte)((byte)Tags.QOI_OP_DIFF | ((byte)(dr + 2)) << 4 | ((byte)(dg + 2)) << 2 | (byte)(db + 2))).ToString("x")}");
                            byteData.Add((byte)((byte)Tags.QOI_OP_DIFF | ((byte)(dr + 2)) << 4 | ((byte)(dg + 2)) << 2 | (byte)(db + 2)));
                            Verbose($"QOI_OP_DIFF: {dr} {dg} {db}");
                        }

                        else
                        {
                            sbyte diff_green = (sbyte)(img[i].g - previous.g             );
                            sbyte dr_dg      = (sbyte)(img[i].r - previous.r - diff_green);
                            sbyte db_dg      = (sbyte)(img[i].b - previous.b - diff_green);

                            if (diff_green >= -32 && diff_green <= 31 && dr_dg >= -8 && dr_dg <= 7 && db_dg >= -8 && db_dg <= 7)
                            {
                                byteData.Add((byte)((byte)Tags.QOI_OP_LUMA | (byte)(diff_green + 32)));
                                byteData.Add((byte)((byte)(dr_dg + 8) << 4 | (byte)(db_dg + 8)));
                                Verbose($"QOI_OP_LUMA: {diff_green} {dr_dg} {db_dg}");
                            }

                            else
                            {
                                byteData.Add((byte)Tags.QOI_OP_RGB);
                                byteData.Add(img[i].r);
                                byteData.Add(img[i].g);
                                byteData.Add(img[i].b);
                                Verbose($"QOI_OP_RGB: {img[i].r} {img[i].g} {img[i].b}");
                            }
                        }
                    }
                    else
                    {
                        byteData.Add((byte)Tags.QOI_OP_RGBA);
                        byteData.Add(img[i].r);
                        byteData.Add(img[i].g);
                        byteData.Add(img[i].b);
                        byteData.Add(img[i].a);
                        Verbose($"QOI_OP_RGBA: {img[i].r} {img[i].g} {img[i].b} {img[i].a}");
                    }
                } 

                previous = img[i];
                array[QOIChunks.getArrayIndex(previous)] = previous;
                //Verbose($"col: {previous.r} {previous.g} {previous.b} {previous.a}");
            }

            // I'd love to remove this and somehow use the above thingy, but I couldn't find a working way. This works though, so...
            if (cur_run_len != 0)
            {
                byteData.Add((byte)((byte)Tags.QOI_OP_RUN | (byte)(cur_run_len - 1)));
                Verbose($"QOI_OP_RUN: {cur_run_len}");
            }

            for (int i = 0; i < 7; i++)
                byteData.Add(0);
            byteData.Add(1);

            FileStream dest = new FileStream(path[1], FileMode.Create, FileAccess.Write);
            BinaryWriter writer = new BinaryWriter(dest, System.Text.Encoding.ASCII);

            writer.Write(byteData.ToArray());

            writer.Close();
            dest.Close();

            writer.Dispose();
            dest.Dispose();
        }

        static void Verbose(string str) { if (verbose) Console.WriteLine(str); }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: qoiencode [options] <source> <destination>\nOptions:\n-h   Print usage and exit.\n-v   Verbose logging.");
        }
    }
}
