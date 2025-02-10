using PdfTextExtractor.ViewModels;

namespace PdfTextExtractor.Views;

    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }

