using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace RoesleinAddIn
{
    /// <summary>
    /// Utility class for creating SolidWorks-compatible image strips with magenta transparency
    /// </summary>
    public static class MagentaImageStripCreator
    {
        // Magenta color for transparency (industry standard)
        private static readonly Color TransparentColor = Color.FromArgb(255, 255, 0, 255); // Magenta

        /// <summary>
        /// Creates SolidWorks-compatible image strips from individual BMP files
        /// Sets magenta as the transparent color
        /// </summary>
        /// <param name="sourceIndividualIconPath">Path to the directory containing individual source BMP icons.</param>
        /// <param name="outputGeneratedStripPath">Path to the directory where combined icon strips will be saved.</param>
        public static void CreateAllStrips(string sourceIndividualIconPath, string outputGeneratedStripPath)
        {
            try
            {
                // Output paths for the image strips will use outputGeneratedStripPath
                string smallStripPath = Path.Combine(outputGeneratedStripPath, "ConnexIcons_20x20.bmp");
                string mediumStripPath = Path.Combine(outputGeneratedStripPath, "ConnexIcons_32x32.bmp");
                string largeStripPath = Path.Combine(outputGeneratedStripPath, "ConnexIcons_40x40.bmp");

                // Source icon paths will use sourceIndividualIconPath
                string[] smallIcons = {
                    Path.Combine(sourceIndividualIconPath, "Produce Assy 20x20.bmp"),
                    Path.Combine(sourceIndividualIconPath, "Produce Single Part 20x20.bmp"),
                    Path.Combine(sourceIndividualIconPath, "Export BOM 20x20.bmp"), 
                    Path.Combine(sourceIndividualIconPath, "Settings 20x20.bmp")     
                };

                string[] mediumIcons = {
                    Path.Combine(sourceIndividualIconPath, "Produce Assy 32x32.bmp"),
                    Path.Combine(sourceIndividualIconPath, "Produce Single Part 32x32.bmp"),
                    Path.Combine(sourceIndividualIconPath, "Export BOM 32x32.bmp"), 
                    Path.Combine(sourceIndividualIconPath, "Settings 32x32.bmp")     
                };

                string[] largeIcons = {
                    Path.Combine(sourceIndividualIconPath, "Produce Assy 40x40.bmp"),
                    Path.Combine(sourceIndividualIconPath, "Produce Single Part 40x40.bmp"),
                    Path.Combine(sourceIndividualIconPath, "Export BOM 40x40.bmp"), 
                    Path.Combine(sourceIndividualIconPath, "Settings 40x40.bmp")     
                };

                // Create the image strips
                CreateMagentaStripFromFiles(smallIcons, smallStripPath, 20);
                CreateMagentaStripFromFiles(mediumIcons, mediumStripPath, 32);
                CreateMagentaStripFromFiles(largeIcons, largeStripPath, 40);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating image strips: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a single image strip with magenta transparency from multiple bitmap files
        /// </summary>
        /// <param name="sourceFiles">Array of source bitmap file paths</param>
        /// <param name="outputPath">Output path for the combined strip</param>
        /// <param name="iconSize">Size of each icon (width and height)</param>
        private static void CreateMagentaStripFromFiles(string[] sourceFiles, string outputPath, int iconSize)
        {
            try
            {
                // Check if all source files exist
                foreach (string filePath in sourceFiles)
                {
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException($"Source icon file not found: {filePath}");
                    }
                }

                // Create a new bitmap for the strip
                using (Bitmap stripBitmap = new Bitmap(iconSize * sourceFiles.Length, iconSize, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(stripBitmap))
                    {
                        // Fill the background with magenta (this will be transparent in SolidWorks)
                        g.Clear(TransparentColor);

                        // Add each icon to the strip
                        for (int i = 0; i < sourceFiles.Length; i++)
                        {
                            using (Bitmap srcBitmap = new Bitmap(sourceFiles[i]))
                            {
                                // Create a magenta-background version of the icon
                                using (Bitmap iconWithMagenta = PrepareIconWithMagenta(srcBitmap))
                                {
                                    // Draw the icon onto the strip
                                    g.DrawImage(iconWithMagenta, i * iconSize, 0, iconSize, iconSize);
                                }
                            }
                        }
                    }

                    // Save the strip with the correct format
                    SaveBitmapWithCorrectFormat(stripBitmap, outputPath);
                }

                Console.WriteLine($"Created image strip: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating image strip {outputPath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Prepares an icon with magenta as the transparent color
        /// </summary>
        private static Bitmap PrepareIconWithMagenta(Bitmap sourceBitmap)
        {
            // Create a new bitmap with the same dimensions
            Bitmap result = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, PixelFormat.Format24bppRgb);

            // Draw the source onto the new bitmap with magenta background
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(TransparentColor); // Fill with magenta
                g.DrawImage(sourceBitmap, 0, 0, sourceBitmap.Width, sourceBitmap.Height);
            }

            // Replace white pixels in the source with magenta for transparency
            // This ensures that any white parts of the icons will be transparent in SolidWorks
            for (int x = 0; x < result.Width; x++)
            {
                for (int y = 0; y < result.Height; y++)
                {
                    Color pixelColor = sourceBitmap.GetPixel(x, y);

                    // Check if the pixel is white or very close to white (allowing for slight variations)
                    if (pixelColor.R > 240 && pixelColor.G > 240 && pixelColor.B > 240)
                    {
                        // Set to magenta for transparency
                        result.SetPixel(x, y, TransparentColor);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Saves a bitmap with the correct format for SolidWorks
        /// </summary>
        private static void SaveBitmapWithCorrectFormat(Bitmap bitmap, string filePath)
        {
            // Create encoder parameters for bitmap
            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);

            // Get the BMP encoder
            ImageCodecInfo bmpEncoder = GetEncoderInfo("image/bmp");

            // Save the bitmap
            bitmap.Save(filePath, bmpEncoder, encoderParams);
        }

        /// <summary>
        /// Gets the encoder info for a specific image format
        /// </summary>
        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            for (int i = 0; i < encoders.Length; i++)
            {
                if (encoders[i].MimeType == mimeType)
                {
                    return encoders[i];
                }
            }

            return null;
        }
    }
}