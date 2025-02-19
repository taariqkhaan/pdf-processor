namespace PdfProcessor.Services;

public class AnnotationQuery
{
    private static readonly Dictionary<string, List<string>> Filters = new()
    {
        { "BOW", new List<string> { "cable_tag", "from_desc", "to_desc", "function", "size", "insulation",
            "from_ref", "to_ref", "voltage", "conductors", "length" }},
        { "TITLE", new List<string> { "facility_name", "facility_id", "dwg_title1", "dwg_title2", "dwg_scale", 
            "dwg_size", "dwg_number", "dwg_sheet", "dwg_rev", "dwg_type" }},
        { "CUSTOM", new List<string> { "facility_name" }}
    };
    
    public List<string> GetFilters(string key)
    {
        return Filters.TryGetValue(key, out var values) ? values : new List<string>();
    }
}