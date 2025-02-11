using System.Globalization;
using System.IO;
using System.Text;
using PdfProcessor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PdfProcessor.Services
{
    public class PdfTextService
    {
        private readonly PdfRegionService _pdfRegionService;
        
        public PdfTextService(PdfRegionService pdfRegionService)
        {
            _pdfRegionService = pdfRegionService;
        }
        public List<PdfTextModel> ExtractTextAndCoordinates(string pdfPath)
        {
            List<PdfTextModel> extractedText = new List<PdfTextModel>();

            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                foreach (Page page in document.GetPages())
                {
                    PdfRectangle regionRect = _pdfRegionService.GetBowRegion(page.Width, page.Height, page.Rotation.Value);
                    extractedText.AddRange(ExtractTextFromPage(page, regionRect, page.Rotation.Value));
                }
            }
            return extractedText;
        }

        private List<PdfTextModel> ExtractTextFromPage(Page page, PdfRectangle region, int rotation)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>();
            List<Word> words = page.GetWords().ToList();
            
            double regionX1;
            double regionY1;
            double regionX2;
            double regionY2;
            
            if (rotation == 0)
            {
                regionX1 = region.BottomLeft.X;
                regionY1 = region.BottomLeft.Y;
                regionX2 = region.TopRight.X;
                regionY2 = region.TopRight.Y;
                
            }   
            else
            {
                regionX1 = region.TopRight.X;
                regionY1 = region.TopRight.Y;
                regionX2 = region.BottomLeft.X;
                regionY2 = region.BottomLeft.Y;
                Console.WriteLine($"{regionX1}, {regionY1}, {regionX2}, {regionY2}");
            }
            
            foreach (Word word in words)
            {
                if (!string.IsNullOrWhiteSpace(word.Text))
                {
                    var firstChar = word.Letters.First();
                    var lastChar = word.Letters.Last();
                    
                    double wordX1 = firstChar.GlyphRectangle.BottomLeft.X;
                    double wordY1 = firstChar.GlyphRectangle.BottomLeft.Y;
                    double wordX2 = lastChar.GlyphRectangle.TopRight.X;
                    double wordY2 = lastChar.GlyphRectangle.TopRight.Y;
                    
                    if (wordX1 >= regionX1 && wordY1 >= regionY1 && wordX2 <= regionX2 && wordY2 <= regionY2)
                    {
                        textModels.Add(new PdfTextModel(
                            word.Text,
                            wordX1,
                            wordY1,
                            wordX2,
                            wordY2
                        ));
                    }
                }
            }
            
            return textModels;
        }
        
        
        
    }
}
