using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace PdfProcessor.Services
{
    public class CoordinateDotService
    {
        public void AnnotatePdfWithDots(string pdfPath, string outputFolder)
        {
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("PDF file not found.");
                return;
            }

            string dbPath = Path.Combine(Path.GetDirectoryName(pdfPath)!, "data.db");
            if (!File.Exists(dbPath))
            {
                Console.WriteLine("Database file not found.");
                return;
            }

            string outputPdfPath = Path.Combine(outputFolder, "highlighted_pdf.pdf");

            // Load PDF
            using PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);

            // Connect to SQLite and fetch sorted data
            string connectionString = $"Data Source={dbPath};Version=3;";
            using SQLiteConnection connection = new(connectionString);
            connection.Open();

            string query = @"
                SELECT SheetNumber, X1, Y1, X2, Y2, Type
                FROM pdf_table
                WHERE Type IN ('cable_tag',
                                'from_desc',
                                'to_desc',
                                'function',
                                'size',
                                'insulation',
                                'from_ref',
                                'to_ref',
                                'voltage',
                                'conductors',
                                'length')
                ORDER BY SheetNumber ASC, ItemNumber ASC, Type ASC";

            using SQLiteCommand command = new(query, connection);
            using SQLiteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                int pageIndex = reader.GetInt32(0) - 1; // Convert 1-based index to 0-based
                double x1 = reader.GetDouble(1);
                double y1 = reader.GetDouble(2);
                double x2 = reader.GetDouble(3);
                double y2 = reader.GetDouble(4);

                if (pageIndex >= 0 && pageIndex < document.Pages.Count)
                {
                    PdfPage page = document.Pages[pageIndex];
                    
                    // Ensure XGraphics is created and disposed properly
                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        double pageHeight = page.Height;
                        double adjustedY1 = pageHeight - y1;
                        double adjustedY2 = pageHeight - y2;

                        // Draw red dots
                        // XBrush redBrush = XBrushes.Red;
                        // double dotSize = 5;
                        //
                        // gfx.DrawEllipse(redBrush, x1 - dotSize / 2, adjustedY1 - dotSize / 2, dotSize, dotSize);
                        // gfx.DrawEllipse(redBrush, x2 - dotSize / 2, adjustedY2 - dotSize / 2, dotSize, dotSize);
                        
                        // Draw the rectangle
                        double rectWidth = x2 - x1;
                        double rectHeight = adjustedY1 - adjustedY2;
                        
                        XColor semiTransparentRed = XColor.FromArgb(76, 255, 0, 0);
                        XSolidBrush redBrush = new XSolidBrush(semiTransparentRed);

                        if (page.Rotation != 0)
                        {
                            gfx.DrawRectangle(redBrush, page.Height - y1, x1,  rectWidth, rectHeight);
                        }
                        else
                        {
                            gfx.DrawRectangle(redBrush, x1, adjustedY2, rectWidth, rectHeight);
                        }
                    } 
                }
            }

            document.Save(outputPdfPath);
            Console.WriteLine($"Annotated PDF saved at: {outputPdfPath}");
        }
    }
}
