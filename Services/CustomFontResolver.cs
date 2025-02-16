using PdfSharp.Fonts;
using System;
using System.IO;
using System.Reflection;


namespace PdfProcessor.Services;

public class CustomFontResolver : IFontResolver
{
    private static readonly string FontFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Fonts");

    public static void Register()
    {
        GlobalFontSettings.FontResolver = new CustomFontResolver();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
        {
            if (isBold && isItalic)
                return new FontResolverInfo("Arial-BoldItalic");
            if (isBold)
                return new FontResolverInfo("Arial-Bold");
            if (isItalic)
                return new FontResolverInfo("Arial-Italic");
            return new FontResolverInfo("Arial-Regular");
        }

        return null;
    }

    public byte[] GetFont(string faceName)
    {
        string fontPath = faceName switch
        {
            "Arial-Regular" => Path.Combine(FontFolder, "arial.ttf"),
            "Arial-Bold" => Path.Combine(FontFolder, "arialbd.ttf"),
            "Arial-Italic" => Path.Combine(FontFolder, "ariali.ttf"),
            "Arial-BoldItalic" => Path.Combine(FontFolder, "arialbi.ttf"),
            _ => throw new InvalidOperationException($"Font '{faceName}' not found!")
        };

        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException($"Font file '{fontPath}' not found!");
        }

        return File.ReadAllBytes(fontPath);
    }
}