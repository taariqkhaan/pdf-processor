using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;

namespace PdfProcessor.Services
{
    public class MissingInfoService
    {
        private readonly string[] requiredTypes = new[]
        {
            "cable_tag", "from_desc", "to_desc", "function", "size", "insulation",
            "from_ref", "to_ref", "voltage", "conductors", "length"
        };

        private readonly Dictionary<string, (int X1, int X2, bool IsLine1)> missingTypeCoordinates = new()
        {
            { "cable_tag", (23, 63, true) },
            { "from_desc", (137, 177, true) },
            { "to_desc", (275, 315, true) },
            { "function", (416, 456, true) },
            { "size", (539, 559, true) },
            { "insulation", (593, 633, true) },
            { "from_ref", (137, 177, false) },
            { "to_ref", (275, 315, false) },
            { "voltage", (452, 492, false) },
            { "conductors", (525, 545, false) },
            { "length", (583, 590, false) }
        };

        private const double Y1StartLine1 = 486;
        private const double Y1StartLine2 = 474;
        private const double Y2Offset = 5.77;
        private const int ItemSpacing = 30;

        public void ProcessDatabase(string dbFilePath)
        {
            using var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;");
            connection.Open();

            var missingEntries = FindMissingEntries(connection);
            InsertMissingEntries(connection, missingEntries);
        }

        private List<PdfEntry> FindMissingEntries(SQLiteConnection connection)
        {
            var missingEntries = new List<PdfEntry>();

            string query = "SELECT SheetNumber, ItemNumber, Type FROM pdf_table ORDER BY SheetNumber ASC, ItemNumber ASC;";
            using var command = new SQLiteCommand(query, connection);
            using var reader = command.ExecuteReader();

            int? currentSheetNumber = null;
            int? currentItemNumber = null;
            HashSet<string> foundTypes = new();

            while (reader.Read())
            {
                int? sheetNumber = reader["SheetNumber"] != DBNull.Value ? Convert.ToInt32(reader["SheetNumber"]) : null;
                int? itemNumber = reader["ItemNumber"] != DBNull.Value ? Convert.ToInt32(reader["ItemNumber"]) : null;
                string type = reader["Type"] != DBNull.Value ? reader["Type"].ToString() : string.Empty;

                // If encountering a NULL ItemNumber, process missing types for previous block
                if (itemNumber == null)
                {
                    if (currentSheetNumber.HasValue && currentItemNumber.HasValue)
                    {
                        var missingTypes = requiredTypes.Except(foundTypes).ToList();
                        foreach (var missingType in missingTypes)
                        {
                            missingEntries.Add(CreateMissingEntry(currentSheetNumber.Value, currentItemNumber.Value, missingType));
                        }
                    }

                    // Reset tracking variables
                    currentSheetNumber = null;
                    currentItemNumber = null;
                    foundTypes.Clear();
                }
                else
                {
                    // If new [SheetNumber, ItemNumber] block, check and reset
                    if (sheetNumber != currentSheetNumber || itemNumber != currentItemNumber)
                    {
                        // Process the previous block's missing types before moving to the new block
                        if (currentSheetNumber.HasValue && currentItemNumber.HasValue)
                        {
                            var missingTypes = requiredTypes.Except(foundTypes).ToList();
                            foreach (var missingType in missingTypes)
                            {
                                missingEntries.Add(CreateMissingEntry(currentSheetNumber.Value, currentItemNumber.Value, missingType));
                            }
                        }

                        // Start tracking new block
                        currentSheetNumber = sheetNumber;
                        currentItemNumber = itemNumber;
                        foundTypes.Clear();
                    }

                    // Add current type to the set of found types
                    foundTypes.Add(type);
                }
            }

            // Process the last batch if it wasn't handled
            if (currentSheetNumber.HasValue && currentItemNumber.HasValue)
            {
                var missingTypes = requiredTypes.Except(foundTypes).ToList();
                foreach (var missingType in missingTypes)
                {
                    missingEntries.Add(CreateMissingEntry(currentSheetNumber.Value, currentItemNumber.Value, missingType));
                }
            }

            return missingEntries;
        }

        private PdfEntry CreateMissingEntry(int sheetNumber, int itemNumber, string missingType)
        {
            var coordinates = missingTypeCoordinates[missingType];
            double y1 = (coordinates.IsLine1 ? Y1StartLine1 : Y1StartLine2) - (ItemSpacing * (itemNumber - 1));
            double y2 = y1 + Y2Offset;

            return new PdfEntry
            {
                Text = "",
                X1 = coordinates.X1,
                Y1 = y1,
                X2 = coordinates.X2,
                Y2 = y2,
                TextRotation = 0,
                SheetNumber = sheetNumber,
                Type = missingType,
                ItemNumber = itemNumber
            };
        }

        private void InsertMissingEntries(SQLiteConnection connection, List<PdfEntry> missingEntries)
        {
            if (missingEntries.Count == 0) return;

            string insertQuery = "INSERT INTO pdf_table (Text, X1, Y1, X2, Y2, TextRotation, SheetNumber, Type, ItemNumber) VALUES (@Text, @X1, @Y1, @X2, @Y2, @TextRotation, @SheetNumber, @Type, @ItemNumber);";

            using var transaction = connection.BeginTransaction();
            using var command = new SQLiteCommand(insertQuery, connection, transaction);

            command.Parameters.Add(new SQLiteParameter("@Text", DbType.String));
            command.Parameters.Add(new SQLiteParameter("@X1", DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@Y1", DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@X2", DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@Y2", DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@TextRotation", DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@SheetNumber", DbType.Int32));
            command.Parameters.Add(new SQLiteParameter("@Type", DbType.String));
            command.Parameters.Add(new SQLiteParameter("@ItemNumber", DbType.Int32));

            foreach (var entry in missingEntries)
            {
                command.Parameters["@Text"].Value = entry.Text;
                command.Parameters["@X1"].Value = entry.X1;
                command.Parameters["@Y1"].Value = entry.Y1;
                command.Parameters["@X2"].Value = entry.X2;
                command.Parameters["@Y2"].Value = entry.Y2;
                command.Parameters["@TextRotation"].Value = entry.TextRotation;
                command.Parameters["@SheetNumber"].Value = entry.SheetNumber;
                command.Parameters["@Type"].Value = entry.Type;
                command.Parameters["@ItemNumber"].Value = entry.ItemNumber;

                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    public class PdfEntry
    {
        public string Text { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public double TextRotation { get; set; }
        public int SheetNumber { get; set; }
        public string Type { get; set; }
        public int ItemNumber { get; set; }
    }
}
