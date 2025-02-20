using System.Data.SQLite;
using System.IO;
using PdfProcessor.Models;

namespace PdfProcessor.Services;

public class DwgTitleService
    {
        private static readonly List<string> RequiredTypes = new()
        {
            "facility_name", "facility_id", "dwg_title1","dwg_title2", "dwg_scale", "dwg_size", "dwg_number", "dwg_sheet",
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
            
            var processedSheets = new HashSet<int>();
            
            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                int lastSheetNumber = 0;
                double y1_current = 0;
                double x1_current = 0;
                
                // This set will track all unique “Type” entries for the current ItemNumber
                var currentItemTags = new HashSet<string>();

                using (var transaction = connection.BeginTransaction())
                using (var updateCmd = new SQLiteCommand("UPDATE pdf_table SET Tag = @WordTag WHERE rowid = @RowId;",
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
                        
                        if (sheetNumber != lastSheetNumber)
                        {
                            if (lastSheetNumber != 0 )
                            {
                                MissingTag(connection, currentItemTags, lastSheetNumber, x1_current, y1_current);
                                currentItemTags.Clear();
                            }
                            if (sheetBounds.TryGetValue(sheetNumber, out var bounds))
                            {
                                x1_current = bounds.MinX;
                                y1_current = bounds.MaxY;
                            }
                            processedSheets.Add(sheetNumber);
                            lastSheetNumber = sheetNumber;
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
                    if (lastSheetNumber != 0)
                    {
                        MissingTag(connection, currentItemTags, lastSheetNumber, x1_current, y1_current);
                        Console.WriteLine($"{lastSheetNumber}, {x1_current}, {y1_current}");
                        currentItemTags.Clear();
                    }
                    transaction.Commit();
                }
            }
            
            foreach (var sheetNumber in sheetBounds.Keys)
            {
                if (!processedSheets.Contains(sheetNumber))
                {
                    var bounds = sheetBounds[sheetNumber];
                    MissingTag(connection, new HashSet<string>(), sheetNumber, bounds.MinX, bounds.MaxY);
                }
            }
        }
        
        private void MissingTag(
            SQLiteConnection connection,
            HashSet<string> existingTags,
            int sheetNumber,
            double x1_current,
            double y1_current
        )
        {
            
            // For convenience, a local function that inserts a row
            void InsertMissingRow(string type, double x1, double y1)
            {
                double width = 0;
                switch (type)
                {
                    case "facility_name":    width = 300; break;
                    case "facility_id":      width = 40; break;
                    case "dwg_title1":       width = 300; break;
                    case "dwg_type":         width = 40; break;
                    case "dwg_scale":        width = 40; break;
                    case "dwg_size":         width = 30; break;
                    case "dwg_number":       width = 40; break;
                    case "dwg_sheet":        width = 40; break;
                    case "dwg_rev":          width = 20; break;
                }
                double x2 = x1 + width;
                double y2 = y1 + 5.77;
        
                // Now insert a new row in pdf_table
                string insertQuery = @"
                    INSERT INTO pdf_table 
                    (Sheet, Item, Tag, X1, Y1, X2, Y2, WordRotation, PageRotation, Word)
                    VALUES
                    (@sheetNumber, @itemNumber, @tag, @x1, @y1, @x2, @y2, @wordRotation, @pageRotation, @word);
                ";
        
                using var cmd = new SQLiteCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@sheetNumber", sheetNumber);
                cmd.Parameters.AddWithValue("@itemNumber", 0);
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
                        case "facility_name":
                            InsertMissingRow("facility_name", x1_current + 70, y1_current - 16);
                            break;
                        case "dwg_title1":
                            InsertMissingRow("dwg_title1", x1_current + 40, y1_current - 30);
                            break;
                        case "dwg_scale":
                            InsertMissingRow("dwg_scale", x1_current + 40, y1_current - 64);
                            break;
                        case "dwg_type":
                            InsertMissingRow("dwg_type", x1_current + 40, y1_current - 54);
                            break;
                        case "facility_id":
                            InsertMissingRow("facility_id", x1_current + 145, y1_current - 72);
                            break;
                        case "dwg_number":
                            InsertMissingRow("dwg_number", x1_current + 295, y1_current - 78);
                            break;
                        case "dwg_size":
                            InsertMissingRow("dwg_size", x1_current + 210, y1_current - 78);
                            break;
                        case "dwg_sheet":
                            InsertMissingRow("dwg_sheet", x1_current + 390, y1_current - 78);
                            break;
                        case "dwg_rev":
                            InsertMissingRow("dwg_rev", x1_current + 444, y1_current - 70);
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
                 _ when  x1_relative.IsBetween(0, 450) && y1_relative.IsBetween(14, 18) => "facility_name",
                 _ when  x1_relative.IsBetween(30, 450) && y1_relative.IsBetween(28, 34) => "dwg_title1",
                 _ when  x1_relative.IsBetween(30, 450) && y1_relative.IsBetween(40, 48) => "dwg_title2",
                 _ when  x1_relative.IsBetween(30, 450) && y1_relative.IsBetween(62, 68) => "dwg_scale",
                 _ when  x1_relative.IsBetween(30, 130) && y1_relative.IsBetween(52, 56) => "dwg_type",
                 _ when  x1_relative.IsBetween(145, 200) && y1_relative.IsBetween(70, 74) => "facility_id",
                 _ when  x1_relative.IsBetween(255, 375) && y1_relative.IsBetween(76, 80) => "dwg_number",
                 _ when  x1_relative.IsBetween(200, 237) && y1_relative.IsBetween(76, 80) => "dwg_size",
                 _ when  x1_relative.IsBetween(390, 420) && y1_relative.IsBetween(76, 80) => "dwg_sheet",
                 _ when  x1_relative.IsBetween(434, 450) && y1_relative.IsBetween(70, 80) => "dwg_rev",
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
    