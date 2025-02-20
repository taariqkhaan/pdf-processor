namespace PdfProcessor.Models;

public static class RangeExtensions
{
    public static bool IsBetween( this double value, double min, double max)
    {
        return value > min && value < max;
    }
}