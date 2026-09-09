using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class IconBuilder
{
    static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: IconBuilder.exe input.jpg output.ico");

        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> images = new List<byte[]>();
        using (Bitmap source = new Bitmap(args[0]))
        {
            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap scaled = new Bitmap(sizes[i], sizes[i], PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(scaled))
                using (MemoryStream image = new MemoryStream())
                {
                    graphics.Clear(Color.Transparent);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.DrawImage(source, new Rectangle(0, 0, sizes[i], sizes[i]), 0, 0,
                        source.Width, source.Height, GraphicsUnit.Pixel);
                    scaled.Save(image, ImageFormat.Png);
                    images.Add(image.ToArray());
                }
            }
        }

        using (BinaryWriter output = new BinaryWriter(File.Create(args[1])))
        {
            output.Write((ushort)0);
            output.Write((ushort)1);
            output.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                output.Write((byte)0);
                output.Write((byte)0);
                output.Write((ushort)1);
                output.Write((ushort)32);
                output.Write((uint)images[i].Length);
                output.Write((uint)offset);
                offset += images[i].Length;
            }
            for (int i = 0; i < images.Count; i++) output.Write(images[i]);
        }
    }
}
