using PdfProcessor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using System.Diagnostics;

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
            Stopwatch stopwatch = Stopwatch.StartNew(); // Start measuring time
            List<PdfTextModel> extractedText = new List<PdfTextModel>();

            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                int pageNumber = 1;
                
                foreach (Page page in document.GetPages())
                {
                    PdfRectangle regionRect = _pdfRegionService.GetBowRegion(page.Width, page.Height, page.Rotation.Value);
                    extractedText.AddRange(ExtractTextFromPage(page, regionRect, page.Rotation.Value, pageNumber));
                    
                    pageNumber++;
                }
            }
            stopwatch.Stop(); // Stop measuring time
            Console.WriteLine($"Total Execution PdfRegionService Time: {stopwatch.ElapsedMilliseconds} ms");
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
            double? dateY1 = null;
            
            bool dateFoundOnPage = false;

            // First pass: Check if "DATE:" exists on the page
            foreach (Word word in words)
            {
                if (!string.IsNullOrWhiteSpace(word.Text) && word.Text.Trim().Equals("DATE:", StringComparison.OrdinalIgnoreCase))
                {
                    dateFoundOnPage = true;
                    var firstChar = word.Letters.First();
                    dateY1 = firstChar.GlyphRectangle.BottomLeft.Y; // Store Y1 of "DATE:"
                    break; // No need to continue checking
                }
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
                    
                    // Smaller characters like . or * can have smaller Y2 value
                    if (Math.Abs(wordY1 - wordY2) < 5)
                    {
                        wordY2 = wordY1 + 5.77;
                    }
                    
                    int textRotation = 0; // Detect text rotation
                    
                    if (wordX1 >= regionX1 && wordY1 >= regionY1 && wordX2 <= regionX2 && wordY2 <= regionY2)
                    {
                        if (dateY1.HasValue && Math.Abs(wordY1 - dateY1.Value) < 2)
                        {
                            continue; // Skip words with similar Y1 values
                        }
                        
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
    }
} 