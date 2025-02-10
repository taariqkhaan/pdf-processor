using PdfSharpCore.Drawing;

namespace PdfProcessor.Services
{
    public class PdfRegionService
    {

        /// Gets the full page size as a rectangle.
        public XRect GetFullPageRegion(double pageWidth, double pageHeight)
        {
            return new XRect(0, 0, pageWidth, pageHeight);
        }


        /// Gets the header region (top 10% of the page).
        public XRect GetHeaderRegion(double pageWidth, double pageHeight)
        {
            double height = pageHeight * 0.1;
            return new XRect(0, 0, pageWidth, height);
        }


        /// Gets the footer region (bottom 10% of the page).
        public XRect GetFooterRegion(double pageWidth, double pageHeight)
        {
            double height = pageHeight * 0.1;
            return new XRect(0, pageHeight - height, pageWidth, height);
        }


        /// Gets the left margin (10% of width).
        public XRect GetLeftMarginRegion(double pageWidth, double pageHeight)
        {
            double width = pageWidth * 0.1;
            return new XRect(0, 0, width, pageHeight);
        }


        /// Gets the right margin (10% of width).
        public XRect GetRightMarginRegion(double pageWidth, double pageHeight)
        {
            double width = pageWidth * 0.1;
            return new XRect(pageWidth - width, 0, width, pageHeight);
        }


        /// Gets the main content area (excluding margins, header, and footer).
        public XRect GetContentRegion(double pageWidth, double pageHeight)
        {
            double marginWidth = pageWidth * 0.1;
            double headerHeight = pageHeight * 0.1;
            double footerHeight = pageHeight * 0.1;

            return new XRect(marginWidth, footerHeight, pageWidth - (2 * marginWidth), pageHeight - (headerHeight + footerHeight));
        }


        /// Gets search region for Southern Company Bill of Wire
        public XRect GetBowRegion(double pageWidth, double pageHeight, int pageRotation)
        {
            if (pageRotation == 0)
            {
                //Console.WriteLine($"{pageHeight}, {pageWidth}, {pageRotation}");
                return new XRect(25,115,745,425);
                
            }
            else if (pageRotation == 90)
            {
                //Console.WriteLine($"{pageHeight}, {pageWidth}, {pageRotation}");
                //return new XRect(115,-150,425,745);
                return new XRect(115,(pageHeight+22) - pageWidth,425,745);
            }
            else
            {
                //Console.WriteLine($"{pageHeight}, {pageWidth}, {pageRotation}");
                return new XRect(0, 0, pageWidth, pageHeight);
            }
            
        }
    }
}
