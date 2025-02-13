using PdfProcessor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PdfProcessor.Services
{
    public class PdfTextServiceMultiCore
    {
        private readonly PdfRegionService _pdfRegionService;

        public PdfTextServiceMultiCore(PdfRegionService pdfRegionService)
        {
            _pdfRegionService = pdfRegionService;
        }

        public List<PdfTextModel> ExtractTextAndCoordinates(string pdfPath)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var extractedText = new List<PdfTextModel>(5000); // Preallocate list size

            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                var pages = document.GetPages().ToArray(); // Avoid enumerating multiple times
                var lockObj = new object(); // Lock object for safe list writing

                Parallel.For(0, pages.Length, i =>
                {
                    var page = pages[i];
                    int pageNumber = i + 1;

                    PdfRectangle regionRect = _pdfRegionService.GetBowRegion(page.Width, page.Height, page.Rotation.Value);
                    var pageText = ExtractTextFromPage(page, regionRect, page.Rotation.Value, pageNumber);

                    // Lock to add results safely
                    lock (lockObj)
                    {
                        extractedText.AddRange(pageText);
                    }
                });
            }

            stopwatch.Stop();
            Console.WriteLine($"Total Execution Time: {stopwatch.ElapsedMilliseconds} ms");
            return extractedText;
        }

        private List<PdfTextModel> ExtractTextFromPage(Page page, PdfRectangle region, int rotation, int pageNumber)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>(100); // Preallocate space
            var words = page.GetWords().ToArray(); // Convert to array for fast indexing

            double regionX1 = region.BottomLeft.X;
            double regionY1 = region.BottomLeft.Y;
            double regionX2 = region.TopRight.X;
            double regionY2 = region.TopRight.Y;
            double? dateY1 = null;

            // Find "DATE:" word efficiently
            foreach (var word in words)
            {
                if (word.Text.Trim().Equals("DATE:", StringComparison.OrdinalIgnoreCase))
                {
                    dateY1 = word.Letters[0].GlyphRectangle.BottomLeft.Y;
                    break; // Stop searching once found
                }
            }

            // Extract text within the region
            foreach (var word in words)
            {
                if (!IsValidWord(word, regionX1, regionY1, regionX2, regionY2, dateY1))
                    continue;

                var firstChar = word.Letters[0];
                var lastChar = word.Letters[^1]; // Faster than `word.Letters.Last()`

                double wordX1 = firstChar.GlyphRectangle.BottomLeft.X;
                double wordY1 = firstChar.GlyphRectangle.BottomLeft.Y;
                double wordX2 = lastChar.GlyphRectangle.TopRight.X;
                double wordY2 = lastChar.GlyphRectangle.TopRight.Y;

                textModels.Add(new PdfTextModel(word.Text, wordX1, wordY1, wordX2, wordY2, 0, pageNumber));
            }

            return textModels;
        }

        private bool IsValidWord(Word word, double regionX1, double regionY1, double regionX2, double regionY2, double? dateY1)
        {
            var firstChar = word.Letters[0];
            double wordY1 = firstChar.GlyphRectangle.BottomLeft.Y;

            // Filter out words outside region
            if (word.Letters[0].GlyphRectangle.BottomLeft.X < regionX1 ||
                wordY1 < regionY1 ||
                word.Letters[^1].GlyphRectangle.TopRight.X > regionX2 ||
                word.Letters[^1].GlyphRectangle.TopRight.Y > regionY2)
                return false;

            // Exclude "DATE:" words if their Y position is similar
            return !(dateY1.HasValue && Math.Abs(wordY1 - dateY1.Value) < 2);
        }
    }
}
