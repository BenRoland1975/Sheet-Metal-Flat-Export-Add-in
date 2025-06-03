using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.IO;
using EPDM.Interop.epdm;
using System.Runtime.InteropServices;
using System.Reflection;

namespace RoesleinAddIn
{
    public partial class ValidationPopupForm : Form
    {
        // Windows API imports for bitmap handling
        [DllImport("gdi32.dll")]
        private static extern IntPtr CopyImage(IntPtr hImage, uint uType, int cxDesired, int cyDesired, uint fuFlags);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint IMAGE_BITMAP = 0;
        private const uint LR_COPYRETURNORG = 0x4;

        // Static fields to remember last selections across multiple popup instances
        private static string lastSelectedMaterial = null;
        private static string lastSelectedShopRoute = null;

        private PictureBox thumbnailPictureBox;
        private Label partNumberLabel;
        private Label descriptionLabel;
        private Label filePathLabel;
        private Label materialLabel;
        private ComboBox materialComboBox;
        private Label shopRouteLabel;
        private ComboBox shopRouteComboBox;
        private Button applyButton;
        private Button skipButton;
        private Label validationMessageLabel;

        // Properties to capture user selections
        public string SelectedMaterial { get; private set; }
        public string SelectedShopRoute { get; private set; }
        public ValidationAction UserAction { get; private set; } = ValidationAction.Skip;

        // Part information
        private ModelDoc2 partDocument;
        private string partNumber;
        private string description;
        private string filePath;

        // Add PDM vault field
        private EPDM.Interop.epdm.IEdmVault5 pdmVault;

        // Add logging delegate field
        private Action<string> logAction;

        // Add SolidWorks application field
        private ISldWorks swApp;

        /// <summary>
        /// Clear the remembered selections (useful for starting fresh or when switching between different types of parts)
        /// </summary>
        public static void ClearRememberedSelections()
        {
            lastSelectedMaterial = null;
            lastSelectedShopRoute = null;
        }

        /// <summary>
        /// Get information about what selections are currently remembered
        /// </summary>
        public static string GetRememberedSelectionsInfo()
        {
            var info = new List<string>();
            if (!string.IsNullOrEmpty(lastSelectedMaterial))
                info.Add($"Material: {lastSelectedMaterial}");
            if (!string.IsNullOrEmpty(lastSelectedShopRoute))
                info.Add($"Shop Route: {lastSelectedShopRoute}");
            
            return info.Count > 0 ? $"Remembered: {string.Join(", ", info)}" : "No remembered selections";
        }

        public ValidationPopupForm(ModelDoc2 document, string partNum, string desc, string path, 
                                 List<string> materialOptions, List<string> shopRouteOptions,
                                 bool needsMaterial = false, bool needsShopRoute = false, 
                                 EPDM.Interop.epdm.IEdmVault5 vault = null, Action<string> logger = null, ISldWorks solidWorksApp = null)
        {
            partDocument = document;
            partNumber = partNum;
            description = desc;
            filePath = path;
            pdmVault = vault;
            logAction = logger ?? (msg => System.Diagnostics.Debug.WriteLine(msg));
            swApp = solidWorksApp;

            InitializeComponent();
            
            // Set form properties
            this.Text = "Missing Material/Shop Route - Fix Now";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;
            this.Size = new Size(700, 500); // Increased size for better layout

            // Set the icon using the same approach as other forms
            try
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(ValidationPopupForm));
                this.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            }
            catch
            {
                // Ignore icon loading errors
            }

            // Populate form data
            PopulatePartInformation();
            PopulateDropdowns(materialOptions, shopRouteOptions);
            ConfigureValidationMessage(needsMaterial, needsShopRoute);
            
            // Show helpful message if selections were pre-filled
            bool hasPrefilledSelections = false;
            if (needsMaterial && !string.IsNullOrEmpty(lastSelectedMaterial) && materialComboBox?.SelectedIndex > 0)
                hasPrefilledSelections = true;
            if (needsShopRoute && !string.IsNullOrEmpty(lastSelectedShopRoute) && shopRouteComboBox?.SelectedIndex > 0)
                hasPrefilledSelections = true;
                
            if (hasPrefilledSelections)
            {
                this.Text += " (Previous selections pre-filled)";
            }
            
            GeneratePartThumbnail();

            // Wire up events
            WireUpEvents();
            
            // Trigger validation to enable Apply button if valid selections were pre-filled
            ValidateSelections(null, null);
        }

        private void PopulatePartInformation()
        {
            if (partNumberLabel != null)
                partNumberLabel.Text = $"Part: {partNumber ?? "Unknown"}";
            
            if (descriptionLabel != null)
                descriptionLabel.Text = $"Description: {description ?? "No description"}";
            
            if (filePathLabel != null)
                filePathLabel.Text = $"Path: {System.IO.Path.GetFileName(filePath) ?? "Unknown"}";
        }

        private void PopulateDropdowns(List<string> materialOptions, List<string> shopRouteOptions)
        {
            // Populate material dropdown
            if (materialComboBox != null && materialOptions != null)
            {
                materialComboBox.Items.Clear();
                materialComboBox.Items.Add("-- Select Material --");
                foreach (var material in materialOptions.OrderBy(m => m))
                {
                    materialComboBox.Items.Add(material);
                }
                materialComboBox.SelectedIndex = 0;
                materialComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                
                // Pre-select last remembered material if it exists in the options
                if (!string.IsNullOrEmpty(lastSelectedMaterial))
                {
                    for (int i = 1; i < materialComboBox.Items.Count; i++) // Start at 1 to skip "-- Select Material --"
                    {
                        if (materialComboBox.Items[i].ToString() == lastSelectedMaterial)
                        {
                            materialComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            // Populate shop route dropdown
            if (shopRouteComboBox != null && shopRouteOptions != null)
            {
                shopRouteComboBox.Items.Clear();
                shopRouteComboBox.Items.Add("-- Select Shop Route --");
                foreach (var route in shopRouteOptions)
                {
                    shopRouteComboBox.Items.Add(route);
                }
                shopRouteComboBox.SelectedIndex = 0;
                shopRouteComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                
                // Pre-select last remembered shop route if it exists in the options
                if (!string.IsNullOrEmpty(lastSelectedShopRoute))
                {
                    for (int i = 1; i < shopRouteComboBox.Items.Count; i++) // Start at 1 to skip "-- Select Shop Route --"
                    {
                        if (shopRouteComboBox.Items[i].ToString() == lastSelectedShopRoute)
                        {
                            shopRouteComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        private void ConfigureValidationMessage(bool needsMaterial, bool needsShopRoute)
        {
            if (validationMessageLabel != null)
            {
                string message = "This part is missing required information:";
                if (needsMaterial && needsShopRoute)
                    message += "\n• Material assignment\n• Shop Route assignment";
                else if (needsMaterial)
                    message += "\n• Material assignment";
                else if (needsShopRoute)
                    message += "\n• Shop Route assignment";

                validationMessageLabel.Text = message;
                validationMessageLabel.ForeColor = Color.DarkRed;
            }

            // Enable/disable dropdowns based on what's needed
            if (materialComboBox != null)
                materialComboBox.Enabled = needsMaterial;
            if (shopRouteComboBox != null)
                shopRouteComboBox.Enabled = needsShopRoute;
        }

        private void GeneratePartThumbnail()
        {
            try
            {
                logAction?.Invoke($"=== GeneratePartThumbnail START ===");
                logAction?.Invoke($"File path: {filePath}");
                logAction?.Invoke($"PDM vault available: {pdmVault != null}");
                logAction?.Invoke($"PDM vault logged in: {pdmVault?.IsLoggedIn}");
                logAction?.Invoke($"Part document available: {partDocument != null}");
                
                if (partDocument != null)
                {
                    logAction?.Invoke($"Part document path: {partDocument.GetPathName()}");
                    logAction?.Invoke($"Part document title: {partDocument.GetTitle()}");
                    logAction?.Invoke($"Part document type: {partDocument.GetType()}");
                    logAction?.Invoke($"Part document is read-only: {partDocument.IsOpenedReadOnly()}");
                    logAction?.Invoke($"Part document is view-only: {partDocument.IsOpenedViewOnly()}");
                }
                
                if (thumbnailPictureBox != null)
                {
                    // First try to get thumbnail from PDM file metadata (most efficient)
                    logAction?.Invoke("Attempting PDM thumbnail extraction...");
                    Bitmap thumbnail = GetPdmThumbnail();
                    
                    if (thumbnail != null)
                    {
                        logAction?.Invoke("SUCCESS: Got thumbnail from PDM!");
                    }
                    else
                    {
                        logAction?.Invoke("PDM thumbnail extraction failed, trying SolidWorks document capture...");
                        // If PDM thumbnail fails, try SolidWorks document capture
                        if (partDocument != null)
                        {
                            thumbnail = GetSolidWorksThumbnail();
                            if (thumbnail != null)
                            {
                                logAction?.Invoke("SUCCESS: Got thumbnail from SolidWorks document!");
                            }
                            else
                            {
                                logAction?.Invoke("SolidWorks thumbnail extraction also failed!");
                            }
                        }
                        else
                        {
                            logAction?.Invoke("Skipping SolidWorks thumbnail capture - using PDM thumbnails only");
                        }
                    }

                    if (thumbnail != null)
                    {
                        // Set the image and use Zoom mode to scale it to fit the picture box
                        thumbnailPictureBox.Image = thumbnail;
                        thumbnailPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                        logAction?.Invoke($"Thumbnail set successfully - Original: {thumbnail.Width}x{thumbnail.Height}, PictureBox: {thumbnailPictureBox.Width}x{thumbnailPictureBox.Height}");
                    }
                    else
                    {
                        logAction?.Invoke("Using placeholder thumbnail...");
                        thumbnailPictureBox.Image = CreatePlaceholderThumbnail();
                        thumbnailPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                    }
                }
                
                logAction?.Invoke("=== GeneratePartThumbnail END ===");
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error in GeneratePartThumbnail: {ex.Message}");
                if (thumbnailPictureBox != null)
                {
                    thumbnailPictureBox.Image = CreatePlaceholderThumbnail();
                    thumbnailPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                }
            }
        }

        private Bitmap GetPdmThumbnail()
        {
            try
            {
                logAction?.Invoke("=== GetPdmThumbnail START ===");
                
                if (pdmVault == null || !pdmVault.IsLoggedIn)
                {
                    logAction?.Invoke("PDM vault not available or not logged in");
                    return null;
                }

                logAction?.Invoke($"PDM vault OK: {pdmVault.Name}");
                logAction?.Invoke($"Looking for file: {filePath}");

                // Get the file from PDM vault
                EPDM.Interop.epdm.IEdmFolder5 folder;
                EPDM.Interop.epdm.IEdmFile5 edmFile = pdmVault.GetFileFromPath(filePath, out folder);

                if (edmFile == null)
                {
                    logAction?.Invoke("File not found in PDM vault");
                    return null;
                }

                logAction?.Invoke($"Found PDM file: {edmFile.Name}, ID: {edmFile.ID}");
                logAction?.Invoke($"Found PDM folder: {folder.Name}, ID: {folder.ID}");

                // Use the exact approach from your working code
                try
                {
                    logAction?.Invoke("Attempting to cast to IEdmFile18 interface...");
                    EPDM.Interop.epdm.IEdmFile18 file18 = (EPDM.Interop.epdm.IEdmFile18)edmFile;
                    logAction?.Invoke("Successfully cast to IEdmFile18 interface");
                    logAction?.Invoke($"File current version: {edmFile.CurrentVersion}");
                    
                    logAction?.Invoke($"Calling GetThumbnail3 with version {edmFile.CurrentVersion}...");
                    IntPtr thumbnailPtr = (IntPtr)file18.GetThumbnail3(edmFile.CurrentVersion);
                    
                    if (thumbnailPtr != IntPtr.Zero)
                    {
                        logAction?.Invoke($"GetThumbnail3 SUCCESS: Got bitmap handle {thumbnailPtr}");
                        
                        using (Bitmap bmp = Image.FromHbitmap(thumbnailPtr))
                        {
                            logAction?.Invoke($"Successfully created bitmap from handle: {bmp.Width}x{bmp.Height}");
                            return new Bitmap(bmp);  // Create a copy to avoid disposal issues
                        }
                    }
                    else
                    {
                        logAction?.Invoke("GetThumbnail3 returned null pointer");
                    }
                }
                catch (Exception ex)
                {
                    logAction?.Invoke($"GetThumbnail3 failed: {ex.Message}");
                }

                // Fallback: Try the older GetThumbnail2 approach if GetThumbnail3 fails
                logAction?.Invoke("Trying fallback GetThumbnail2 approach...");
                return GetPdmThumbnailFallback(edmFile, folder);
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error in GetPdmThumbnail: {ex.Message}");
                return null;
            }
        }

        private Bitmap GetPdmThumbnailFallback(EPDM.Interop.epdm.IEdmFile5 edmFile, EPDM.Interop.epdm.IEdmFolder5 folder)
        {
            try
            {
                // Try to cast to IEdmFile15 for GetThumbnail2 method
                if (edmFile is EPDM.Interop.epdm.IEdmFile15 file15)
                {
                    logAction?.Invoke("Fallback: Successfully cast to IEdmFile15 interface");
                    logAction?.Invoke($"File current version: {edmFile.CurrentVersion}");
                    
                    try
                    {
                        logAction?.Invoke($"Calling GetThumbnail2 with version {edmFile.CurrentVersion}...");
                        object previewData = file15.GetThumbnail2(edmFile.CurrentVersion);
                        
                        if (previewData != null)
                        {
                            logAction?.Invoke($"GetThumbnail2 SUCCESS with version {edmFile.CurrentVersion}: {previewData.GetType().FullName}");
                            return ConvertPdmPreviewToBitmap(previewData);
                        }
                        else
                        {
                            logAction?.Invoke($"GetThumbnail2 returned null for version {edmFile.CurrentVersion}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logAction?.Invoke($"GetThumbnail2 failed for version {edmFile.CurrentVersion}: {ex.Message}");
                    }
                }
                else
                {
                    logAction?.Invoke("Fallback: Failed to cast to IEdmFile15 interface");
                }

                return null;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error in GetPdmThumbnailFallback: {ex.Message}");
                return null;
            }
        }

        private Bitmap ConvertPdmPreviewToBitmap(object previewData)
        {
            try
            {
                logAction?.Invoke($"ConvertPdmPreviewToBitmap: Processing {previewData?.GetType()?.FullName ?? "null"}");

                if (previewData == null)
                {
                    logAction?.Invoke("Preview data is null");
                    return null;
                }

                // Handle System.__ComObject (most common case for PDM thumbnails)
                if (previewData.GetType().FullName == "System.__ComObject")
                {
                    logAction?.Invoke("Processing System.__ComObject - attempting direct conversion...");
                    return ConvertIPictureDispToBitmap(previewData);
                }

                // Handle IPictureDisp COM object using reflection
                if (previewData.GetType().Name.Contains("IPictureDisp") || 
                    previewData.GetType().GetInterfaces().Any(i => i.Name.Contains("IPictureDisp")))
                {
                    logAction?.Invoke("Converting IPictureDisp COM object...");
                    return ConvertIPictureDispToBitmap(previewData);
                }

                // Handle byte array
                if (previewData is byte[] byteArray && byteArray.Length > 0)
                {
                    logAction?.Invoke($"Converting byte array of length {byteArray.Length}...");
                    using (var stream = new MemoryStream(byteArray))
                    {
                        var image = Image.FromStream(stream);
                        return new Bitmap(image);
                    }
                }

                // Handle base64 string
                if (previewData is string base64String && !string.IsNullOrEmpty(base64String))
                {
                    logAction?.Invoke("Converting base64 string...");
                    try
                    {
                        var bytes = Convert.FromBase64String(base64String);
                        using (var stream = new MemoryStream(bytes))
                        {
                            var image = Image.FromStream(stream);
                            return new Bitmap(image);
                        }
                    }
                    catch (FormatException)
                    {
                        logAction?.Invoke("String is not valid base64");
                    }
                }

                // Handle System.Drawing.Image directly
                if (previewData is Image directImage)
                {
                    logAction?.Invoke("Converting System.Drawing.Image directly...");
                    return new Bitmap(directImage);
                }

                logAction?.Invoke($"Unable to convert preview data of type: {previewData.GetType().FullName}");
                return null;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error converting preview data: {ex.Message}");
                return null;
            }
        }

        // Windows API declarations for IPictureDisp conversion
        [DllImport("oleaut32.dll")]
        private static extern int OleLoadPicture(IntPtr pStream, int lSize, bool fRunmode, ref Guid riid, out IntPtr ppvObj);

        private static readonly Guid IID_IPictureDisp = new Guid("7BF80981-BF32-101A-8BBB-00AA00300CAB");

        private Bitmap ConvertIPictureDispToBitmap(object previewData)
        {
            try
            {
                if (previewData == null)
                {
                    logAction?.Invoke("Preview data is null");
                    return null;
                }

                logAction?.Invoke($"Preview data type: {previewData.GetType().FullName}");

                // Method 1: Try direct property access using reflection
                try
                {
                    var handleProperty = previewData.GetType().GetProperty("Handle");
                    var typeProperty = previewData.GetType().GetProperty("Type");
                    var widthProperty = previewData.GetType().GetProperty("Width");
                    var heightProperty = previewData.GetType().GetProperty("Height");

                    if (handleProperty != null && typeProperty != null)
                    {
                        var handle = handleProperty.GetValue(previewData);
                        var type = typeProperty.GetValue(previewData);
                        var width = widthProperty?.GetValue(previewData);
                        var height = heightProperty?.GetValue(previewData);

                        logAction?.Invoke($"Found properties - Handle: {handle}, Type: {type}, Width: {width}, Height: {height}");

                        // Check if it's a bitmap type (1 = bitmap, 2 = metafile, 3 = icon)
                        if (type != null && Convert.ToInt32(type) == 1) // Bitmap
                        {
                            IntPtr hBitmap = new IntPtr(Convert.ToInt32(handle));
                            if (hBitmap != IntPtr.Zero)
                            {
                                logAction?.Invoke("Creating bitmap from handle using Method 1...");
                                
                                // Create a copy of the bitmap to avoid issues with ownership
                                IntPtr hBitmapCopy = CopyImage(hBitmap, IMAGE_BITMAP, 0, 0, LR_COPYRETURNORG);
                                if (hBitmapCopy != IntPtr.Zero)
                                {
                                    try
                                    {
                                        var bitmap = Image.FromHbitmap(hBitmapCopy);
                                        logAction?.Invoke($"SUCCESS Method 1: Created bitmap {bitmap.Width}x{bitmap.Height}");
                                        return new Bitmap(bitmap);
                                    }
                                    finally
                                    {
                                        DeleteObject(hBitmapCopy);
                                    }
                                }
                                else
                                {
                                    logAction?.Invoke("Method 1: Failed to copy bitmap handle");
                                }
                            }
                            else
                            {
                                logAction?.Invoke("Method 1: Handle is null or zero");
                            }
                        }
                        else
                        {
                            logAction?.Invoke($"Method 1: Not a bitmap type: {type}");
                        }
                    }
                    else
                    {
                        logAction?.Invoke("Method 1: Required properties (Handle, Type) not found");
                    }
                }
                catch (Exception ex1)
                {
                    logAction?.Invoke($"Method 1 failed: {ex1.Message}");
                }

                // Method 2: Try clipboard approach (similar to your working code)
                try
                {
                    logAction?.Invoke("Trying Method 2: Clipboard approach...");
                    
                    // Clear clipboard first
                    Clipboard.Clear();
                    
                    // Set the COM object to clipboard
                    Clipboard.SetDataObject(previewData, false);
                    
                    // Try to get image from clipboard
                    if (Clipboard.ContainsImage())
                    {
                        var clipboardImage = Clipboard.GetImage();
                        if (clipboardImage != null)
                        {
                            logAction?.Invoke($"SUCCESS Method 2: Got image from clipboard {clipboardImage.Width}x{clipboardImage.Height}");
                            return new Bitmap(clipboardImage);
                        }
                        else
                        {
                            logAction?.Invoke("Method 2: Clipboard contains image but GetImage returned null");
                        }
                    }
                    else
                    {
                        logAction?.Invoke("Method 2: Clipboard does not contain image after setting COM object");
                    }
                }
                catch (Exception ex2)
                {
                    logAction?.Invoke($"Method 2 failed: {ex2.Message}");
                }

                // Method 3: Try OLE conversion using Windows API
                try
                {
                    logAction?.Invoke("Trying Method 3: OLE conversion...");
                    
                    // This is a more advanced approach that might work with some COM objects
                    IntPtr pUnknown = Marshal.GetIUnknownForObject(previewData);
                    if (pUnknown != IntPtr.Zero)
                    {
                        logAction?.Invoke("Method 3: Got IUnknown pointer, attempting conversion...");
                        // For now, just log that we got the pointer
                        // More complex OLE conversion would go here
                        Marshal.Release(pUnknown);
                    }
                }
                catch (Exception ex3)
                {
                    logAction?.Invoke($"Method 3 failed: {ex3.Message}");
                }

                logAction?.Invoke("All conversion methods failed");
                return null;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error converting preview data: {ex.Message}");
                return null;
            }
        }

        private Bitmap GetSolidWorksThumbnail()
        {
            // For now, return null to use PDM thumbnails only
            // This avoids complex SolidWorks document manipulation
            logAction?.Invoke("Skipping SolidWorks thumbnail capture - using PDM thumbnails only");
            return null;
        }

        private Bitmap ScaleThumbnail(Bitmap original, int maxWidth, int maxHeight)
        {
            try
            {
                if (original == null) return null;

                // Calculate scaling to maintain aspect ratio
                float scaleX = (float)maxWidth / original.Width;
                float scaleY = (float)maxHeight / original.Height;
                float scale = Math.Min(scaleX, scaleY);

                int newWidth = (int)(original.Width * scale);
                int newHeight = (int)(original.Height * scale);

                Bitmap scaledBitmap = new Bitmap(maxWidth, maxHeight);
                using (Graphics g = Graphics.FromImage(scaledBitmap))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    // Fill background with light gray
                    g.FillRectangle(new SolidBrush(Color.FromArgb(240, 240, 240)), 0, 0, maxWidth, maxHeight);

                    // Center the image
                    int x = (maxWidth - newWidth) / 2;
                    int y = (maxHeight - newHeight) / 2;

                    g.DrawImage(original, x, y, newWidth, newHeight);
                }

                return scaledBitmap;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"Error scaling thumbnail: {ex.Message}");
                return original; // Return original if scaling fails
            }
        }

        private Bitmap CreatePlaceholderThumbnail()
        {
            try
            {
                int width = thumbnailPictureBox?.Width ?? 150;
                int height = thumbnailPictureBox?.Height ?? 150;
                
                Bitmap placeholder = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(placeholder))
                {
                    // Fill background with a light gray
                    g.FillRectangle(new SolidBrush(Color.FromArgb(245, 245, 245)), 0, 0, width, height);
                    
                    // Draw border
                    g.DrawRectangle(new Pen(Color.FromArgb(200, 200, 200), 2), 1, 1, width - 2, height - 2);
                    
                    // Determine file type and icon color
                    string fileExt = Path.GetExtension(filePath)?.ToUpper() ?? "";
                    Color iconColor = Color.FromArgb(70, 130, 180); // Steel blue default
                    string fileTypeText = "PART";
                    
                    if (fileExt.Contains("SLDASM"))
                    {
                        iconColor = Color.FromArgb(34, 139, 34); // Forest green for assemblies
                        fileTypeText = "ASSEMBLY";
                    }
                    else if (fileExt.Contains("SLDDRW"))
                    {
                        iconColor = Color.FromArgb(255, 140, 0); // Dark orange for drawings
                        fileTypeText = "DRAWING";
                    }
                    
                    // Draw a simple 3D box icon
                    int iconSize = Math.Min(width, height) / 3;
                    int iconX = (width - iconSize) / 2;
                    int iconY = height / 4;
                    
                    using (Brush iconBrush = new SolidBrush(iconColor))
                    {
                        // Draw main face
                        g.FillRectangle(iconBrush, iconX, iconY, iconSize, iconSize);
                        
                        // Draw top face (3D effect)
                        Point[] topFace = {
                            new Point(iconX, iconY),
                            new Point(iconX + iconSize/4, iconY - iconSize/4),
                            new Point(iconX + iconSize + iconSize/4, iconY - iconSize/4),
                            new Point(iconX + iconSize, iconY)
                        };
                        using (Brush topBrush = new SolidBrush(Color.FromArgb(Math.Min(255, iconColor.R + 40), 
                                                                              Math.Min(255, iconColor.G + 40), 
                                                                              Math.Min(255, iconColor.B + 40))))
                        {
                            g.FillPolygon(topBrush, topFace);
                        }
                        
                        // Draw right face (3D effect)
                        Point[] rightFace = {
                            new Point(iconX + iconSize, iconY),
                            new Point(iconX + iconSize + iconSize/4, iconY - iconSize/4),
                            new Point(iconX + iconSize + iconSize/4, iconY + iconSize - iconSize/4),
                            new Point(iconX + iconSize, iconY + iconSize)
                        };
                        using (Brush rightBrush = new SolidBrush(Color.FromArgb(Math.Max(0, iconColor.R - 30), 
                                                                               Math.Max(0, iconColor.G - 30), 
                                                                               Math.Max(0, iconColor.B - 30))))
                        {
                            g.FillPolygon(rightBrush, rightFace);
                        }
                    }
                    
                    // Draw file type text
                    using (Font typeFont = new Font("Arial", 8, FontStyle.Bold))
                    {
                        SizeF typeSize = g.MeasureString(fileTypeText, typeFont);
                        float typeX = (width - typeSize.Width) / 2;
                        float typeY = iconY + iconSize + 10;
                        
                        g.DrawString(fileTypeText, typeFont, new SolidBrush(Color.FromArgb(100, 100, 100)), typeX, typeY);
                    }
                    
                    // Draw part number if available
                    if (!string.IsNullOrEmpty(partNumber))
                    {
                        using (Font partFont = new Font("Arial", 7, FontStyle.Regular))
                        {
                            string displayPartNum = partNumber.Length > 15 ? partNumber.Substring(0, 12) + "..." : partNumber;
                            SizeF partSize = g.MeasureString(displayPartNum, partFont);
                            float partX = (width - partSize.Width) / 2;
                            float partY = height - 25;
                            
                            g.DrawString(displayPartNum, partFont, new SolidBrush(Color.FromArgb(80, 80, 80)), partX, partY);
                        }
                    }
                    
                    // Draw "Preview" text
                    using (Font previewFont = new Font("Arial", 6, FontStyle.Italic))
                    {
                        string previewText = "Preview";
                        SizeF previewSize = g.MeasureString(previewText, previewFont);
                        float previewX = (width - previewSize.Width) / 2;
                        float previewY = height - 10;
                        
                        g.DrawString(previewText, previewFont, new SolidBrush(Color.FromArgb(150, 150, 150)), previewX, previewY);
                    }
                }
                
                return placeholder;
            }
            catch (Exception ex)
            {
                // Fallback to simple placeholder
                logAction?.Invoke($"Error creating placeholder thumbnail: {ex.Message}");
                
                int width = thumbnailPictureBox?.Width ?? 150;
                int height = thumbnailPictureBox?.Height ?? 150;
                Bitmap simplePlaceholder = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(simplePlaceholder))
                {
                    g.FillRectangle(new SolidBrush(Color.LightGray), 0, 0, width, height);
                    g.DrawString("No Preview", new Font("Arial", 10), new SolidBrush(Color.Black), 10, height/2 - 10);
                }
                return simplePlaceholder;
            }
        }

        private void WireUpEvents()
        {
            if (applyButton != null)
                applyButton.Click += ApplyButton_Click;
            
            if (skipButton != null)
                skipButton.Click += SkipButton_Click;

            // Validate selections when dropdowns change
            if (materialComboBox != null)
                materialComboBox.SelectedIndexChanged += ValidateSelections;
            
            if (shopRouteComboBox != null)
                shopRouteComboBox.SelectedIndexChanged += ValidateSelections;
        }

        private void ValidateSelections(object sender, EventArgs e)
        {
            bool hasValidSelections = true;

            // Check material selection if enabled
            if (materialComboBox != null && materialComboBox.Enabled)
            {
                hasValidSelections &= materialComboBox.SelectedIndex > 0;
            }

            // Check shop route selection if enabled
            if (shopRouteComboBox != null && shopRouteComboBox.Enabled)
            {
                hasValidSelections &= shopRouteComboBox.SelectedIndex > 0;
            }

            // Enable/disable apply button based on selections
            if (applyButton != null)
                applyButton.Enabled = hasValidSelections;
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            if (CaptureSelections())
            {
                UserAction = ValidationAction.Apply;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void SkipButton_Click(object sender, EventArgs e)
        {
            UserAction = ValidationAction.Skip;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private bool CaptureSelections()
        {
            // Capture material selection
            if (materialComboBox != null && materialComboBox.Enabled && materialComboBox.SelectedIndex > 0)
            {
                SelectedMaterial = materialComboBox.SelectedItem.ToString();
                // Remember this selection for next time
                lastSelectedMaterial = SelectedMaterial;
            }

            // Capture shop route selection
            if (shopRouteComboBox != null && shopRouteComboBox.Enabled && shopRouteComboBox.SelectedIndex > 0)
            {
                SelectedShopRoute = shopRouteComboBox.SelectedItem.ToString();
                // Remember this selection for next time
                lastSelectedShopRoute = SelectedShopRoute;
            }

            return true;
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ValidationPopupForm));
            this.thumbnailPictureBox = new System.Windows.Forms.PictureBox();
            this.partNumberLabel = new System.Windows.Forms.Label();
            this.descriptionLabel = new System.Windows.Forms.Label();
            this.filePathLabel = new System.Windows.Forms.Label();
            this.validationMessageLabel = new System.Windows.Forms.Label();
            this.materialLabel = new System.Windows.Forms.Label();
            this.materialComboBox = new System.Windows.Forms.ComboBox();
            this.shopRouteLabel = new System.Windows.Forms.Label();
            this.shopRouteComboBox = new System.Windows.Forms.ComboBox();
            this.applyButton = new System.Windows.Forms.Button();
            this.skipButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.thumbnailPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // thumbnailPictureBox
            // 
            this.thumbnailPictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.thumbnailPictureBox.Location = new System.Drawing.Point(20, 20);
            this.thumbnailPictureBox.Name = "thumbnailPictureBox";
            this.thumbnailPictureBox.Size = new System.Drawing.Size(200, 160);
            this.thumbnailPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.thumbnailPictureBox.TabIndex = 0;
            this.thumbnailPictureBox.TabStop = false;
            // 
            // partNumberLabel
            // 
            this.partNumberLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.partNumberLabel.Location = new System.Drawing.Point(224, 20);
            this.partNumberLabel.Name = "partNumberLabel";
            this.partNumberLabel.Size = new System.Drawing.Size(446, 25);
            this.partNumberLabel.TabIndex = 1;
            this.partNumberLabel.Text = "Part: ";
            // 
            // descriptionLabel
            // 
            this.descriptionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.descriptionLabel.Location = new System.Drawing.Point(224, 50);
            this.descriptionLabel.Name = "descriptionLabel";
            this.descriptionLabel.Size = new System.Drawing.Size(446, 40);
            this.descriptionLabel.TabIndex = 2;
            this.descriptionLabel.Text = "Description: ";
            // 
            // filePathLabel
            // 
            this.filePathLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F);
            this.filePathLabel.ForeColor = System.Drawing.Color.Gray;
            this.filePathLabel.Location = new System.Drawing.Point(224, 95);
            this.filePathLabel.Name = "filePathLabel";
            this.filePathLabel.Size = new System.Drawing.Size(446, 25);
            this.filePathLabel.TabIndex = 3;
            this.filePathLabel.Text = "Path: ";
            // 
            // validationMessageLabel
            // 
            this.validationMessageLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.validationMessageLabel.ForeColor = System.Drawing.Color.DarkRed;
            this.validationMessageLabel.Location = new System.Drawing.Point(20, 190);
            this.validationMessageLabel.Name = "validationMessageLabel";
            this.validationMessageLabel.Size = new System.Drawing.Size(650, 60);
            this.validationMessageLabel.TabIndex = 4;
            this.validationMessageLabel.Text = "This part is missing required information:";
            // 
            // materialLabel
            // 
            this.materialLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.materialLabel.Location = new System.Drawing.Point(20, 270);
            this.materialLabel.Name = "materialLabel";
            this.materialLabel.Size = new System.Drawing.Size(100, 25);
            this.materialLabel.TabIndex = 5;
            this.materialLabel.Text = "Material:";
            // 
            // materialComboBox
            // 
            this.materialComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.materialComboBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.materialComboBox.Location = new System.Drawing.Point(130, 270);
            this.materialComboBox.Name = "materialComboBox";
            this.materialComboBox.Size = new System.Drawing.Size(540, 23);
            this.materialComboBox.TabIndex = 6;
            // 
            // shopRouteLabel
            // 
            this.shopRouteLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.shopRouteLabel.Location = new System.Drawing.Point(20, 310);
            this.shopRouteLabel.Name = "shopRouteLabel";
            this.shopRouteLabel.Size = new System.Drawing.Size(100, 25);
            this.shopRouteLabel.TabIndex = 7;
            this.shopRouteLabel.Text = "Shop Route:";
            // 
            // shopRouteComboBox
            // 
            this.shopRouteComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.shopRouteComboBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.shopRouteComboBox.Location = new System.Drawing.Point(130, 310);
            this.shopRouteComboBox.Name = "shopRouteComboBox";
            this.shopRouteComboBox.Size = new System.Drawing.Size(540, 23);
            this.shopRouteComboBox.TabIndex = 8;
            // 
            // applyButton
            // 
            this.applyButton.BackColor = System.Drawing.Color.LightGreen;
            this.applyButton.Enabled = false;
            this.applyButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.applyButton.Location = new System.Drawing.Point(428, 354);
            this.applyButton.Name = "applyButton";
            this.applyButton.Size = new System.Drawing.Size(152, 35);
            this.applyButton.TabIndex = 9;
            this.applyButton.Text = "Apply && Continue";
            this.applyButton.UseVisualStyleBackColor = false;
            // 
            // skipButton
            // 
            this.skipButton.BackColor = System.Drawing.Color.LightGray;
            this.skipButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F);
            this.skipButton.Location = new System.Drawing.Point(590, 354);
            this.skipButton.Name = "skipButton";
            this.skipButton.Size = new System.Drawing.Size(80, 35);
            this.skipButton.TabIndex = 10;
            this.skipButton.Text = "Skip";
            this.skipButton.UseVisualStyleBackColor = false;
            // 
            // ValidationPopupForm
            // 
            this.ClientSize = new System.Drawing.Size(684, 421);
            this.Controls.Add(this.thumbnailPictureBox);
            this.Controls.Add(this.partNumberLabel);
            this.Controls.Add(this.descriptionLabel);
            this.Controls.Add(this.filePathLabel);
            this.Controls.Add(this.validationMessageLabel);
            this.Controls.Add(this.materialLabel);
            this.Controls.Add(this.materialComboBox);
            this.Controls.Add(this.shopRouteLabel);
            this.Controls.Add(this.shopRouteComboBox);
            this.Controls.Add(this.applyButton);
            this.Controls.Add(this.skipButton);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ValidationPopupForm";
            ((System.ComponentModel.ISupportInitialize)(this.thumbnailPictureBox)).EndInit();
            this.ResumeLayout(false);

        }
    }

    public enum ValidationAction
    {
        Skip,
        Apply
    }
} 