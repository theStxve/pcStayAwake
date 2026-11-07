using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace PcStayAwake
{
    internal enum IconVariant
    {
        /// <summary>Aktiv: Bildschirm und PC bleiben wach.</summary>
        Active,
        /// <summary>Aktiv: nur der PC bleibt wach, Bildschirm darf aus.</summary>
        SystemOnly,
        /// <summary>Pausiert.</summary>
        Paused
    }

    /// <summary>Zeichnet das App-Icon vektoriell in jeder Größe und schreibt .ico-Dateien.</summary>
    internal static class IconArt
    {
        public static readonly int[] StandardSizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

        public static Bitmap Render(int size, IconVariant variant)
        {
            Color top, bottom;
            switch (variant)
            {
                case IconVariant.SystemOnly:
                    top = Color.FromArgb(139, 92, 246); bottom = Color.FromArgb(76, 29, 149); break;
                case IconVariant.Paused:
                    top = Color.FromArgb(156, 163, 175); bottom = Color.FromArgb(75, 85, 99); break;
                default:
                    top = Color.FromArgb(56, 189, 248); bottom = Color.FromArgb(37, 99, 235); break;
            }

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size;

                // Hintergrund-Kachel
                var tile = new RectangleF(0.02f * s, 0.02f * s, 0.96f * s, 0.96f * s);
                using (var path = RoundedRect(tile, 0.22f * s))
                using (var brush = new LinearGradientBrush(tile, top, bottom, LinearGradientMode.Vertical))
                    g.FillPath(brush, path);

                // Monitor
                var monitor = new RectangleF(0.16f * s, 0.20f * s, 0.68f * s, 0.46f * s);
                using (var path = RoundedRect(monitor, 0.07f * s))
                    g.FillPath(Brushes.White, path);

                var screen = new RectangleF(0.22f * s, 0.26f * s, 0.56f * s, 0.34f * s);
                using (var path = RoundedRect(screen, 0.035f * s))
                using (var brush = new SolidBrush(Darken(bottom, 0.45f)))
                    g.FillPath(brush, path);

                // Standfuß
                g.FillRectangle(Brushes.White, 0.44f * s, 0.655f * s, 0.12f * s, 0.10f * s);
                using (var path = RoundedRect(new RectangleF(0.30f * s, 0.74f * s, 0.40f * s, 0.07f * s), 0.035f * s))
                    g.FillPath(Brushes.White, path);

                // Auge im Bildschirm
                float cx = 0.50f * s, cy = 0.43f * s, w = 0.20f * s, h = 0.11f * s;
                if (variant == IconVariant.Paused)
                {
                    // Pause-Symbol
                    float bw = 0.065f * s, bh = 0.20f * s, gap = 0.05f * s;
                    using (var path = RoundedRect(new RectangleF(cx - gap / 2 - bw, cy - bh / 2, bw, bh), bw * 0.3f))
                        g.FillPath(Brushes.White, path);
                    using (var path = RoundedRect(new RectangleF(cx + gap / 2, cy - bh / 2, bw, bh), bw * 0.3f))
                        g.FillPath(Brushes.White, path);
                }
                else
                {
                    using (var eye = new GraphicsPath())
                    {
                        eye.AddBezier(cx - w, cy, cx - w / 2, cy - h * 1.35f, cx + w / 2, cy - h * 1.35f, cx + w, cy);
                        eye.AddBezier(cx + w, cy, cx + w / 2, cy + h * 1.35f, cx - w / 2, cy + h * 1.35f, cx - w, cy);
                        eye.CloseFigure();
                        g.FillPath(Brushes.White, eye);
                    }
                    float r = 0.065f * s;
                    using (var brush = new SolidBrush(bottom))
                        g.FillEllipse(brush, cx - r, cy - r, 2 * r, 2 * r);
                }
            }
            return bmp;
        }

        public static Icon CreateIcon(IconVariant variant, params int[] sizes)
        {
            if (sizes == null || sizes.Length == 0) sizes = StandardSizes;
            var ms = new MemoryStream();
            WriteIco(ms, variant, sizes);
            ms.Position = 0;
            return new Icon(ms);
        }

        public static void WriteIco(Stream output, IconVariant variant, int[] sizes)
        {
            var images = new List<byte[]>();
            foreach (int size in sizes)
            {
                using (var bmp = Render(size, variant))
                    images.Add(size >= 256 ? EncodePng(bmp) : EncodeDib(bmp));
            }

            var bw = new BinaryWriter(output);
            bw.Write((ushort)0);            // reserved
            bw.Write((ushort)1);            // type: icon
            bw.Write((ushort)sizes.Length); // count

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                bw.Write((byte)(size >= 256 ? 0 : size));
                bw.Write((byte)(size >= 256 ? 0 : size));
                bw.Write((byte)0);   // palette
                bw.Write((byte)0);   // reserved
                bw.Write((ushort)1); // planes
                bw.Write((ushort)32);// bpp
                bw.Write((uint)images[i].Length);
                bw.Write((uint)offset);
                offset += images[i].Length;
            }
            foreach (var data in images) bw.Write(data);
            bw.Flush();
        }

        static byte[] EncodePng(Bitmap bmp)
        {
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        static byte[] EncodeDib(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            int maskStride = ((w + 31) / 32) * 4;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[data.Stride * h];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            using (var ms = new MemoryStream())
            {
                var bw = new BinaryWriter(ms);
                // BITMAPINFOHEADER
                bw.Write(40); bw.Write(w); bw.Write(h * 2);
                bw.Write((ushort)1); bw.Write((ushort)32);
                bw.Write(0); bw.Write(w * h * 4 + maskStride * h);
                bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
                // Pixel (bottom-up, BGRA)
                for (int y = h - 1; y >= 0; y--)
                    bw.Write(pixels, y * data.Stride, w * 4);
                // AND-Maske (leer, Alphakanal wird verwendet)
                bw.Write(new byte[maskStride * h]);
                bw.Flush();
                return ms.ToArray();
            }
        }

        static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static Color Darken(Color c, float factor)
        {
            return Color.FromArgb(c.A, (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));
        }
    }
}
