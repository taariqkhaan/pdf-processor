using System.Globalization;
using System.Data.SQLite;
using System.IO;


namespace PdfProcessor.Services;

public class CableSummaryService
{
    public void GenerateCableSummaryCsv(string dbFilePath)
        {
            if (!File.Exists(dbFilePath))
            {
                Console.WriteLine("Database file not found.");
                return;
            }
            // Define the CSV output path in the same folder as the database
            string outputCsvPath = Path.Combine(Path.GetDirectoryName(dbFilePath), "cable_summary.csv");
            
            using var conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;");
            conn.Open();

            // Get all rows with Tag = 'keymark' and group by Word (Keymark)
            var keymarkGroups = new Dictionary<string, List<(int Sheet, int Item)>>();

            string keymarkQuery = @"
                SELECT Word, Sheet, Item 
                FROM BOW_table 
                WHERE Tag = 'keymark'
                ORDER BY Word;";

            using var keymarkCmd = new SQLiteCommand(keymarkQuery, conn);
            using var keymarkReader = keymarkCmd.ExecuteReader();

            while (keymarkReader.Read())
            {
                string keymark = keymarkReader["Word"].ToString();
                int sheet = Convert.ToInt32(keymarkReader["Sheet"]);
                int item = Convert.ToInt32(keymarkReader["Item"]);

                if (!keymarkGroups.ContainsKey(keymark))
                {
                    keymarkGroups[keymark] = new List<(int Sheet, int Item)>();
                }

                keymarkGroups[keymark].Add((sheet, item));
            }

            // Find corresponding lengths for each Keymark
            var keymarkLengths = new Dictionary<string, double>();

            foreach (var keymark in keymarkGroups.Keys)
            {
                double totalLength = 0;

                foreach (var (sheet, item) in keymarkGroups[keymark])
                {
                    string lengthQuery = @"
                        SELECT Word 
                        FROM BOW_table 
                        WHERE Tag = 'length' AND Sheet = @Sheet AND Item = @Item;";

                    using var lengthCmd = new SQLiteCommand(lengthQuery, conn);
                    lengthCmd.Parameters.AddWithValue("@Sheet", sheet);
                    lengthCmd.Parameters.AddWithValue("@Item", item);

                    using var lengthReader = lengthCmd.ExecuteReader();
                    while (lengthReader.Read())
                    {
                        if (!lengthReader.IsDBNull(0))
                        {
                            string wordValue = lengthReader["Word"].ToString()?.Trim();

                            if (int.TryParse(wordValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int length))
                            {
                                totalLength += length;
                            }
                        }
                    }
                }

                keymarkLengths[keymark] = totalLength;
            }
            conn.Close();

            //Write the results to a CSV file
            using (var writer = new StreamWriter(outputCsvPath))
            {
                // Write CSV headers
                writer.WriteLine("Keymark,Total Length");

                // Write CSV data
                foreach (var entry in keymarkLengths)
                {
                    writer.WriteLine($"{entry.Key},{entry.Value.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            Console.WriteLine($"Cable summary CSV saved at: {outputCsvPath}");
        }
}