using System;
using System.Drawing.Imaging;
using System.IO;

namespace PcStayAwake.Tools
{
    /// <summary>Erzeugt assets/app.ico und ein PNG-Logo für die README aus IconArt.</summary>
    internal static class IconGen
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: IconGen <assets-dir>");
                return 1;
            }
            string dir = args[0];
            Directory.CreateDirectory(dir);

            using (var fs = File.Create(Path.Combine(dir, "app.ico")))
                IconArt.WriteIco(fs, IconVariant.Active, IconArt.StandardSizes);

            using (var bmp = IconArt.Render(256, IconVariant.Active))
                bmp.Save(Path.Combine(dir, "logo.png"), ImageFormat.Png);

            using (var bmp = IconArt.Render(64, IconVariant.Active))
                bmp.Save(Path.Combine(dir, "tray-active.png"), ImageFormat.Png);
            using (var bmp = IconArt.Render(64, IconVariant.SystemOnly))
                bmp.Save(Path.Combine(dir, "tray-systemonly.png"), ImageFormat.Png);
            using (var bmp = IconArt.Render(64, IconVariant.Paused))
                bmp.Save(Path.Combine(dir, "tray-paused.png"), ImageFormat.Png);

            Console.WriteLine("Icons geschrieben nach " + dir);
            return 0;
        }
    }
}
