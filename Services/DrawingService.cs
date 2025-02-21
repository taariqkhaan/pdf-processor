using System;
using System.Data.SQLite;
using System.IO;

namespace PdfProcessor.Services
{
    public class DrawingService
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
                    UpdateAndDeleteRows(connection);
                    CreateDwgNumberTable(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing database: {ex.Message}");
            }
        }

        private void UpdateAndDeleteRows(SQLiteConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // Update rows in DWG_table where Word matches in BOW_table
                    string updateQuery = @"
                        UPDATE DWG_table 
                        SET Tag = @newTag
                        WHERE Word IN (SELECT Word FROM BOW_table WHERE Tag = @bowTag);";

                    using (var updateCmd = new SQLiteCommand(updateQuery, connection, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@newTag", "cable_tag");
                        updateCmd.Parameters.AddWithValue("@bowTag", "cable_tag");
                        updateCmd.ExecuteNonQuery();
                    }

                    // Delete remaining rows
                    string deleteQuery = @"
                        DELETE FROM DWG_table 
                        WHERE Tag = 'NA';";

                    using (var deleteCmd = new SQLiteCommand(deleteQuery, connection, transaction))
                    {
                        deleteCmd.ExecuteNonQuery();
                    }
                    
                    transaction.Commit();
                    
                    // free-up disk space after deleting rows
                    using (var vacuumCmd = new SQLiteCommand("VACUUM;", connection))
                    {
                        vacuumCmd.ExecuteNonQuery();
                    }

                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Console.WriteLine($"Error during database update/delete: {ex.Message}");
                }
            }
        }

        private void CreateDwgNumberTable(SQLiteConnection connection)
        {
            try
            {
                using (var transaction = connection.BeginTransaction())
                {
                    // Delete any existing full_dwg_number entries to avoid duplicates
                    string deleteExistingQuery = @"
                    DELETE FROM DWG_table WHERE Tag = 'full_dwg_number';";

                    using (var deleteCmd = new SQLiteCommand(deleteExistingQuery, connection, transaction))
                    {
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Insert concatenated FullNumbers into the Word column where Tag is 'full_dwg_number'
                    string insertQuery = @"
                    INSERT INTO DWG_table (Sheet, Word, Tag, X1, Y1, X2, Y2, PageRotation, WordRotation, Item, ColorFlag)
                    SELECT 
                        ds.Sheet,
                        COALESCE(dz.Word, 'NULL') || '-' || 
                        COALESCE(dn.Word, 'NULL') || '-' || 
                        COALESCE(ds.Word, 'NULL') AS FullNumber,
                        'full_dwg_number',
                        0 AS X1, 0 AS Y1, 0 AS X2, 0 AS Y2,  -- Default coordinate values
                        0 AS PageRotation, 0 AS WordRotation,  -- Default rotation values
                        0 AS Item, 0 AS ColorFlag  -- Default attributes
                    FROM DWG_table ds
                    LEFT JOIN DWG_table dz ON dz.Tag = 'dwg_size' AND dz.Sheet = ds.Sheet
                    LEFT JOIN DWG_table dn ON dn.Tag = 'dwg_number' AND dn.Sheet = ds.Sheet
                    WHERE ds.Tag = 'dwg_sheet'
                    GROUP BY ds.Sheet;";

                    using (var insertCmd = new SQLiteCommand(insertQuery, connection, transaction))
                    {
                        insertCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inserting full DWG numbers: {ex.Message}");
            }
        }
    }
}
