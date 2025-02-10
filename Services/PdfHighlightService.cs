using System;
using System.IO;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfTextExtractor.Services
{
    public class PdfHighlightService
    {
        public void HighlightPdf(string inputPdfPath, string outputFolder, double highlightX, double highlightY, double width, double height)
        {
            string outputPdfPath = Path.Combine(outputFolder, "HighlightedPDF.pdf");

            // Open the existing PDF document for editing
            using (PdfSharpCore.Pdf.PdfDocument document = PdfSharpCore.Pdf.IO.PdfReader.Open(inputPdfPath, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Modify))
            {
                foreach (PdfSharpCore.Pdf.PdfPage page in document.Pages)
                {
                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    // Draw the yellow highlight
                    XSolidBrush highlightBrush = new XSolidBrush(XColor.FromArgb(150, 255, 255, 0));
                    gfx.DrawRectangle(highlightBrush, highlightX, page.Height - highlightY - height, width, height);
                }

                document.Save(outputPdfPath);
            }

            Console.WriteLine($"Highlighted PDF saved at: {outputPdfPath}");
        }
    }
}