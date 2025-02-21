using System.Data.SQLite;
using System.IO;
using PdfProcessor.Models;


namespace PdfProcessor.Services;

public class ComparisonLogic
{
    public void CompareDatabase(string dbFilePath)
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
                AnalyzeBowTable(connection);
                
                //DeleteNullRows(connection);
                //UpdateColorFlag(connection);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing database: {ex.Message}");
        }
    }

    private void AnalyzeBowTable(SQLiteConnection connection)
    {
        string selectQuery = @"
            SELECT rowid, Word, Sheet, Tag, Item
            FROM BOW_table
            ORDER BY Item;";

        string fromRefValue = null;
        string toRefValue = null;
        
        using (var cmd = new SQLiteCommand(selectQuery, connection))
        using (var reader = cmd.ExecuteReader())
        {
            using (var transaction = connection.BeginTransaction())
            using (var updateCmd = new SQLiteCommand(@"UPDATE BOW_table 
                                                       SET ColorFlag = @ColorFlag
                                                       WHERE rowid = @RowId;", connection, transaction))
            {
                updateCmd.Parameters.Add(new SQLiteParameter("@ColorFlag"));
                updateCmd.Parameters.Add(new SQLiteParameter("@RowId"));

                while (reader.Read())
                {
                    int rowId = reader.GetInt32(0);
                    string wordValue = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                    int sheetNumber = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                    string tagValue = reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim();
                    int itemNumber = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);


                    if (tagValue == "cable_tag")
                    {
                        // Fetch wordValue for "from_ref" and "to_ref" with the same Item
                        fromRefValue = GetWordValueByTag(connection, itemNumber, "from_ref").Replace("(", "").Replace(")", "");
                        toRefValue = GetWordValueByTag(connection, itemNumber, "to_ref").Replace("(", "").Replace(")", "");
                        
                        AnalyzeDwgTable(connection, wordValue);
                    }
                    
                }
                
                transaction.Commit();
            }
        }
    }
    
    private void AnalyzeDwgTable(SQLiteConnection connection, string bowWordValue)
    {
        string selectQuery = @"
        SELECT Sheet
        FROM DWG_table
        WHERE Word = @BowWordValue;";

        using (var cmd = new SQLiteCommand(selectQuery, connection))
        {
            cmd.Parameters.AddWithValue("@BowWordValue", bowWordValue);

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    int sheetNumber = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    Console.WriteLine($"{bowWordValue}, {sheetNumber}");
                    
                    string selectWordQuery = @"
                        SELECT GROUP_CONCAT(Word, '-') as ConcatenatedWords
                        FROM (
                            SELECT Word
                            FROM DWG_table
                            WHERE Sheet = @SheetNumber 
                              AND Tag IN ('dwg_size', 'dwg_number', 'dwg_sheet')
                            ORDER BY 
                                CASE Tag 
                                    WHEN 'dwg_size' THEN 1
                                    WHEN 'dwg_number' THEN 2
                                    WHEN 'dwg_sheet' THEN 3
                                END
                        ) AS OrderedWords;";

                    using (var wordCmd = new SQLiteCommand(selectWordQuery, connection))
                    {
                        wordCmd.Parameters.AddWithValue("@SheetNumber", sheetNumber);

                        using (var wordReader = wordCmd.ExecuteReader())
                        {
                            while (wordReader.Read())
                            {
                                string concatenatedWords = wordReader.IsDBNull(0) ? "NULL" : wordReader.GetString(0);
                                Console.WriteLine($" {concatenatedWords}");
                                break;
                            }
                            
                            
                        }
                    }
                }
            }
        }
    }

    
    private string GetWordValueByTag(SQLiteConnection connection, int itemNumber, string tagType)
    {
        string query = @"
                        SELECT Word 
                        FROM BOW_table 
                        WHERE Item = @ItemNumber AND Tag = @TagType 
                        LIMIT 1;"; // Assuming there's only one matching entry per item

        using (var cmd = new SQLiteCommand(query, connection))
        {
            cmd.Parameters.AddWithValue("@ItemNumber", itemNumber);
            cmd.Parameters.AddWithValue("@TagType", tagType);

            object result = cmd.ExecuteScalar();
            return result != null ? result.ToString().Trim() : string.Empty;
        }
    }
    
}