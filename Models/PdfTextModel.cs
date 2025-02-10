namespace PdfTextExtractor.Models;

public class PdfTextModel
{
    public string Text { get; set; }
    public double BottomLeftX { get; set; }
    public double BottomLeftY { get; set; }
    public double TopRightX { get; set; }
    public double TopRightY { get; set; }

    public PdfTextModel(string text, double bottomLeftX, double bottomLeftY, double topRightX, double topRightY)
    {
        Text = text;
        BottomLeftX = bottomLeftX;
        BottomLeftY = bottomLeftY;
        TopRightX = topRightX;
        TopRightY = topRightY;
    }
}