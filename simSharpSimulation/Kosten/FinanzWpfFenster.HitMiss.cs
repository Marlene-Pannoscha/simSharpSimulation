using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private Grid ErstelleHitMissTab()
    {
        Grid inhaltGrid = ErzeugeGeteiltesTabGrid();

        // Links: Hit/Miss Statistik
        hitMissErgebnisTextBox = ErzeugeErgebnisTextBox();
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
