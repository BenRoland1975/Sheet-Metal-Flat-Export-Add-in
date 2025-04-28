using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;

namespace RoesleinAddIn
{
    public static class ImageConverter
    {
        public static void CreateIconStrips()
        {
            string resourcePath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "Resources");

            // Source PNG files
            string[] sourceFiles = {
                Path.Combine(resourcePath, "Produce Assy.png"),
                Path.Combine(resourcePath, "Produce Single Part.png"),
                Path.Combine(resourcePath, "Settings.png")
            };

            // Create icon strips for each size
            CreateIconStrip(sourceFiles, Path.Combine(resourcePath, "toolbar_icons_20.bmp"), 20);
            CreateIconStrip(sourceFiles, Path.Combine(resourcePath, "toolbar_icons_32.bmp"), 32);
            CreateIconStrip(sourceFiles, Path.Combine(resourcePath, "toolbar_icons_40.bmp"), 40);
        }

        private static void CreateIconStrip(string[] sourceFiles, string outputFile, int size)
        {
            // Create a new bitmap to hold all icons side by side
            using (Bitmap strip = new Bitmap(size * sourceFiles.Length, size, PixelFormat.Format8bppIndexed))
            {
                using (Graphics g = Graphics.FromImage(strip))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    // Add each icon to the strip
                    for (int i = 0; i < sourceFiles.Length; i++)
                    {
                        using (Bitmap source = new Bitmap(sourceFiles[i]))
                        {
                            // Create temporary bitmap for resizing
                            using (Bitmap resized = new Bitmap(size, size))
                            {
                                using (Graphics rg = Graphics.FromImage(resized))
                                {
                                    rg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                    rg.DrawImage(source, 0, 0, size, size);
                                }
                                // Draw the resized image onto the strip
                                g.DrawImage(resized, i * size, 0);
                            }
                        }
                    }
                }

                // Save with 256 color palette
                ColorPalette palette = strip.Palette;
                for (int i = 0; i < 256; i++)
                {
                    palette.Entries[i] = Color.FromArgb(255, i, i, i);
                }
                strip.Palette = palette;

                strip.Save(outputFile, ImageFormat.Bmp);
            }
        }
    }
} 