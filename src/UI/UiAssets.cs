using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StrandedDeepModManager.UI
{
    internal static class UiAssets
    {
        private static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static string AssetRoot
        {
            get
            {
                return Path.Combine(
                    Application.StartupPath,
                    "assets");
            }
        }

        public static Image HeaderBackground()
        {
            return Load(
                Path.Combine(
                    "background",
                    "mod-manager-header-1672x250.png"));
        }

        public static Image Identity(string packageId)
        {
            if (String.IsNullOrWhiteSpace(packageId))
                return null;

            return Load(
                Path.Combine(
                    "identity",
                    "72",
                    packageId + ".png"));
        }

        public static Image SystemIcon(string key)
        {
            if (String.IsNullOrWhiteSpace(key))
                return null;

            return Load(
                Path.Combine(
                    "system-ui",
                    "24",
                    key + ".png"));
        }

        public static Image Highlight(string key)
        {
            if (String.IsNullOrWhiteSpace(key))
                return null;

            return Load(
                Path.Combine(
                    "highlights",
                    "32",
                    key + ".png"));
        }

        private static Image Load(string relativePath)
        {
            string key =
                relativePath.Replace(
                    Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar);

            Image cached;

            if (Cache.TryGetValue(key, out cached))
                return cached;

            string path =
                Path.Combine(
                    AssetRoot,
                    key);

            if (!File.Exists(path))
                return null;

            using (Image source = Image.FromFile(path))
            {
                Image copy =
                    new Bitmap(source.Width, source.Height);

                using (Graphics graphics =
                    Graphics.FromImage(copy))
                {
                    graphics.DrawImage(
                        source,
                        new Rectangle(
                            0,
                            0,
                            source.Width,
                            source.Height));
                }

                Cache[key] = copy;
                return copy;
            }
        }
    }
}
