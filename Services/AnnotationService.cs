using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace PdfProcessor.Services;

public class AnnotationService
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
        
        string outputPdfPath = Path.Combine(outputFolder, "highlighted_dwg.pdf");
        
        using PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
        
        // Connect to SQLite and fetch sorted data
        string connectionString = $"Data Source={dbPath};Version=3;";
        using SQLiteConnection connection = new(connectionString);
        connection.Open();
        
        string query = @"
                SELECT Word, X1, Y1, X2, Y2, Sheet, WordRotation, Tag, Item
                FROM pdf_table
                WHERE Tag IN ('facility_name', 'facility_id', 'dwg_title1','dwg_title2', 'dwg_scale', 'dwg_size', 'dwg_number', 'dwg_sheet', 'dwg_rev', 'dwg_type')
                ORDER BY Sheet ASC, Item ASC";

        using SQLiteCommand command = new(query, connection);
        using SQLiteDataReader reader = command.ExecuteReader();
        
        // Define a font for drawing text (can be reused)
        CustomFontResolver.Register();
        XFont drawFont = new XFont("Arial", 7.0);
        
        while (reader.Read())
        {
            string wordValue = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            double real_x1 = reader.GetDouble(1);
            double real_y1 = reader.GetDouble(2);
            double real_x2 = reader.GetDouble(3);
            double real_y2 = reader.GetDouble(4);
            int pageIndex = reader.GetInt32(5) - 1;
            int wordRotation = reader.GetInt32(6);
            string tagValue = reader.IsDBNull(7) ? string.Empty : reader.GetString(7).Trim();

            (double x1, double y1, double x2, double y2) = AdjustCoordinates(wordRotation, 
                wordValue, real_x1, real_y1, real_x2, real_y2);
            

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
                    bool missingValue   = string.IsNullOrWhiteSpace(wordValue);
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
                    
                    if (string.IsNullOrWhiteSpace(wordValue))
                    {
                        // Measure the string so we can center it
                        XSize textSize = gfx.MeasureString(tagValue, drawFont);

                        // Center the text in the rectangle
                        double textX = adjustedX1 + (adjustedWidth - textSize.Width) / 2.0;
                        double textY = adjustedRectY + (adjustedHeight - textSize.Height) / 2.0 + textSize.Height * 0.9;

                        // Draw the Type text in black
                        gfx.DrawString(tagValue + "?", drawFont, XBrushes.Red, new XPoint(textX, textY));

                    }
                    
                    gfx.DrawRectangle(outlinePen, adjustedX1, adjustedRectY, adjustedWidth, adjustedHeight);
                    gfx.DrawRectangle(brush,adjustedX1, adjustedRectY, adjustedWidth, adjustedHeight);
                }
            }
        }
        document.Save(outputPdfPath);
        Console.WriteLine($"Annotated PDF saved at: {outputPdfPath}");
    }
    
    // Adjusts coordinates from PDFpig to work with PDFSharp based on text rotation
    private (double, double, double, double) AdjustCoordinates(int wordRotation, 
        string textValue, double real_x1, double real_y1, double real_x2, double real_y2)
    {
        if (string.IsNullOrEmpty(textValue))
        {
            return (real_x1, real_y1, real_x2, real_y2);
        }
        
        char firstChar = textValue[0];
        char lastChar = textValue[^1];
        double bottomLeftX = real_x1;
        double bottomLeftY = real_y1;
        double topRightX = real_x2;
        double topRightY = real_y2;

        switch (wordRotation)
        {
            case 0:
                if (".,_".Contains(lastChar))
                    topRightY += 5;

                if ("-+=".Contains(firstChar))
                    bottomLeftY -= 2.5;

                if ("-+=".Contains(lastChar))
                    topRightY += 4;

                if ("`'\"".Contains(firstChar))
                    bottomLeftY -= 5;
                break;

            case 90:
                topRightY -= 4;
                bottomLeftY += 4;

                if (".,_".Contains(lastChar))
                    topRightX += 9;

                if ("-+=".Contains(firstChar))
                    bottomLeftX -= 2.5;

                if ("-+=".Contains(lastChar))
                    topRightX += 4;

                if ("`'\"".Contains(firstChar))
                    bottomLeftX -= 5;
                break;

            case 180:
                (bottomLeftX, topRightX) = (topRightX, bottomLeftX);
                (bottomLeftY, topRightY) = (topRightY, bottomLeftY);

                if (".,_".Contains(lastChar))
                    bottomLeftY -= 9;

                if ("-+=".Contains(firstChar))
                    topRightY += 4;

                if ("-+=".Contains(lastChar))
                    bottomLeftY -= 5;

                if ("`'\"".Contains(firstChar))
                    topRightY += 4;
                break;

            case 270:
                bottomLeftX += 5;
                topRightX -= 5;

                if (".,_".Contains(firstChar))
                    bottomLeftX += 2;

                if (".,_".Contains(lastChar))
                    topRightX -= 12;

                if ("-+=".Contains(lastChar))
                    topRightX -= 7;

                if ("`'\"".Contains(firstChar))
                    bottomLeftX += 10;
                break;
        }

        return (bottomLeftX, bottomLeftY, topRightX, topRightY);
    }

}