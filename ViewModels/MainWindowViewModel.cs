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
        private string _allFilePath;
        private string _outputFolderPath;
        private readonly PdfTextService _pdfTextService;

        public string AllFilePath
        {
            get => _allFilePath;
            set { _allFilePath = value; OnPropertyChanged(nameof(AllFilePath)); }
        }

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set { _outputFolderPath = value; OnPropertyChanged(nameof(OutputFolderPath)); }
        }

        public ICommand BrowseFileCommand { get; }
        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand ExtractAndSaveCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            var pdfRegionService = new PdfRegionService();
            _pdfTextService = new PdfTextService(pdfRegionService);
            
            BrowseFileCommand = new RelayCommand(BrowseFile);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            ExtractAndSaveCommand = new RelayCommand(Process);
        }

        private void BrowseFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "All Files (*.*)|*.*" };
            if (openFileDialog.ShowDialog() == true)
                AllFilePath = openFileDialog.FileName;
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

        private void Process()
        {
            if (string.IsNullOrEmpty(AllFilePath) || string.IsNullOrEmpty(OutputFolderPath))
            {
                System.Windows.MessageBox.Show("Please select a file and an output folder.");
                return;
            }
            
            // Highlight a section 
            // var pdfHighlightService = new PdfHighlightService();
            // pdfHighlightService.HighlightPdfRegions(AllFilePath, OutputFolderPath);
            
            // Save text as .csv and.db
            // List<PdfTextModel> extractedData = _pdfTextService.ExtractTextAndCoordinates(AllFilePath);
            //
            // ExportService exportService = new ExportService();
            //
            // exportService.SaveToCsv(extractedData, Path.Combine(OutputFolderPath, "data.csv"));
            // exportService.SaveToDatabase(extractedData, Path.Combine(OutputFolderPath, "data.db"));
            
            // Analyze the database for cable schedule 
            // CableScheduleService cableScheduleService = new CableScheduleService();
            // cableScheduleService.ProcessDatabase(AllFilePath);
            
            // Analyze the database for cable schedule 
            CoordinateDotService coordinateDotService = new CoordinateDotService();
            coordinateDotService.AnnotatePdfWithDots(AllFilePath, OutputFolderPath);
            
            
            
            System.Windows.MessageBox.Show($"Extraction completed!\nSaved at: {OutputFolderPath}");
        }
        
        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
