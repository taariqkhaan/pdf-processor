using PdfSharp.Drawing;
using UglyToad.PdfPig.Core;

namespace PdfProcessor.Services
{
    public class PdfRegionService
    {
        /// Gets the full page size as a rectangle.
        public PdfRectangle GetFullPageRegion(double pageWidth, double pageHeight)
        {
            return new PdfRectangle(0, 0, pageWidth, pageHeight);
        }
        
        /// Gets the header region (top 10% of the page).
        public PdfRectangle GetHeaderRegion(double pageWidth, double pageHeight)
        {
            double height = pageHeight * 0.1;
            return new PdfRectangle(0, pageHeight - height, pageWidth, height);
        }
        
        /// Gets the footer region (bottom 10% of the page).
        public PdfRectangle GetFooterRegion(double pageWidth, double pageHeight)
        {
            double height = pageHeight * 0.1;
            return new PdfRectangle(0, 0, pageWidth, height);
        }
        
        /// Gets search region for Southern Company Bill of Wire
        public PdfRectangle GetBowRegion(double pageWidth, double pageHeight, int pageRotation)
        {
                return new PdfRectangle(23, 50, 760, 500);
 
        }
    }
}
