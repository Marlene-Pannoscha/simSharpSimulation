using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace simSharpSimulation;

internal sealed partial class FinanzWpfFenster : Window
{
    // Diese Steuerelemente werden benoetigt, um Eingaben zu lesen und Ergebnisse anzuzeigen.
    private readonly TextBox aerzteTextBox;
    private readonly TextBox schwesternTextBox;
    private readonly TextBox rezeptionTextBox;
    private readonly TextBox behandlungsraeumeSchwesterTextBox;
    private readonly TextBox behandlungsflaecheSchwesterTextBox;
    private readonly TextBox behandlungsraeumeArztTextBox;
    private readonly TextBox behandlungsflaecheArztTextBox;
    private readonly ComboBox zeitraumComboBox;
    private TextBox ergebnisTextBox = null!;
    private Image finanzenImage = null!;
    private Image gewinnImage = null!;
    private readonly TextBlock statusTextBlock;
    
    // Hit/Miss Tab-Steuerelemente
    private TextBox hitMissErgebnisTextBox = null!;
    private Image hitMissImage = null!;

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
        eingabeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        eingabeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        eingabeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Zeile 1: Personal
        Grid personalGrid = new();
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        personalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });

        Label aerzteLabel = new() { Content = "Anzahl Aerzte:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(aerzteLabel, 0);
        personalGrid.Children.Add(aerzteLabel);

        aerzteTextBox = new TextBox
        {
            Text = ArztKonfiguration.ANZAHL_AERZTE.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(aerzteTextBox, 1);
        personalGrid.Children.Add(aerzteTextBox);

        Label schwesternLabel = new() { Content = "Anzahl Schwestern:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(schwesternLabel, 3);
        personalGrid.Children.Add(schwesternLabel);

        schwesternTextBox = new TextBox
        {
            Text = SchwesterKonfiguration.ANZAHL_SCHWESTERN.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(schwesternTextBox, 4);
        personalGrid.Children.Add(schwesternTextBox);

        Label rezeptionLabel = new() { Content = "Anzahl Rezeption:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rezeptionLabel, 6);
        personalGrid.Children.Add(rezeptionLabel);

        rezeptionTextBox = new TextBox
        {
            Text = RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rezeptionTextBox, 7);
        personalGrid.Children.Add(rezeptionTextBox);
        Grid.SetRow(personalGrid, 0);
        eingabeGrid.Children.Add(personalGrid);

        // Zeile 2: Behandlungsraeume
        Grid raeumeGrid = new();
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        raeumeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });

        Label behandlungsSchwesterLabel = new() { Content = "Zimmer Schwester:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(behandlungsSchwesterLabel, 0);
        raeumeGrid.Children.Add(behandlungsSchwesterLabel);

        behandlungsraeumeSchwesterTextBox = new TextBox
        {
            Text = KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(behandlungsraeumeSchwesterTextBox, 1);
        raeumeGrid.Children.Add(behandlungsraeumeSchwesterTextBox);

        Label behandlungsSchwesterFlaecheLabel = new() { Content = "Schwester-Zimmer Fläche m²:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(behandlungsSchwesterFlaecheLabel, 3);
        raeumeGrid.Children.Add(behandlungsSchwesterFlaecheLabel);

        behandlungsflaecheSchwesterTextBox = new TextBox
        {
            Text = KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(behandlungsflaecheSchwesterTextBox, 4);
        raeumeGrid.Children.Add(behandlungsflaecheSchwesterTextBox);

        Label behandlungsArztLabel = new() { Content = "Zimmer Arzt:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(behandlungsArztLabel, 6);
        raeumeGrid.Children.Add(behandlungsArztLabel);

        behandlungsraeumeArztTextBox = new TextBox
        {
            Text = KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(behandlungsraeumeArztTextBox, 7);
        raeumeGrid.Children.Add(behandlungsraeumeArztTextBox);

        Label behandlungsArztFlaecheLabel = new() { Content = "Arzt-Zimmer Fläche m²:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(behandlungsArztFlaecheLabel, 9);
        raeumeGrid.Children.Add(behandlungsArztFlaecheLabel);

        behandlungsflaecheArztTextBox = new TextBox
        {
            Text = KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumArztQuadratmeter.ToString(CultureInfo.InvariantCulture),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(behandlungsflaecheArztTextBox, 10);
        raeumeGrid.Children.Add(behandlungsflaecheArztTextBox);
        Grid.SetRow(raeumeGrid, 1);
        eingabeGrid.Children.Add(raeumeGrid);

        // Zeile 3: Zeitraum + Start
        Grid aktionenGrid = new();
        aktionenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        aktionenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        aktionenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        aktionenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Label zeitraumLabel = new() { Content = "Zeitraum:", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(zeitraumLabel, 0);
        aktionenGrid.Children.Add(zeitraumLabel);

        zeitraumComboBox = new ComboBox
        {
            ItemsSource = FinanzVisualisierung.ZeitraumOptionen,
            SelectedItem = "Jahr",
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEditable = false
        };
        Grid.SetColumn(zeitraumComboBox, 1);
        aktionenGrid.Children.Add(zeitraumComboBox);

        Button startenButton = new()
        {
            Content = "Simulation starten",
            Padding = new Thickness(14, 6, 14, 6),
            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold
        };
        startenButton.Click += SimulationStarten_Click;
        Grid.SetColumn(startenButton, 3);
        aktionenGrid.Children.Add(startenButton);
        Grid.SetRow(aktionenGrid, 2);
        eingabeGrid.Children.Add(aktionenGrid);

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

        // TabControl mit zwei Tabs: Finanzen und Hit/Miss
        TabControl tabControl = new();
        
        // Tab 1: Finanzen
        TabItem finanzenTab = new()
        {
            Header = "Finanzen",
            Content = ErstelleFinanzenTab()
        };
        tabControl.Items.Add(finanzenTab);
        
        // Tab 2: Hit/Miss Analyse
        TabItem hitMissTab = new()
        {
            Header = "Hit/Miss Analyse",
            Content = ErstelleHitMissTab()
        };
        tabControl.Items.Add(hitMissTab);
        
        Grid.SetRow(tabControl, 2);
        root.Children.Add(tabControl);

        Content = root;
    }

    public static void StarteFenster()
    {
        // Startpunkt fuer den WPF-Modus der Finanzsimulation.
        try
        {
            Console.WriteLine("--- Starte WPF-Finanzsimulation ---");
            Application app = new();
            app.DispatcherUnhandledException += (s, e) =>
            {
                Console.WriteLine($"Fehler in WPF-Dispatcher: {e.Exception}");
                e.Handled = false;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Console.WriteLine($"Unerwarteter Fehler: {e.ExceptionObject}");
            };
            app.Run(new FinanzWpfFenster());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Starten des WPF-Fensters: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw;
        }
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

            // Rezeption und Verteilung der Behandlungszimmer aus UI einlesen
            if (!TryParseInt(rezeptionTextBox.Text, 0, 80, out int anzahlRezeptionisten, out string rezeptionFehler))
                throw new InvalidOperationException(rezeptionFehler);

            if (!TryParseInt(behandlungsraeumeSchwesterTextBox.Text, 0, 100, out int anzahlSchwesterZimmer, out string schwesterZimmerFehler))
                throw new InvalidOperationException(schwesterZimmerFehler);

            if (!TryParseDouble(behandlungsflaecheSchwesterTextBox.Text, 1.0, 1000.0, out double flaecheSchwester, out string schwesterFlaecheFehler))
                throw new InvalidOperationException(schwesterFlaecheFehler);

            if (!TryParseInt(behandlungsraeumeArztTextBox.Text, 0, 100, out int anzahlArztZimmer, out string arztZimmerFehler))
                throw new InvalidOperationException(arztZimmerFehler);

            if (!TryParseDouble(behandlungsflaecheArztTextBox.Text, 1.0, 1000.0, out double flaecheArzt, out string arztFlaecheFehler))
                throw new InvalidOperationException(arztFlaecheFehler);

            if (anzahlSchwesterZimmer + anzahlArztZimmer <= 0)
                throw new InvalidOperationException("Bitte mindestens ein Behandlungszimmer insgesamt angeben.");

            // Setze globale Konfigurationen, damit Simulation und Auswertung die Werte verwenden
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = anzahlRezeptionisten;
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester = anzahlSchwesterZimmer;
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter = flaecheSchwester;
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt = anzahlArztZimmer;
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumArztQuadratmeter = flaecheArzt;

            string zeitraum = zeitraumComboBox.SelectedItem?.ToString() ?? "Jahr";

            statusTextBlock.Text = "Simulation laeuft...";
            FinanzErgebnis ergebnis = FinanzVisualisierung.Simuliere(anzahlAerzte, anzahlSchwestern, zeitraum);
            (string finanzenPfad, string gewinnPfad) =
                FinanzVisualisierung.ErzeugeDiagramme(ergebnis, anzahlAerzte, anzahlSchwestern);
            
            // Hit/Miss Diagramm erzeugen
            (int anzahlHit, int anzahlMiss, string hitMissPfad) = 
                FinanzVisualisierung.ErzeugeHitMissDiagramm(ergebnis);

            // Textbericht und Bilder werden gemeinsam aktualisiert, damit die Ansicht konsistent bleibt.
            ergebnisTextBox.Text = ErzeugeErgebnisText(ergebnis, finanzenPfad, gewinnPfad);
            finanzenImage.Source = LadeBild(finanzenPfad);
            gewinnImage.Source = LadeBild(gewinnPfad);
            
            // Hit/Miss Tab aktualisieren
            hitMissErgebnisTextBox.Text = ErzeugeHitMissErgebnisText(anzahlHit, anzahlMiss, hitMissPfad);
            hitMissImage.Source = LadeBild(hitMissPfad);
            
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

    private static bool TryParseDouble(string? input, double min, double max, out double value, out string error)
    {
        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !double.TryParse(input, NumberStyles.Float, DeCulture, out value))
        {
            error = $"Bitte eine Zahl zwischen {min.ToString(DeCulture)} und {max.ToString(DeCulture)} eingeben.";
            return false;
        }

        if (value < min || value > max)
        {
            error = $"Wert ausserhalb des Bereichs: erlaubt ist {min.ToString(DeCulture)} bis {max.ToString(DeCulture)}.";
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
