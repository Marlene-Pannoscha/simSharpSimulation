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
        Grid inhaltGrid = ErzeugeGeteiltesTabGrid();

        // Links stehen Textauswertung und Kennzahlen, rechts die erzeugten Diagramme.
        var ergebnisBox = ErzeugeErgebnisTextBox();
        Grid.SetColumn(ergebnisBox, 0);
        Grid.SetRow(ergebnisBox, 0);
        inhaltGrid.Children.Add(ergebnisBox);
        ergebnisTextBox = ergebnisBox;

        var ticker = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        var rowDef = new RowDefinition { Height = GridLength.Auto };
        inhaltGrid.RowDefinitions.Add(rowDef);
        Grid.SetRow(ticker, 1);
        inhaltGrid.Children.Add(ticker);
        breakEvenTicker = ticker;


        Grid bilderGrid = new();
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        bilderGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border finanzenBorder = ErzeugeBildContainer("Umsatz und Kosten", out finanzenImage);
        Grid.SetRow(finanzenBorder, 0);
        bilderGrid.Children.Add(finanzenBorder);

        Border gewinnBorder = ErzeugeBildContainer("Gewinn", out gewinnImage);
        Grid.SetRow(gewinnBorder, 2);
        bilderGrid.Children.Add(gewinnBorder);

        Border kostenstrukturBorder = ErzeugeBildContainer("Kostenstruktur", out kostenstrukturImage);
        Grid.SetRow(kostenstrukturBorder, 4);
        bilderGrid.Children.Add(kostenstrukturBorder);

        ScrollViewer scrollViewer = new ScrollViewer
        {
            Content = bilderGrid,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        Grid.SetColumn(scrollViewer, 2);
        Grid.SetRow(scrollViewer, 0);
        Grid.SetRowSpan(scrollViewer, 2); // Span over the text and ticker rows
        inhaltGrid.Children.Add(scrollViewer);

        return inhaltGrid;
    }

    private string ErzeugeErgebnisText(FinanzErgebnis ergebnis, string finanzenPfad, string gewinnPfad, string kostenstrukturPfad)
    {
        Tagesergebnis tagesergebnis = ergebnis.Tagesergebnisse.FirstOrDefault();

        Versicherungsverteilung versicherungen = ergebnis.VersicherungenGesamt;
        Umsatzverteilung umsatzverteilung = ergebnis.UmsatzverteilungGesamt;
        Behandlungsmix behandlungsmix = ergebnis.BehandlungsmixGesamt;
        Kostenstruktur kostenstruktur = tagesergebnis.Kostenstruktur;
        BreakEvenPoint breakEven = tagesergebnis.BreakEven;

        double privatAnteilProzent = KonfigurationJsonExport.Finanzen.Versicherung.AnteilPrivatversichert * 100.0;
        double gesetzlichAnteilProzent = (1.0 - KonfigurationJsonExport.Finanzen.Versicherung.AnteilPrivatversichert) * 100.0;
        int durchschnittPatientenProTagGerundet = (int)Math.Round(ergebnis.DurchschnittBehandeltePatientenProTag);

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
        sb.AppendLine($"Durchschnitt behandelte Patienten pro Tag: {ergebnis.DurchschnittBehandeltePatientenProTag.ToString("N1", DeCulture)}");
        sb.AppendLine();
        sb.AppendLine("Praxisdetails");
        sb.AppendLine($"Gesamtfläche: {ergebnis.Gesamtflaeche.ToString("N2", DeCulture)} m²");
        sb.AppendLine($"Mietkosten pro m²/Monat: {FinanzVisualisierung.FormatEuro(ergebnis.MietkostenProQm)}");
        sb.AppendLine($"Gesamtmietkosten pro Monat: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtMietkostenProTag * 30)}");
        sb.AppendLine();
        sb.AppendLine("Kostenstruktur (Anteil am Umsatz)");
        sb.AppendLine($"Personalkosten: {kostenstruktur.PersonalkostenAnteil:P2}");
        sb.AppendLine($"Mietkosten: {kostenstruktur.MietkostenAnteil:P2}");
        sb.AppendLine($"Infrastruktur: {kostenstruktur.InfrastrukturkostenAnteil:P2}");
        sb.AppendLine($"Medizinisches Material: {kostenstruktur.MaterialkostenAnteil:P2}");
        sb.AppendLine($"Sonstige Fixkosten: {kostenstruktur.SonstigeFixkostenAnteil:P2}");
        sb.AppendLine($"Behandlungskosten: {kostenstruktur.BehandlungskostenAnteil:P2}");
        sb.AppendLine();
        sb.AppendLine("Versicherung");
        sb.AppendLine($"Privat ({privatAnteilProzent.ToString("N2", DeCulture)} %): {versicherungen.PrivatPatienten} Patienten / {FinanzVisualisierung.FormatEuro(umsatzverteilung.UmsatzPrivat)}");
        sb.AppendLine($"Gesetzlich ({gesetzlichAnteilProzent.ToString("N2", DeCulture)} %): {versicherungen.GesetzlichPatienten} Patienten / {FinanzVisualisierung.FormatEuro(umsatzverteilung.UmsatzGesetzlich)}");
        sb.AppendLine();
        sb.AppendLine("Behandlungsdauer");
        sb.AppendLine($"Kurz: {behandlungsmix.KurzPatienten} Patienten / {FinanzVisualisierung.FormatEuro(behandlungsmix.KurzKosten)}");
        sb.AppendLine($"Mittel: {behandlungsmix.MittelPatienten} Patienten / {FinanzVisualisierung.FormatEuro(behandlungsmix.MittelKosten)}");
        sb.AppendLine($"Lang: {behandlungsmix.LangPatienten} Patienten / {FinanzVisualisierung.FormatEuro(behandlungsmix.LangKosten)}");
        sb.AppendLine($"Zusatzkosten Behandlungsdauer: {FinanzVisualisierung.FormatEuro(behandlungsmix.Gesamtkosten)}");
        sb.AppendLine();
        sb.AppendLine("Dateien");
        sb.AppendLine($"- Finanzen: {finanzenPfad}");
        sb.AppendLine($"- Gewinn: {gewinnPfad}");
        sb.AppendLine($"- Kostenstruktur: {kostenstrukturPfad}");

        // Update Break-Even Ticker
        if (breakEvenTicker != null)
        {
            breakEvenTicker.Text = $"Break-Even: {breakEven.Patienten} Patienten oder nach {breakEven.Tage:N1} Tagen";
        }

        return sb.ToString();
    }
}
