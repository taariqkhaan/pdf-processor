using System;
using System.IO;
using System.Data.SQLite;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Pdf.Filespec;
using iText.Kernel.Geom;

namespace PdfProcessor.Services;

public class HyperlinkService
{
    public void HyperlinkMain(string bowPath, string dwgPath)
    {
        string dbFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(bowPath), "data.db");
        
        if (!File.Exists(dbFilePath))
        {
            Console.WriteLine("Database file not found.");
            return;
        }

        try
        {
            using (var connection = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
            {
                connection.Open();
                AddHyperlink(connection, bowPath, dwgPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing database: {ex.Message}");
        }
    }

    public void AddHyperlink(SQLiteConnection connection, string bowPath, string dwgPath)
    {
        string outputBowPdf = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(bowPath), "bow-linked.pdf");
        
        using (var pdfReader = new PdfReader(bowPath))
        using (var pdfWriter = new PdfWriter(outputBowPdf))
        using (var bowPdfDoc = new PdfDocument(pdfReader, pdfWriter))
        {
            var dwgPdfReader = new PdfReader(dwgPath);
            var dwgPdfDoc = new PdfDocument(dwgPdfReader);
            
           // Create a PdfFileSpec for external linking
            PdfFileSpec fileSpec = PdfFileSpec.CreateExternalFileSpec(bowPdfDoc, dwgPath);
            
            // 1) Query BOW_table for rows with Tag = "from_ref" AND ColorFlag = 1
            string sqlBOW = @"
                SELECT Id, Word, X1, Y1, X2, Y2, Sheet
                FROM BOW_table
                WHERE Tag = 'from_ref' 
                  AND ColorFlag = 1
            ";

            using (var cmdBOW = new SQLiteCommand(sqlBOW, connection))
            using (var readerBOW = cmdBOW.ExecuteReader())
            {
                while (readerBOW.Read())
                {
                    // Extract columns
                    string bowWord = readerBOW.GetString(1);
                    bowWord = bowWord.Replace("(", "").Replace(")", "");
                    float bowX1 = readerBOW.GetFloat(2);
                    float bowY1 = readerBOW.GetFloat(3);
                    float bowX2 = readerBOW.GetFloat(4);
                    float bowY2 = readerBOW.GetFloat(5);
                    int bowSheet = readerBOW.GetInt32(6);

                    // Search for the Word in DWG_table to find its corresponding Sheet
                    int? dwgSheetFound = GetSheetForWord(connection, bowWord);
                    if (dwgSheetFound == null)
                        continue;

                    // Look for Tag = "cable_tag" in DWG_table with that sheet
                    var cableTagCoords = GetCableTagCoords(connection, dwgSheetFound.Value);
                    if (cableTagCoords == null)
                        continue;

                    // cableTagCoords is (dwgX1, dwgY1, dwgX2, dwgY2, dwgSheet)
                    float dwgX1 = cableTagCoords.Value.Item1;
                    float dwgY1 = cableTagCoords.Value.Item2;
                    float dwgX2 = cableTagCoords.Value.Item3;
                    float dwgY2 = cableTagCoords.Value.Item4;
                    int dwgSheet = cableTagCoords.Value.Item5;

                    // Make sure bowSheet is valid in the PDF doc (1-based indexing).
                    if (bowSheet < 1 || bowSheet > bowPdfDoc.GetNumberOfPages())
                        continue;
                    var page = bowPdfDoc.GetPage(bowSheet);

                    // The rectangle for the link area on the BOW PDF:
                    float linkWidth = Math.Abs(bowX2 - bowX1);
                    float linkHeight = Math.Abs(bowY2 - bowY1);
                    Rectangle linkLocation = new Rectangle(bowX1, bowY1, linkWidth, linkHeight);
                    PdfLinkAnnotation linkAnnotation = new PdfLinkAnnotation(linkLocation);
                    
                    
                    // Calculate center of the cable tag area (target location in DWG)
                    float targetX = (dwgX1 + dwgX2) / 2;
                    float targetY = (dwgY1 + dwgY2) / 2;
                    float zoomLevel = 3.0f;  // Adjust as needed (1 = 100%, 2 = 200%, etc.)
                    

                    // Correct way to create GoToR with zoom:
                    PdfDestination destination = PdfExplicitRemoteGoToDestination .CreateXYZ(dwgSheet,targetX, targetY, zoomLevel);
                    PdfAction gotoRemote = PdfAction.CreateGoToR(fileSpec, destination, false);
                    
                    linkAnnotation.SetAction(gotoRemote);
                    
                    // 6) Add annotation to the page
                    page.AddAnnotation(linkAnnotation);
                }
            }
            
            
        }

        Console.WriteLine($"Hyperlinks added. Output PDF: {outputBowPdf}");
    }
    
    private int? GetSheetForWord(SQLiteConnection connection, string word)
    {
        string sql = @"
                SELECT Sheet 
                FROM DWG_table
                WHERE Word = @word
                LIMIT 1
            ";
        using (var cmd = new SQLiteCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("@word", word);
            object result = cmd.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt32(result);
            }
        }
        return null;
    }
    private (float, float, float, float, int)? GetCableTagCoords(SQLiteConnection connection, int sheet)
    {
        string sql = @"
                SELECT X1, Y1, X2, Y2, Sheet 
                FROM DWG_table
                WHERE Tag = 'cable_tag'
                  AND Sheet = @sheet
                LIMIT 1
            ";
        using (var cmd = new SQLiteCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("@sheet", sheet);
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    float x1 = reader.GetFloat(0);
                    float y1 = reader.GetFloat(1);
                    float x2 = reader.GetFloat(2);
                    float y2 = reader.GetFloat(3);
                    int sheetNum = reader.GetInt32(4);
                    return (x1, y1, x2, y2, sheetNum);
                }
            }
        }
        return null;
    }
    
}