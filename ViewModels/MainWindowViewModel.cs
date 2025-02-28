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
        
        private bool _qualityCheck;
        private bool _cableSummary;
        private bool _cableDetails;
        private bool _test1;
        private bool _isRotateVerticalDrawings;
        private bool _isRevertVerticalDrawings;
        private bool _isNoRotationDrawings;
        
        private readonly PdfTextService _pdfTextService;
        private string documentType;
        private List<int> verticalPages;
        private Stopwatch stopwatch;
        private string _statusMessage;
        private bool qualityChecked = false;
        private bool definedException = false;
        
        
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
        
        public bool QualityCheck
        {
            get => _qualityCheck;
            set
            {
                _qualityCheck = value;
                OnPropertyChanged(nameof(QualityCheck));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool CableSummary
        {
            get => _cableSummary;
            set
            {
                _cableSummary = value;
                OnPropertyChanged(nameof(CableSummary));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool CableDetails
        {
            get => _cableDetails;
            set
            {
                _cableDetails = value;
                OnPropertyChanged(nameof(CableDetails));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool Test1
        {
            get => _test1;
            set
            {
                _test1 = value;
                OnPropertyChanged(nameof(Test1));
                ((RelayCommand)ProcessCommand).RaiseCanExecuteChanged();
            }
        }
        public bool IsRotateVerticalDrawings
        {
            get => _isRotateVerticalDrawings;
            set
            {
                _isRotateVerticalDrawings = value;
                if (value)
                {
                    IsRevertVerticalDrawings = false;
                    IsNoRotationDrawings = false;
                }
                OnPropertyChanged(nameof(IsRotateVerticalDrawings));
            }
        }
        public bool IsRevertVerticalDrawings
        {
            get => _isRevertVerticalDrawings;
            set
            {
                _isRevertVerticalDrawings = value;
                if (value)
                {
                    IsRotateVerticalDrawings = false;
                    IsNoRotationDrawings = false;
                }
                OnPropertyChanged(nameof(IsRevertVerticalDrawings));
            }
        }
        public bool IsNoRotationDrawings
        {
            get => _isNoRotationDrawings;
            set
            {
                _isNoRotationDrawings = value;
                if (value)
                {
                    IsRotateVerticalDrawings = false;
                    IsRevertVerticalDrawings = false;
                }
                OnPropertyChanged(nameof(IsNoRotationDrawings));
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
                () => IsEnabled && (QualityCheck || CableSummary || 
                                    CableDetails || Test1 || IsRotateVerticalDrawings
                                    || IsRevertVerticalDrawings || IsNoRotationDrawings));
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
        
        private void FileIsAccessible(string filePath)
        {
            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
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
            
            if (QualityCheck)
            {
                StatusMessage = "Processing cable schedule...";
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
                
                StatusMessage = "Processing drawings...";
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
                // exportService.SaveToCsv(extractedTitleData, Path.Combine(Path.GetDirectoryName(DrawingsPath), 
                //     Path.GetFileNameWithoutExtension(DrawingsPath) + ".csv"));
                await exportService.SaveToDatabase(extractedTitleData, 
                    Path.Combine(Path.GetDirectoryName(DrawingsPath), "data.db"), documentType);
                
                // Add tags to relevant texts
                await Task.Run(() =>
                {
                    DwgTitleService dwgTitleService = new DwgTitleService();
                    dwgTitleService.ProcessDatabase(Path.Combine(Path.GetDirectoryName(DrawingsPath), "data.db"));
                });
                
                //-------------------------------highlight region in a pdf----------------------------------------------
                // RegionHighlightService regionHighlightService = new RegionHighlightService();
                // regionHighlightService.HighlightRegion(DrawingsPath, "TITLE");
                
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
                
                 
                 string outputBowPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BowPath), 
                     $"highlighted_BOW.pdf");
                 string outputDwgPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(DrawingsPath), 
                     $"highlighted_DWG.pdf");


                 if (!File.Exists(outputBowPath))
                 {
                     Console.WriteLine($"BOW doesn't exist");
                     if (!File.Exists(outputDwgPath))
                     { Console.WriteLine($"DWG doesn't exist");
                         //----------------------------Highlight PDFs---------------------------------------------------
                         AnnotationService annotationService = new AnnotationService();
                         annotationService.AnnotatePdf(BowPath, "BOW");
                         annotationService.AnnotatePdf(DrawingsPath, "DWG");
                     }
                     else
                     {Console.WriteLine($"DWG exists");
                         try
                         {
                             FileIsAccessible(outputDwgPath);
                             Console.WriteLine($"DWG is accessible");
                             //----------------------------Highlight PDFs---------------------------------------------------
                             AnnotationService annotationService = new AnnotationService();
                             annotationService.AnnotatePdf(BowPath, "BOW");
                             annotationService.AnnotatePdf(DrawingsPath, "DWG");
                         }
                         catch (IOException)
                         {
                             StatusMessage = "Highlighted PDFs are open! Close PDFs before continuing.";
                             IsEnabled = true;
                             return;
                         }
                     }
                 }
                 else
                 {Console.WriteLine($"BOW exists");
                     try
                     {
                         FileIsAccessible(outputBowPath);
                         Console.WriteLine($"BOW is accessible");
                         
                         if (!File.Exists(outputDwgPath))
                         {Console.WriteLine($"DWG doesn't exist");
                             //----------------------------Highlight PDFs---------------------------------------------------
                             AnnotationService annotationService = new AnnotationService();
                             annotationService.AnnotatePdf(BowPath, "BOW");
                             annotationService.AnnotatePdf(DrawingsPath, "DWG");
                         }
                         else
                         {Console.WriteLine($"DWG exists");
                             try
                             {
                                 FileIsAccessible(outputDwgPath);
                                 Console.WriteLine($"DWG is accessible");
                                 //----------------------------Highlight PDFs---------------------------------------------------
                                 AnnotationService annotationService = new AnnotationService();
                                 annotationService.AnnotatePdf(BowPath, "BOW");
                                 annotationService.AnnotatePdf(DrawingsPath, "DWG");
                             }
                             catch (IOException)
                             {
                                 StatusMessage = "Highlighted PDFs are open! Close PDFs before continuing.";
                                 IsEnabled = true;
                                 return;
                             }
                         }
                     }
                     catch (IOException)
                     {
                         StatusMessage = "Highlighted PDFs are open! Close PDFs before continuing.";
                         IsEnabled = true;
                         return;
                     }
                 }
                 
                 StatusMessage = "Creating hyperlinks...";
                 //----------------------------Create hyperlinks----------------------------------------------------
            
                 HyperlinkService hyperlinkService = new HyperlinkService();
                 hyperlinkService.HyperlinkMain(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"));
            
                 //-----------------------Add keymarks to the cable schedule----------------------------------------
                 CableDetailsService cableDetailsService = new CableDetailsService();
                 cableDetailsService.ProcessDatabase(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"), BowPath);
                     
                qualityChecked = true;
                definedException = false;
            }

            if (qualityChecked)
            {
                if (CableSummary)
                {
                    //-----------------------Generate Cable summary---------------------------------------------
                    CableSummaryService  cableSummaryService = new  CableSummaryService();
                    cableSummaryService.GenerateCableSummaryCsv(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"));
                    definedException = false;
                }
                if (IsRotateVerticalDrawings)
                {
                    StatusMessage = "Rotating vertical drawings...";
                    PdfRotationService pdfRotationService = new PdfRotationService();
                    pdfRotationService.RotatePdfPages(DrawingsPath, verticalPages);
                    definedException = false;
                }
                if (IsRevertVerticalDrawings)
                {
                    StatusMessage = "Reverting vertical drawings rotation...";
                    PdfRotationService pdfRotationService = new PdfRotationService();
                    pdfRotationService.RevertRotations(DrawingsPath);
                    definedException = false;
                }
                if (IsNoRotationDrawings)
                {
                    StatusMessage = "Skipping vertical drawings rotation...";
                    Console.WriteLine($"Vertical pages rotation skipped");
                    definedException = false;
                }
            }
            else
            {
                StatusMessage = "Run quality check !!";
                definedException = true;
            }
            if (Test1)
            {
                
            }

            if (!definedException)
            {
                StatusMessage = "Processing success!";
            }
            //-----------------------Delete the database------------------------------------------------------------
            File.Delete(Path.Combine(Path.GetDirectoryName(BowPath), "data.db"));
            Console.WriteLine("Database deleted successfully.");
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
