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
                    using (var alterCmd =
                           new SQLiteCommand("ALTER TABLE pdf_table ADD COLUMN ItemNumber TEXT;", connection))
                    {
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void UpdateRowsBasedOnConditions(SQLiteConnection connection)
        {
            string selectQuery =
                "SELECT rowid, X1, Y1, SheetNumber, Text FROM pdf_table ORDER BY SheetNumber, Y1 DESC;";

            var reportX1BySheet = new Dictionary<int, double>();
            var reportY1BySheet = new Dictionary<int, double>();

            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    string text = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

                    if (text.Equals("REPORT", StringComparison.OrdinalIgnoreCase))
                    {
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        reportX1BySheet[sheetNumber] = x1;
                        reportY1BySheet[sheetNumber] = y1;
                    }
                }
            }

            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                int itemNumber = 1;
                double y1_current = 0;
                double x1_current = 0;
                bool y1_current_set = false;

                using (var transaction = connection.BeginTransaction())
                using (var updateCmd =
                       new SQLiteCommand(
                           "UPDATE pdf_table SET Type = @Type, ItemNumber = @ItemNumber WHERE rowid = @RowId;",
                           connection, transaction))
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

                        if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber )
                        {
                            x1_current = reportX1BySheet[sheetNumber];
                            y1_current = reportY1BySheet[sheetNumber] - 70;
                            itemNumber = 1;
                            lastSheetNumber = sheetNumber;
                        }

                        // filters out all the lines below report but above the cable item
                        if (y1 > y1_current)
                        {
                            continue;
                        }
                        
                        if (!y1_current_set)
                        {
                            y1_current = y1;
                            y1_current_set = true;
                        }
                        
                        // Switches control for Y1 current to the second line of a cable item
                        if (Math.Abs(y1 - y1_current) > 17)
                        {
                            y1_current = y1;
                            itemNumber += 1;
                        }
                        
                        string tag = IsValidTag(x1, y1, ref x1_current, ref y1_current);

                        if (!string.IsNullOrEmpty(tag))
                        {
                            updateCmd.Parameters["@Type"].Value = tag;
                            updateCmd.Parameters["@ItemNumber"].Value = itemNumber;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }


        private string IsValidTag(double x1, double y1, ref double x1_current, ref double y1_current)
        {
            if (Math.Abs(y1 - y1_current) < 5)
            {
                return x1 switch
                { 
                    _ when x1 > x1_current * 0.8 && x1 <= x1_current + 100 => "cable_tag",
                    // _ when x1 > reportX1 + 114 * 0.97 && x1 <= reportX1 + 270 => "from_desc",
                    // _ when x1 > reportX1 + 138 * 0.97 && x1 <= reportX1 + 410 => "to_desc",
                    // _ when x1 > reportX1 + 141 * 0.97 && x1 <= reportX1 + 467 => "function",
                    // _ when x1 > reportX1 + 123 * 0.97 && x1 <= reportX1 + 594 => "size",
                    // _ when x1 > reportX1 + 54 * 0.97 && x1 <= reportX1 + 650 => "insulation",
                    _ => string.Empty
                };
            }

            if (Math.Abs(y1 - y1_current) > 5)
            {
                return x1 switch
                {
                    // _ when x1 > reportX1 + 137 * 0.97 && x1 <= reportX1 + 270 => "from_ref",
                    // _ when x1 > reportX1 + 275 * 0.97 && x1 <= reportX1 + 410 => "to_ref",
                    // _ when x1 > reportX1 + 452 * 0.97 && x1 <= reportX1 + 500 => "voltage",
                    // _ when x1 > reportX1 + 525 * 0.97 && x1 <= reportX1 + 560 => "conductors",
                    // _ when x1 > reportX1 + 583 * 0.97 && x1 <= reportX1 + 597 => "length",
                    _ => string.Empty
                };
            }

            Console.WriteLine($"{x1},{y1}");
            return string.Empty;
        }

        private void UpdateDatabase(SQLiteConnection connection, int rowId, string typeValue, int itemNumber)
        {
            using (var updateCmd =
                   new SQLiteCommand(
                       "UPDATE pdf_table SET Type = @Type, ItemNumber = @ItemNumber WHERE rowid = @RowId;",
                       connection))
            {
                updateCmd.Parameters.AddWithValue("@Type", typeValue);
                updateCmd.Parameters.AddWithValue("@ItemNumber", itemNumber);
                updateCmd.Parameters.AddWithValue("@RowId", rowId);
                updateCmd.ExecuteNonQuery();
            }
        }
    }
}
