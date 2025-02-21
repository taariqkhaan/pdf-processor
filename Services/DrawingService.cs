using System.Data.SQLite;
using System.IO;
using PdfProcessor.Models;

namespace PdfProcessor.Services;

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
                    UpdateRowsBasedOnConditions(connection);
                    //DeleteNullRows(connection);
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
            SELECT Word
            FROM BOW_table
            WHERE Tag = 'cable_tag';";

            using (var cmd = new SQLiteCommand(selectQuery, connection))
            using (var reader = cmd.ExecuteReader())
            {
                using (var transaction = connection.BeginTransaction())
                using (var deleteCmd = new SQLiteCommand(@"DELETE FROM DWG_table 
                                                   WHERE Word NOT IN (SELECT Word FROM BOW_table WHERE Tag = 'cable_tag') 
                                                   AND Tag = 'NA';", connection, transaction))
                {
                    deleteCmd.ExecuteNonQuery();
                    transaction.Commit();
                }
            }
        }

    }
    