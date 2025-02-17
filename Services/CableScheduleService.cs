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
                    UpdateRowsBasedOnConditions(connection);
                    DeleteNullRows(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing database: {ex.Message}");
            }
        }

        private void UpdateRowsBasedOnConditions(SQLiteConnection connection)
        {
            string selectQuery = @"
                SELECT X1, Y1, Sheet, Word,
                FROM pdf_table
                ORDER BY Sheet, Y1 DESC;";
            

            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                int currentItemNumber = 1;
                
                double y1_current = 0;
                double x1_current = 0;
                double maxX1 = 0;
                double maxY1 = 0;
                bool y1_current_set = false;
                
                // This set will track all unique “Type” entries for the current ItemNumber
                var currentItemTags = new HashSet<string>();

                using (var transaction = connection.BeginTransaction())
                using (var updateCmd =
                       new SQLiteCommand(
                           "UPDATE pdf_table SET Tag = @WordTag, Item = @ItemNumber WHERE rowid = @RowId;",
                           connection, transaction))
                {
                    updateCmd.Parameters.Add(new SQLiteParameter("@WordTag"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@ItemNumber"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                    while (reader.Read())
                    {
                        int rowId = reader.GetInt32(0);
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                        maxX1 = reader.IsDBNull(4) ? 0 : reader.GetDouble(4);
                        maxY1 = reader.IsDBNull(5) ? 0 : reader.GetDouble(5);

                        if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber)
                        {
                            
                            if (lastSheetNumber != -1)
                            {
                                // We finished an item: check for missing tags
                                MissingTag(connection, currentItemTags, lastSheetNumber, currentItemNumber, x1_current, y1_current);
                                currentItemTags.Clear();
                            }

                            x1_current = 24; //sheetX1Min[sheetNumber];
                            y1_current = 200; //sheetY1Max[sheetNumber] - 75;
                            currentItemNumber = 1;
                            lastSheetNumber = sheetNumber;
                            y1_current_set = false;
                            currentItemTags.Clear();
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
                        if (Math.Abs(y1 - y1_current) > 18)
                        {
                            // We finished the old item. Check for missing tags.
                            MissingTag(connection, currentItemTags, sheetNumber, currentItemNumber, x1_current, y1_current);
                        
                            // Reset for the new item
                            currentItemTags.Clear();
                            currentItemNumber++;
                            y1_current = y1;
                        }

                        string tag = IsValidTag(x1, y1, ref x1_current, ref y1_current);

                        if (!string.IsNullOrEmpty(tag))
                        {
                            updateCmd.Parameters["@WordTag"].Value = tag;
                            updateCmd.Parameters["@ItemNumber"].Value = currentItemNumber;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                            
                            // Add to our set of encountered tags
                            currentItemTags.Add(tag);
                        }
                    }
                    if (lastSheetNumber != -1 && currentItemTags.Count > 0)
                    {
                        MissingTag(connection, currentItemTags, lastSheetNumber, currentItemNumber, x1_current, y1_current);
                        currentItemTags.Clear();
                    }
                    transaction.Commit();
                }
            }
        }
        
        private void MissingTag(
            SQLiteConnection connection,
            HashSet<string> existingTags,
            int sheetNumber,
            int itemNumber,
            double x1_current,
            double y1_current
        )
        {
            double line1Y = y1_current;
            double line2Y = y1_current - 12;
            

            // For convenience, define a local function that inserts a row
            void InsertMissingRow(string type, double x1, double y1)
            {
                double x2 = 0;
                double y2 = y1 + 5.77; // per the user’s instruction for Y2
                
                double width = 0;
                switch (type)
                {
                    // first-line tags
                    case "cable_tag":      width = 40; break;
                    case "from_desc":      width = 40; break;
                    case "to_desc":        width = 40; break;
                    case "function":       width = 40; break;
                    case "size":           width = 10; break;
                    case "insulation":     width = 40; break;

                    // second-line tags
                    case "from_ref":       width = 40; break;
                    case "to_ref":         width = 40; break;
                    case "voltage":        width = 40; break;
                    case "conductors":     width = 10; break;
                    case "length":         width = 10; break;
                }
                x2 = x1 + width;

                // Now insert a new row in pdf_table
                // (You could also do a batch insert with a single transaction outside.)
                string insertQuery = @"
                    INSERT INTO pdf_table 
                    (SheetNumber, ItemNumber, Type, X1, Y1, X2, Y2, Text, TextRotation)
                    VALUES
                    (@sheetNumber, @itemNumber, @type, @x1, @y1, @x2, @y2, @text, @textRotation);
                ";

                using var cmd = new SQLiteCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@sheetNumber", sheetNumber);
                cmd.Parameters.AddWithValue("@itemNumber", itemNumber);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@x1", x1);
                cmd.Parameters.AddWithValue("@y1", y1);
                cmd.Parameters.AddWithValue("@x2", x2);
                cmd.Parameters.AddWithValue("@y2", y2);
                cmd.Parameters.AddWithValue("@text", string.Empty);
                cmd.Parameters.AddWithValue("@textRotation", 0);
                cmd.ExecuteNonQuery();
            }

            // Check each required type; if missing, insert it
            foreach (var requiredTag in RequiredTypes)
            {
                if (!existingTags.Contains(requiredTag))
                {
                    switch (requiredTag)
                    {
                        // First line tags
                        case "cable_tag":
                            InsertMissingRow("cable_tag", x1_current, line1Y);
                            break;
                        case "from_desc":
                            InsertMissingRow("from_desc", x1_current + 110, line1Y);
                            break;
                        case "to_desc":
                            InsertMissingRow("to_desc", x1_current + 247, line1Y);
                            break;
                        case "function":
                            InsertMissingRow("function", x1_current + 388, line1Y);
                            break;
                        case "size":
                            InsertMissingRow("size", x1_current + 516, line1Y);
                            break;
                        case "insulation":
                            InsertMissingRow("insulation", x1_current + 570, line1Y);
                            break;

                        // Second line tags
                        case "from_ref":
                            InsertMissingRow("from_ref", x1_current + 110, line2Y);
                            break;
                        case "to_ref":
                            InsertMissingRow("to_ref", x1_current + 247, line2Y);
                            break;
                        case "voltage":
                            InsertMissingRow("voltage", x1_current + 424, line2Y);
                            break;
                        case "conductors":
                            InsertMissingRow("conductors", x1_current + 500, line2Y);
                            break;
                        case "length":
                            InsertMissingRow("length", x1_current + 555, line2Y);
                            break;
                    }
                }
            }
        }
        
        private string IsValidTag(double x1, double y1, ref double x1_current, ref double y1_current)
        {
            if (Math.Abs(y1 - y1_current) < 5)
            {
                return x1 switch
                { 
                     _ when x1 > x1_current - 2 && x1 <= x1_current + 95 => "cable_tag",
                     _ when x1 > x1_current + 100 && x1 <= x1_current + 230 => "from_desc",
                     _ when x1 > x1_current + 231 && x1 <= x1_current + 360 => "to_desc",
                     _ when x1 > x1_current + 361 && x1 <= x1_current + 420 => "function",
                     _ when x1 > x1_current + 486 && x1 <= x1_current + 530 => "size",
                     _ when x1 > x1_current + 540 && x1 <= x1_current + 700 => "insulation",
                    _ => string.Empty
                };
            }

            if (Math.Abs(y1 - y1_current) > 5)
            {
                return x1 switch
                {
                     _ when x1 > x1_current + 100 && x1 <= x1_current + 230 => "from_ref",
                     _ when x1 > x1_current + 231 && x1 <= x1_current + 360 => "to_ref",
                     _ when x1 > x1_current + 400 && x1 <= x1_current + 450 => "voltage",
                     _ when x1 > x1_current + 450 && x1 <= x1_current + 520 => "conductors",
                     _ when x1 > x1_current + 521 && x1 <= x1_current + 566 => "length", 
                     _ => string.Empty
                };
            }

            Console.WriteLine($"{x1},{y1}");
            return string.Empty;
        }
        
        private void DeleteNullRows(SQLiteConnection connection)
        {
            var deleteQuery = @"
                DELETE FROM pdf_table
                WHERE ItemNumber IS NULL
                   OR ItemNumber = '';
            ";

            using var cmd = new SQLiteCommand(deleteQuery, connection);
            int rowsDeleted = cmd.ExecuteNonQuery();
            
        }
        
    }
}
