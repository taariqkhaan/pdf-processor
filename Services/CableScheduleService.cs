using System.Data.SQLite;
using System.IO;

namespace PdfProcessor.Services
{
    public class CableScheduleService
    {
        private static readonly List<string> RequiredTypes = new() 
        {
            "cable_tag", "from_desc", "to_desc", "function", "size", "insulation", 
            "from_ref", "to_ref", "voltage", "conductors", "length"
        };
        public void ProcessDatabase(string dbFilePath)
        {
            if (!File.Exists(dbFilePath))
            {
                Console.WriteLine("Database file not found.");
                return;
            }

            try
            {
                using (var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
                {
                    connection.Open();
                    EnsureColumnsExist(connection);
                    UpdateRowsBasedOnConditions(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing database: {ex.Message}");
            }
        }

        private void EnsureColumnsExist(SQLiteConnection connection)
        {
            using (var cmd = new SQLiteCommand("PRAGMA table_info(pdf_table);", connection))
            using (var reader = cmd.ExecuteReader())
            {
                bool typeColumnExists = false;
                bool itemNumberColumnExists = false;

                while (reader.Read())
                {
                    string columnName = reader[1].ToString();
                    if (columnName.Equals("Type", StringComparison.OrdinalIgnoreCase))
                        typeColumnExists = true;
                    if (columnName.Equals("ItemNumber", StringComparison.OrdinalIgnoreCase))
                        itemNumberColumnExists = true;
                }

                if (!typeColumnExists)
                {
                    using (var alterCmd = new SQLiteCommand("ALTER TABLE pdf_table ADD COLUMN Type TEXT;", connection))
                    {
                        alterCmd.ExecuteNonQuery();
                    }
                }

                if (!itemNumberColumnExists)
                {
                    using (var alterCmd = new SQLiteCommand("ALTER TABLE pdf_table ADD COLUMN ItemNumber TEXT;", connection))
                    {
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void UpdateRowsBasedOnConditions(SQLiteConnection connection)
        {
            string selectQuery = "SELECT rowid, X1, Y1, SheetNumber FROM pdf_table ORDER BY SheetNumber, Y1 DESC;";
            
            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                int itemNumber = 1;
                double y1_current = 0;
                
                // Start Transaction
                using (var transaction = connection.BeginTransaction())
                using (var updateCmd = new SQLiteCommand("UPDATE pdf_table SET Type = @Type, ItemNumber = @ItemNumber WHERE rowid = @RowId;", connection, transaction))
                {
                    updateCmd.Parameters.Add(new SQLiteParameter("@Type"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@ItemNumber"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                    while (reader.Read())
                    {
                        int rowId = reader.GetInt32(0);
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

                        if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber)
                        {
                            y1_current = y1;
                            itemNumber = 1;
                            lastSheetNumber = sheetNumber;
                        }

                        if (Math.Abs(y1 - y1_current) > 17)
                        {
                            y1_current = y1;
                            itemNumber += 1;
                        }

                        string tag = IsValidTag(x1, y1, ref y1_current);
                        if (!string.IsNullOrEmpty(tag))
                        {
                            // Use parameterized query to avoid overhead
                            updateCmd.Parameters["@Type"].Value = tag;
                            updateCmd.Parameters["@ItemNumber"].Value = itemNumber;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                    
                    // Commit Transaction
                    transaction.Commit();
                }
            }
        }

        
        private string IsValidTag(double x1, double y1, ref double y1_current)
        {
            if (Math.Abs(y1 - y1_current) < 2)
            {
                return x1 switch
                {
                    _ when x1 > 23 * 0.97 && x1 <= 133 => "cable_tag",
                    _ when x1 > 137 * 0.97 && x1 <= 270 => "from_desc",
                    _ when x1 > 275 * 0.97 && x1 <= 410 => "to_desc",
                    _ when x1 > 416 * 0.97 && x1 <= 467 => "function",
                    _ when x1 > 539 * 0.97 && x1 <= 594 => "size",
                    _ when x1 > 593 * 0.97 && x1 <= 650 => "insulation",
                    _ => string.Empty // Default case if no condition is met
                };
            }
            if (Math.Abs(y1 - y1_current) > 2)
            {
                return x1 switch
                {
                    _ when x1 > 137 * 0.97 && x1 <= 270 => "from_ref",
                    _ when x1 > 275 * 0.97 && x1 <= 410 => "to_ref",
                    _ when x1 > 452 * 0.97 && x1 <= 500 => "voltage",
                    _ when x1 > 525 * 0.97 && x1 <= 560 => "conductors",
                    _ when x1 > 583 * 0.97 && x1 <= 597 => "length",
                    _ => string.Empty // Default case if no condition is met
                };
            }
            
            Console.WriteLine($"{x1},{y1}");
            
            return string.Empty;
        }

        private void UpdateDatabase(SQLiteConnection connection, int rowId, string typeValue, int itemNumber)
        {
            using (var updateCmd = new SQLiteCommand("UPDATE pdf_table SET Type = @Type, ItemNumber = @ItemNumber WHERE rowid = @RowId;", connection))
            {
                updateCmd.Parameters.AddWithValue("@Type", typeValue);
                updateCmd.Parameters.AddWithValue("@ItemNumber", itemNumber);
                updateCmd.Parameters.AddWithValue("@RowId", rowId);
                updateCmd.ExecuteNonQuery();
            }
        }

    }
}
