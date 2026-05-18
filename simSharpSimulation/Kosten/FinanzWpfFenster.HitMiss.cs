using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private Grid ErstelleHitMissTab()
    {
        Grid inhaltGrid = new();
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Links: Hit/Miss Statistik
        hitMissErgebnisTextBox = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(8)
        };
        Grid.SetColumn(hitMissErgebnisTextBox, 0);
        Grid.SetRow(hitMissErgebnisTextBox, 0);
        inhaltGrid.Children.Add(hitMissErgebnisTextBox);

        // Rechts: Hit/Miss Diagramm
        Border diagrammBorder = ErzeugeBildContainer("Hit vs Miss Verteilung", out hitMissImage);
        Grid.SetColumn(diagrammBorder, 2);
        Grid.SetRow(diagrammBorder, 0);
        inhaltGrid.Children.Add(diagrammBorder);

        return inhaltGrid;
    }

    private string ErzeugeHitMissErgebnisText(int anzahlHit, int anzahlMiss, string hitMissPfad)
    {
        int gesamt = anzahlHit + anzahlMiss;
        double hitQuote = gesamt > 0 ? (anzahlHit / (double)gesamt) * 100.0 : 0.0;
        double missQuote = gesamt > 0 ? (anzahlMiss / (double)gesamt) * 100.0 : 0.0;

        StringBuilder sb = new();
        sb.AppendLine("Hit/Miss Analyse");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Gesamtnachfrage: {gesamt.ToString("N0", DeCulture)}");
        sb.AppendLine($"Behandelt (Hit): {anzahlHit.ToString("N0", DeCulture)} ({hitQuote.ToString("N2", DeCulture)} %)");
        sb.AppendLine($"Nicht behandelt (Miss): {anzahlMiss.ToString("N0", DeCulture)} ({missQuote.ToString("N2", DeCulture)} %)");
        sb.AppendLine($"Hit-Quote: {hitQuote.ToString("N2", DeCulture)} %");
        sb.AppendLine($"Miss-Quote: {missQuote.ToString("N2", DeCulture)} %");
        sb.AppendLine();
        sb.AppendLine("Interpretation");
        sb.AppendLine($"Von {gesamt.ToString("N0", DeCulture)} angefragten Patienten konnten {anzahlHit.ToString("N0", DeCulture)} ({hitQuote.ToString("N2", DeCulture)} %) versorgt werden.");
        sb.AppendLine($"{anzahlMiss.ToString("N0", DeCulture)} Patienten ({missQuote.ToString("N2", DeCulture)} %) konnten wegen begrenzter Tageskapazitaet nicht behandelt werden.");
        sb.AppendLine();
        sb.AppendLine("Datei");
        sb.AppendLine($"- Hit/Miss: {hitMissPfad}");

        return sb.ToString();
    }
}
