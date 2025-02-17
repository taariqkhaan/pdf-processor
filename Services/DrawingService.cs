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

                    // Get the search region for this page
                    PdfRectangle searchRegion = _regionService.GetDwgRegion(pageWidth, pageHeight, pageRotation);

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


                        // Determine text rotation
                        if (word.Text.Length > 1)
                        {
                            if (xDiffAbs > yDiffAbs)
                            {
                                if (xDiff < 0)
                                {
                                    wordRotation = 0;
                                }
                                else if (xDiff > 0)
                                {
                                    wordRotation = 180;
                                }

                            }
                            else
                            {
                                if (yDiff < 0)
                                {
                                    wordRotation = 270;
                                }
                                else if (yDiff > 0)
                                {
                                    wordRotation = 90;
                                }
                            }

                        }
                        else
                        {
                            if (xDiffAbs > yDiffAbs)
                            {
                                if (yDiff < 0)
                                {
                                    wordRotation = 270;
                                    if ("w".Contains(lastLetter.Value) || "w".Contains(firstLetter.Value))
                                    {
                                        wordRotation = 0;
                                    }
                                }
                                else if (yDiff > 0)
                                {
                                    wordRotation = 90;
                                    if ("w".Contains(lastLetter.Value) || "w".Contains(firstLetter.Value))
                                    {
                                        wordRotation = 180;
                                    }
                                }

                            }
                            else
                            {
                                if (yDiff < 0)
                                {
                                    wordRotation = 0;
                                    if ("w".Contains(lastLetter.Value) || "w".Contains(firstLetter.Value))
                                    {
                                        wordRotation = 270;
                                    }
                                }
                                else if (yDiff > 0)
                                {
                                    wordRotation = 180;
                                    if ("w".Contains(lastLetter.Value) || "w".Contains(firstLetter.Value))
                                    {
                                        wordRotation = 90;
                                    }
                                }
                            }
                        }

                        // Adjust coordinates based on rotation
                        switch (wordRotation)
                        {
                            case 0:
                                if (".,_".Contains(lastLetter.Value))
                                    topRightY += 9;
        
                                if ("-+=".Contains(firstLetter.Value))
                                    bottomLeftY -= 2.5;
        
                                if ("-+=".Contains(lastLetter.Value))
                                    topRightY += 4;
        
                                if ("'\"".Contains(firstLetter.Value))
                                    bottomLeftY -= 5;
                                break;

                            case 90:
                                topRightY -= 4;
                                bottomLeftY += 4;

                                if (".,_".Contains(lastLetter.Value))
                                    topRightX += 9;
        
                                if ("-+=".Contains(firstLetter.Value))
                                    bottomLeftX -= 2.5;
        
                                if ("-+=".Contains(lastLetter.Value))
                                    topRightX += 4;
        
                                if ("'\"".Contains(firstLetter.Value))
                                    bottomLeftX -= 5;
                                break;

                            case 180:
                                (bottomLeftX, topRightX) = (topRightX, bottomLeftX);
                                (bottomLeftY, topRightY) = (topRightY, bottomLeftY);

                                if (".,_".Contains(lastLetter.Value))
                                    bottomLeftY -= 9;
        
                                if ("-+=".Contains(firstLetter.Value))
                                    topRightY += 4;
        
                                if ("-+=".Contains(lastLetter.Value))
                                    bottomLeftY -= 5;
        
                                if ("'\"".Contains(firstLetter.Value))
                                    topRightY += 4;
                                break;

                            case 270:
                                bottomLeftX += 5;
                                topRightX -= 5;

                                if (".,_".Contains(firstLetter.Value))
                                    bottomLeftX += 2;
        
                                if (".,_".Contains(lastLetter.Value))
                                    topRightX -= 12;
        
                                if ("-+=".Contains(lastLetter.Value))
                                    topRightX -= 7;
        
                                if ("'\"".Contains(firstLetter.Value))
                                    bottomLeftX += 10;
                                break;
                        }
                        

                        extractedTextData.Add(new PdfTextModel(
                            word.Text,
                            bottomLeftX,
                            bottomLeftY,
                            topRightX,
                            topRightY,
                            wordRotation,
                            pageIndex
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
