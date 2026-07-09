using System;
using System.IO;
using UglyToad.PdfPig;
using System.Text.RegularExpressions;

using var writer = new StreamWriter("output_struct.txt");

try {
    using var pdf = PdfDocument.Open(@"D:\_vibe\objetmetiers\V1210_Sage 100_Structure des fichiers.pdf");
    foreach (var page in pdf.GetPages()) {
        var text = page.Text;
        if (Regex.IsMatch(text, @"\bP_DEVISE\b") || Regex.IsMatch(text, @"\bP_DOSSIER\b")) {
            writer.WriteLine($"\n=== Structure Page {page.Number} ===");
            writer.WriteLine(text);
        }
    }
} catch (Exception ex) {
    writer.WriteLine($"Error Structure: {ex.Message}");
}
