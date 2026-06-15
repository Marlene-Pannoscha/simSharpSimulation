using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 15 - Verteilung der Auslastung fuer Personal und Behandlungszimmer.
    internal static partial class GenerateDiagramme
    {
        private const double TagesNachlaufPufferMinuten = 180.0;

        private static void ErzeugeAuslastungsVerteilungsDiagramm(
            IReadOnlyList<string> traceData,
            IReadOnlyDictionary<PatientenTyp, List<double>> arztBehandlungszeitenNachTyp,
            IReadOnlyDictionary<PatientenTyp, List<double>> schwesternBehandlungszeitenNachTyp,
            IReadOnlyList<double> rezeptionsBehandlungszeiten,
            double simulationsdauer,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            Dictionary<string, BelegungsStatistik> belegungen = BerechneBelegungen(traceData);
            int arztPatienten = arztBehandlungszeitenNachTyp.Values.Sum(werte => werte.Count);
            int schwesterPatienten = schwesternBehandlungszeitenNachTyp.Values.Sum(werte => werte.Count);
            int rezeptionPatienten = rezeptionsBehandlungszeiten.Count;

            List<AuslastungPunkt> auslastungen = new()
            {
                ErzeugeAuslastungPunkt(
                    "Rezeption",
                    belegungen["Rezeption"].BelegteMinuten,
                    RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
                    rezeptionPatienten,
                    BerechneErwartbarePatientenKapazitaet(
                        RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
                        RezeptionKonfiguration.MITTELREZEPTIONSZEIT,
                        simulationsdauer),
                    simulationsdauer),
                ErzeugeAuslastungPunkt(
                    "Aerzte",
                    belegungen["Arzt belegt"].BelegteMinuten,
                    anzahlAerzte,
                    arztPatienten,
                    BerechneErwartbarePatientenKapazitaet(anzahlAerzte, BerechneMittlereArztBehandlungszeit(), simulationsdauer),
                    simulationsdauer),
                ErzeugeAuslastungPunkt(
                    "Schwestern",
                    belegungen["Schwester belegt"].BelegteMinuten,
                    anzahlSchwestern,
                    schwesterPatienten,
                    BerechneErwartbarePatientenKapazitaet(anzahlSchwestern, BerechneMittlereSchwesterBehandlungszeit(), simulationsdauer),
                    simulationsdauer),
                ErzeugeAuslastungPunkt(
                    "Arztzimmer",
                    belegungen["Arztzimmer"].BelegteMinuten,
                    KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt,
                    arztPatienten,
                    BerechneErwartbarePatientenKapazitaet(
                        KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt,
                        BerechneMittlereArztBehandlungszeit(),
                        simulationsdauer),
                    simulationsdauer),
                ErzeugeAuslastungPunkt(
                    "Schwesterzimmer",
                    belegungen["Schwesterzimmer"].BelegteMinuten,
                    KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester,
                    schwesterPatienten,
                    BerechneErwartbarePatientenKapazitaet(
                        KonfigurationJsonExport.Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester,
                        BerechneMittlereSchwesterBehandlungszeit(),
                        simulationsdauer),
                    simulationsdauer)
            };

            var plot = new ScottPlot.Plot(1200, 620);
            const float titelSchriftgroesse = 32;
            const float achsenTitelSchriftgroesse = 18;
            const float achsenTickSchriftgroesse = 16;
            const float legendenSchriftgroesse = 18;
            const float prozentwertSchriftgroesse = 15;
            double[] xs = Enumerable.Range(0, auslastungen.Count).Select(i => (double)i).ToArray();
            double[] zeitbasiert = auslastungen.Select(a => a.ZeitbasierteAuslastungProzent).ToArray();
            double[] patientenbasiert = auslastungen.Select(a => a.PatientenbasierteAuslastungProzent).ToArray();
            string[] labels = auslastungen.Select(a => a.Name).ToArray();

            var zeitBalken = plot.AddBar(zeitbasiert, xs.Select(x => x - 0.18).ToArray());
            zeitBalken.BarWidth = 0.34;
            zeitBalken.FillColor = Color.SteelBlue;
            zeitBalken.BorderColor = Color.Black;
            zeitBalken.Label = "Zeitbasierte Auslastung";
            zeitBalken.ShowValuesAboveBars = true;
            zeitBalken.ValueFormatter = wert => wert.ToString("N1", CultureInfo.GetCultureInfo("de-DE")) + " %";
            zeitBalken.Font.Size = prozentwertSchriftgroesse;
            zeitBalken.Font.Bold = true;
            zeitBalken.Font.Color = Color.Black;

            var patientenBalken = plot.AddBar(patientenbasiert, xs.Select(x => x + 0.18).ToArray());
            patientenBalken.BarWidth = 0.34;
            patientenBalken.FillColor = Color.MediumSeaGreen;
            patientenBalken.BorderColor = Color.Black;
            patientenBalken.Label = "Patientenbasierte Auslastung";
            patientenBalken.ShowValuesAboveBars = true;
            patientenBalken.ValueFormatter = wert => wert.ToString("N1", CultureInfo.GetCultureInfo("de-DE")) + " %";
            patientenBalken.Font.Size = prozentwertSchriftgroesse;
            patientenBalken.Font.Bold = true;
            patientenBalken.Font.Color = Color.Black;

            var vollauslastung = plot.AddHorizontalLine(100.0, color: Color.DarkSlateGray);
            vollauslastung.LineStyle = ScottPlot.LineStyle.Dash;
            vollauslastung.Label = "100 %";

            double hoechsterWert = Math.Max(100.0, zeitbasiert.Concat(patientenbasiert).DefaultIfEmpty(0.0).Max());
            plot.XTicks(xs, labels);
            plot.Title("Verteilung der Auslastung", bold: true, color: Color.Black, size: titelSchriftgroesse);
            plot.XAxis.Label("Ressource", color: Color.Black, size: achsenTitelSchriftgroesse, bold: false);
            plot.YAxis.Label("Auslastung in Prozent", color: Color.Black, size: achsenTitelSchriftgroesse, bold: false);
            plot.XAxis.TickLabelStyle(fontSize: achsenTickSchriftgroesse, fontBold: false, color: Color.Black);
            plot.YAxis.TickLabelStyle(fontSize: achsenTickSchriftgroesse, fontBold: false, color: Color.Black);
            var legende = plot.Legend(location: ScottPlot.Alignment.UpperRight);
            legende.FontSize = legendenSchriftgroesse;
            legende.FontBold = false;
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.SetAxisLimits(yMin: 0, yMax: hoechsterWert * 1.18);

            string outputPath = ErzeugeOutputPfad("auslastung_verteilung.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 15 gespeichert: {outputPath} ---");
        }

        private static Dictionary<string, BelegungsStatistik> BerechneBelegungen(IReadOnlyList<string> traceData)
        {
            string[] namen =
            {
                "Rezeption",
                "Arzt belegt",
                "Schwester belegt",
                "Arztzimmer",
                "Schwesterzimmer"
            };

            Dictionary<string, HashSet<int>> patientenJeZaehler = namen
                .ToDictionary(name => name, _ => new HashSet<int>(), StringComparer.Ordinal);
            Dictionary<string, BelegungsStatistik> statistiken = namen
                .ToDictionary(name => name, _ => new BelegungsStatistik(), StringComparer.Ordinal);

            List<AuslastungTraceEvent> events = traceData
                .Select(ParseAuslastungTraceEvent)
                .Where(e => e is not null)
                .Select(e => e!)
                .OrderBy(e => e.GlobalZeit)
                .ThenBy(e => e.Index)
                .ToList();

            double letzteZeit = events.Count > 0 ? events[0].GlobalZeit : 0.0;
            foreach (AuslastungTraceEvent traceEvent in events)
            {
                double dauer = Math.Max(0.0, traceEvent.GlobalZeit - letzteZeit);
                foreach ((string name, BelegungsStatistik statistik) in statistiken)
                    statistik.ErfasseDauer(dauer, patientenJeZaehler[name].Count);

                VerarbeiteAuslastungEvent(traceEvent, patientenJeZaehler);
                letzteZeit = traceEvent.GlobalZeit;
            }

            return statistiken;
        }

        private static void VerarbeiteAuslastungEvent(
            AuslastungTraceEvent traceEvent,
            Dictionary<string, HashSet<int>> patientenJeZaehler)
        {
            int patientId = traceEvent.PatientId;
            switch (traceEvent.EventTyp)
            {
                case "betritt_rezeption":
                    patientenJeZaehler["Rezeption"].Add(patientId);
                    break;
                case "beendet_rezeption":
                case "bricht_ab_wegen_feierabend_rezeption":
                    patientenJeZaehler["Rezeption"].Remove(patientId);
                    break;
                case "betritt_schwesterzimmer":
                    patientenJeZaehler["Schwesterzimmer"].Add(patientId);
                    break;
                case "startet_schwester_prozess":
                    patientenJeZaehler["Schwester belegt"].Add(patientId);
                    break;
                case "beendet_schwester_prozess":
                case "bricht_ab_wegen_feierabend_schwester":
                    patientenJeZaehler["Schwester belegt"].Remove(patientId);
                    patientenJeZaehler["Schwesterzimmer"].Remove(patientId);
                    break;
                case "betritt_arztzimmer":
                    patientenJeZaehler["Arztzimmer"].Add(patientId);
                    break;
                case "startet_arzt_behandlung":
                    patientenJeZaehler["Arzt belegt"].Add(patientId);
                    break;
                case "beendet_arzt_behandlung":
                case "bricht_ab_wegen_feierabend_arzt":
                    patientenJeZaehler["Arzt belegt"].Remove(patientId);
                    patientenJeZaehler["Arztzimmer"].Remove(patientId);
                    break;
                case "geht_zum_ausgang":
                case "verlaesst_klinik":
                    foreach (HashSet<int> patienten in patientenJeZaehler.Values)
                        patienten.Remove(patientId);
                    break;
            }
        }

        private static AuslastungPunkt ErzeugeAuslastungPunkt(
            string name,
            double belegteMinuten,
            int kapazitaet,
            int behandeltePatienten,
            double erwartbarePatientenKapazitaet,
            double simulationsdauer)
        {
            double verfuegbareKapazitaetsminuten = kapazitaet * Program.SimulierteArbeitstage * simulationsdauer;
            double zeitbasierteAuslastung = verfuegbareKapazitaetsminuten > 0.0
                ? (belegteMinuten / verfuegbareKapazitaetsminuten) * 100.0
                : 0.0;
            double patientenbasierteAuslastung = erwartbarePatientenKapazitaet > 0.0
                ? (behandeltePatienten / erwartbarePatientenKapazitaet) * 100.0
                : 0.0;

            return new AuslastungPunkt(
                name,
                Math.Round(zeitbasierteAuslastung, 2),
                Math.Round(patientenbasierteAuslastung, 2));
        }

        private static double BerechneErwartbarePatientenKapazitaet(
            int kapazitaet,
            double mittlereDauerMinuten,
            double simulationsdauer)
        {
            if (kapazitaet <= 0 || mittlereDauerMinuten <= 0.0)
                return 0.0;

            return kapazitaet * Program.SimulierteArbeitstage * simulationsdauer / mittlereDauerMinuten;
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

        private static AuslastungTraceEvent? ParseAuslastungTraceEvent(string zeile, int index)
        {
            string[] teile = zeile.Split(';');
            if (teile.Length < 5)
                return null;

            if (!double.TryParse(teile[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double zeit))
                return null;

            if (!int.TryParse(teile[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int patientId))
                return null;

            int tagIndex = Math.Max(0, (patientId - 1) / 10_000);
            double tagesAbstandMinuten = SimulationKonfiguration.SIMULATIONSDAUER + TagesNachlaufPufferMinuten;
            return new AuslastungTraceEvent(index, zeit + (tagIndex * tagesAbstandMinuten), teile[1], patientId);
        }

        private sealed class BelegungsStatistik
        {
            private double belegteMinuten;

            public double BelegteMinuten => belegteMinuten;

            public void ErfasseDauer(double dauer, int wert)
            {
                if (dauer <= 0.0)
                    return;

                belegteMinuten += wert * dauer;
            }
        }

        private sealed record AuslastungTraceEvent(int Index, double GlobalZeit, string EventTyp, int PatientId);

        private sealed record AuslastungPunkt(
            string Name,
            double ZeitbasierteAuslastungProzent,
            double PatientenbasierteAuslastungProzent);
    }
}
