using System.Data.SQLite;
using System.IO;

namespace PdfProcessor.Services
{
    public class CableScheduleService
    {
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
            string selectQuery = "SELECT rowid, X1, Y2, SheetNumber FROM pdf_table ORDER BY SheetNumber, Y2 DESC;";
    
            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                int itemNumber = 1;
                double y2_current = 0;

                while (reader.Read())
                {
                    int rowId = reader.GetInt32(0);
                    double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                    double y2 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                    int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

                    // Reset y2_step if the sheet number changes
                    if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber)
                    {
                        y2_current = y2;
                        itemNumber = 1;
                        lastSheetNumber = sheetNumber;
                    }
                    
                    if (Math.Abs(y2 - y2_current) > 17)
                    {
                        y2_current = y2;
                        itemNumber += 1;
                    }

                    string tag = IsValidTag(x1, y2, ref y2_current);
                    if (!string.IsNullOrEmpty(tag))
                    {
                        UpdateDatabase(connection, rowId, tag, itemNumber);
                        
                    }
                    
                }
            }
        }
        
        private string IsValidTag(double x1, double y2, ref double y2_current)
        {
            if (Math.Abs(y2 - y2_current) < 2)
            {
                return x1 switch
                {
                    _ when x1 > 28 * 0.97 && x1 <= 136 => "cable_tag",
                    _ when x1 > 137 * 0.97 && x1 <= 274 => "from_desc",
                    _ when x1 > 275 * 0.97 && x1 <= 415 => "to_desc",
                    _ when x1 > 416 * 0.97 && x1 <= 467 => "function",
                    _ when x1 > 543 * 0.97 && x1 <= 596 => "size",
                    _ when x1 > 597 * 0.97 && x1 <= 650 => "insulation",
                    _ => string.Empty // Default case if no condition is met
                };
            }
            if (Math.Abs(y2 - y2_current) > 2)
            {
                return x1 switch
                {
                    _ when x1 > 137 * 0.97 && x1 <= 274 => "from_ref",
                    _ when x1 > 275 * 0.97 && x1 <= 415 => "to_ref",
                    _ when x1 > 452 * 0.97 && x1 <= 529 => "voltage",
                    _ when x1 > 530 * 0.97 && x1 <= 582 => "conductors",
                    _ when x1 > 583 * 0.97 && x1 <= 597 => "length",
                    _ => string.Empty // Default case if no condition is met
                };
            }
            
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
