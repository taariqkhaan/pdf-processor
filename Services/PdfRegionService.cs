using UglyToad.PdfPig.Core;

namespace PdfProcessor.Services
{
    public class PdfRegionService
    {
        // Gets the full page size as a rectangle.
        public PdfRectangle GetFullPageRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(0, 0, pageWidth, pageHeight);
        }
        
        // Gets search region for Southern Company Bill of Wire
        public PdfRectangle GetBowRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(x1_min, y1_min + 10, pageWidth - x1_min, y2_max - 85);
        }
        
        // Gets search region for Southern Company Drawing Title
        public PdfRectangle GetDwgTitleRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(pageWidth - 495, 0, pageWidth, 114);
                
        }
        // Gets search region for Southern Company Drawing
        public PdfRectangle GetDwgRegion(double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            return new PdfRectangle(0, 95, pageWidth, pageHeight);
        }
        
        // Method to get a function reference based on user input
        public PdfRectangle GetRegionByName(string regionName, double pageWidth, double pageHeight, int pageRotation, 
            double x1_min, double y1_min, double x2_max, double y2_max)
        {
            var regionMap = new Dictionary<string, Func<double, double, int, double,double,double, double, 
                PdfRectangle>>(StringComparer.OrdinalIgnoreCase)
            {
                { "FULL", GetFullPageRegion },
                { "BOW", GetBowRegion },
                { "TITLE", GetDwgTitleRegion },
                { "DWG", GetDwgRegion }
            };

            if (regionMap.TryGetValue(regionName, out var regionFunc))
            {
                return regionFunc(pageWidth, pageHeight, pageRotation, x1_min, y1_min, x2_max, y2_max);
            }

            throw new ArgumentException($"Invalid region name: {regionName}");
        }
    }
}
