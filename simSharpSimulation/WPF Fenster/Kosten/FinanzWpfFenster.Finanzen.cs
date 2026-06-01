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
        Versicherungsverteilung versicherungen = ergebnis.VersicherungenGesamt;
        Umsatzverteilung umsatzverteilung = ergebnis.UmsatzverteilungGesamt;
        Behandlungsmix behandlungsmix = ergebnis.BehandlungsmixGesamt;
        Kostenstruktur kostenstruktur = new()
        {
            PersonalkostenAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtPersonalkosten / ergebnis.GesamtUmsatz : 0.0,
            MietkostenAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMietkosten / ergebnis.GesamtUmsatz : 0.0,
            InfrastrukturkostenAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtInfrastrukturkosten / ergebnis.GesamtUmsatz : 0.0,
            MaterialkostenAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMaterialkosten / ergebnis.GesamtUmsatz : 0.0,
            GeraeteLeasingAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtLeasingkosten / ergebnis.GesamtUmsatz : 0.0,
            SonstigeFixkostenAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtSonstigeFixkosten / ergebnis.GesamtUmsatz : 0.0,
            BehandlungskostenAnteil = ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtBehandlungskosten / ergebnis.GesamtUmsatz : 0.0,
        };
        BreakEvenPoint breakEven = ergebnis.BreakEven;

        double privatAnteilProzent = KonfigurationJsonExport.Finanzen.Versicherung.AnteilPrivatversichert * 100.0;
        double gesetzlichAnteilProzent = (1.0 - KonfigurationJsonExport.Finanzen.Versicherung.AnteilPrivatversichert) * 100.0;
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
        sb.AppendLine(FinanzVisualisierung.FormatBreakEven(ergebnis.BreakEven, ergebnis.DurchschnittBehandeltePatientenProTag));
        sb.AppendLine();
        sb.AppendLine("Praxisdetails");
        sb.AppendLine($"Gesamtfläche: {ergebnis.Gesamtflaeche.ToString("N2", DeCulture)} m²");
        sb.AppendLine($"Mietkosten pro m²/Monat: {FinanzVisualisierung.FormatEuro(ergebnis.MietkostenProQm)}");
        // Zeige Mietkosten eindeutig: pro Tag, pro Monat (auf Basis m² * Preis/Monat) und pro Jahr
        sb.AppendLine($"Gesamtmietkosten pro Tag: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtMietkostenProTag)}");
        double gesamtMietkostenMonat = ergebnis.MietkostenProQm * ergebnis.Gesamtflaeche; // exakter Monatswert
        sb.AppendLine($"Gesamtmietkosten pro Monat: {FinanzVisualisierung.FormatEuro(gesamtMietkostenMonat)}");
        sb.AppendLine($"Gesamtmietkosten pro Jahr: {FinanzVisualisierung.FormatEuro(gesamtMietkostenMonat * 12)}");
        sb.AppendLine();
        sb.AppendLine("Kosten-und Gewinn-struktur (von Umsatz)");
        sb.AppendLine($"Personalkosten: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtPersonalkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtPersonalkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Mietkosten (im Zeitraum, gesamt): {FinanzVisualisierung.FormatEuro(ergebnis.GesamtMietkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMietkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Infrastruktur: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtInfrastrukturkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtInfrastrukturkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Medizinisches Material: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtMaterialkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtMaterialkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Geräte-Leasing: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtLeasingkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtLeasingkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Sonstige Fixkosten: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtSonstigeFixkosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtSonstigeFixkosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Behandlungskosten: {FinanzVisualisierung.FormatEuro(ergebnis.GesamtBehandlungskosten)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.GesamtBehandlungskosten / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine($"Gewinn: {FinanzVisualisierung.FormatEuro(ergebnis.Gesamtgewinn)} ({(ergebnis.GesamtUmsatz > 0 ? ergebnis.Gesamtgewinn / ergebnis.GesamtUmsatz : 0.0):P2})");
        sb.AppendLine();
        sb.AppendLine("Saisonaler Gewinn (im gewählten Zeitraum)");
        foreach (string saison in new[] { "Winter", "Fruehling", "Sommer", "Herbst" })
        {
            double saisonWert = ergebnis.SaisonGewinn.TryGetValue(saison, out double wert) ? wert : 0.0;
            sb.AppendLine($"{saison}: {FinanzVisualisierung.FormatEuro(saisonWert)}");
        }
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
            breakEvenTicker.Text = FinanzVisualisierung.FormatBreakEven(breakEven, ergebnis.DurchschnittBehandeltePatientenProTag);
        }

        return sb.ToString();
    }
}
