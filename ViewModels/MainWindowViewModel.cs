using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Forms;
using PdfProcessor.Models;
using PdfProcessor.Services;



namespace PdfProcessor.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        
        private string _allFilePath;
        private string _outputFolderPath;
        private readonly PdfTextService _pdfTextService;
        private string searchRegionType;
        private Stopwatch stopwatch;
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

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
        public ICommand ProcessCommand { get; }

        public MainWindowViewModel()
        {
            BrowseFileCommand = new RelayCommand(BrowseFile);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            ProcessCommand = new RelayCommand(async () => await Process(), () => IsEnabled);
        }

        private void BrowseFile()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "All Files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    AllFilePath = openFileDialog.FileName;
                }
            }
        }
        
        private void BrowseOutputFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OutputFolderPath = dialog.SelectedPath;
                }
            }
        }
        
        private async Task Process()
        {
            IsEnabled = false;
            
            if (string.IsNullOrEmpty(AllFilePath) || string.IsNullOrEmpty(OutputFolderPath))
            {
                System.Windows.MessageBox.Show("Please select a file and an output folder.");
                IsEnabled = true;
                return;
            }

            await Task.Run(() =>
            {

                //Extract text
                PdfTextService _pdfTextService = new PdfTextService();
                searchRegionType = "FULL";
                stopwatch = Stopwatch.StartNew();
                List<PdfTextModel> extractedData = _pdfTextService.ExtractTextAndCoordinates(AllFilePath, searchRegionType);
                stopwatch.Stop();
                Console.WriteLine($"PdfRegionService Time: {stopwatch.ElapsedMilliseconds} ms");
                
                
                //Extract text
                // DrawingService drawingService = new DrawingService();
                // stopwatch = Stopwatch.StartNew();
                // List<PdfTextModel> extractedData = drawingService.ExtractText(AllFilePath);
                // stopwatch.Stop();
                // Console.WriteLine($"DrawingService Time: {stopwatch.ElapsedMilliseconds} ms");
                
                // // Extract text and save .csv file using python
                // PdfToTextPython pdfToTextPython = new PdfToTextPython();
                // Stopwatch stopwatch = Stopwatch.StartNew();
                // List<PdfTextModel> extractedData = pdfToTextPython.ExtractTextFromPdf(AllFilePath);
                // stopwatch.Stop();
                // Console.WriteLine($"SaveToCsv using Python Time: {stopwatch.ElapsedMilliseconds} ms");
                
                //Save text in .csv and .db format
                ExportService exportService = new ExportService();
                stopwatch = Stopwatch.StartNew();
                exportService.SaveToCsv(extractedData, Path.Combine(OutputFolderPath, "data.csv"));
                stopwatch.Stop();
                Console.WriteLine($"SaveToCsv Time: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch = Stopwatch.StartNew();
                exportService.SaveToDatabase(extractedData, Path.Combine(OutputFolderPath, "data.db"));
                stopwatch.Stop();
                Console.WriteLine($"SaveToDatabase Time: {stopwatch.ElapsedMilliseconds} ms");
                
                //Analyze the database for cable schedule 
                // CableScheduleService cableScheduleService = new CableScheduleService();
                // stopwatch = Stopwatch.StartNew();
                // cableScheduleService.ProcessDatabase(Path.Combine(OutputFolderPath, "data.db"));
                // stopwatch.Stop();
                // Console.WriteLine($"CableScheduleService Time: {stopwatch.ElapsedMilliseconds} ms");

                // Highlight cable schedule 
                // AnnotatePdfService annotatePdfService = new AnnotatePdfService();
                // stopwatch = Stopwatch.StartNew();
                // annotatePdfService.AnnotatePdf(AllFilePath, OutputFolderPath);
                // stopwatch.Stop();
                // Console.WriteLine($"AnnotatePdf Time: {stopwatch.ElapsedMilliseconds} ms");
                
                // Highlight Drawing 
                AnnotationService annotationService = new AnnotationService();
                stopwatch = Stopwatch.StartNew();
                annotationService.AnnotatePdf(AllFilePath, OutputFolderPath);
                stopwatch.Stop();
                Console.WriteLine($"AnnotatePdf Time: {stopwatch.ElapsedMilliseconds} ms");
                
                

            });
            System.Windows.MessageBox.Show($"Processing success!");
            IsEnabled = true;
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged;
    }
}
