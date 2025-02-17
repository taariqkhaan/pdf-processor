using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using PdfProcessor.Models;

namespace PdfProcessor.Services
{
    public class DrawingService
    {
        private double topRightX;
        private double topRightY;
        private double bottomLeftX;
        private double bottomLeftY;
        
        private readonly PdfRegionService _regionService;
        public DrawingService()
        {
            _regionService = new PdfRegionService();
        }
        public List<PdfTextModel> ExtractText(string pdfPath)
        {
            string wordTag = "NA";
            int itemNumber = 0;
            
            List<PdfTextModel> extractedTextData = new List<PdfTextModel>();

            // Open the PDF
            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                int pageIndex = 1;
                
                // Iterate over each page in the PDF
                foreach (var page in document.GetPages())
                {
                    double pageWidth = page.Width;
                    double pageHeight = page.Height;
                    int pageRotation = page.Rotation.Value;
                    double x1_min = 0;
                    double y1_max = 0;

                    // Get the search region for this page
                    PdfRectangle searchRegion = _regionService.GetDwgTitleRegion(pageWidth, pageHeight, pageRotation, x1_min, y1_max, pageIndex);

                    var words = page.GetWords(DefaultWordExtractor.Instance);

                    foreach (var word in words)
                    {
                        var letters = word.Letters;
                        if (letters.Count == 0) continue;

                        var firstLetter = letters.First();
                        var bottomLeft = firstLetter.GlyphRectangle.BottomLeft;
                        var topLeft = firstLetter.GlyphRectangle.TopLeft;
                        bottomLeftX = bottomLeft.X;
                        bottomLeftY = bottomLeft.Y;
                        
                        var lastLetter = letters.Last();
                        var topRight = lastLetter.GlyphRectangle.TopRight;
                        var bottomRight = lastLetter.GlyphRectangle.BottomRight;
                        topRightX = topRight.X;
                        topRightY = topRight.Y;
                        
                        // Check if the word falls within the search region
                        if (!IsWithinRegion(bottomLeft.X, bottomLeft.Y, topRight.X, topRight.Y, searchRegion))
                            continue;

                        int wordRotation = 0;
                        double xDiff = bottomLeft.X - topRight.X;
                        double yDiff = bottomLeft.Y - topRight.Y;
                        double xDiffAbs = Math.Abs(xDiff);
                        double yDiffAbs = Math.Abs(yDiff);
                        
                        
                        extractedTextData.Add(new PdfTextModel(
                            word.Text,
                            bottomLeftX,
                            bottomLeftY,
                            topRightX,
                            topRightY,
                            pageIndex,
                            pageRotation,
                            wordRotation,
                            wordTag,
                            itemNumber
                        ));
                    }
                    pageIndex++;
                }
            }
            return extractedTextData;
        }
        
        private bool IsWithinRegion(double wordX1, double wordY1, double wordX2, double wordY2, PdfRectangle region)
        {
            return wordX1 >= region.Left && wordX2 <= region.Right &&
                   wordY1 >= region.Bottom && wordY2 <= region.Top;
        }

    }
}
