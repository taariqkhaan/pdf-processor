using System.Globalization;
using System.IO;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using PdfTextExtractor.Models;

namespace PdfTextExtractor.Services
{
    public class PdfTextService
    {
        public List<PdfTextModel> ExtractTextAndCoordinates(string pdfPath)
        {
            List<PdfTextModel> extractedText = new List<PdfTextModel>();

            using (PdfDocument document = PdfDocument.Open(pdfPath))
            {
                foreach (Page page in document.GetPages())
                {
                    extractedText.AddRange(ExtractTextFromPage(page));
                }
            }

            return extractedText;
        }

        private List<PdfTextModel> ExtractTextFromPage(Page page)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>();
            List<Word> words = page.GetWords().ToList();

            foreach (Word word in words)
            {
                if (!string.IsNullOrWhiteSpace(word.Text))
                {
                    var firstChar = word.Letters.First();
                    var lastChar = word.Letters.Last();

                    textModels.Add(new PdfTextModel(
                        word.Text,
                        firstChar.GlyphRectangle.BottomLeft.X,
                        firstChar.GlyphRectangle.BottomLeft.Y,
                        lastChar.GlyphRectangle.TopRight.X,
                        lastChar.GlyphRectangle.TopRight.Y
                    ));
                }
            }

            return textModels;
        }

        public void SaveToCsv(List<PdfTextModel> extractedText, string outputCsvPath)
        {
            StringBuilder csvContent = new StringBuilder();
            csvContent.AppendLine("Text,BottomLeftX,BottomLeftY,TopRightX,TopRightY");

            foreach (var item in extractedText)
            {
                csvContent.AppendLine($"\"{item.Text}\",{item.BottomLeftX.ToString(CultureInfo.InvariantCulture)},{item.BottomLeftY.ToString(CultureInfo.InvariantCulture)},{item.TopRightX.ToString(CultureInfo.InvariantCulture)},{item.TopRightY.ToString(CultureInfo.InvariantCulture)}");
            }

            File.WriteAllText(outputCsvPath, csvContent.ToString());
        }
    }
}
