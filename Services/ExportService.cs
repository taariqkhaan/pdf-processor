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
            if (extractedText == null || extractedText.Count == 0)
            {
                throw new ArgumentException("Extracted text list is null or empty.", nameof(extractedText));
            }

            using (StreamWriter writer = new StreamWriter(outputCsvPath, false, Encoding.UTF8))
            {
                writer.WriteLine("Word,X1,Y1,X2,Y2,Sheet,PageRotation,WordRotation,Tag,Item");

                foreach (var item in extractedText.Where(t => !string.IsNullOrWhiteSpace(t.PageWord)))
                {
                    writer.WriteLine($"\"{item.PageWord.Replace("\"", "\"\"")}\"," +
                                     $"{item.BottomLeftX.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.BottomLeftY.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.TopRightX.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.TopRightY.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.PageNumber.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.PageRotation.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.WordRotation.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.WordTag.ToString(CultureInfo.InvariantCulture)}," +
                                     $"{item.ItemNumber.ToString(CultureInfo.InvariantCulture)}");
                }
            }
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
                    Word TEXT NOT NULL,
                    X1 REAL NOT NULL,
                    Y1 REAL NOT NULL,
                    X2 REAL NOT NULL,
                    Y2 REAL NOT NULL,
                    Sheet INTEGER NOT NULL,
                    PageRotation INTEGER NOT NULL,
                    WordRotation INTEGER NOT NULL,
                    Tag TEXT NOT NULL,
                    Item INTEGER NOT NULL                 
                );";

                using (SQLiteCommand command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Insert extracted text and coordinates into the database
                string insertQuery = @"INSERT INTO pdf_table 
                                                   (Word, 
                                                    X1, 
                                                    Y1, 
                                                    X2, 
                                                    Y2,
                                                    Sheet,
                                                    PageRotation,
                                                    WordRotation,
                                                    Tag,
                                                    Item
                                                    ) 
                                        VALUES (@PageWord, 
                                                @BottomLeftX, 
                                                @BottomLeftY, 
                                                @TopRightX, 
                                                @TopRightY,
                                                @PageNumber,
                                                @PageRotation,
                                                @WordRotation,
                                                @WordTag,
                                                @ItemNumber
                                                );";

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.Add(new SQLiteParameter("@PageWord"));
                    command.Parameters.Add(new SQLiteParameter("@BottomLeftX"));
                    command.Parameters.Add(new SQLiteParameter("@BottomLeftY"));
                    command.Parameters.Add(new SQLiteParameter("@TopRightX"));
                    command.Parameters.Add(new SQLiteParameter("@TopRightY"));
                    command.Parameters.Add(new SQLiteParameter("@PageNumber"));
                    command.Parameters.Add(new SQLiteParameter("@PageRotation"));
                    command.Parameters.Add(new SQLiteParameter("@WordRotation"));
                    command.Parameters.Add(new SQLiteParameter("@WordTag"));
                    command.Parameters.Add(new SQLiteParameter("@ItemNumber"));

                    foreach (var data in extractedData)
                    {
                        command.Parameters["@PageWord"].Value = data.PageWord;
                        command.Parameters["@BottomLeftX"].Value = data.BottomLeftX;
                        command.Parameters["@BottomLeftY"].Value = data.BottomLeftY;
                        command.Parameters["@TopRightX"].Value = data.TopRightX;
                        command.Parameters["@TopRightY"].Value = data.TopRightY;
                        command.Parameters["@PageNumber"].Value = data.PageNumber;
                        command.Parameters["@PageRotation"].Value = data.PageRotation;
                        command.Parameters["@WordRotation"].Value = data.WordRotation;
                        command.Parameters["@WordTag"].Value = data.WordTag;
                        command.Parameters["@ItemNumber"].Value = data.ItemNumber;
                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                
                // var deleteQuery = "DELETE FROM pdf_table WHERE Text IS NULL OR TRIM(Text) = '';";
                // using (SQLiteCommand cmd = new SQLiteCommand(deleteQuery, connection))
                // {
                //     cmd.ExecuteNonQuery();
                // }
            }
        }
    }
}