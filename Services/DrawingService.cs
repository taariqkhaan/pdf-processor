using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace PdfProcessor.Services
{
    public class DrawingService
    {
        public void ExtractText(string pdfPath, string outputFolder)
        {
            // Ensure the output folder exists
            Directory.CreateDirectory(outputFolder);
            
            // This CSV file will hold word coordinates
            string csvPath = Path.Combine(outputFolder, "word_coordinates.csv");

            // Open the PDF
            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                // Prepare a list to hold CSV lines
                List<string> csvLines = new List<string>();
                // Add CSV header
                csvLines.Add("SheetNumber,Word,BottomLeftX,BottomLeftY,TopRightX,TopRightY");

                int pageIndex = 1;
                // Iterate over each page in the PDF
                foreach (var page in document.GetPages())
                {
                    // Extract words from the page
                    var words = page.GetWords(NearestNeighbourWordExtractor.Instance);

                    // For each word, get the bounding coordinates
                    foreach (var word in words)
                    {
                        var letters = word.Letters;
                        if (letters.Count == 0) continue;

                        // Coordinates of the first character
                        var firstLetter = letters.First();
                        var bottomLeft = firstLetter.GlyphRectangle.BottomLeft;

                        // Coordinates of the last character
                        var lastLetter = letters.Last();
                        var topRight = lastLetter.GlyphRectangle.TopRight;
                        
                        //Compute rotation:
                        // If |x1 - x2| < |y1 - y2|, rotation = 90, else = 0
                        double wordRotation = 0;
                        if (word.Text.Length > 1)
                        {
                            double xDiff = Math.Abs(bottomLeft.X - topRight.X);
                            double yDiff = Math.Abs(bottomLeft.Y - topRight.Y);
                            wordRotation = xDiff < yDiff ? 90 : 0;
                        }

                        // Construct a CSV line:
                        // SheetNumber, Word Text, BottomLeftX, BottomLeftY, TopRightX, TopRightY
                        // Create CSV line
                        string line = string.Format(
                            "{0},\"{1}\",{2},{3},{4},{5},{6}",
                            pageIndex,
                            EscapeCsv(word.Text),
                            bottomLeft.X,
                            bottomLeft.Y,
                            topRight.X,
                            topRight.Y,
                            wordRotation
                        );

                        csvLines.Add(line);
                    }

                    pageIndex++;
                }

                // Write all lines to the CSV file
                File.WriteAllLines(csvPath, csvLines);
            }
        }

        /// <summary>
        /// Simple method to escape quotes for CSV output.
        /// </summary>
        private static string EscapeCsv(string text)
        {
            // Escape quotes by doubling them
            return text.Replace("\"", "\"\"");
        }
    }
}
