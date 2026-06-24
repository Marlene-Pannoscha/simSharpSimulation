using System.Diagnostics;
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
    private TextBox ergebnisTextBox = new TextBox();
    private TextBlock breakEvenTicker = new TextBlock();
    private Image finanzenImage = new Image();
    private Image gewinnImage = new Image();
    private Image kostenstrukturImage = new Image();

    private TextBox aerzteTextBox = new TextBox();
    private TextBox schwesternTextBox = new TextBox();
    private TextBox rezeptionTextBox = new TextBox();
    private TextBox behandlungsraeumeSchwesterTextBox = new TextBox();
    private TextBox behandlungsflaecheSchwesterTextBox = new TextBox();
    private TextBox behandlungsraeumeArztTextBox = new TextBox();
    private TextBox behandlungsflaecheArztTextBox = new TextBox();
    private TextBox wartezimmerflaecheTextBox = new TextBox();
    private TextBox infrastrukturProTagTextBox = new TextBox();
    private TextBox itUndVerwaltungProTagTextBox = new TextBox();
    private TextBox versicherungenProTagTextBox = new TextBox();
    private TextBox energiekostenProQmProMonatTextBox = new TextBox();
    private TextBox reinigungskostenProQmProMonatTextBox = new TextBox();
    private TextBox materialProPatientTextBox = new TextBox();
    private TextBox geraeteLeasingProTagTextBox = new TextBox();
    private TextBox geraeteWartungProTagTextBox = new TextBox();
    private ComboBox zeitraumComboBox = new ComboBox();

    private Button? exportButton;
    private Button? exportFinanzenButton;

    private readonly TextBlock statusTextBlock;
    // Kennzahlen Anzeige (wird im Konfiguration-Tab angezeigt)
    private TextBlock raeumeKurzinfoTextBlock = new TextBlock();
    private TextBox mietkostenProQmTextBox = null!;
    private TextBox gesamtFlaecheTextBox = null!;
    private TextBox gesamtMietkostenTextBox = null!;
    
    // Hit/Miss Tab-Steuerelemente
    private TextBox hitMissErgebnisTextBox = null!;
    private Image hitMissImage = null!;
    private TextBox simulationsUebersichtTextBox = null!;
    private TextBox wartezeitenTextBox = null!;
    private DataGrid warteschlangenDataGrid = null!;
    private DataGrid bereicheDataGrid = null!;
    private DataGrid wartezeitenDataGrid = null!;
    private DataGrid auslastungDataGrid = null!;
    private DataGrid behandlungszeitenDataGrid = null!;

    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");
    private const double ErgebnisSpaltenBreite = 360;
    private const double SpaltenAbstand = 12;
    private static readonly Brush FensterHintergrund = new SolidColorBrush(Color.FromRgb(246, 248, 251));
    private static readonly Brush FlaechenHintergrund = Brushes.White;
    private static readonly Brush DezenteFlaeche = new SolidColorBrush(Color.FromRgb(241, 245, 249));
    private static readonly Brush RandFarbe = new SolidColorBrush(Color.FromRgb(214, 221, 230));
    private static readonly Brush TextFarbe = new SolidColorBrush(Color.FromRgb(31, 41, 55));
    private static readonly Brush SekundaerTextFarbe = new SolidColorBrush(Color.FromRgb(88, 99, 113));
    private static readonly Brush AkzentFarbe = new SolidColorBrush(Color.FromRgb(37, 99, 235));

    public FinanzWpfFenster()
    {
        // Baut das komplette WPF-Fenster programmatisch ohne separate XAML-Datei auf.
        Title = "Arztpraxis Finanzsimulation (WPF)";
        Width = 1400;
        Height = 900;
        MinWidth = 1100;
        MinHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = FensterHintergrund;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        Grid root = new();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Margin = new Thickness(14);

        Grid eingabeGrid = new()
        {
            Margin = new Thickness(0, 0, 0, 6),
            MinWidth = 1040
        };
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        eingabeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

        Grid personalGrid = ErzeugeParameterGrid();
        aerzteTextBox = FuegeParameterZeile(personalGrid, "Aerzte", ArztKonfiguration.ANZAHL_AERZTE.ToString(CultureInfo.InvariantCulture));
        schwesternTextBox = FuegeParameterZeile(personalGrid, "Schwestern", SchwesterKonfiguration.ANZAHL_SCHWESTERN.ToString(CultureInfo.InvariantCulture));
        rezeptionTextBox = FuegeParameterZeile(personalGrid, "Rezeption", RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN.ToString(CultureInfo.InvariantCulture));
        Border personalBox = ErzeugeParameterGruppe("Personal", personalGrid);
        Grid.SetColumn(personalBox, 0);
        eingabeGrid.Children.Add(personalBox);

        UIElement konfigurationTabInhalt = ErstelleKonfigurationTab();

        raeumeKurzinfoTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = TextFarbe,
            LineHeight = 20
        };
        Border raeumeKurzinfoBox = ErzeugeParameterGruppe("Raeume und Kosten", raeumeKurzinfoTextBlock);
        Grid.SetColumn(raeumeKurzinfoBox, 2);
        eingabeGrid.Children.Add(raeumeKurzinfoBox);
        AktualisiereRaeumeKurzinfo();

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
            Background = AkzentFarbe,
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 12, 0, 0),
            BorderThickness = new Thickness(0),
            MinHeight = 34
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

        ScrollViewer eingabeScrollViewer = new()
        {
            Content = eingabeGrid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(eingabeScrollViewer, 0);
        root.Children.Add(eingabeScrollViewer);

        statusTextBlock = new TextBlock
        {
            Text = "Bereit.",
            Margin = new Thickness(2, 8, 0, 10),
            Foreground = SekundaerTextFarbe
        };
        Grid.SetRow(statusTextBlock, 1);
        root.Children.Add(statusTextBlock);

        // TabControl mit Tabs: Uebersicht, Finanzen, Hit/Miss, Wartezeiten, Prognose
        TabControl tabControl = new()
        {
            Background = Brushes.Transparent,
            BorderBrush = RandFarbe,
            BorderThickness = new Thickness(1)
        };

        TabItem uebersichtTab = new()
        {
            Header = "Uebersicht",
            Content = ErstelleSimulationsUebersichtTab()
        };
        tabControl.Items.Add(uebersichtTab);

        TabItem konfigurationTab = new()
        {
            Header = "Konfiguration",
            Content = konfigurationTabInhalt
        };
        tabControl.Items.Add(konfigurationTab);
        
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

        TabItem wartezeitenTab = new()
        {
            Header = "Wartezeiten",
            Content = ErstelleWartezeitenTab()
        };
        tabControl.Items.Add(wartezeitenTab);

        TabItem prognoseTab = new()
        {
            Header = "Prognose",
            Content = ErstellePrognoseTab()
        };
        tabControl.Items.Add(prognoseTab);
        
        Grid.SetRow(tabControl, 2);
        root.Children.Add(tabControl);

        Content = root;

        AktualisiereSimulationsUebersicht(null);
        AktualisiereWartezeitenTab(null);
        AktualisierePrognoseTab();
    }

    private UIElement ErstelleKonfigurationTab()
    {
        Grid inhaltGrid = new()
        {
            Margin = new Thickness(10)
        };
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        inhaltGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        inhaltGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock hinweisText = new()
        {
            Text = "Raeume und Flaechen koennen hier angepasst werden. Die Kurzuebersicht oben aktualisiert sich automatisch.",
            Foreground = SekundaerTextFarbe,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 2, 0)
        };
        Grid.SetRow(hinweisText, 0);
        Grid.SetColumnSpan(hinweisText, 3);
        inhaltGrid.Children.Add(hinweisText);

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

        gesamtFlaecheTextBox = ErzeugeKennzahlTextBox();
        FuegeParameterZeile(raeumeGrid, "Gesamtflaeche", gesamtFlaecheTextBox);

        Border raeumeBox = ErzeugeParameterGruppe("Raeume und Flaechen", raeumeGrid);
        Grid.SetRow(raeumeBox, 2);
        Grid.SetColumn(raeumeBox, 0);
        inhaltGrid.Children.Add(raeumeBox);

        Grid kostenGrid = ErzeugeParameterGrid();
        infrastrukturProTagTextBox = FuegeParameterZeile(
            kostenGrid,
            "Infrastruktur pro Tag",
            KonfigurationJsonExport.Finanzen.Fixkosten.InfrastrukturProTag.ToString(CultureInfo.InvariantCulture));
        itUndVerwaltungProTagTextBox = FuegeParameterZeile(
            kostenGrid,
            "IT und Verwaltung pro Tag",
            KonfigurationJsonExport.Finanzen.Fixkosten.ITUndVerwaltungProTag.ToString(CultureInfo.InvariantCulture));
        versicherungenProTagTextBox = FuegeParameterZeile(
            kostenGrid,
            "Versicherungen pro Tag",
            KonfigurationJsonExport.Finanzen.Fixkosten.VersicherungenProTag.ToString(CultureInfo.InvariantCulture));
        energiekostenProQmProMonatTextBox = FuegeParameterZeile(
            kostenGrid,
            "Energie pro m2/Monat",
            KonfigurationJsonExport.Finanzen.Fixkosten.EnergiekostenProQmProMonat.ToString(CultureInfo.InvariantCulture));
        reinigungskostenProQmProMonatTextBox = FuegeParameterZeile(
            kostenGrid,
            "Reinigung pro m2/Monat",
            KonfigurationJsonExport.Finanzen.Fixkosten.ReinigungskostenProQmProMonat.ToString(CultureInfo.InvariantCulture));
        materialProPatientTextBox = FuegeParameterZeile(
            kostenGrid,
            "Medizinisches Material pro Patient",
            KonfigurationJsonExport.Finanzen.VariableKosten.MedizinischesMaterialProPatient.ToString(CultureInfo.InvariantCulture));
        geraeteLeasingProTagTextBox = FuegeParameterZeile(
            kostenGrid,
            "Geraete-Leasing pro Tag",
            KonfigurationJsonExport.Finanzen.Fixkosten.GeraeteLeasingProTag.ToString(CultureInfo.InvariantCulture));
        geraeteWartungProTagTextBox = FuegeParameterZeile(
            kostenGrid,
            "Geraete-Wartung pro Tag",
            KonfigurationJsonExport.Finanzen.Fixkosten.GeraeteWartungProTag.ToString(CultureInfo.InvariantCulture));

        mietkostenProQmTextBox = ErzeugeKennzahlTextBox();
        FuegeParameterZeile(kostenGrid, "Mietkosten pro m2/Monat", mietkostenProQmTextBox);

        gesamtMietkostenTextBox = ErzeugeKennzahlTextBox();
        FuegeParameterZeile(kostenGrid, "Gesamtmietkosten pro Tag", gesamtMietkostenTextBox);

        Border kostenBox = ErzeugeParameterGruppe("Fixkosten und Kennzahlen", kostenGrid);
        Grid.SetRow(kostenBox, 2);
        Grid.SetColumn(kostenBox, 2);
        inhaltGrid.Children.Add(kostenBox);

        RegistriereRaeumeKurzinfoAktualisierung();
        AktualisiereRaeumeKurzinfo();

        return new ScrollViewer
        {
            Content = inhaltGrid,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private void RegistriereRaeumeKurzinfoAktualisierung()
    {
        TextChangedEventHandler handler = (_, _) => AktualisiereRaeumeKurzinfo();
        behandlungsraeumeSchwesterTextBox.TextChanged += handler;
        behandlungsflaecheSchwesterTextBox.TextChanged += handler;
        behandlungsraeumeArztTextBox.TextChanged += handler;
        behandlungsflaecheArztTextBox.TextChanged += handler;
        wartezimmerflaecheTextBox.TextChanged += handler;
        infrastrukturProTagTextBox.TextChanged += handler;
        itUndVerwaltungProTagTextBox.TextChanged += handler;
        versicherungenProTagTextBox.TextChanged += handler;
        energiekostenProQmProMonatTextBox.TextChanged += handler;
        reinigungskostenProQmProMonatTextBox.TextChanged += handler;
        materialProPatientTextBox.TextChanged += handler;
        geraeteLeasingProTagTextBox.TextChanged += handler;
        geraeteWartungProTagTextBox.TextChanged += handler;
    }

    private void AktualisiereRaeumeKurzinfo()
    {
        if (!TryParseKurzinfoInt(behandlungsraeumeSchwesterTextBox.Text, out int schwesterZimmer) ||
            !TryParseKurzinfoDouble(behandlungsflaecheSchwesterTextBox.Text, out double flaecheSchwester) ||
            !TryParseKurzinfoInt(behandlungsraeumeArztTextBox.Text, out int arztZimmer) ||
            !TryParseKurzinfoDouble(behandlungsflaecheArztTextBox.Text, out double flaecheArzt) ||
            !TryParseKurzinfoDouble(wartezimmerflaecheTextBox.Text, out double flaecheWartezimmer) ||
            !TryParseKurzinfoDouble(infrastrukturProTagTextBox.Text, out double infrastrukturProTag) ||
            !TryParseKurzinfoDouble(itUndVerwaltungProTagTextBox.Text, out double itVerwaltungProTag) ||
            !TryParseKurzinfoDouble(versicherungenProTagTextBox.Text, out double versicherungenProTag) ||
            !TryParseKurzinfoDouble(energiekostenProQmProMonatTextBox.Text, out double energieProQmProMonat) ||
            !TryParseKurzinfoDouble(reinigungskostenProQmProMonatTextBox.Text, out double reinigungProQmProMonat) ||
            !TryParseKurzinfoDouble(materialProPatientTextBox.Text, out double materialProPatient) ||
            !TryParseKurzinfoDouble(geraeteLeasingProTagTextBox.Text, out double leasingProTag) ||
            !TryParseKurzinfoDouble(geraeteWartungProTagTextBox.Text, out double wartungProTag))
        {
            raeumeKurzinfoTextBlock.Text = "Konfiguration unvollstaendig.\nDetails im Tab Konfiguration pruefen.";
            return;
        }

        double gesamtFlaeche = schwesterZimmer * flaecheSchwester
            + arztZimmer * flaecheArzt
            + flaecheWartezimmer;
        double mietkostenProQm = FinanzRechner.GetMietkostenProQuadratmeterProMonat(gesamtFlaeche);
        double gesamtMietkostenProTag = (mietkostenProQm * gesamtFlaeche * 12) / 365.0;
        double energieProTag = (energieProQmProMonat * gesamtFlaeche * 12) / 365.0;
        double reinigungProTag = (reinigungProQmProMonat * gesamtFlaeche * 12) / 365.0;

        if (gesamtFlaecheTextBox is not null)
            gesamtFlaecheTextBox.Text = gesamtFlaeche.ToString("N2", DeCulture) + " m2";
        if (mietkostenProQmTextBox is not null)
            mietkostenProQmTextBox.Text = FinanzVisualisierung.FormatEuro(mietkostenProQm);
        if (gesamtMietkostenTextBox is not null)
            gesamtMietkostenTextBox.Text = FinanzVisualisierung.FormatEuro(gesamtMietkostenProTag);

        raeumeKurzinfoTextBlock.Text =
            $"Raeume: {schwesterZimmer.ToString("N0", DeCulture)} Schwesterzimmer, {arztZimmer.ToString("N0", DeCulture)} Arztzimmer\n" +
            $"Flaeche: {gesamtFlaeche.ToString("N2", DeCulture)} m2\n" +
            $"Miete/Tag: {FinanzVisualisierung.FormatEuro(gesamtMietkostenProTag)}\n" +
            $"Energie + Reinigung/Tag: {FinanzVisualisierung.FormatEuro(energieProTag + reinigungProTag)}\n" +
            $"Infrastruktur + IT + Versicherung/Tag: {FinanzVisualisierung.FormatEuro(infrastrukturProTag + itVerwaltungProTag + versicherungenProTag)}\n" +
            $"Leasing + Wartung/Tag: {FinanzVisualisierung.FormatEuro(leasingProTag + wartungProTag)}\n" +
            $"Material/Patient: {FinanzVisualisierung.FormatEuro(materialProPatient)}\n" +
            "Details im Tab Konfiguration anpassen.";
    }

    private static bool TryParseKurzinfoInt(string? input, out int value)
    {
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ||
            int.TryParse(input, NumberStyles.Integer, DeCulture, out value);
    }

    private static bool TryParseKurzinfoDouble(string? input, out double value)
    {
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            double.TryParse(input, NumberStyles.Float, DeCulture, out value);
    }

    private static TextBox ErzeugeKennzahlTextBox()
    {
        TextBox textBox = ErzeugeParameterTextBox(string.Empty);
        textBox.IsReadOnly = true;
        textBox.BorderThickness = new Thickness(0);
        textBox.Background = Brushes.Transparent;
        return textBox;
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

            if (!TryParseDouble(infrastrukturProTagTextBox.Text, 0.0, 100000.0, out double infrastrukturProTag, out string infrastrukturFehler))
                throw new InvalidOperationException(infrastrukturFehler);

            if (!TryParseDouble(itUndVerwaltungProTagTextBox.Text, 0.0, 100000.0, out double itUndVerwaltungProTag, out string itVerwaltungFehler))
                throw new InvalidOperationException(itVerwaltungFehler);

            if (!TryParseDouble(versicherungenProTagTextBox.Text, 0.0, 100000.0, out double versicherungenProTag, out string versicherungenFehler))
                throw new InvalidOperationException(versicherungenFehler);

            if (!TryParseDouble(energiekostenProQmProMonatTextBox.Text, 0.0, 10000.0, out double energieProQmProMonat, out string energieFehler))
                throw new InvalidOperationException(energieFehler);

            if (!TryParseDouble(reinigungskostenProQmProMonatTextBox.Text, 0.0, 10000.0, out double reinigungProQmProMonat, out string reinigungFehler))
                throw new InvalidOperationException(reinigungFehler);

            if (!TryParseDouble(materialProPatientTextBox.Text, 0.0, 100000.0, out double materialProPatient, out string materialFehler))
                throw new InvalidOperationException(materialFehler);

            if (!TryParseDouble(geraeteLeasingProTagTextBox.Text, 0.0, 100000.0, out double geraeteLeasingProTag, out string leasingFehler))
                throw new InvalidOperationException(leasingFehler);

            if (!TryParseDouble(geraeteWartungProTagTextBox.Text, 0.0, 100000.0, out double geraeteWartungProTag, out string wartungFehler))
                throw new InvalidOperationException(wartungFehler);

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
            KonfigurationJsonExport.Finanzen.Fixkosten.InfrastrukturProTag = infrastrukturProTag;
            KonfigurationJsonExport.Finanzen.Fixkosten.ITUndVerwaltungProTag = itUndVerwaltungProTag;
            KonfigurationJsonExport.Finanzen.Fixkosten.VersicherungenProTag = versicherungenProTag;
            KonfigurationJsonExport.Finanzen.Fixkosten.EnergiekostenProQmProMonat = energieProQmProMonat;
            KonfigurationJsonExport.Finanzen.Fixkosten.ReinigungskostenProQmProMonat = reinigungProQmProMonat;
            KonfigurationJsonExport.Finanzen.VariableKosten.MedizinischesMaterialProPatient = materialProPatient;
            KonfigurationJsonExport.Finanzen.Fixkosten.GeraeteLeasingProTag = geraeteLeasingProTag;
            KonfigurationJsonExport.Finanzen.Fixkosten.GeraeteWartungProTag = geraeteWartungProTag;

            string zeitraum = zeitraumComboBox.SelectedItem?.ToString() ?? "Jahr";

            statusTextBlock.Text = "Simulation laeuft...";
            FinanzErgebnis ergebnis = FinanzVisualisierung.Simuliere(anzahlAerzte, anzahlSchwestern, zeitraum);
            (string finanzenPfad, string gewinnPfad, string kostenstrukturPfad) =
                FinanzVisualisierung.ErzeugeDiagramme(ergebnis, anzahlAerzte, anzahlSchwestern);
            
            // Hit/Miss Diagramm erzeugen
            (int anzahlHit, int anzahlMiss, string hitMissPfad) = 
                FinanzVisualisierung.ErzeugeHitMissDiagramm(ergebnis);

            SimulationsDaten simulationsDaten = new();
            PatientenProzess patientenProzess = new(SimulationKonfiguration.RANDOM_SEED, simulationsDaten);
            Stopwatch simulationsStoppuhr = Stopwatch.StartNew();
            patientenProzess.FuehreAus();
            simulationsStoppuhr.Stop();
            string simulationszeit = Program.FormatiereDauer(simulationsStoppuhr.Elapsed);
            Console.WriteLine($"Reine Simulationszeit (ohne Diagramm- und Dateierzeugung): {simulationszeit}");
            simulationsDaten.SchreibePrognoseReport("prognose_report.txt");
            simulationsDaten.SchreibePrognoseDatenJson("prognose_daten.json");

            // Textbericht und Bilder werden gemeinsam aktualisiert, damit die Ansicht konsistent bleibt.
            if (ergebnisTextBox != null)
            {
                ergebnisTextBox.Text = FinanzVisualisierung.GenerateErgebnisReportText(ergebnis, finanzenPfad, gewinnPfad, kostenstrukturPfad);
            }
            if (breakEvenTicker != null)
            {
                breakEvenTicker.Text = FinanzVisualisierung.FormatBreakEven(ergebnis.BreakEven, ergebnis.DurchschnittBehandeltePatientenProTag);
            }
            if (finanzenImage != null)
            {
                finanzenImage.Source = LadeBild(finanzenPfad);
            }
            if (gewinnImage != null)
            {
                gewinnImage.Source = LadeBild(gewinnPfad);
            }
            if (kostenstrukturImage != null)
            {
                kostenstrukturImage.Source = LadeBild(kostenstrukturPfad);
            }

            // Aktualisiere die Kennzahlen-Anzeige im Eingabebereich
            try
            {
                mietkostenProQmTextBox.Text = FinanzVisualisierung.FormatEuro(ergebnis.MietkostenProQm);
                gesamtFlaecheTextBox.Text = ergebnis.Gesamtflaeche.ToString("N2", DeCulture) + " m2";
                gesamtMietkostenTextBox.Text = FinanzVisualisierung.FormatEuro(ergebnis.GesamtMietkostenProTag);
                AktualisiereRaeumeKurzinfo();
            }
            catch
            {
                // Ignoriere UI-Update-Fehler (sollte nicht passieren)
            }
            
            // Hit/Miss Tab aktualisieren
            hitMissErgebnisTextBox.Text = ErzeugeHitMissErgebnisText(anzahlHit, anzahlMiss, hitMissPfad);
            hitMissImage.Source = LadeBild(hitMissPfad);

            AktualisiereWartezeitenTab(simulationsDaten);
            AktualisiereSimulationsUebersicht(simulationsDaten);
            AktualisierePrognoseTab();
            
            statusTextBlock.Text = $"Simulation erfolgreich abgeschlossen. Reine Simulationszeit: {simulationszeit}";
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
            FontSize = 16,
            Foreground = TextFarbe,
            Margin = new Thickness(12, 10, 12, 6)
        };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        image = new Image
        {
            Stretch = Stretch.Uniform,
            Margin = new Thickness(12)
        };
        panel.Children.Add(image);

        return new Border
        {
            BorderBrush = RandFarbe,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Background = FlaechenHintergrund,
            Child = panel
        };
    }

    private static Grid ErzeugeGeteiltesTabGrid()
    {
        Grid inhaltGrid = new();
        inhaltGrid.Margin = new Thickness(10);
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
            Padding = new Thickness(10),
            Background = FlaechenHintergrund,
            Foreground = TextFarbe,
            BorderBrush = RandFarbe,
            BorderThickness = new Thickness(1)
        };
    }

    private static Border ErzeugeParameterGruppe(string titel, UIElement inhalt)
    {
        DockPanel panel = new();

        TextBlock header = new()
        {
            Text = titel,
            FontWeight = FontWeights.SemiBold,
            Foreground = SekundaerTextFarbe,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);
        panel.Children.Add(inhalt);

        return new Border
        {
            BorderBrush = RandFarbe,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Background = FlaechenHintergrund,
            Child = panel
        };
    }

    private static Grid ErzeugeParameterGrid()
    {
        Grid grid = new();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
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
            Foreground = TextFarbe,
            TextWrapping = TextWrapping.Wrap,
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
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.White,
            BorderBrush = RandFarbe
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
