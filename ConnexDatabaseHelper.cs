using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;
using RoesleinAddIn;

namespace RoesleinAddIn
{
    /// <summary>
    /// Utility methods for pushing ShippingLabelData to the Connex MES SQL Server.
    /// </summary>
    public static class ConnexDatabaseHelper
    {
        // Fallback defaults if settings file is missing values
        private const string FallbackServerInstance = "SOLIDWORKS\\ConnexDB";
        private const string FallbackLiveDb = "RoesleinConnex";
        private const string FallbackTestDb = "RoesleinConnexTest";

        /// <summary>
        /// Attempts to insert the specified label into both Live and Test Connex databases.
        /// Any individual database failure is logged but does not abort the entire operation.
        /// </summary>
        public static void PushShippingLabelToConnex(ShippingLabelData label)
        {
            var cfg = GetDbConfig();
            foreach (var dbName in cfg.targetDbs)
            {
                try
                {
                    InsertLabelIntoDatabase(label, dbName, cfg.serverInstance);
                    Logger.Info($"Inserted label {label.InstanceName} into database {dbName}.");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to insert label {label.InstanceName} into database {dbName}: {ex.Message}");
                }
            }
        }

        private static (string serverInstance, List<string> targetDbs) GetDbConfig()
        {
            var set = Settings.LoadSettings();
            if (set == null)
            {
                Logger.Warning("Settings.LoadSettings() returned null – using Connex fallback defaults.");
            }

            string server = set?.ConnexServerInstance ?? FallbackServerInstance;
            var dbs = new List<string>();

            if (set == null)
            {
                dbs.Add(FallbackLiveDb);
                dbs.Add(FallbackTestDb);
            }
            else
            {
                switch (set.ConnexTarget)
                {
                    case ConnexPushTarget.LiveOnly:
                        dbs.Add(set.ConnexLiveDatabase ?? FallbackLiveDb);
                        break;
                    case ConnexPushTarget.TestOnly:
                        dbs.Add(set.ConnexTestDatabase ?? FallbackTestDb);
                        break;
                    default:
                        dbs.Add(set.ConnexLiveDatabase ?? FallbackLiveDb);
                        dbs.Add(set.ConnexTestDatabase ?? FallbackTestDb);
                        break;
                }
            }

            return (server, dbs);
        }

        private static void InsertLabelIntoDatabase(ShippingLabelData label, string databaseName, string serverInstance)
        {
            // Build connection string – using Windows auth (Trusted_Connection)
            var connectionString = BuildConnectionString(serverInstance, databaseName);

            const string sql = @"INSERT INTO JOB_DATA
                ([Handle],[DeletedStatus],[DeletedReason],[BlockName],[PROJECT_NO],[JOB_NO],[SYSTEM],[ID_NUMBER],[DESCRIPTION],
                 [PART_NO],[NOTE],[QTY],[PRINT_QTY],[DATE_ADDED],[DATE_MODIFIED],[TAG_TYPE],[Company_ID],[DWG],[DWG_NUMBER],[Company_Letter],[CrateNumbers])
                VALUES
                (@Handle,0,NULL,@BlockName,@ProjectNo,@JobNo,@System,@IdNumber,@Description,
                 @PartNo,'',@Qty,@Qty,@DateNow,@DateNow,@TagType,@CompanyId,@Dwg,@DwgNumber,@CompanyLetter,'')";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Handle",        label.InstanceName ?? string.Empty);
                cmd.Parameters.AddWithValue("@BlockName",     "SHIPPING_LABEL");
                cmd.Parameters.AddWithValue("@ProjectNo",     label.ProjectNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@JobNo",         label.JobNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@System",        label.SystemNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@IdNumber",      label.IDNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@Description",   label.Description ?? string.Empty);
                cmd.Parameters.AddWithValue("@PartNo",        label.PartNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@Qty",           ParseInt(label.Quantity));
                cmd.Parameters.AddWithValue("@DateNow",       DateTime.Now);
                cmd.Parameters.AddWithValue("@TagType",       MapArrowTypeToTagType(label.ArrowType));
                cmd.Parameters.AddWithValue("@CompanyId",     label.CompanyNumber ?? string.Empty);
                cmd.Parameters.AddWithValue("@Dwg",           label.DrawingName ?? string.Empty);
                cmd.Parameters.AddWithValue("@DwgNumber",     label.DrawingName ?? string.Empty);
                cmd.Parameters.AddWithValue("@CompanyLetter", label.CompanyLetter ?? string.Empty);

                if (!TryOpen(conn, databaseName)) return;
                cmd.ExecuteNonQuery();
            }
        }

        private static int ParseInt(string value)
        {
            if (int.TryParse(value, out var i)) return i;
            return 0;
        }

        /// <summary>
        /// Synchronise all shipping labels for a drawing: insert new, update existing, mark missing as deleted.
        /// </summary>
        public static void SyncShippingLabels(IEnumerable<ShippingLabelData> labels)
        {
            var labelList = labels?.ToList() ?? new List<ShippingLabelData>();
            if (labelList.Count == 0) return;

            string drawingNumber = labelList.First().DrawingName ?? string.Empty;
            var cfg = GetDbConfig();
            foreach (var db in cfg.targetDbs)
            {
                try
                {
                    SyncDrawingInDatabase(labelList, drawingNumber, db, cfg.serverInstance);
                    Logger.Info($"Synced {drawingNumber} ({labelList.Count} labels) in DB {db}");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Sync failed for DB {db}: {ex.Message}");
                }
            }
        }

        private static void SyncDrawingInDatabase(List<ShippingLabelData> labels, string drawingNumber, string databaseName, string serverInstance)
        {
            var cs = BuildConnectionString(serverInstance, databaseName);

            using (var conn = new SqlConnection(cs))
            {
                if (!TryOpen(conn, databaseName)) return;

                // 1. Read existing rows for this drawing
                var existing = new Dictionary<string, int>(); // handle -> row count/id
                using (var cmd = new SqlCommand("SELECT Handle FROM JOB_DATA WHERE DWG_NUMBER = @DwgNumber", conn))
                {
                    cmd.Parameters.AddWithValue("@DwgNumber", drawingNumber);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string h = rdr[0]?.ToString();
                            if (!existing.ContainsKey(h)) existing[h] = 1;
                        }
                    }
                }

                // 2. Insert new / update existing
                for (int i = 0; i < labels.Count; i++)
                {
                    var lbl = labels[i];
                    if (existing.ContainsKey(lbl.InstanceName))
                    {
                        UpdateLabel(conn, lbl);
                    }
                    else
                    {
                        InsertLabel(conn, lbl, i + 1);
                    }
                }

                // 3. Handle deletions (rows in DB but not in label list & not already -1)
                var handlesInGrid = new HashSet<string>(labels.Select(l => l.InstanceName));
                foreach (string dbHandle in existing.Keys)
                {
                    if (!handlesInGrid.Contains(dbHandle))
                    {
                        MarkRowDeleted(conn, dbHandle, drawingNumber);
                    }
                }
            }
        }

        private static void InsertLabel(SqlConnection conn, ShippingLabelData label, int sequence)
        {
            if (string.IsNullOrWhiteSpace(label.InstanceName) || !label.InstanceName.StartsWith("SW"))
                label.InstanceName = GenerateHandle(label.DrawingName, sequence);
            using (var cmd = new SqlCommand(GetInsertSql(), conn))
            {
                FillParameters(cmd, label);
                cmd.ExecuteNonQuery();
            }
        }

        private static void UpdateLabel(SqlConnection conn, ShippingLabelData label)
        {
            const string updateSql = @"UPDATE JOB_DATA SET
                [DeletedStatus]=0,[DeletedReason]=NULL,
                [BlockName]=@BlockName,[PROJECT_NO]=@ProjectNo,[JOB_NO]=@JobNo,[SYSTEM]=@System,[ID_NUMBER]=@IdNumber,
                [DESCRIPTION]=@Description,[PART_NO]=@PartNo,[NOTE]='',[QTY]=@Qty,[PRINT_QTY]=@Qty,[DATE_MODIFIED]=@DateNow,
                [TAG_TYPE]=@TagType,[Company_ID]=@CompanyId,[DWG]=@Dwg,[Company_Letter]=@CompanyLetter
                WHERE Handle=@Handle AND DWG_NUMBER=@DwgNumber";

            using (var cmd = new SqlCommand(updateSql, conn))
            {
                FillParameters(cmd, label);
                cmd.ExecuteNonQuery();
            }
        }

        private static void MarkRowDeleted(SqlConnection conn, string handle, string drawingNumber)
        {
            const string delSql = "UPDATE JOB_DATA SET Handle='-1', DeletedStatus=1, DATE_MODIFIED=@DateNow WHERE Handle=@Handle AND DWG_NUMBER=@DwgNumber";
            using (var cmd = new SqlCommand(delSql, conn))
            {
                cmd.Parameters.AddWithValue("@DateNow", DateTime.Now);
                cmd.Parameters.AddWithValue("@Handle", handle);
                cmd.Parameters.AddWithValue("@DwgNumber", drawingNumber);
                cmd.ExecuteNonQuery();
            }
        }

        private static void FillParameters(SqlCommand cmd, ShippingLabelData label)
        {
            cmd.Parameters.AddWithValue("@Handle", label.InstanceName ?? string.Empty);
            cmd.Parameters.AddWithValue("@BlockName", "SHIPPING_LABEL");
            cmd.Parameters.AddWithValue("@ProjectNo", label.ProjectNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@JobNo", label.JobNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@System", label.SystemNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@IdNumber", label.IDNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@Description", label.Description ?? string.Empty);
            cmd.Parameters.AddWithValue("@PartNo", label.PartNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@Qty", ParseInt(label.Quantity));
            cmd.Parameters.AddWithValue("@DateNow", DateTime.Now);
            cmd.Parameters.AddWithValue("@TagType", MapArrowTypeToTagType(label.ArrowType));
            cmd.Parameters.AddWithValue("@CompanyId", label.CompanyNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@Dwg", label.DrawingName ?? string.Empty);
            cmd.Parameters.AddWithValue("@DwgNumber", label.DrawingName ?? string.Empty);
            cmd.Parameters.AddWithValue("@CompanyLetter", label.CompanyLetter ?? string.Empty);
        }

        private static string GetInsertSql() => @"INSERT INTO JOB_DATA
            ([Handle],[DeletedStatus],[DeletedReason],[BlockName],[PROJECT_NO],[JOB_NO],[SYSTEM],[ID_NUMBER],[DESCRIPTION],
             [PART_NO],[NOTE],[QTY],[PRINT_QTY],[DATE_ADDED],[DATE_MODIFIED],[TAG_TYPE],[Company_ID],[DWG],[DWG_NUMBER],[Company_Letter],[CrateNumbers])
            VALUES
            (@Handle,0,NULL,@BlockName,@ProjectNo,@JobNo,@System,@IdNumber,@Description,
             @PartNo,'',@Qty,@Qty,@DateNow,@DateNow,@TagType,@CompanyId,@Dwg,@DwgNumber,@CompanyLetter,'')";

        private static string BuildConnectionString(string serverInstance, string dbName)
            => $"Data Source={serverInstance};Initial Catalog={dbName};Integrated Security=True;";

        private static bool TryOpen(SqlConnection conn, string dbName)
        {
            try
            {
                conn.Open();
                return true;
            }
            catch (SqlException ex)
            {
                // 18456 = login failed, 4060 = cannot open DB, 229 = permission denied on object
                if (ex.Number == 18456 || ex.Number == 4060 || ex.Number == 229)
                {
                    MessageBox.Show($"Your Windows account does not have permission to access the Connex database '{dbName}'.\n\nSQL Error {ex.Number}: {ex.Message}",
                        "Connex Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                throw;
            }
        }

        private static string GenerateHandle(string drawingNumber, int sequence)
        {
            // Remove path and extension
            if (string.IsNullOrWhiteSpace(drawingNumber)) drawingNumber = "UNKNOWN";
            string clean = System.IO.Path.GetFileNameWithoutExtension(drawingNumber);
            // Replace any spaces or invalid characters with underscore to keep it SQL-safe
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                clean = clean.Replace(c, '_');
            return $"SW{clean}-{sequence.ToString("D4")}";
        }

        private static string MapArrowTypeToTagType(ShippingLabelArrowType arrowType)
        {
            switch (arrowType)
            {
                case ShippingLabelArrowType.RightArrow:
                    return "CAN FLOW RIGHT";
                case ShippingLabelArrowType.LeftArrow:
                    return "CAN FLOW LEFT";
                case ShippingLabelArrowType.Slot:
                    return "SLOT BUBBLE";
                case ShippingLabelArrowType.DoubleArrow:
                    return "BI-DI";
                default:
                    return arrowType.ToString();
            }
        }

        private static ShippingLabelArrowType MapTagTypeToArrowType(string tagType)
        {
            if (string.IsNullOrWhiteSpace(tagType)) return ShippingLabelArrowType.Slot;
            switch (tagType.Trim().ToUpperInvariant())
            {
                case "CAN FLOW RIGHT":
                    return ShippingLabelArrowType.RightArrow;
                case "CAN FLOW LEFT":
                    return ShippingLabelArrowType.LeftArrow;
                case "SLOT BUBBLE":
                    return ShippingLabelArrowType.Slot;
                case "BI-DI":
                    return ShippingLabelArrowType.DoubleArrow;
                default:
                    if (Enum.TryParse(tagType, true, out ShippingLabelArrowType at))
                        return at;
                    return ShippingLabelArrowType.Slot;
            }
        }
    }
} 