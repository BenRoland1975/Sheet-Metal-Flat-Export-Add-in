using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    public class RawSheetMaterial
    {
        public string PartNumber { get; set; }
        public string LegacyPartNumber { get; set; }
        public string Material { get; set; }
        public double Thickness { get; set; }
        public string Description { get; set; }
        public double TotalSQInch { get; set; }
        public double SheetLengthInch { get; set; }
        public double SheetHeightInch { get; set; }
        public decimal LastCost { get; set; }
    }

    public class RawLinearMaterial
    {
        public string PartNumber { get; set; }
        public string LegacyPartNumber { get; set; }
        public string Material { get; set; }
        public double Thickness { get; set; }
        public string Description { get; set; }
        public double LengthInch { get; set; }
        public decimal LastCost { get; set; }
    }

    public static class RawMaterialManager
    {
        private static string GetSettingsFolder()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string roesleinFolder = Path.Combine(appDataFolder, "Roeslein", "AddIn");
            
            if (!Directory.Exists(roesleinFolder))
                Directory.CreateDirectory(roesleinFolder);
                
            return roesleinFolder;
        }

        public static List<RawSheetMaterial> LoadRawSheetMaterials()
        {
            string filePath = Path.Combine(GetSettingsFolder(), "RawSheetMaterials.json");
            if (File.Exists(filePath))
            {
                try
                {
                    string jsonData = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<RawSheetMaterial>>(jsonData);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error loading raw sheet materials", ex);
                    MessageBox.Show($"Error loading raw sheet materials: {ex.Message}\nUsing default data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                Logger.Info("Raw materials file not found. Using default data.");
            }
            
            // Return default data for the first time
            return GetDefaultRawSheetMaterials();
        }

        public static void SaveRawSheetMaterials(List<RawSheetMaterial> materials)
        {
            string filePath = Path.Combine(GetSettingsFolder(), "RawSheetMaterials.json");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonData = JsonSerializer.Serialize(materials, options);
                File.WriteAllText(filePath, jsonData);
                Logger.Info($"Saved {materials.Count} raw sheet materials.");
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving raw sheet materials", ex);
                MessageBox.Show($"Error saving raw sheet materials: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static List<RawSheetMaterial> GetDefaultRawSheetMaterials()
        {
            // Initial data from provided table
            return new List<RawSheetMaterial>
            {
                new RawSheetMaterial { PartNumber = "328-151", LegacyPartNumber = "328-151", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.0747, Description = "SHEET, 14 GA CR 48 x 121", TotalSQInch = 5808, SheetLengthInch = 121, SheetHeightInch = 48, LastCost = 80.69M },
                new RawSheetMaterial { PartNumber = "328-151", LegacyPartNumber = "328-151", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.075, Description = "SHEET, 14 GA CR 48 x 121", TotalSQInch = 5808, SheetLengthInch = 121, SheetHeightInch = 48, LastCost = 80.69M },
                new RawSheetMaterial { PartNumber = "328-191", LegacyPartNumber = "328-191", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.1046, Description = "SHEET, 12 GA CR 48 x 122", TotalSQInch = 5856, SheetLengthInch = 122, SheetHeightInch = 48, LastCost = 152.00M },
                new RawSheetMaterial { PartNumber = "328-191", LegacyPartNumber = "328-191", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.105, Description = "SHEET, 12 GA CR 48 x 122", TotalSQInch = 5856, SheetLengthInch = 122, SheetHeightInch = 48, LastCost = 152.00M },
                new RawSheetMaterial { PartNumber = "328-103", LegacyPartNumber = "328-103", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.0598, Description = "SHEET, 16 GA CR 48 x 144", TotalSQInch = 6912, SheetLengthInch = 144, SheetHeightInch = 48, LastCost = 85.00M },
                new RawSheetMaterial { PartNumber = "328-047", LegacyPartNumber = "328-047", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.1345, Description = "SHEET, 10 GA CR 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 155.00M },
                new RawSheetMaterial { PartNumber = "328-047", LegacyPartNumber = "328-047", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.135, Description = "SHEET, 10 GA CR 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 155.00M },
                new RawSheetMaterial { PartNumber = "328-060", LegacyPartNumber = "328-060", Material = "ASTM A36 Steel- Hot Rolled Sheet", Thickness = 0.1345, Description = "SHEET, 10 GA HR 60 x 120", TotalSQInch = 7200, SheetLengthInch = 120, SheetHeightInch = 60, LastCost = 205.00M },
                new RawSheetMaterial { PartNumber = "328-002", LegacyPartNumber = "328-002", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.0595, Description = "SHEET, 16 GA SS 48 x 120 w/ PVC WHITE", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 192.00M },
                new RawSheetMaterial { PartNumber = "328-002", LegacyPartNumber = "328-002", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.06, Description = "SHEET, 16 GA SS 48 x 120 w/ PVC WHITE", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 192.00M },
                new RawSheetMaterial { PartNumber = "328-016", LegacyPartNumber = "328-016", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.0751, Description = "SHEET, 14 GA SS 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 226.80M },
                new RawSheetMaterial { PartNumber = "328-016", LegacyPartNumber = "328-016", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.078, Description = "SHEET, 14 GA SS 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 226.80M },
                new RawSheetMaterial { PartNumber = "328-016", LegacyPartNumber = "328-016", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.075, Description = "SHEET, 14 GA SS 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 226.80M },
                new RawSheetMaterial { PartNumber = "328-004", LegacyPartNumber = "328-004", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.105, Description = "SHEET, 12 GA SS 48 x 120 304-2B", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 317.52M },
                new RawSheetMaterial { PartNumber = "328-004", LegacyPartNumber = "328-004", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.1054, Description = "SHEET, 12 GA SS 48 x 120 304-2B", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 317.52M },
                new RawSheetMaterial { PartNumber = "328-013", LegacyPartNumber = "328-013", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.135, Description = "SHEET, 10 GA SS 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 408.24M },
                new RawSheetMaterial { PartNumber = "328-113", LegacyPartNumber = "328-113", Material = "AISI 304 Stainless Steel Sheet", Thickness = 0.184, Description = "SHEET, 7 GA SS 48 x 120 304", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 625.00M },
                new RawSheetMaterial { PartNumber = "382-001", LegacyPartNumber = "382-001", Material = "5052 Alloy Sheet", Thickness = 0.08, Description = "SHEET, .080 ALUM 48 x 96", TotalSQInch = 4608, SheetLengthInch = 96, SheetHeightInch = 48, LastCost = 55.40M },
                new RawSheetMaterial { PartNumber = "339-105", LegacyPartNumber = "339-105", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.1875, Description = "PLATE, .19, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 165.00M },
                new RawSheetMaterial { PartNumber = "339-108", LegacyPartNumber = "339-108", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.25, Description = "PLATE, .25, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 225.00M },
                new RawSheetMaterial { PartNumber = "339-050", LegacyPartNumber = "339-050", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.375, Description = "PLATE, .38, HR, 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 640.00M },
                new RawSheetMaterial { PartNumber = "339-003", LegacyPartNumber = "339-003", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.5, Description = "PLATE, .5, HR, 48 x 96", TotalSQInch = 4608, SheetLengthInch = 96, SheetHeightInch = 48, LastCost = 457.00M },
                new RawSheetMaterial { PartNumber = "339-106", LegacyPartNumber = "339-106", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.75, Description = "PLATE, .75, HR, 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 1102.68M },
                new RawSheetMaterial { PartNumber = "328-151", LegacyPartNumber = "328-151", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.079, Description = "SHEET, 14 GA CR 48 x 121", TotalSQInch = 5808, SheetLengthInch = 121, SheetHeightInch = 48, LastCost = 80.69M },
                new RawSheetMaterial { PartNumber = "328-151", LegacyPartNumber = "328-151", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.07874016, Description = "SHEET, 14 GA CR 48 x 121", TotalSQInch = 5808, SheetLengthInch = 121, SheetHeightInch = 48, LastCost = 80.69M },
                new RawSheetMaterial { PartNumber = "328-191", LegacyPartNumber = "328-191", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.118, Description = "SHEET, 12 GA CR 48 x 122", TotalSQInch = 5856, SheetLengthInch = 122, SheetHeightInch = 48, LastCost = 125.00M },
                new RawSheetMaterial { PartNumber = "328-191", LegacyPartNumber = "328-191", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.11811024, Description = "SHEET, 12 GA CR 48 x 122", TotalSQInch = 5856, SheetLengthInch = 122, SheetHeightInch = 48, LastCost = 125.00M },
                new RawSheetMaterial { PartNumber = "328-191", LegacyPartNumber = "328-191", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.1181, Description = "SHEET, 12 GA CR 48 x 122", TotalSQInch = 5856, SheetLengthInch = 122, SheetHeightInch = 48, LastCost = 125.00M },
                new RawSheetMaterial { PartNumber = "328-047", LegacyPartNumber = "328-047", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.134, Description = "SHEET, 10 GA CR 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 155.00M },
                new RawSheetMaterial { PartNumber = "328-047", LegacyPartNumber = "328-047", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.177, Description = "SHEET, 10 GA CR 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 155.00M },
                new RawSheetMaterial { PartNumber = "328-047", LegacyPartNumber = "328-047", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.13448819, Description = "SHEET, 10 GA CR 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 155.00M },
                new RawSheetMaterial { PartNumber = "339-108", LegacyPartNumber = "339-108", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.236, Description = "PLATE, .25, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 225.00M },
                new RawSheetMaterial { PartNumber = "339-108", LegacyPartNumber = "339-108", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.239, Description = "PLATE, .25, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 225.00M },
                new RawSheetMaterial { PartNumber = "339-108", LegacyPartNumber = "339-108", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.2391, Description = "PLATE, .25, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 225.00M },
                new RawSheetMaterial { PartNumber = "339-105", LegacyPartNumber = "339-105", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.17929134, Description = "PLATE, .19, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 190.00M },
                new RawSheetMaterial { PartNumber = "339-105", LegacyPartNumber = "339-105", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.188, Description = "PLATE, .19, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 190.00M },
                new RawSheetMaterial { PartNumber = "339-105", LegacyPartNumber = "339-105", Material = "ASTM A572 Steel- Hot Rolled plate", Thickness = 0.1969, Description = "PLATE, .19, HR, 48 x 120 (P&O)", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 190.00M },
                new RawSheetMaterial { PartNumber = "328-046", LegacyPartNumber = "328-046", Material = "ASTM A1008 Steel- Cold Rolled Sheet", Thickness = 0.12, Description = "SHEET, 11 GA CR 48 x 120", TotalSQInch = 5760, SheetLengthInch = 120, SheetHeightInch = 48, LastCost = 140.29M }
            };
        }
    }
} 