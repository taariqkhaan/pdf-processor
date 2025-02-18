using System.Data.SQLite;
using System.IO;

namespace PdfProcessor.Services;

public class DwgTitleService
    {
        private static readonly List<string> RequiredTypes = new()
        {
            "facility_name", "facility_id", "dwg_title", "dwg_size", "dwg_number", "dwg_sheet",
            "dwg_rev", "dwg_type"
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
            // Gather min X1 and max Y1 for each sheet in advance
            var sheetBounds = new Dictionary<int, (double MinX, double MaxY)>();

            string boundsQuery = @"
                SELECT
                    Sheet,
                    MIN(X1) AS MinX,
                    MAX(Y1) AS MaxY
                FROM pdf_table
                GROUP BY Sheet
            ";

            using (var boundsCmd = new SQLiteCommand(boundsQuery, connection))
            using (var boundsReader = boundsCmd.ExecuteReader())
            {
                while (boundsReader.Read())
                {
                    int sheetNumber = boundsReader.GetInt32(0);
                    double minX = boundsReader.IsDBNull(1) ? 0 : boundsReader.GetDouble(1);
                    double maxY = boundsReader.IsDBNull(2) ? 0 : boundsReader.GetDouble(2);

                    sheetBounds[sheetNumber] = (minX, maxY);
                }
            }
            
            string selectQuery = @"
                SELECT rowid, X1, Y1, Sheet
                FROM pdf_table
                ORDER BY Sheet, Y1 DESC;";
            

            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = -1;
                double y1_current = 0;
                double x1_current = 0;
                bool y1_current_set = false;
                
                // This set will track all unique “Type” entries for the current ItemNumber
                var currentItemTags = new HashSet<string>();

                using (var transaction = connection.BeginTransaction())
                using (var updateCmd =
                       new SQLiteCommand(
                           "UPDATE pdf_table SET Tag = @WordTag WHERE rowid = @RowId;",
                           connection, transaction))
                {
                    updateCmd.Parameters.Add(new SQLiteParameter("@WordTag"));
                    updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                    while (reader.Read())
                    {
                        int rowId = reader.GetInt32(0);
                        double x1 = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                        double y1 = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        int sheetNumber = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);


                        if (lastSheetNumber == -1 || sheetNumber != lastSheetNumber)
                        {
                            if (lastSheetNumber != -1)
                            {
                                currentItemTags.Clear();
                            }

                            if (sheetBounds.TryGetValue(sheetNumber, out var bounds))
                            {
                                x1_current = bounds.MinX;
                                y1_current = bounds.MaxY;
                            }
                            lastSheetNumber = sheetNumber;
                            currentItemTags.Clear();
                        }
                        
                        string tag = IsValidTag(x1, y1, ref x1_current, ref y1_current);

                        if (!string.IsNullOrEmpty(tag))
                        {
                            updateCmd.Parameters["@WordTag"].Value = tag;
                            updateCmd.Parameters["@RowId"].Value = rowId;
                            updateCmd.ExecuteNonQuery();
                            
                            currentItemTags.Add(tag);
                        }
                    }
                    if (lastSheetNumber != -1 && currentItemTags.Count > 0)
                    {
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
            
        
            // For convenience, a local function that inserts a row
            void InsertMissingRow(string type, double x1, double y1)
            {
                double x2 = 0;
                double y2 = y1 + 5.77; 
                
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
                string insertQuery = @"
                    INSERT INTO pdf_table 
                    (Sheet, Item, Tag, X1, Y1, X2, Y2, WordRotation, PageRotation, Word)
                    VALUES
                    (@sheetNumber, @itemNumber, @tag, @x1, @y1, @x2, @y2, @wordRotation, @pageRotation, @word);
                ";
        
                using var cmd = new SQLiteCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@sheetNumber", sheetNumber);
                cmd.Parameters.AddWithValue("@itemNumber", itemNumber);
                cmd.Parameters.AddWithValue("@tag", type);
                cmd.Parameters.AddWithValue("@x1", x1);
                cmd.Parameters.AddWithValue("@y1", y1);
                cmd.Parameters.AddWithValue("@x2", x2);
                cmd.Parameters.AddWithValue("@y2", y2);
                cmd.Parameters.AddWithValue("@word", string.Empty);
                cmd.Parameters.AddWithValue("@wordRotation", 0);
                cmd.Parameters.AddWithValue("@pageRotation", 0);
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
        double x1_relative = Math.Abs(x1_current - x1);
        double y1_relative = Math.Abs(y1_current - y1);
        
            return x1 switch
            { 
                 _ when  x1_relative.IsBetween(0, 442) && y1_relative.IsBetween(14, 18) => "facility_name",
                 _ when  x1_relative.IsBetween(30, 442) && y1_relative.IsBetween(28, 32) => "dwg_title",
                 _ when  x1_relative.IsBetween(30, 130) && y1_relative.IsBetween(52, 56) => "dwg_type",
                 _ when  x1_relative.IsBetween(145, 200) && y1_relative.IsBetween(70, 74) => "facility_id",
                 _ when  x1_relative.IsBetween(255, 375) && y1_relative.IsBetween(76, 80) => "dwg_number",
                 _ when  x1_relative.IsBetween(200, 237) && y1_relative.IsBetween(76, 80) => "dwg_size",
                 _ when  x1_relative.IsBetween(390, 420) && y1_relative.IsBetween(76, 80) => "dwg_sheet",
                 _ when  x1_relative.IsBetween(435, 442) && y1_relative.IsBetween(76, 80) => "dwg_rev",
                 _ => string.Empty
            };
    }
    
        private void DeleteNullRows(SQLiteConnection connection)
        {
            var deleteQuery = @"
                DELETE FROM pdf_table
                WHERE Tag = 'NA';
            ";

            using var cmd = new SQLiteCommand(deleteQuery, connection);
            cmd.ExecuteNonQuery();
        }

    }

public static class RangeExtensions
{
    public static bool IsBetween(this double value, double min, double max)
    {
        return value > min && value < max;
    }
}
    

    