using iText.Kernel.Pdf;
using System.Collections.Generic;
using System.IO;

namespace PdfProcessor.Services
{
    public class PdfRotationService
    {
        public void RotatePdfPages(string filePath, List<int> verticalPageList)
        {
            string pdfPath = System.IO.Path.Combine( System.IO.Path.GetDirectoryName(filePath)!, "highlighted_DWG.pdf");
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("Drawing PDF file not found.");
                return;
            }
            
            string tempFilePath = pdfPath + ".tmp";

            using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(pdfPath), new PdfWriter(tempFilePath)))
            {
                foreach (int pageNumber in verticalPageList)
                {
                    PdfPage page = pdfDoc.GetPage(pageNumber);
                    page.SetRotation(90);
                }
            }

            // Overwrite the original file
            File.Delete(pdfPath);
            File.Move(tempFilePath, pdfPath);
            Console.WriteLine($"Vertical pages rotated");
        }
        
        public void RevertRotations(string filePath)
        {
            string pdfPath = Path.Combine(Path.GetDirectoryName(filePath)!, "highlighted_DWG.pdf");
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine("Drawing PDF file not found.");
                return;
            }

            string tempFilePath = pdfPath + ".tmp";

            using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(pdfPath), new PdfWriter(tempFilePath)))
            {
                int totalPages = pdfDoc.GetNumberOfPages();
                for (int i = 1; i <= totalPages; i++)
                {
                    PdfPage page = pdfDoc.GetPage(i);
                    page.SetRotation(0);
                }
            }
            // Overwrite the original file
            File.Delete(pdfPath);
            File.Move(tempFilePath, pdfPath);
            Console.WriteLine($"Vertical pages rotation reverted");
        }
    }
}