﻿using System;
using System.IO;
using System.Data.SQLite;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Action;
using iText.Kernel.Pdf.Navigation;
using iText.Kernel.Pdf.Filespec;
using iText.Kernel.Geom;
using iText.Kernel.Colors;

namespace PdfProcessor.Services;

public class HyperlinkService
{
    double currentFromX1 = 5000;
    double currentFromY1 = 5000;
    double currentFromX2 = 0;
    double currentFromY2 = 0;
    double currentToX1 = 5000;
    double currentToY1 = 5000;
    double currentToX2 = 0;
    double currentToY2 = 0;
    
    public void HyperlinkMain(string dbFilePath)
    {
        string bowPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dbFilePath), "highlighted_BOW.pdf");
        string dwgPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dbFilePath), "highlighted_DWG.pdf");
        
        
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

        string cableWord = null;
        double targetX = 0;
        double targetY = 0;
        double zoomLevel = 0;
        int dwgSheet = 0;
        int currentItem = 0;
        int currentSheet = 0;
        int currentToPage = 0;
        int currentFromPage = 0;
        float currentToWidth = 0;
        float currentFromWidth = 0;
        float dwgWidth = 0;
        float dwgHeight = 0;
        
        
        using (var dwgPdfReader = new PdfReader(dwgPath))
        using (var dwgPdfDoc = new PdfDocument(dwgPdfReader))
        using (var pdfReader = new PdfReader(bowPath))
        using (var pdfWriter = new PdfWriter(outputBowPdf))
        using (var bowPdfDoc = new PdfDocument(pdfReader, pdfWriter))
        {
            
           // Create a PdfFileSpec for external linking
            PdfFileSpec fileSpec = PdfFileSpec.CreateExternalFileSpec(bowPdfDoc, dwgPath);
            
            // Query BOW_table for rows with drawing references
            string sqlBOW = @"
                SELECT Id, Word, X1, Y1, X2, Y2, Sheet, Item, Tag, ColorFlag
                FROM BOW_table
                ORDER BY Sheet ASC, Item ASC, Tag
            ";

            using (var cmdBOW = new SQLiteCommand(sqlBOW, connection))
            using (var readerBOW = cmdBOW.ExecuteReader())
            {
                while (readerBOW.Read())
                {
                    string bowWord = readerBOW.GetString(1)?.Trim();
                    if (string.IsNullOrWhiteSpace(bowWord))
                        continue; // Skip this iteration and move to the next row
                    float bowX1 = readerBOW.GetFloat(2);
                    float bowY1 = readerBOW.GetFloat(3);
                    float bowX2 = readerBOW.GetFloat(4);
                    float bowY2 = readerBOW.GetFloat(5);
                    int bowSheet = readerBOW.GetInt32(6);
                    int bowItem = readerBOW.GetInt32(7);
                    string bowTag = readerBOW.GetString(8)?.Trim();
                    
                    if (currentSheet == 0)
                    {
                        currentSheet = bowSheet;
                        currentItem = bowItem;
                    }

                    // Determine and place From and To drawing references hyperlinks
                    if (bowTag == "from_ref" || bowTag == "to_ref")
                    {
                        bowWord = bowWord.Replace("(", "").Replace(")", "");
                        
                        string sqlCableTag = @"
                        SELECT Word FROM BOW_table 
                        WHERE Tag = 'cable_tag' AND Item = @ItemNumber AND Sheet = @SheetNumber
                        ";

                        using (var cmdCableTag = new SQLiteCommand(sqlCableTag, connection))
                        {
                            cmdCableTag.Parameters.AddWithValue("@ItemNumber", bowItem);
                            cmdCableTag.Parameters.AddWithValue("@SheetNumber", bowSheet);
                            object cableWordObj = cmdCableTag.ExecuteScalar();
                            cableWord = cableWordObj.ToString();
                    
                        }
                        // Search for the Word in DWG_table to find its corresponding Sheet
                        int? dwgSheetFound = GetSheetForWord(connection, bowWord);
                        if (dwgSheetFound == null)
                            continue;
                        PdfPage dwgPage = dwgPdfDoc.GetPage(dwgSheetFound.Value);
                        Rectangle pageSize = dwgPage.GetPageSize();
                        dwgWidth = pageSize.GetWidth();
                        dwgHeight = pageSize.GetHeight();
                        int dwgrotation = dwgPage.GetRotation();
                        

                        if (bowTag == "from_ref")
                        {
                            currentFromPage = dwgSheetFound.Value;
                            currentFromWidth = dwgWidth;
                        }
                        if (bowTag == "to_ref")
                        {
                            currentToPage = dwgSheetFound.Value;
                            currentToWidth = dwgWidth;
                        }
                        
                        // Look for Tag = "cable_tag" in DWG_table with that sheet
                        var cableTagCoords = GetCableTagCoords(connection, dwgSheetFound.Value, cableWord);
                        if (cableTagCoords == null)
                            continue;
                        
                        // cableTagCoords is (dwgX1, dwgY1, dwgX2, dwgY2, dwgSheet)
                        double dwgX1 = cableTagCoords.Value.Item1;
                        double dwgY1 = cableTagCoords.Value.Item2;
                        double dwgX2 = cableTagCoords.Value.Item3;
                        double dwgY2 = cableTagCoords.Value.Item4;
                        dwgSheet = cableTagCoords.Value.Item5;
                        int wordRot = cableTagCoords.Value.Item6;
                        
                        // Calculate center of the cable tag area (target location in DWG)
                        if (wordRot == 0)
                        {
                            targetX = dwgX1 - 20;
                            targetY = dwgY2 + 20;
                            zoomLevel = 2.5;
                        }
                        else
                        {
                            targetX = dwgX1 - 20;
                            targetY = dwgY1 - 20;
                            zoomLevel = 2.5;
                        }
                        
                        // Make sure bowSheet is valid in the PDF doc (1-based indexing).
                        if (bowSheet < 1 || bowSheet > bowPdfDoc.GetNumberOfPages())
                            continue;
                        var page = bowPdfDoc.GetPage(bowSheet);
                    
                        // The rectangle for the link area on the BOW PDF:
                        float linkWidth = Math.Abs(bowX2 - bowX1);
                        float linkHeight = Math.Abs(bowY2 - bowY1);
                        Rectangle linkLocation = new Rectangle(bowX1, bowY1, linkWidth, linkHeight);
                        PdfLinkAnnotation linkAnnotation = new PdfLinkAnnotation(linkLocation);
                    
                    
                        // create GoToR with zoom:
                        PdfDestination destination = PdfExplicitRemoteGoToDestination.CreateXYZ(dwgSheet,
                            (float)targetX, (float)targetY, (float)zoomLevel);
                        PdfAction gotoRemote = PdfAction.CreateGoToR(fileSpec, destination, false);
                    
                        linkAnnotation.SetAction(gotoRemote);
                        page.AddAnnotation(linkAnnotation);
                    }
                    
                    // Determine and place From and To description hyperlinks
                    if (currentSheet == bowSheet)
                    {  
                        if (currentItem == bowItem)
                        {
                            if (bowTag == "from_desc")
                            { 
                                if (currentFromX1 >= bowX1)
                                {
                                    currentFromX1 = bowX1;
                                    currentFromY1 = bowY1;
                                }
                                if (currentFromX2 <= bowX2)
                                {
                                    currentFromX2 = bowX2;
                                    currentFromY2 = bowY2;
                                }
                            }
                            if (bowTag == "to_desc")
                            {
                                if (currentToX1 >= bowX1)
                                {
                                    currentToX1 = bowX1;
                                    currentToY1 = bowY1;
                                }
                                if (currentToX2 <= bowX2)
                                {
                                    currentToX2 = bowX2;
                                    currentToY2 = bowY2;
                                }
                            }
                            
                        }
                        else
                        {
                            Console.WriteLine($"{bowItem - 1}, {bowSheet}");
                            AddPdfLinkAnnotation(bowPdfDoc, currentSheet, fileSpec, currentFromPage, 
                                currentToPage, currentFromWidth, currentToWidth);
                            currentItem = bowItem;
                        }
                    }
                    else
                    {   
                        Console.WriteLine($" last item {bowSheet - 1}");
                        AddPdfLinkAnnotation(bowPdfDoc, currentSheet, fileSpec, currentFromPage, 
                            currentToPage, currentFromWidth, currentToWidth);
                        currentSheet = bowSheet;
                        currentItem = bowItem;
                    }
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
    private (double, double, double, double, int, int)? GetCableTagCoords(SQLiteConnection connection, int sheet, string cableWord)
    {
        string sql = @"
                SELECT X1, Y1, X2, Y2, Sheet, WordRotation 
                FROM DWG_table
                WHERE Tag = 'cable_tag'
                  AND Sheet = @sheet
                  AND Word = @cableWord
                LIMIT 1
            ";
        using (var cmd = new SQLiteCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("@sheet", sheet);
            cmd.Parameters.AddWithValue("@cableWord", cableWord);
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    double x1 = reader.GetFloat(0);
                    double y1 = reader.GetFloat(1);
                    double x2 = reader.GetFloat(2);
                    double y2 = reader.GetFloat(3);
                    int sheetNum = reader.GetInt32(4);
                    int wordRot = reader.GetInt32(5);
                    return (x1, y1, x2, y2, sheetNum, wordRot);
                }
            }
        }
        return null;
    }
    private void AddPdfLinkAnnotation(PdfDocument bowPdfDoc, int currentSheet, PdfFileSpec fileSpec, 
        int currentFromPage, int currentToPage, float currentFromWidth, float currentToWidth)
    {
        
        
        var page = bowPdfDoc.GetPage(currentSheet);
        
        // Add 'To' Reference Link
        float linkWidth = (float)Math.Abs(currentToX2 - currentToX1);
        float linkHeight = (float)Math.Abs(currentToY2 - currentToY1);
        Rectangle linkLocation = new Rectangle((float)currentToX1, (float)currentToY1, linkWidth, linkHeight);
        PdfLinkAnnotation linkAnnotation = new PdfLinkAnnotation(linkLocation);
        PdfDestination destination = PdfExplicitRemoteGoToDestination.CreateXYZ(currentToPage, currentToWidth - 500, 110, 1.0f);
        linkAnnotation.SetAction(PdfAction.CreateGoToR(fileSpec, destination, false));
        page.AddAnnotation(linkAnnotation);
        

        // Add 'From' Reference Link
        linkWidth = (float)Math.Abs(currentFromX2 - currentFromX1);
        linkHeight = (float)Math.Abs(currentFromY2 - currentFromY1);
        linkLocation = new Rectangle((float)currentFromX1, (float)currentFromY1, linkWidth, linkHeight);
        linkAnnotation = new PdfLinkAnnotation(linkLocation);
        destination = PdfExplicitRemoteGoToDestination.CreateXYZ(currentFromPage, currentFromWidth - 500, 110, 1.0f);
        linkAnnotation.SetAction(PdfAction.CreateGoToR(fileSpec, destination, false));
        page.AddAnnotation(linkAnnotation);
        
        currentFromX1 = currentToX1 = 5000;
        currentFromY1 = currentToY1 = 5000;
        currentFromX2 = currentToX2 = 0;
        currentFromY2 = currentToY2 = 0;
    }
    
}