using System.Globalization;
using System.IO;
using System.Text;
using PdfProcessor.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

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
                    
                    var regionRect = _pdfRegionService.GetBowRegion(page.Width, page.Height, page.Rotation.Value);
                    var region = (regionRect.X, regionRect.Y, regionRect.Width, regionRect.Height);
                    Console.WriteLine($"{region}");
                    extractedText.AddRange(ExtractTextFromPage(page, region, page.Rotation.Value));
                }
            }

            return extractedText;
        }

        private List<PdfTextModel> ExtractTextFromPage(Page page, (double x, double y, double width, double height) region, int rotation)
        {
            List<PdfTextModel> textModels = new List<PdfTextModel>();
            List<Word> words = page.GetWords().ToList();
            
            double regionX1;
            double regionY1;
            double regionX2;
            double regionY2;
            
            if (rotation == 0)
            {
                regionX1 = region.x;//same
                regionY1 = page.Height - (region.y + region.height);
                regionX2 = region.x + region.width;//same
                regionY2 = page.Height - region.y;
            }
            else
            {
                regionX1 = region.width + region.y; //done
                regionY1 = region.height + region.x;
                regionX2 = region.y; //done
                regionY2 = region.x; //done
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
