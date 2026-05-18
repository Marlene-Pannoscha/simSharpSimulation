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
    private readonly TextBox wartezimmerflaecheTextBox;
    private readonly ComboBox zeitraumComboBox;
    private TextBox ergebnisTextBox = null!;
    private Image finanzenImage = null!;
    private Image gewinnImage = null!;
    private readonly TextBlock statusTextBlock;
    
    // Hit/Miss Tab-Steuerelemente
    private TextBox hitMissErgebnisTextBox = null!;
    private Image hitMissImage = null!;

    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");
    private const double ErgebnisSpaltenBreite = 360;
    private const double SpaltenAbstand = 12;

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

        Grid eingabeGrid = new()
        {
            Margin = new Thickness(0, 0, 0, 4)
        };
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });

        Grid personalGrid = ErzeugeParameterGrid();
        aerzteTextBox = FuegeParameterZeile(personalGrid, "Aerzte", ArztKonfiguration.ANZAHL_AERZTE.ToString(CultureInfo.InvariantCulture));
        schwesternTextBox = FuegeParameterZeile(personalGrid, "Schwestern", SchwesterKonfiguration.ANZAHL_SCHWESTERN.ToString(CultureInfo.InvariantCulture));
        rezeptionTextBox = FuegeParameterZeile(personalGrid, "Rezeption", RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN.ToString(CultureInfo.InvariantCulture));
        Border personalBox = ErzeugeParameterGruppe("Personal", personalGrid);
        Grid.SetColumn(personalBox, 0);
        eingabeGrid.Children.Add(personalBox);

        Grid raeumeGrid = ErzeugeParameterGrid();
        behandlungsraeumeSchwesterTextBox = FuegeParameterZeile(
            raeumeGrid,
            "Schwesterzimmer",
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester.ToString(CultureInfo.InvariantCulture));
        behandlungsflaecheSchwesterTextBox = FuegeParameterZeile(
            raeumeGrid,
            "Flaeche je Schwesterzimmer (m2)",
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter.ToString(CultureInfo.InvariantCulture));
        behandlungsraeumeArztTextBox = FuegeParameterZeile(
            raeumeGrid,
            "Arztzimmer",
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt.ToString(CultureInfo.InvariantCulture));
        behandlungsflaecheArztTextBox = FuegeParameterZeile(
            raeumeGrid,
            "Flaeche je Arztzimmer (m2)",
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumArztQuadratmeter.ToString(CultureInfo.InvariantCulture));
        wartezimmerflaecheTextBox = FuegeParameterZeile(
            raeumeGrid,
            "Wartezimmerflaeche (m2)",
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheWartezimmerQuadratmeter.ToString(CultureInfo.InvariantCulture));
        Border raeumeBox = ErzeugeParameterGruppe("Raeume und Flaechen", raeumeGrid);
        Grid.SetColumn(raeumeBox, 2);
        eingabeGrid.Children.Add(raeumeBox);

        Grid aktionenGrid = ErzeugeParameterGrid();

        zeitraumComboBox = new ComboBox
        {
            ItemsSource = FinanzVisualisierung.ZeitraumOptionen,
            SelectedItem = "Jahr",
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEditable = false,
            MinHeight = 30,
            Padding = new Thickness(8, 2, 8, 2)
        };
        FuegeParameterZeile(aktionenGrid, "Zeitraum", zeitraumComboBox);

        Button startenButton = new()
        {
            Content = "Simulation starten",
            Padding = new Thickness(14, 7, 14, 7),
            Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 0)
        };
        startenButton.Click += SimulationStarten_Click;
        aktionenGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(startenButton, aktionenGrid.RowDefinitions.Count - 1);
        Grid.SetColumn(startenButton, 0);
        Grid.SetColumnSpan(startenButton, 2);
        aktionenGrid.Children.Add(startenButton);

        Border aktionenBox = ErzeugeParameterGruppe("Simulation", aktionenGrid);
        Grid.SetColumn(aktionenBox, 4);
        eingabeGrid.Children.Add(aktionenBox);

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

            if (!TryParseDouble(wartezimmerflaecheTextBox.Text, 1.0, 1000.0, out double flaecheWartezimmer, out string wartezimmerFlaecheFehler))
                throw new InvalidOperationException(wartezimmerFlaecheFehler);

            if (anzahlSchwesterZimmer + anzahlArztZimmer <= 0)
                throw new InvalidOperationException("Bitte mindestens ein Behandlungszimmer insgesamt angeben.");

            // Setze globale Konfigurationen, damit Simulation und Auswertung die Werte verwenden
            ArztKonfiguration.ANZAHL_AERZTE = anzahlAerzte;
            SchwesterKonfiguration.ANZAHL_SCHWESTERN = anzahlSchwestern;
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN = anzahlRezeptionisten;
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester = anzahlSchwesterZimmer;
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter = flaecheSchwester;
            KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt = anzahlArztZimmer;
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheBehandlungsraumArztQuadratmeter = flaecheArzt;
            KonfigurationJsonExport.Finanzen.Fixkosten.FlaecheWartezimmerQuadratmeter = flaecheWartezimmer;

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

    private static Grid ErzeugeGeteiltesTabGrid()
    {
        Grid inhaltGrid = new();
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ErgebnisSpaltenBreite) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SpaltenAbstand) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        return inhaltGrid;
    }

    private static TextBox ErzeugeErgebnisTextBox()
    {
        return new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            Padding = new Thickness(8)
        };
    }

    private static Border ErzeugeParameterGruppe(string titel, UIElement inhalt)
    {
        DockPanel panel = new();

        TextBlock header = new()
        {
            Text = titel,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(inhalt);

        return new Border
        {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Background = Brushes.WhiteSmoke,
            Child = panel
        };
    }

    private static Grid ErzeugeParameterGrid()
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        return grid;
    }

    private static TextBox FuegeParameterZeile(Grid grid, string labelText, string wert)
    {
        TextBox textBox = ErzeugeParameterTextBox(wert);
        FuegeParameterZeile(grid, labelText, textBox);
        return textBox;
    }

    private static void FuegeParameterZeile(Grid grid, string labelText, Control eingabe)
    {
        int row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock label = new()
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 3, 12, 3)
        };
        Grid.SetRow(label, row);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        eingabe.Margin = new Thickness(0, 3, 0, 3);
        Grid.SetRow(eingabe, row);
        Grid.SetColumn(eingabe, 1);
        grid.Children.Add(eingabe);
    }

    private static TextBox ErzeugeParameterTextBox(string wert)
    {
        return new TextBox
        {
            Text = wert,
            MinHeight = 30,
            Padding = new Thickness(8, 3, 8, 3),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center
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
