using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Text.Json;
using PdfProcessor.Models;


namespace PdfProcessor.Services;

public class PdfToTextPython
{
    public List<PdfTextModel> ExtractTextFromPdf(string pdfFilePath)
    {
        string pythonExe = @"E:\Python\DetectLines\.venv\Scripts\python.exe";
        string scriptPath = @"E:\Python\DetectLines\TextExtractor.py";

        if (!File.Exists(pythonExe) || !File.Exists(scriptPath))
        {
            MessageBox.Show("Python executable or script not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{scriptPath}\" \"{pdfFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using (Process process = new Process { StartInfo = psi })
            {
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(errors))
                {
                    MessageBox.Show("Error occurred:\n" + errors, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<PdfTextModel>>(output) ?? new List<PdfTextModel>();
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing JSON: {ex.Message}");
                        return new List<PdfTextModel>();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Exception: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        return new List<PdfTextModel>();
    }
}