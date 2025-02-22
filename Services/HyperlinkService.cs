using System;
using System.IO;
using System.Data.SQLite;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Geom;

namespace PdfProcessor.Services;

public class HyperlinkService
{
    public void HyperlinkMain(string dbFilePath, string bowPath, string dwgPath)
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
                AddHyperlink(connection,bowPath, dwgPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing database: {ex.Message}");
        }
    }
    
    public void AddHyperlink(SQLiteConnection connection,string bowPath, string dwgPath)
    {
        using (PdfReader reader = new PdfReader(bowPath))
        using (PdfWriter writer = new PdfWriter(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(bowPath),"hyperlinked_BOW.pdf")))
        using (PdfDocument pdfDoc = new PdfDocument(reader, writer))
        {
            // PdfPage page = pdfDoc.GetPage(sourcePage);
            //
            // // Create a rectangle where the link should appear
            // Rectangle linkArea = new Rectangle(x, y, width, height);
            //
            // // Define the GoToR action (link to another PDF with specific coordinates)
            // PdfAction action = PdfAction.CreateGoToR(
            //     targetPdf, 
            //     new PdfDestination(PdfDestination.FIT), // FIT ensures it navigates to the right page
            //     false);

            // // Create the link annotation
            // PdfLinkAnnotation linkAnnotation = new PdfLinkAnnotation(linkArea)
            //     .SetAction(action);
            //
            // // Add annotation to the page
            // page.AddAnnotation(linkAnnotation);

            Console.WriteLine("Hyperlink added successfully.");
        }
    }
}