namespace PdfProcessor.Models;

public class PdfTextModel
{
    public string PageWord { get; set; }
    public double BottomLeftX { get; set; }
    public double BottomLeftY { get; set; }
    public double TopRightX { get; set; }
    public double TopRightY { get; set; }
    public int PageNumber { get; set; }
    public int PageRotation { get; set; }
    public int WordRotation { get; set; }
    public string WordTag { get; set; }
    public int ItemNumber { get; set; }

    public PdfTextModel(string pageWord, 
        double bottomLeftX, 
        double bottomLeftY, 
        double topRightX, 
        double topRightY, 
        int pageNumber, 
        int pageRotation, 
        int wordRotation,
        string wordTag, 
        int itemNumber)
    {
        PageWord = pageWord;
        BottomLeftX = bottomLeftX;
        BottomLeftY = bottomLeftY;
        TopRightX = topRightX;
        TopRightY = topRightY;
        PageNumber = pageNumber;
        PageRotation = pageRotation;
        WordRotation = wordRotation;
        WordTag = wordTag;
        ItemNumber = itemNumber;
    }
}