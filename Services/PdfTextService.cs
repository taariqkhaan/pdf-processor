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
                int pageNumber = 1; // Page numbers start from 1
                
                foreach (Page page in document.GetPages())
                {
                    PdfRectangle regionRect = _pdfRegionService.GetBowRegion(page.Width, page.Height, page.Rotation.Value);
                    extractedText.AddRange(ExtractTextFromPage(page, regionRect, page.Rotation.Value, pageNumber));
                    //Console.WriteLine($"{extractedText[150].PageNumber}");
                    
                    pageNumber++; // Increment page number for next iteration
                }
            }
            return extractedText;
        }

        private List<PdfTextModel> ExtractTextFromPage(Page page, PdfRectangle region, int rotation, int pageNumber)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>();
            List<Word> words = page.GetWords().ToList();
            
            double regionX1 = region.BottomLeft.X;
            double regionY1 = region.BottomLeft.Y;
            double regionX2 = region.TopRight.X;
            double regionY2 = region.TopRight.Y;
            
            if (rotation != 0)
            {
                regionX1 = region.TopRight.X;
                regionY1 = region.TopRight.Y;
                regionX2 = region.BottomLeft.X;
                regionY2 = region.BottomLeft.Y;
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
                    
                    int textRotation = GetTextRotation(firstChar, lastChar); // Detect text rotation
                    
                    if (wordX1 >= regionX1 && wordY1 >= regionY1 && wordX2 <= regionX2 && wordY2 <= regionY2)
                    {
                        textModels.Add(new PdfTextModel(
                            word.Text,
                            wordX1,
                            wordY1,
                            wordX2,
                            wordY2,
                            textRotation,
                            pageNumber
                        ));
                    }
                }
            }
            return textModels;
        }
        private int GetTextRotation(Letter firstChar, Letter lastChar)
        {
            // Calculate angle of text based on first and last character positions
            double deltaX = lastChar.GlyphRectangle.BottomLeft.X - firstChar.GlyphRectangle.BottomLeft.X;
            double deltaY = lastChar.GlyphRectangle.BottomLeft.Y - firstChar.GlyphRectangle.BottomLeft.Y;
            
            double angle = Math.Atan2(deltaY, deltaX) * (180 / Math.PI);

            if (angle >= -10 && angle <= 10)
                return 0; // Horizontal text
            if (angle >= 80 && angle <= 100)
                return 90; // Vertical text (clockwise)
            if (angle >= 260 && angle <= 280)
                return 270; // Vertical text (counterclockwise)

            return 0; // Default to horizontal
        }
    }
}
