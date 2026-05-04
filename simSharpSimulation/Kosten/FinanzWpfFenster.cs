using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace simSharpSimulation;

internal sealed class FinanzWpfFenster : Window
{
    // Diese Steuerelemente werden benoetigt, um Eingaben zu lesen und Ergebnisse anzuzeigen.
    private readonly TextBox aerzteTextBox;
    private readonly TextBox schwesternTextBox;
    private readonly TextBox rezeptionTextBox;
    private readonly TextBox behandlungsraeumeTextBox;
    private readonly ComboBox zeitraumComboBox;
    private readonly TextBox ergebnisTextBox;
    private readonly Image finanzenImage;
    private readonly Image gewinnImage;
    private readonly TextBlock statusTextBlock;

    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    public FinanzWpfFenster()
    {
        // Baut das komplette WPF-Fenster programmatisch ohne separate XAML-Datei auf.
        Title = "Arztpraxis Finanzsimulation (WPF)";
        Width = 1400;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Margin = new Thickness(12);

        Grid eingabeGrid = new();
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        // Rezeption
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        // Behandlungsraeume
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Label aerzteLabel = new() { Content = "Anzahl Aerzte:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(aerzteLabel, 0);
        eingabeGrid.Children.Add(aerzteLabel);

        aerzteTextBox = new TextBox
        {
            Text = ArztKonfiguration.ANZAHL_AERZTE.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(aerzteTextBox, 1);
        eingabeGrid.Children.Add(aerzteTextBox);

        Label schwesternLabel = new() { Content = "Anzahl Schwestern:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(schwesternLabel, 3);
        eingabeGrid.Children.Add(schwesternLabel);

        schwesternTextBox = new TextBox
        {
            Text = SchwesterKonfiguration.ANZAHL_SCHWESTERN.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(schwesternTextBox, 4);
        eingabeGrid.Children.Add(schwesternTextBox);

        // Rezeption
        Label rezeptionLabel = new() { Content = "Anzahl Rezeption:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rezeptionLabel, 6);
        eingabeGrid.Children.Add(rezeptionLabel);

        rezeptionTextBox = new TextBox
        {
            Text = RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rezeptionTextBox, 7);
        eingabeGrid.Children.Add(rezeptionTextBox);

        // Behandlungsraeume
        Label behandlungsLabel = new() { Content = "Anzahl Behandlungszimmer:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(behandlungsLabel, 9);
        eingabeGrid.Children.Add(behandlungsLabel);

        behandlungsraeumeTextBox = new TextBox
        {
            Text = KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeume.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(behandlungsraeumeTextBox, 10);
        eingabeGrid.Children.Add(behandlungsraeumeTextBox);

        Label zeitraumLabel = new() { Content = "Zeitraum:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(zeitraumLabel, 12);
        eingabeGrid.Children.Add(zeitraumLabel);

        zeitraumComboBox = new ComboBox
        {
            ItemsSource = FinanzVisualisierung.ZeitraumOptionen,
            SelectedItem = "Jahr",
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEditable = false
        };
        Grid.SetColumn(zeitraumComboBox, 13);
        eingabeGrid.Children.Add(zeitraumComboBox);

        Button startenButton = new()
        {
            Content = "Simulation starten",
            Padding = new Thickness(14, 6, 14, 6),
            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold
        };
        startenButton.Click += SimulationStarten_Click;
        Grid.SetColumn(startenButton, 15);
        eingabeGrid.Children.Add(startenButton);

        Grid.SetRow(eingabeGrid, 0);
        root.Children.Add(eingabeGrid);

        statusTextBlock = new TextBlock
        {
            Text = "Bereit.",
            Margin = new Thickness(0, 8, 0, 8),
            Foreground = Brushes.DimGray
        };
        Grid.SetRow(statusTextBlock, 1);
        root.Children.Add(statusTextBlock);

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

        Grid.SetRow(inhaltGrid, 2);
        root.Children.Add(inhaltGrid);

        Content = root;
    }

    public static void StarteFenster()
    {
        // Startpunkt fuer den WPF-Modus der Finanzsimulation.
        Application app = new();
        app.Run(new FinanzWpfFenster());
    }

    private void SimulationStarten_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Eingaben werden zuerst validiert, bevor die Simulation und Diagrammerzeugung startet.
            if (!TryParseInt(aerzteTextBox.Text, 1, 50, out int anzahlAerzte, out string aerzteFehler))
                throw new InvalidOperationException(aerzteFehler);

            if (!TryParseInt(schwesternTextBox.Text, 1, 80, out int anzahlSchwestern, out string schwesternFehler))
                throw new InvalidOperationException(schwesternFehler);

            // Rezeption und Anzahl Behandlungszimmer aus UI einlesen
            if (!TryParseInt(rezeptionTextBox.Text, 0, 80, out int anzahlRezeptionisten, out string rezeptionFehler))
                throw new InvalidOperationException(rezeptionFehler);

            if (!TryParseInt(behandlungsraeumeTextBox.Text, 1, 100, out int anzahlBehandlungsraeume, out string raeumeFehler))
                throw new InvalidOperationException(raeumeFehler);

            // Setze globale Konfigurationen, damit Simulation und Auswertung die Werte verwenden
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = anzahlRezeptionisten;
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeume = anzahlBehandlungsraeume;

            string zeitraum = zeitraumComboBox.SelectedItem?.ToString() ?? "Jahr";

            statusTextBlock.Text = "Simulation laeuft...";
            FinanzErgebnis ergebnis = FinanzVisualisierung.Simuliere(anzahlAerzte, anzahlSchwestern, zeitraum);
            (string finanzenPfad, string gewinnPfad) =
                FinanzVisualisierung.ErzeugeDiagramme(ergebnis, anzahlAerzte, anzahlSchwestern);

            // Textbericht und Bilder werden gemeinsam aktualisiert, damit die Ansicht konsistent bleibt.
            ergebnisTextBox.Text = ErzeugeErgebnisText(ergebnis, finanzenPfad, gewinnPfad);
            finanzenImage.Source = LadeBild(finanzenPfad);
            gewinnImage.Source = LadeBild(gewinnPfad);
            statusTextBlock.Text = "Simulation erfolgreich abgeschlossen.";
        }
        catch (Exception ex)
        {
            statusTextBlock.Text = "Fehler bei der Simulation.";
            MessageBox.Show(this, ex.Message, "Simulation fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool TryParseInt(string? input, int min, int max, out int value, out string error)
    {
        // Zentralisierte Validierung fuer numerische Benutzereingaben im Formular.
        if (!int.TryParse(input, out value))
        {
            error = $"Bitte eine ganze Zahl zwischen {min} und {max} eingeben.";
            return false;
        }

        if (value < min || value > max)
        {
            error = $"Wert ausserhalb des Bereichs: erlaubt ist {min} bis {max}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static Border ErzeugeBildContainer(string titel, out Image image)
    {
        // Kapselt die gemeinsame Darstellung fuer beide Diagrammbereiche.
        DockPanel panel = new();

        TextBlock header = new()
        {
            Text = titel,
            FontWeight = FontWeights.SemiBold,
            FontSize = 18,
            Margin = new Thickness(8, 6, 8, 4)
        };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        image = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(8)
        };
        panel.Children.Add(image);

        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = panel
        };
    }

    private static string ErzeugeErgebnisText(FinanzErgebnis ergebnis, string finanzenPfad, string gewinnPfad)
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
        double mietkostenProTag = KonfigurationJsonExport.MietkostenProTag;
        sb.AppendLine($"Fixkosten: {FinanzVisualisierung.FormatEuro(mietkostenProTag + KonfigurationJsonExport.Finanzen.Fixkosten.WeitereFixkostenProTag)}");
        sb.AppendLine();
        sb.AppendLine("Dateien");
        sb.AppendLine($"- Finanzen: {finanzenPfad}");
        sb.AppendLine($"- Gewinn: {gewinnPfad}");

        return sb.ToString();
    }

    private static BitmapImage LadeBild(string dateiPfad)
    {
        if (!File.Exists(dateiPfad))
            throw new FileNotFoundException($"Bild nicht gefunden: {dateiPfad}", dateiPfad);

        // OnLoad loest die Datei direkt ein, damit sie danach nicht dauerhaft gesperrt bleibt.
        BitmapImage image = new();
        using FileStream stream = new(dateiPfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
