using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Data;
using PdfProcessor.Models;
using PdfProcessor.Services;




namespace PdfProcessor.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        
        private string _bowPath;
        private string _drawingsPath;
        
        private bool _processBow;
        private bool _analyzeDatabase;
        private bool _processDrawing;
        private bool _createHyperlinks;
        private bool _isRotateVerticalDrawings;
        private bool _isRevertVerticalDrawings;
        
        private readonly PdfTextService _pdfTextService;
        private string documentType;
        private List<int> verticalPages;
        private Stopwatch stopwatch;
        private string _statusMessage;
        
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public string BowPath
        {
            get => _bowPath;
            set { _bowPath = value; OnPropertyChanged(nameof(BowPath)); }
        }

        public string DrawingsPath
        {
            get => _drawingsPath;
            set { _drawingsPath = value; OnPropertyChanged(nameof(DrawingsPath)); }
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
        
        public bool ProcessBow
        {
            get => _processBow;
            set
            {
                _processBow = value;
                OnPropertyChanged(nameof(ProcessBow));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }

        public bool AnalyzeDatabase
        {
            get => _analyzeDatabase;
            set
            {
                _analyzeDatabase = value;
                OnPropertyChanged(nameof(AnalyzeDatabase));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }

        public bool ProcessDrawing
        {
            get => _processDrawing;
            set
            {
                _processDrawing = value;
                OnPropertyChanged(nameof(ProcessDrawing));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool CreateHyperlinks
        {
            get => _createHyperlinks;
            set
            {
                _createHyperlinks = value;
                OnPropertyChanged(nameof(CreateHyperlinks));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool IsRotateVerticalDrawings
        {
            get => _isRotateVerticalDrawings;
            set
            {
                _isRotateVerticalDrawings = value;
                if (value) IsRevertVerticalDrawings = false;
                OnPropertyChanged(nameof(IsRotateVerticalDrawings));
            }
        }
        public bool IsRevertVerticalDrawings
        {
            get => _isRevertVerticalDrawings;
            set
            {
                _isRevertVerticalDrawings = value;
                if (value) IsRotateVerticalDrawings = false;
                OnPropertyChanged(nameof(IsRevertVerticalDrawings));
            }
        }

        public ICommand BrowseBowCommand { get; }
        public ICommand BrowseDrawingsCommand { get; }
        public ICommand ProcessCommand { get; }

        public MainWindowViewModel()
        {
            BrowseBowCommand = new RelayCommand(BrowseBow);
            BrowseDrawingsCommand = new RelayCommand(BrowseDrawings);
            ProcessCommand = new RelayCommand(async () => await Process(), 
                () => IsEnabled && (ProcessBow || AnalyzeDatabase || 
                                    ProcessDrawing || CreateHyperlinks || IsRotateVerticalDrawings
                                    || IsRevertVerticalDrawings));
        }

        private void BrowseBow()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a bill of wire PDF binder";
                openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    BowPath = openFileDialog.FileName;
                }
            }
        }
        
        private void BrowseDrawings()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a Drawings PDF binder";
                openFileDialog.Filter = "PDF Files (*.pdf)|*.pdf";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    DrawingsPath = openFileDialog.FileName;
                }
            }
        }
        
        private async Task Process()
        {
            IsEnabled = false;
            StatusMessage = "Processing started...";
            
            if (string.IsNullOrEmpty(BowPath))
            {
                StatusMessage = "Please select Bill of Wire PDF.";
                IsEnabled = true;
                return;
            }
            if (string.IsNullOrEmpty(DrawingsPath))
            {
                StatusMessage = "Please select Drawings PDF.";
                IsEnabled = true;
                return;
            }
            
            if (ProcessBow)
            {
                StatusMessage = "Importing cable schedule to database...";
                //---------------------------------------Extract text from cable schedule-------------------------------
                
                documentType = "BOW";
                var result = await Task.Run(() =>
                {
                    PdfTextService pdfTextService = new PdfTextService();
                    return pdfTextService.ExtractTextAndCoordinates(BowPath, documentType);
                });
                List<PdfTextModel> extractedBowData = result.ExtractedText;

                // Save text to database
                ExportService exportService = new ExportService();
                // exportService.SaveToCsv(extractedBowData, Path.Combine(Path.GetDirectoryName(BowPath), 
                //     Path.GetFileNameWithoutExtension(BowPath) + ".csv"));
                await exportService.SaveToDatabase(extractedBowData, 
                    Path.Combine(Path.GetDirectoryName(BowPath), "data.db"), documentType);

                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    CableScheduleService cableScheduleService = new CableScheduleService();
                    cableScheduleService.ProcessDatabase(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"));
                });
                
                StatusMessage = "Importing drawings to database...";
               //---------------------------------------Extract text from title block-----------------------------------
               
                documentType = "TITLE";
                result = await Task.Run(() =>
                {
                    PdfTextService pdfTextService = new PdfTextService();
                    return pdfTextService.ExtractTextAndCoordinates(DrawingsPath, documentType);
                });
                List<PdfTextModel> extractedTitleData = result.ExtractedText;
                
                
                documentType = "DWG";
                // Save text to database
                exportService = new ExportService();
                await exportService.SaveToDatabase(extractedTitleData, 
                    Path.Combine(Path.GetDirectoryName(DrawingsPath), "data.db"), documentType);
                
                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    DwgTitleService dwgTitleService = new DwgTitleService();
                    dwgTitleService.ProcessDatabase(Path.Combine(Path.GetDirectoryName(DrawingsPath), "data.db"));
                });
                
                //---------------------------------------Extract text from drawing area---------------------------------
                result = await Task.Run(() =>
                {
                    PdfTextService pdfTextService = new PdfTextService();
                    return pdfTextService.ExtractTextAndCoordinates(DrawingsPath, documentType);
                });
                List<PdfTextModel> extractedDwgData = result.ExtractedText;
                verticalPages = result.VerticalPages;
                
                // Save text to database
                exportService = new ExportService();
                // exportService.SaveToCsv(extractedBowData, Path.Combine(Path.GetDirectoryName(BowPath), 
                //     Path.GetFileNameWithoutExtension(BowPath) + ".csv"));
                await exportService.SaveToDatabase(extractedDwgData, 
                    Path.Combine(Path.GetDirectoryName(DrawingsPath), "data.db"), documentType);
                
                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    DrawingService drawingService = new DrawingService();
                    drawingService.ProcessDatabase(Path.Combine(Path.GetDirectoryName(DrawingsPath), "data.db"));
                });
                
                StatusMessage = "Comparing cable schedule to drawings...";
                //----------------------------Compare cable schedule to drawings----------------------------------------
                
                ComparisonLogic comparisonLogic = new ComparisonLogic();
                comparisonLogic.CompareDatabase(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"));

                //Highlight the drawing
                AnnotationService annotationService = new AnnotationService();
                annotationService.AnnotatePdf(DrawingsPath, "DWG");
                annotationService.AnnotatePdf(BowPath, "BOW");
                
                StatusMessage = "Creating hyperlinks...";
                //----------------------------Create hyperlinks---------------------------------------------------------
                
                HyperlinkService hyperlinkService = new HyperlinkService();
                hyperlinkService.HyperlinkMain(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"));
                
            }
            if (IsRotateVerticalDrawings)
            {
                StatusMessage = "Rotating vertical drawings...";
                PdfRotationService pdfRotationService = new PdfRotationService();
                pdfRotationService.RotatePdfPages(DrawingsPath, verticalPages);
                
            }
            if (IsRevertVerticalDrawings)
            {
                StatusMessage = "Reverting vertical drawings rotation...";
                PdfRotationService pdfRotationService = new PdfRotationService();
                pdfRotationService.RevertRotations(DrawingsPath);
            }
                
            StatusMessage = "Processing success!";
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
        
        private readonly Func<bool> _canExecute;
        private readonly Action _execute;
        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
        public void Execute(object parameter) => _execute();
        
        public void RaiseCanExecuteChanged() 
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    

}
