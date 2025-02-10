using PdfProcessor.ViewModels;

namespace PdfProcessor.Views;

    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }

