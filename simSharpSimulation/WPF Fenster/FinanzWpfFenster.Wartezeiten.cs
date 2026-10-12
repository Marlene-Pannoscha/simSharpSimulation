using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster
{
    private Grid ErstelleWartezeitenTab()
    {
        Grid inhaltGrid = ErzeugeGeteiltesTabGrid();

        wartezeitenTextBox = ErzeugeErgebnisTextBox();
        Grid.SetColumn(wartezeitenTextBox, 0);
        Grid.SetRow(wartezeitenTextBox, 0);
        inhaltGrid.Children.Add(wartezeitenTextBox);

        StackPanel tabellenPanel = new()
        {
            Orientation = Orientation.Vertical
        };

        Border warteschlangenBorder = ErzeugeTabellenContainer("Warteschlangen: Patientenanzahl", out warteschlangenDataGrid);
        tabellenPanel.Children.Add(warteschlangenBorder);

        Border auslastungBorder = ErzeugeTabellenContainer("Auslastung", out auslastungDataGrid);
        tabellenPanel.Children.Add(auslastungBorder);

        Border bereicheBorder = ErzeugeTabellenContainer("Bereiche: Patientenanzahl", out bereicheDataGrid);
        tabellenPanel.Children.Add(bereicheBorder);

        Border wartezeitenBorder = ErzeugeTabellenContainer("Wartezeiten je Warteschlange", out wartezeitenDataGrid);
        tabellenPanel.Children.Add(wartezeitenBorder);

        Border behandlungszeitenBorder = ErzeugeTabellenContainer("Behandlungszeit: Ist vs. Erwartet", out behandlungszeitenDataGrid);
        tabellenPanel.Children.Add(behandlungszeitenBorder);

        ScrollViewer tabellenScrollViewer = new()
        {
            Content = tabellenPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 4, 0)
        };

        Grid.SetColumn(tabellenScrollViewer, 2);
        Grid.SetRow(tabellenScrollViewer, 0);
        inhaltGrid.Children.Add(tabellenScrollViewer);

        return inhaltGrid;
    }

    private void AktualisiereWartezeitenTab(SimulationsDaten? daten, string? zeitraum = null)
    {
        if (daten is null)
        {
            wartezeitenTextBox.Text = "Keine Wartezeiten-Auswertung vorhanden.\n\nStarte die Simulation, um Warteschlangen- und Bereichskennzahlen zu berechnen.";
            warteschlangenDataGrid.ItemsSource = null;
            auslastungDataGrid.ItemsSource = null;
            bereicheDataGrid.ItemsSource = null;
            wartezeitenDataGrid.ItemsSource = null;
            behandlungszeitenDataGrid.ItemsSource = null;
            return;
        }

        string ausgewaehlterZeitraum = zeitraum ?? zeitraumComboBox.SelectedItem?.ToString() ?? "Jahr";
        WartezeitenAuswertung auswertung = WartezeitenAuswertung.Erzeuge(daten, ausgewaehlterZeitraum);
        warteschlangenDataGrid.ItemsSource = auswertung.Warteschlangen;
        auslastungDataGrid.ItemsSource = auswertung.Auslastungen;
        bereicheDataGrid.ItemsSource = auswertung.Bereiche;
        wartezeitenDataGrid.ItemsSource = auswertung.Wartezeiten;
        behandlungszeitenDataGrid.ItemsSource = auswertung.Behandlungszeiten;
        wartezeitenTextBox.Text = ErzeugeWartezeitenText(auswertung);
    }

    private static Border ErzeugeTabellenContainer(string titel, out DataGrid dataGrid)
    {
        DockPanel panel = new();

        Border headerLeiste = new()
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 250)),
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 7, 10, 7)
        };

        TextBlock header = new()
        {
            Text = titel,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(35, 45, 55))
        };
        headerLeiste.Child = header;
        DockPanel.SetDock(headerLeiste, Dock.Top);
        panel.Children.Add(headerLeiste);

        dataGrid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(8),
            MinRowHeight = 26,
            RowHeight = 28,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            Background = Brushes.White,
            BorderThickness = new Thickness(0),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(232, 236, 240)),
            CanUserResizeColumns = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow
        };
        ScrollViewer.SetVerticalScrollBarVisibility(dataGrid, ScrollBarVisibility.Visible);
        ScrollViewer.SetHorizontalScrollBarVisibility(dataGrid, ScrollBarVisibility.Auto);
        panel.Children.Add(dataGrid);

        return new Border
        {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = Brushes.White,
            Height = 220,
            Margin = new Thickness(0, 0, 0, 12),
            Child = panel
        };
    }

    private static string ErzeugeWartezeitenText(WartezeitenAuswertung auswertung)
    {
        StringBuilder sb = new();
        sb.AppendLine("Wartezeiten und Warteschlangen");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"Auswertungsdauer: {auswertung.AuswertungsdauerMinuten.ToString("N2", DeCulture)} Minuten");
        sb.AppendLine($"Trace-Ereignisse: {auswertung.TraceEreignisse.ToString("N0", DeCulture)}");
        sb.AppendLine($"Zeitraum/Saisonfaktor: {auswertung.Zeitraum} x {auswertung.Saisonfaktor.ToString("N2", DeCulture)}");
        sb.AppendLine();
        sb.AppendLine("Auslastung");
        foreach (AuslastungZeile zeile in auswertung.Auslastungen)
        {
            sb.AppendLine($"{zeile.Name}: {zeile.ZeitbasierteAuslastungProzent.ToString("N2", DeCulture)} % zeitbasiert, {zeile.PatientenbasierteAuslastungProzent.ToString("N2", DeCulture)} % patientenbasiert");
        }
        sb.AppendLine();
        sb.AppendLine("Durchschnittliche Wartezeiten");
        foreach (WartezeitZeile zeile in auswertung.Wartezeiten)
        {
            sb.AppendLine($"{zeile.Name}: {zeile.Durchschnitt.ToString("N2", DeCulture)} Minuten ({zeile.AnzahlAusgewertet.ToString("N0", DeCulture)} ausgewertet, {zeile.AusreisserAnzahl.ToString("N0", DeCulture)} Ausreisser)");
        }
        sb.AppendLine();
        sb.AppendLine("Behandlungszeit: Ist vs. Erwartet");
        foreach (BehandlungszeitVergleichZeile zeile in auswertung.Behandlungszeiten)
        {
            sb.AppendLine($"{zeile.Name}: Ist {zeile.TatsaechlichDurchschnittMinuten.ToString("N2", DeCulture)} min, erwartet {zeile.ErwartetMinuten.ToString("N2", DeCulture)} min");
        }
        sb.AppendLine();
        sb.AppendLine("Hinweis");
        sb.AppendLine("Zeitbasierte Auslastung: belegte Minuten / verfuegbare Kapazitaetsminuten.");
        sb.AppendLine("Patientenbasierte Auslastung: behandelte Patienten / erwartbare Patienten-Kapazitaet.");
        sb.AppendLine("Der Saisonfaktor skaliert Mengen, Belegung und Auslastung; gemessene Wartezeit-Dauern bleiben aus der 30-Tage-Simulation.");
        sb.AppendLine("Min/Avg/Max der Patientenanzahl werden zeitgewichtet aus dem Trace rekonstruiert.");
        sb.AppendLine("Die Warteschlangen zaehlen Patienten ab Eintritt in den jeweiligen Wartebereich bis zum Betreten des Behandlungsbereichs oder Abbruch.");
        sb.AppendLine("Rezeption-Wartezeiten von mehr als 15 Minuten werden als Ausreisser gezaehlt und nicht in Min/Avg/Max der Wartezeit eingerechnet.");
        sb.AppendLine("Die Raumauslastung ist aus Patienten im Zimmer abgeleitet; Raeume begrenzen die Simulation noch nicht als eigene Ressource.");

        return sb.ToString();
    }

}
