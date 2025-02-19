using System.Data.SQLite;
using System.IO;

namespace PdfProcessor.Services
{
    public class DrawingService
    {
        private double topRightX;
        private double topRightY;
        private double bottomLeftX;
        private double bottomLeftY;
        
        private readonly PdfRegionService _regionService;
        public DrawingService()
        {
            _regionService = new PdfRegionService();
        }
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
                    //UpdateRowsBasedOnConditions(connection);
                    //DeleteNullRows(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing database: {ex.Message}");
            }
        }

    }
}
