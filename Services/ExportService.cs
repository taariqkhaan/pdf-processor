using System.Globalization;
using System.IO;
using System.Text;
using System.Data.SQLite;
using PdfProcessor.Models;


namespace PdfProcessor.Services
{
    public class ExportService
    {
        public void SaveToCsv(List<PdfTextModel> extractedText, string outputCsvPath)
        {
            StringBuilder csvContent = new StringBuilder();
            csvContent.AppendLine("Text,BottomLeftX,BottomLeftY,TopRightX,TopRightY,TextRotation,SheetNumber");

            foreach (var item in extractedText.Where(t => !string.IsNullOrWhiteSpace(t.Text)))
            {
                csvContent.AppendLine($"\"{item.Text.Replace("\"", "\"\"")}\"," +
                                      $"{item.BottomLeftX.ToString(CultureInfo.InvariantCulture)}," +
                                      $"{item.BottomLeftY.ToString(CultureInfo.InvariantCulture)}," +
                                      $"{item.TopRightX.ToString(CultureInfo.InvariantCulture)}," +
                                      $"{item.TopRightY.ToString(CultureInfo.InvariantCulture)}," +
                                      $"{item.Rotation.ToString(CultureInfo.InvariantCulture)}," +
                                      $"{item.PageNumber.ToString(CultureInfo.InvariantCulture)}");
            }
            File.WriteAllText(outputCsvPath, csvContent.ToString());
        }
        
        public void SaveToDatabase(List<PdfTextModel> extractedData, string databasePath)
        {
            string connectionString = $"Data Source={databasePath};Version=3;";

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                // Create the table if it doesn't exist
                string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS pdf_table (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Text TEXT NOT NULL,
                    X1 REAL NOT NULL,
                    Y1 REAL NOT NULL,
                    X2 REAL NOT NULL,
                    Y2 REAL NOT NULL,
                    TextRotation INTEGER NOT NULL,
                    SheetNumber INTEGER NOT NULL
                );";

                using (SQLiteCommand command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Insert extracted text and coordinates into the database
                string insertQuery = "INSERT INTO pdf_table (Text, X1, Y1, X2, Y2, TextRotation, SheetNumber) VALUES (@Text, @BottomLeftX, @BottomLeftY, @TopRightX, @TopRightY, @Rotation, @PageNumber);";

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.Add(new SQLiteParameter("@Text"));
                    command.Parameters.Add(new SQLiteParameter("@BottomLeftX"));
                    command.Parameters.Add(new SQLiteParameter("@BottomLeftY"));
                    command.Parameters.Add(new SQLiteParameter("@TopRightX"));
                    command.Parameters.Add(new SQLiteParameter("@TopRightY"));
                    command.Parameters.Add(new SQLiteParameter("@Rotation"));
                    command.Parameters.Add(new SQLiteParameter("@PageNumber"));

                    foreach (var data in extractedData)
                    {
                        command.Parameters["@Text"].Value = data.Text;
                        command.Parameters["@BottomLeftX"].Value = data.BottomLeftX;
                        command.Parameters["@BottomLeftY"].Value = data.BottomLeftY;
                        command.Parameters["@TopRightX"].Value = data.TopRightX;
                        command.Parameters["@TopRightY"].Value = data.TopRightY;
                        command.Parameters["@Rotation"].Value = data.Rotation;
                        command.Parameters["@PageNumber"].Value = data.PageNumber;
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                
                var deleteQuery = "DELETE FROM pdf_table WHERE Text IS NULL OR TRIM(Text) = '';";
                using (SQLiteCommand cmd = new SQLiteCommand(deleteQuery, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}

