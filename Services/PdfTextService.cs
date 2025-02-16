using PdfProcessor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;


namespace PdfProcessor.Services
{
    public class PdfTextService
    {
        public List<PdfTextModel> ExtractTextAndCoordinates(string pdfPath)
        {
            
            List<PdfTextModel> extractedText = new List<PdfTextModel>();

            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                int pageNumber = 1;
                
                foreach (Page page in document.GetPages())
                {
                    extractedText.AddRange(ExtractTextFromPage(page, page.Rotation.Value, pageNumber));
                    pageNumber++;
                }
            }
            return extractedText;
        }
        private List<PdfTextModel> ExtractTextFromPage(Page page, int rotation, int pageNumber)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>();
            List<Word> words = page.GetWords().ToList();

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
                    if (Math.Abs(wordY1 - wordY2) < 5 )
                    {
                        if (word.Text.Contains("\"") || word.Text.Contains("'"))
                        {
                            wordY1 = wordY2 - 5.77;
                        }
                        else
                        {
                            wordY2 = wordY1 + 5.77;
                        }
                    }
                    
                    int textRotation = DetermineWordRotation(word);
                    
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
            return textModels;
        }
        
        private int DetermineWordRotation(Word word)
        {
            // Get the first and last letters' center positions 
            // (or use BottomLeft, TopRight, etc. as you prefer).
            Letter firstLetter = word.Letters.First();
            Letter lastLetter  = word.Letters.Last();

            // Compute a simple vector from the first letter to the last letter.
            double deltaX = lastLetter.GlyphRectangle.BottomLeft.X - firstLetter.GlyphRectangle.BottomLeft.X;
            double deltaY = lastLetter.GlyphRectangle.BottomLeft.Y - firstLetter.GlyphRectangle.BottomLeft.Y;

            // Decide if the word is predominantly horizontal or vertical
            if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            {
                return (deltaX >= 0) ? 0 : 180;
            }
            
            return (deltaY >= 0) ? 90 : 270;
            
        }

    }
} 