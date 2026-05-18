using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private Grid ErstelleFinanzenTab()
    {
        Grid inhaltGrid = new();
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Links stehen Textauswertung und Kennzahlen, rechts die erzeugten Diagramme.
        ergebnisTextBox = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(8)
        };
        Grid.SetColumn(ergebnisTextBox, 0);
        Grid.SetRow(ergebnisTextBox, 0);
        inhaltGrid.Children.Add(ergebnisTextBox);

        Grid bilderGrid = new();
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border finanzenBorder = ErzeugeBildContainer("Umsatz und Kosten", out finanzenImage);
        Grid.SetRow(finanzenBorder, 0);
        bilderGrid.Children.Add(finanzenBorder);

        Border gewinnBorder = ErzeugeBildContainer("Gewinn", out gewinnImage);
        Grid.SetRow(gewinnBorder, 2);
        bilderGrid.Children.Add(gewinnBorder);

        Grid.SetColumn(bilderGrid, 2);
        Grid.SetRow(bilderGrid, 0);
        inhaltGrid.Children.Add(bilderGrid);

        return inhaltGrid;
    }

    private string ErzeugeErgebnisText(FinanzErgebnis ergebnis, string finanzenPfad, string gewinnPfad)
    {
        Versicherungsverteilung versicherungen = ergebnis.VersicherungenGesamt;
        Umsatzverteilung umsatzverteilung = ergebnis.UmsatzverteilungGesamt;
        Behandlungsmix behandlungsmix = ergebnis.BehandlungsmixGesamt;

        // Der Bericht fasst die Simulation kompakt fuer die linke Textspalte zusammen.
        StringBuilder sb = new();
        sb.AppendLine("Ergebnis");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Zeitraum: {ergebnis.Zeitraum}");
        sb.AppendLine($"Simulierte Tage: {ergebnis.SimulierteTage}");
        sb.AppendLine($"Gesamtumsatz: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtUmsatz)}");
        sb.AppendLine($"Gesamtkosten: {FinanzVisualisierung.FormatEuro(ergebnis.Gesamtkosten)}");
        sb.AppendLine($"Gesamtgewinn: {FinanzVisualisierung.FormatEuro(ergebnis.Gesamtgewinn)}");
        sb.AppendLine($"Durchschnitt Umsatz pro Tag: {FinanzVisualisierung.FormatEuro(ergebnis.DurchschnittlicherUmsatzProTag)}");
        sb.AppendLine($"Durchschnitt Kosten pro Tag: {FinanzVisualisierung.FormatEuro(ergebnis.DurchschnittlicheKostenProTag)}");
        sb.AppendLine($"Durchschnitt Gewinn pro {ergebnis.DurchschnittLabel}: {FinanzVisualisierung.FormatEuro(ergebnis.DurchschnittlicherGewinnProEinheit)}");
        sb.AppendLine($"Durchschnitt behandelte Patienten pro Tag: {ergebnis.Tagespunkte.Average(t => t.BehandeltePatienten).ToString("N1", DeCulture)}");
        sb.AppendLine();
        sb.AppendLine("Versicherung");
        sb.AppendLine($"Privat (20 %): {versicherungen.PrivatPatienten} Patienten / {FinanzVisualisierung.FormatEuro(umsatzverteilung.UmsatzPrivat)}");
        sb.AppendLine($"Gesetzlich (80 %): {versicherungen.GesetzlichPatienten} Patienten / {FinanzVisualisierung.FormatEuro(umsatzverteilung.UmsatzGesetzlich)}");
        sb.AppendLine();
        sb.AppendLine("Behandlungsdauer");
        sb.AppendLine($"Kurz: {behandlungsmix.KurzPatienten} Patienten / {FinanzVisualisierung.FormatEuro(behandlungsmix.KurzKosten)}");
        sb.AppendLine($"Mittel: {behandlungsmix.MittelPatienten} Patienten / {FinanzVisualisierung.FormatEuro(behandlungsmix.MittelKosten)}");
        sb.AppendLine($"Lang: {behandlungsmix.LangPatienten} Patienten / {FinanzVisualisierung.FormatEuro(behandlungsmix.LangKosten)}");
        sb.AppendLine($"Zusatzkosten Behandlungsdauer: {FinanzVisualisierung.FormatEuro(behandlungsmix.Gesamtkosten)}");
        sb.AppendLine();
        sb.AppendLine("Kostenstruktur pro Tag");
        sb.AppendLine($"Aerzte: {FinanzVisualisierung.FormatEuro(FinanzRechner.BerechneArztlohn(ArztKonfiguration.ANZAHL_AERZTE, (int)Math.Round(ergebnis.Tagespunkte.Average(t => t.BehandeltePatienten))))}");
        sb.AppendLine($"Schwestern: {FinanzVisualisierung.FormatEuro(FinanzRechner.BerechneSchwesterlohn(SchwesterKonfiguration.ANZAHL_SCHWESTERN))}");
        sb.AppendLine($"Rezeption: {FinanzVisualisierung.FormatEuro(FinanzRechner.BerechneRezeptionlohn(RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN))}");
        sb.AppendLine($"Zimmer: {KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester} Schwester / {KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt} Arzt");
        sb.AppendLine($"Fläche: {KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter.ToString("N1", DeCulture)} m² Schwester / {KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumArztQuadratmeter.ToString("N1", DeCulture)} m² Arzt");
        double mietkostenProTag = KonfigurationJsonExport.MietkostenProTag;
        sb.AppendLine($"Mietkosten Behandlungsräume: {FinanzVisualisierung.FormatEuro(mietkostenProTag)}");
        sb.AppendLine($"Fixkosten: {FinanzVisualisierung.FormatEuro(mietkostenProTag + KonfigurationJsonExport.Finanzen.Fixkosten.WeitereFixkostenProTag)}");
        sb.AppendLine();
        sb.AppendLine("Dateien");
        sb.AppendLine($"- Finanzen: {finanzenPfad}");
        sb.AppendLine($"- Gewinn: {gewinnPfad}");

        return sb.ToString();
    }
}
