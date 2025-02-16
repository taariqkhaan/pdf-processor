using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace PdfProcessor.Services;

public class DrawingAnnotationService
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
    }
}