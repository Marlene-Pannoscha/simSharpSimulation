using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;

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

        Grid tabellenGrid = new();
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        tabellenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Border warteschlangenBorder = ErzeugeTabellenContainer("Warteschlangen: Patientenanzahl", out warteschlangenDataGrid);
        Grid.SetRow(warteschlangenBorder, 0);
        tabellenGrid.Children.Add(warteschlangenBorder);

        Border auslastungBorder = ErzeugeTabellenContainer("Auslastung", out auslastungDataGrid);
        Grid.SetRow(auslastungBorder, 2);
        tabellenGrid.Children.Add(auslastungBorder);

        Border bereicheBorder = ErzeugeTabellenContainer("Bereiche: Patientenanzahl", out bereicheDataGrid);
        Grid.SetRow(bereicheBorder, 4);
        tabellenGrid.Children.Add(bereicheBorder);

        Border wartezeitenBorder = ErzeugeTabellenContainer("Wartezeiten je Warteschlange", out wartezeitenDataGrid);
        Grid.SetRow(wartezeitenBorder, 6);
        tabellenGrid.Children.Add(wartezeitenBorder);

        Border behandlungszeitenBorder = ErzeugeTabellenContainer("Behandlungszeit: Ist vs. Erwartet", out behandlungszeitenDataGrid);
        Grid.SetRow(behandlungszeitenBorder, 8);
        tabellenGrid.Children.Add(behandlungszeitenBorder);

        Grid.SetColumn(tabellenGrid, 2);
        Grid.SetRow(tabellenGrid, 0);
        inhaltGrid.Children.Add(tabellenGrid);

        return inhaltGrid;
    }

    private void AktualisiereWartezeitenTab(SimulationsDaten? daten)
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

        WartezeitenAuswertung auswertung = WartezeitenAuswertung.Erzeuge(daten);
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

        TextBlock header = new()
        {
            Text = titel,
            FontWeight = FontWeights.SemiBold,
            FontSize = 16,
            Margin = new Thickness(8, 6, 8, 4)
        };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        dataGrid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(8),
            MinRowHeight = 24
        };
        panel.Children.Add(dataGrid);

        return new Border
        {
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
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
        sb.AppendLine("Min/Avg/Max der Patientenanzahl werden zeitgewichtet aus dem Trace rekonstruiert.");
        sb.AppendLine("Die Warteschlangen zaehlen Patienten ab Eintritt in den jeweiligen Wartebereich bis zum Betreten des Behandlungsbereichs oder Abbruch.");
        sb.AppendLine("Rezeption-Wartezeiten von mehr als 15 Minuten werden als Ausreisser gezaehlt und nicht in Min/Avg/Max der Wartezeit eingerechnet.");
        sb.AppendLine("Die Raumauslastung ist aus Patienten im Zimmer abgeleitet; Raeume begrenzen die Simulation noch nicht als eigene Ressource.");

        return sb.ToString();
    }

    private sealed record WartezeitenAuswertung(
        double AuswertungsdauerMinuten,
        int TraceEreignisse,
        List<AnzahlZeile> Warteschlangen,
        List<AuslastungZeile> Auslastungen,
        List<AnzahlZeile> Bereiche,
        List<WartezeitZeile> Wartezeiten,
        List<BehandlungszeitVergleichZeile> Behandlungszeiten)
    {
        private const double TagesNachlaufPufferMinuten = 180.0;
        private const double RezeptionsAusreisserGrenzeMinuten = 15.0;
        private static double TagesAbstandMinuten => SimulationKonfiguration.SIMULATIONSDAUER + TagesNachlaufPufferMinuten;

        public static WartezeitenAuswertung Erzeuge(SimulationsDaten daten)
        {
            List<TraceEvent> events = daten.TraceData
                .Select(ParseTraceEvent)
                .Where(e => e is not null)
                .Select(e => e!)
                .OrderBy(e => e.GlobalZeit)
                .ThenBy(e => e.Index)
                .ToList();

            string[] zaehlerNamen =
            {
                "Warteschlange Rezeption",
                "Warteschlange Schwester",
                "Warteschlange Arzt",
                "Klinik gesamt",
                "Rezeption",
                "Schwester belegt",
                "Arzt belegt",
                "Schwesterzimmer",
                "Arztzimmer"
            };

            Dictionary<string, HashSet<int>> patientenJeZaehler = zaehlerNamen
                .ToDictionary(name => name, _ => new HashSet<int>(), StringComparer.Ordinal);
            Dictionary<string, AnzahlStatistik> statistiken = zaehlerNamen
                .ToDictionary(name => name, _ => new AnzahlStatistik(), StringComparer.Ordinal);
            Dictionary<string, Dictionary<int, double>> warteStart = new(StringComparer.Ordinal)
            {
                ["Rezeption"] = new Dictionary<int, double>(),
                ["Schwester"] = new Dictionary<int, double>(),
                ["Arzt"] = new Dictionary<int, double>()
            };
            Dictionary<string, List<double>> warteDauern = new(StringComparer.Ordinal)
            {
                ["Rezeption"] = new List<double>(),
                ["Schwester"] = new List<double>(),
                ["Arzt"] = new List<double>()
            };

            double letzteZeit = events.Count > 0 ? events[0].GlobalZeit : 0.0;
            int index = 0;
            foreach (TraceEvent traceEvent in events)
            {
                double dauer = Math.Max(0.0, traceEvent.GlobalZeit - letzteZeit);
                foreach ((string name, AnzahlStatistik statistik) in statistiken)
                    statistik.ErfasseDauer(dauer, patientenJeZaehler[name].Count);

                VerarbeiteEvent(traceEvent, patientenJeZaehler, warteStart, warteDauern);

                foreach ((string name, AnzahlStatistik statistik) in statistiken)
                    statistik.ErfasseMoment(patientenJeZaehler[name].Count);

                letzteZeit = traceEvent.GlobalZeit;
                index++;
            }

            List<AnzahlZeile> warteschlangen = new()
            {
                ErzeugeAnzahlZeile("Rezeption", statistiken["Warteschlange Rezeption"]),
                ErzeugeAnzahlZeile("Schwester", statistiken["Warteschlange Schwester"]),
                ErzeugeAnzahlZeile("Arzt", statistiken["Warteschlange Arzt"])
            };

            List<AnzahlZeile> bereiche = new()
            {
                ErzeugeAnzahlZeile("Klinik gesamt", statistiken["Klinik gesamt"]),
                ErzeugeAnzahlZeile("Rezeption", statistiken["Rezeption"]),
                ErzeugeAnzahlZeile("Schwesterzimmer", statistiken["Schwesterzimmer"]),
                ErzeugeAnzahlZeile("Arztzimmer", statistiken["Arztzimmer"])
            };

            List<AuslastungZeile> auslastungen = new()
            {
                ErzeugeAuslastungZeile(
                    "Aerzte",
                    statistiken["Arzt belegt"],
                    ArztKonfiguration.ANZAHL_AERZTE,
                    daten.ArztBehandlungszeitenMitTermin.Count + daten.ArztBehandlungszeitenOhneTermin.Count,
                    BerechneErwartbarePatientenKapazitaet(ArztKonfiguration.ANZAHL_AERZTE, BerechneMittlereArztBehandlungszeit())),
                ErzeugeAuslastungZeile(
                    "Schwestern",
                    statistiken["Schwester belegt"],
                    SchwesterKonfiguration.ANZAHL_SCHWESTERN,
                    daten.SchwesternBehandlungszeitenMitTermin.Count + daten.SchwesternBehandlungszeitenOhneTermin.Count,
                    BerechneErwartbarePatientenKapazitaet(SchwesterKonfiguration.ANZAHL_SCHWESTERN, BerechneMittlereSchwesterBehandlungszeit())),
                ErzeugeAuslastungZeile(
                    "Arztzimmer",
                    statistiken["Arztzimmer"],
                    KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt,
                    daten.ArztBehandlungszeitenMitTermin.Count + daten.ArztBehandlungszeitenOhneTermin.Count,
                    BerechneErwartbarePatientenKapazitaet(KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt, BerechneMittlereArztBehandlungszeit())),
                ErzeugeAuslastungZeile(
                    "Schwesterzimmer",
                    statistiken["Schwesterzimmer"],
                    KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester,
                    daten.SchwesternBehandlungszeitenMitTermin.Count + daten.SchwesternBehandlungszeitenOhneTermin.Count,
                    BerechneErwartbarePatientenKapazitaet(KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester, BerechneMittlereSchwesterBehandlungszeit()))
            };

            List<WartezeitZeile> wartezeiten = new()
            {
                ErzeugeWartezeitZeile("Rezeption", warteDauern["Rezeption"], RezeptionsAusreisserGrenzeMinuten),
                ErzeugeWartezeitZeile("Schwester", warteDauern["Schwester"]),
                ErzeugeWartezeitZeile("Arzt", warteDauern["Arzt"])
            };

            List<BehandlungszeitVergleichZeile> behandlungszeiten = new()
            {
                ErzeugeBehandlungszeitVergleichZeile(
                    "Rezeption",
                    daten.RezeptionsBehandlungszeitenMitTermin.Concat(daten.RezeptionsBehandlungszeitenOhneTermin).ToList(),
                    RezeptionKonfiguration.MITTELREZEPTIONSZEIT),
                ErzeugeBehandlungszeitVergleichZeile(
                    "Schwester",
                    daten.SchwesternBehandlungszeitenMitTermin.Concat(daten.SchwesternBehandlungszeitenOhneTermin).ToList(),
                    BerechneMittlereSchwesterBehandlungszeit()),
                ErzeugeBehandlungszeitVergleichZeile(
                    "Arzt",
                    daten.ArztBehandlungszeitenMitTermin.Concat(daten.ArztBehandlungszeitenOhneTermin).ToList(),
                    BerechneMittlereArztBehandlungszeit())
            };

            double auswertungsdauer = statistiken.Values.FirstOrDefault()?.Gesamtdauer ?? 0.0;
            return new WartezeitenAuswertung(auswertungsdauer, events.Count, warteschlangen, auslastungen, bereiche, wartezeiten, behandlungszeiten);
        }

        private static void VerarbeiteEvent(
            TraceEvent traceEvent,
            Dictionary<string, HashSet<int>> patientenJeZaehler,
            Dictionary<string, Dictionary<int, double>> warteStart,
            Dictionary<string, List<double>> warteDauern)
        {
            int patientId = traceEvent.PatientId;
            switch (traceEvent.EventTyp)
            {
                case "betritt_klinik":
                    patientenJeZaehler["Klinik gesamt"].Add(patientId);
                    break;
                case "geht_zum_ausgang":
                    BeendeAlleWartezeiten(patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    EntferneAusWarteschlangen(patientId, patientenJeZaehler);
                    break;
                case "verlaesst_klinik":
                    BeendeAlleWartezeiten(patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    EntferneAusAllen(patientId, patientenJeZaehler);
                    break;
                case "betritt_rezeption_warteschlange":
                    patientenJeZaehler["Warteschlange Rezeption"].Add(patientId);
                    BeginneWarten("Rezeption", patientId, traceEvent.GlobalZeit, warteStart);
                    break;
                case "betritt_rezeption":
                    patientenJeZaehler["Warteschlange Rezeption"].Remove(patientId);
                    patientenJeZaehler["Rezeption"].Add(patientId);
                    BeendeWarten("Rezeption", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "beendet_rezeption":
                    patientenJeZaehler["Warteschlange Rezeption"].Remove(patientId);
                    patientenJeZaehler["Rezeption"].Remove(patientId);
                    break;
                case "bricht_ab_wegen_feierabend_rezeption":
                    patientenJeZaehler["Warteschlange Rezeption"].Remove(patientId);
                    patientenJeZaehler["Rezeption"].Remove(patientId);
                    BeendeWarten("Rezeption", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "betritt_wartezimmer":
                case "betritt_schwester_warteschlange":
                    patientenJeZaehler["Warteschlange Schwester"].Add(patientId);
                    BeginneWarten("Schwester", patientId, traceEvent.GlobalZeit, warteStart);
                    break;
                case "verlaesst_wartezimmer":
                    patientenJeZaehler["Warteschlange Schwester"].Remove(patientId);
                    BeendeWarten("Schwester", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "betritt_schwesterzimmer":
                    patientenJeZaehler["Warteschlange Schwester"].Remove(patientId);
                    patientenJeZaehler["Schwesterzimmer"].Add(patientId);
                    BeendeWarten("Schwester", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "geht_zur_schwester":
                    patientenJeZaehler["Schwester belegt"].Add(patientId);
                    break;
                case "beendet_schwester_prozess":
                case "bricht_ab_wegen_feierabend_schwester":
                    patientenJeZaehler["Warteschlange Schwester"].Remove(patientId);
                    patientenJeZaehler["Schwester belegt"].Remove(patientId);
                    patientenJeZaehler["Schwesterzimmer"].Remove(patientId);
                    BeendeWarten("Schwester", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "betritt_wartezimmer_fuer_arzt":
                    patientenJeZaehler["Warteschlange Arzt"].Add(patientId);
                    BeginneWarten("Arzt", patientId, traceEvent.GlobalZeit, warteStart);
                    break;
                case "verlaesst_wartezimmer_fuer_arzt":
                    patientenJeZaehler["Warteschlange Arzt"].Remove(patientId);
                    BeendeWarten("Arzt", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "betritt_arztzimmer":
                    patientenJeZaehler["Warteschlange Arzt"].Remove(patientId);
                    patientenJeZaehler["Arztzimmer"].Add(patientId);
                    BeendeWarten("Arzt", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
                case "geht_zum_arzt":
                    patientenJeZaehler["Arzt belegt"].Add(patientId);
                    break;
                case "beendet_arzt_behandlung":
                case "bricht_ab_wegen_feierabend_arzt":
                    patientenJeZaehler["Warteschlange Arzt"].Remove(patientId);
                    patientenJeZaehler["Arzt belegt"].Remove(patientId);
                    patientenJeZaehler["Arztzimmer"].Remove(patientId);
                    BeendeWarten("Arzt", patientId, traceEvent.GlobalZeit, warteStart, warteDauern);
                    break;
            }
        }

        private static void BeginneWarten(
            string name,
            int patientId,
            double zeitpunkt,
            Dictionary<string, Dictionary<int, double>> warteStart)
        {
            warteStart[name].TryAdd(patientId, zeitpunkt);
        }

        private static void BeendeWarten(
            string name,
            int patientId,
            double zeitpunkt,
            Dictionary<string, Dictionary<int, double>> warteStart,
            Dictionary<string, List<double>> warteDauern)
        {
            if (!warteStart[name].Remove(patientId, out double start))
                return;

            warteDauern[name].Add(Math.Max(0.0, zeitpunkt - start));
        }

        private static void BeendeAlleWartezeiten(
            int patientId,
            double zeitpunkt,
            Dictionary<string, Dictionary<int, double>> warteStart,
            Dictionary<string, List<double>> warteDauern)
        {
            foreach (string name in warteStart.Keys)
            {
                BeendeWarten(name, patientId, zeitpunkt, warteStart, warteDauern);
            }
        }

        private static void EntferneAusWarteschlangen(int patientId, Dictionary<string, HashSet<int>> patientenJeZaehler)
        {
            patientenJeZaehler["Warteschlange Rezeption"].Remove(patientId);
            patientenJeZaehler["Warteschlange Schwester"].Remove(patientId);
            patientenJeZaehler["Warteschlange Arzt"].Remove(patientId);
        }

        private static void EntferneAusAllen(int patientId, Dictionary<string, HashSet<int>> patientenJeZaehler)
        {
            foreach (HashSet<int> patienten in patientenJeZaehler.Values)
                patienten.Remove(patientId);
        }

        private static AnzahlZeile ErzeugeAnzahlZeile(string name, AnzahlStatistik statistik)
        {
            return new AnzahlZeile(
                name,
                statistik.Minimum,
                Math.Round(statistik.Durchschnitt, 2),
                statistik.Maximum);
        }

        private static WartezeitZeile ErzeugeWartezeitZeile(
            string name,
            IReadOnlyList<double> werte,
            double? ausreisserGrenzeMinuten = null)
        {
            List<double> ausgewerteteWerte = ausreisserGrenzeMinuten.HasValue
                ? werte.Where(wert => wert <= ausreisserGrenzeMinuten.Value).ToList()
                : werte.ToList();
            int ausreisserAnzahl = werte.Count - ausgewerteteWerte.Count;

            return new WartezeitZeile(
                name,
                werte.Count,
                "Minuten",
                ausgewerteteWerte.Count,
                ausreisserAnzahl,
                ausreisserGrenzeMinuten.HasValue ? $"> {ausreisserGrenzeMinuten.Value.ToString("N0", DeCulture)} min" : "-",
                Math.Round(ausgewerteteWerte.Count > 0 ? ausgewerteteWerte.Min() : 0.0, 2),
                Math.Round(ausgewerteteWerte.Count > 0 ? ausgewerteteWerte.Average() : 0.0, 2),
                Math.Round(ausgewerteteWerte.Count > 0 ? ausgewerteteWerte.Max() : 0.0, 2));
        }

        private static BehandlungszeitVergleichZeile ErzeugeBehandlungszeitVergleichZeile(
            string name,
            IReadOnlyList<double> tatsaechlicheWerte,
            double erwarteteDauer)
        {
            double durchschnitt = tatsaechlicheWerte.Count > 0 ? tatsaechlicheWerte.Average() : 0.0;
            double differenz = durchschnitt - erwarteteDauer;
            double differenzProzent = erwarteteDauer > 0.0 ? (differenz / erwarteteDauer) * 100.0 : 0.0;

            return new BehandlungszeitVergleichZeile(
                name,
                tatsaechlicheWerte.Count,
                "Minuten",
                Math.Round(durchschnitt, 2),
                Math.Round(erwarteteDauer, 2),
                Math.Round(differenz, 2),
                Math.Round(differenzProzent, 2));
        }

        private static AuslastungZeile ErzeugeAuslastungZeile(
            string name,
            AnzahlStatistik statistik,
            int kapazitaet,
            int behandeltePatienten,
            double erwartbarePatientenKapazitaet)
        {
            double verfuegbareKapazitaetsminuten = kapazitaet * Program.SimulierteArbeitstage * SimulationKonfiguration.SIMULATIONSDAUER;
            double zeitbasierteAuslastung = verfuegbareKapazitaetsminuten > 0.0
                ? (statistik.BelegteMinuten / verfuegbareKapazitaetsminuten) * 100.0
                : 0.0;
            double patientenbasierteAuslastung = erwartbarePatientenKapazitaet > 0.0
                ? (behandeltePatienten / erwartbarePatientenKapazitaet) * 100.0
                : 0.0;

            return new AuslastungZeile(
                name,
                kapazitaet,
                Math.Round(statistik.BelegteMinuten, 2),
                Math.Round(zeitbasierteAuslastung, 2),
                behandeltePatienten,
                Math.Round(erwartbarePatientenKapazitaet, 2),
                Math.Round(patientenbasierteAuslastung, 2));
        }

        private static double BerechneErwartbarePatientenKapazitaet(int kapazitaet, double mittlereDauerMinuten)
        {
            if (kapazitaet <= 0 || mittlereDauerMinuten <= 0.0)
                return 0.0;

            return kapazitaet * Program.SimulierteArbeitstage * SimulationKonfiguration.SIMULATIONSDAUER / mittlereDauerMinuten;
        }

        private static double BerechneMittlereArztBehandlungszeit()
        {
            return PatientenKonfiguration.TYPEN_VERTEILUNG
                .Sum(t => t.Wahrscheinlichkeit * t.BehandlungszeitArzt);
        }

        private static double BerechneMittlereSchwesterBehandlungszeit()
        {
            return PatientenKonfiguration.TYPEN_VERTEILUNG
                .Sum(t => t.Wahrscheinlichkeit * t.BehandlungszeitSchwester);
        }

        private static TraceEvent? ParseTraceEvent(string zeile, int index)
        {
            string[] teile = zeile.Split(';');
            if (teile.Length < 5)
                return null;

            if (!double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double zeit))
                return null;

            if (!int.TryParse(teile[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int patientId))
                return null;

            int tagIndex = Math.Max(0, (patientId - 1) / 10_000);
            return new TraceEvent(index, zeit + (tagIndex * TagesAbstandMinuten), teile[1], patientId);
        }
    }

    private sealed class AnzahlStatistik
    {
        private double gewichteteSumme;

        public int Minimum { get; private set; }
        public int Maximum { get; private set; }
        public double Gesamtdauer { get; private set; }
        public double BelegteMinuten => gewichteteSumme;
        public double Durchschnitt => Gesamtdauer > 0.0 ? gewichteteSumme / Gesamtdauer : 0.0;

        public void ErfasseDauer(double dauer, int wert)
        {
            ErfasseMoment(wert);
            if (dauer <= 0.0)
                return;

            gewichteteSumme += wert * dauer;
            Gesamtdauer += dauer;
        }

        public void ErfasseMoment(int wert)
        {
            Minimum = Math.Min(Minimum, wert);
            Maximum = Math.Max(Maximum, wert);
        }
    }

    private sealed record TraceEvent(int Index, double GlobalZeit, string EventTyp, int PatientId);

    private sealed record AnzahlZeile(string Name, int Minimum, double Durchschnitt, int Maximum);

    private sealed record AuslastungZeile(
        string Name,
        int Kapazitaet,
        double BelegteMinuten,
        double ZeitbasierteAuslastungProzent,
        int BehandeltePatienten,
        double ErwartbarePatientenKapazitaet,
        double PatientenbasierteAuslastungProzent);

    private sealed record WartezeitZeile(
        string Name,
        int AnzahlGesamt,
        string Einheit,
        int AnzahlAusgewertet,
        int AusreisserAnzahl,
        string AusreisserRegel,
        double Minimum,
        double Durchschnitt,
        double Maximum);

    private sealed record BehandlungszeitVergleichZeile(
        string Name,
        int Anzahl,
        string Einheit,
        double TatsaechlichDurchschnittMinuten,
        double ErwartetMinuten,
        double AbweichungMinuten,
        double AbweichungProzent);
}
