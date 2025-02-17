using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace PdfProcessor.Services
{
    public class AnnotatepdfService
    {
        public void AnnotatePdf(string pdfPath, string outputFolder)
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

            string outputPdfPath = Path.Combine(outputFolder, "highlighted_bow.pdf");

            // Load PDF
            using PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);

            // Connect to SQLite and fetch sorted data
            string connectionString = $"Data Source={dbPath};Version=3;";
            using SQLiteConnection connection = new(connectionString);
            connection.Open();

            string query = @"
                SELECT SheetNumber, X1, Y1, X2, Y2, Type, Text
                FROM pdf_table
                WHERE Type IN ('cable_tag','from_desc','to_desc','function','size','insulation','from_ref','to_ref','voltage','conductors','length')
                ORDER BY SheetNumber ASC, ItemNumber ASC, Type ASC";

            using SQLiteCommand command = new(query, connection);
            using SQLiteDataReader reader = command.ExecuteReader();
            
            // Define a font for drawing text (can be reused)
            CustomFontResolver.Register();
            XFont drawFont = new XFont("Arial", 7.0);

            while (reader.Read())
            {
                int pageIndex = reader.GetInt32(0) - 1; // Convert 1-based index to 0-based
                double x1 = reader.GetDouble(1);
                double y1 = reader.GetDouble(2);
                double x2 = reader.GetDouble(3);
                double y2 = reader.GetDouble(4);
                string typeValue = reader.IsDBNull(5) ? string.Empty : reader.GetString(5).Trim();
                string textValue = reader.IsDBNull(6) ? string.Empty : reader.GetString(6).Trim();

                if (pageIndex >= 0 && pageIndex < document.Pages.Count)
                {
                    PdfPage page = document.Pages[pageIndex];

                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        double pageHeight = page.Height;
                        double adjustedY1 = pageHeight - y1;
                        double adjustedY2 = pageHeight - y2;

                        // Draw the rectangle
                        double rectWidth = x2 - x1;
                        double rectHeight = adjustedY1 - adjustedY2;

                        // Choose color based on text value
                        bool missingValue   = string.IsNullOrEmpty(textValue);
                        XColor outlineColor = missingValue
                            ? XColor.FromArgb(0, 255, 0, 0) // missing values
                            : XColor.FromArgb(0, 255, 230, 0); // tags present
                        XColor fillcolor =  missingValue
                            ? XColor.FromArgb(50, 255, 0, 0) // missing values
                            : XColor.FromArgb(80, 255, 230, 0); // tags present

                        double penThickness = 3; // Thickness of the outline
                        XPen outlinePen = new XPen(outlineColor, 1);
                        XSolidBrush brush = new XSolidBrush(fillcolor);

                        if (page.Rotation != 0)
                        {
                            gfx.TranslateTransform(page.Width - page.Height, page.Height);
                            gfx.RotateTransform(270);
                        }
                        
                        // Adjust rectangle size & position for outward-growing stroke
                        double outlineOffset = penThickness / 2;
                        double adjustedX1 = x1 - outlineOffset;
                        double adjustedRectY    = adjustedY2 - outlineOffset;
                        double adjustedWidth = rectWidth + penThickness;
                        double adjustedHeight = rectHeight + penThickness;
                        
                        gfx.DrawRectangle(outlinePen, adjustedX1, adjustedRectY, adjustedWidth, adjustedHeight);
                        gfx.DrawRectangle(brush,adjustedX1, adjustedRectY, adjustedWidth, adjustedHeight);

                        if (missingValue && !string.IsNullOrEmpty(typeValue))
                        {
                            // Measure the string so we can center it
                            XSize textSize = gfx.MeasureString(typeValue, drawFont);

                            // Center the text in the rectangle
                            double textX = adjustedX1 + (adjustedWidth - textSize.Width) / 2.0;
                            double textY = adjustedRectY + (adjustedHeight - textSize.Height) / 2.0 + textSize.Height * 0.9;

                            // Draw the Type text in black
                            gfx.DrawString(typeValue + "?", drawFont, XBrushes.Red, new XPoint(textX, textY));
                        }
                    }
                }
            }

            document.Save(outputPdfPath);
            Console.WriteLine($"Annotated PDF saved at: {outputPdfPath}");
        }
    }
}
