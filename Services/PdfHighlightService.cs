using System.IO;
using PdfSharpCore.Drawing;


namespace PdfProcessor.Services
{
    public class PdfHighlightService
    {
        private readonly PdfRegionService _regionService = new PdfRegionService();
        public void HighlightPdfRegions(string inputPdfPath, string outputFolder)
        {
            string outputPdfPath = Path.Combine(outputFolder, "HighlightedPDF.pdf");

            // Open the existing PDF document for editing
            using (PdfSharpCore.Pdf.PdfDocument document = PdfSharpCore.Pdf.IO.PdfReader.Open(inputPdfPath, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Modify))
            {
                foreach (PdfSharpCore.Pdf.PdfPage page in document.Pages)
                {
                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    
                    double pageWidth = page.Width;
                    double pageHeight = page.Height;
                    int pageRotation = (int)page.Rotate;
                    
                    // Define different regions
                    XRect headerRegion = _regionService.GetHeaderRegion(pageWidth, pageHeight);
                    XRect footerRegion = _regionService.GetFooterRegion(pageWidth, pageHeight);
                    XRect contentRegion = _regionService.GetContentRegion(pageWidth, pageHeight);
                    XRect fullPageRegion = _regionService.GetFullPageRegion(pageWidth, pageHeight);
                    XRect bowRegion = _regionService.GetBowRegion(pageWidth, pageHeight, pageRotation);
                    
                    

                    // Draw the yellow highlight
                    XSolidBrush highlightBrush = new XSolidBrush(XColor.FromArgb(150, 255, 255, 0));
                    gfx.DrawRectangle(highlightBrush, bowRegion);
                }

                document.Save(outputPdfPath);
            }

            Console.WriteLine($"Highlighted PDF saved at: {outputPdfPath}");
        }
    }
}