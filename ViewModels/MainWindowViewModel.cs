using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using PdfProcessor.Helpers;
using PdfProcessor.Models;
using PdfProcessor.Services;


namespace PdfProcessor.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private string _pdfFilePath;
        private string _outputFolderPath;
        private readonly PdfTextService _pdfTextService;

        public string PdfFilePath
        {
            get => _pdfFilePath;
            set { _pdfFilePath = value; OnPropertyChanged(nameof(PdfFilePath)); }
        }

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set { _outputFolderPath = value; OnPropertyChanged(nameof(OutputFolderPath)); }
        }

        public ICommand BrowsePdfCommand { get; }
        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand ExtractAndSaveCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            var pdfRegionService = new PdfRegionService();
            _pdfTextService = new PdfTextService(pdfRegionService);
            BrowsePdfCommand = new RelayCommand(BrowsePdf);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            ExtractAndSaveCommand = new RelayCommand(ExtractAndSave);
        }

        private void BrowsePdf()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "PDF Files|*.pdf" };
            if (openFileDialog.ShowDialog() == true)
                PdfFilePath = openFileDialog.FileName;
        }

        private void BrowseOutputFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                OutputFolderPath = dialog.FileName;
            }
        }

        private void ExtractAndSave()
        {
            if (string.IsNullOrEmpty(PdfFilePath) || string.IsNullOrEmpty(OutputFolderPath))
            {
                System.Windows.MessageBox.Show("Please select a PDF file and an output folder.");
                return;
            }
            
            // Highlight a section 
            // var pdfHighlightService = new PdfHighlightService();
            // pdfHighlightService.HighlightPdfRegions(PdfFilePath, OutputFolderPath);

            string outputFile = Path.Combine(OutputFolderPath, "ExtractedText.csv");
            
            // Step 1: Extract structured text data
            List<PdfTextModel> extractedData = _pdfTextService.ExtractTextAndCoordinates(PdfFilePath);
            
            // Step 2: Save extracted data to CSV
            _pdfTextService.SaveToCsv(extractedData, outputFile);

            System.Windows.MessageBox.Show($"Extraction completed!\nSaved at: {outputFile}");
        }


        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
