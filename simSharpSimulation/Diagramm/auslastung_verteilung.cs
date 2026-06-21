using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace simSharpSimulation
{
    // Zeitbasierte Personalauslastung je Simulationstag innerhalb der 8-Stunden-Schicht.
    internal static partial class GenerateDiagramme
    {
        private static void ErzeugeAuslastungsVerteilungsDiagramm(
            IReadOnlyList<string> traceData,
            double simulationsdauer,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            List<Dictionary<string, BelegungsStatistik>> belegungenProTag =
                BerechneBelegungenProTag(traceData, simulationsdauer);

            (string Name, string TraceName, int Kapazitaet, Color Farbe)[] ressourcen =
            {
                ("Rezeption", "Rezeption", RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN, Color.SteelBlue),
                ("Aerzte", "Arzt belegt", anzahlAerzte, Color.Firebrick),
                ("Schwestern", "Schwester belegt", anzahlSchwestern, Color.SeaGreen)
            };

            var plot = new ScottPlot.Plot(1500, 650);
            const float titelSchriftgroesse = 28;
            const float achsenTitelSchriftgroesse = 18;
            const float achsenTickSchriftgroesse = 16;
            const float legendenSchriftgroesse = 16;
            double[] tage = Enumerable.Range(1, Program.SimulierteArbeitstage).Select(tag => (double)tag).ToArray();
            var alleWerte = new List<double>();

            foreach (var ressource in ressourcen)
            {
                double[] tagesAuslastung = belegungenProTag
                    .Select(tag => BerechneZeitbasierteAuslastung(
                        tag[ressource.TraceName].BelegteMinuten,
                        ressource.Kapazitaet,
                        simulationsdauer))
                    .ToArray();
                alleWerte.AddRange(tagesAuslastung);

                plot.AddScatter(
                    tage,
                    tagesAuslastung,
                    color: ressource.Farbe,
                    lineWidth: 3,
                    markerSize: 8,
                    label: ressource.Name);
            }

            var vollauslastung = plot.AddHorizontalLine(100.0, color: Color.DarkSlateGray);
            vollauslastung.LineStyle = ScottPlot.LineStyle.Dash;
            vollauslastung.Label = "100 %";

            double hoechsterWert = Math.Max(100.0, alleWerte.DefaultIfEmpty(0.0).Max());
            double[] beschrifteteTage = tage
                .Where(tag => tag == 1.0 || tag == Program.SimulierteArbeitstage || tag % 2.0 == 0.0)
                .ToArray();
            plot.XTicks(beschrifteteTage, beschrifteteTage.Select(tag => $"Tag {tag:0}").ToArray());
            plot.Title(
                $"Zeitbasierte Personalauslastung über {Program.SimulierteArbeitstage} Arbeitstage " +
                $"(je {simulationsdauer / 60.0:0.#} Stunden)",
                bold: true,
                color: Color.Black,
                size: titelSchriftgroesse);
            plot.XAxis.Label("Simulationstag", color: Color.Black, size: achsenTitelSchriftgroesse, bold: false);
            plot.YAxis.Label("Auslastung in Prozent", color: Color.Black, size: achsenTitelSchriftgroesse, bold: false);
            plot.XAxis.TickLabelStyle(fontSize: achsenTickSchriftgroesse, fontBold: false, color: Color.Black);
            plot.YAxis.TickLabelStyle(fontSize: achsenTickSchriftgroesse, fontBold: false, color: Color.Black);
            var legende = plot.Legend(location: ScottPlot.Alignment.UpperRight);
            legende.FontSize = legendenSchriftgroesse;
            legende.FontBold = false;
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.SetAxisLimits(
                xMin: 0.7,
                xMax: Program.SimulierteArbeitstage + 0.3,
                yMin: 0,
                yMax: hoechsterWert * 1.12);

            string outputPath = ErzeugeOutputPfad("auslastung_verteilung.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 15 gespeichert: {outputPath} ---");
        }

        private static List<Dictionary<string, BelegungsStatistik>> BerechneBelegungenProTag(
            IReadOnlyList<string> traceData,
            double simulationsdauer)
        {
            string[] namen = { "Rezeption", "Arzt belegt", "Schwester belegt" };
            Dictionary<string, HashSet<int>> patientenJeZaehler = namen
                .ToDictionary(name => name, _ => new HashSet<int>(), StringComparer.Ordinal);
            var statistikenProTag = new List<Dictionary<string, BelegungsStatistik>>();

            List<AuslastungTraceEvent> events = traceData
                .Select(ParseAuslastungTraceEvent)
                .Where(e => e is not null)
                .Select(e => e!)
                .ToList();

            for (int tagIndex = 0; tagIndex < Program.SimulierteArbeitstage; tagIndex++)
            {
                Dictionary<string, BelegungsStatistik> statistiken = namen
                    .ToDictionary(name => name, _ => new BelegungsStatistik(), StringComparer.Ordinal);
                statistikenProTag.Add(statistiken);

                foreach (HashSet<int> patienten in patientenJeZaehler.Values)
                    patienten.Clear();

                double letzteZeit = 0.0;
                IEnumerable<AuslastungTraceEvent> tagesEvents = events
                    .Where(e => e.TagIndex == tagIndex && e.ZeitMinuten >= 0.0)
                    .OrderBy(e => e.ZeitMinuten)
                    .ThenBy(e => e.Index);

                foreach (AuslastungTraceEvent traceEvent in tagesEvents)
                {
                    double begrenzteZeit = Math.Min(traceEvent.ZeitMinuten, simulationsdauer);
                    double dauer = Math.Max(0.0, begrenzteZeit - letzteZeit);
                    foreach ((string name, BelegungsStatistik statistik) in statistiken)
                        statistik.ErfasseDauer(dauer, patientenJeZaehler[name].Count);

                    letzteZeit = begrenzteZeit;
                    if (traceEvent.ZeitMinuten >= simulationsdauer)
                        break;

                    VerarbeiteAuslastungEvent(traceEvent, patientenJeZaehler);
                }

                double restDesArbeitstags = Math.Max(0.0, simulationsdauer - letzteZeit);
                foreach ((string name, BelegungsStatistik statistik) in statistiken)
                    statistik.ErfasseDauer(restDesArbeitstags, patientenJeZaehler[name].Count);
            }

            return statistikenProTag;
        }

        private static void VerarbeiteAuslastungEvent(
            AuslastungTraceEvent traceEvent,
            Dictionary<string, HashSet<int>> patientenJeZaehler)
        {
            int patientId = traceEvent.PatientId;
            switch (traceEvent.EventTyp)
            {
                case "startet_rezeption":
                    patientenJeZaehler["Rezeption"].Add(patientId);
                    break;
                case "beendet_rezeption":
                case "bricht_ab_wegen_feierabend_rezeption":
                    patientenJeZaehler["Rezeption"].Remove(patientId);
                    break;
                case "startet_schwester_prozess":
                    patientenJeZaehler["Schwester belegt"].Add(patientId);
                    break;
                case "beendet_schwester_prozess":
                case "bricht_ab_wegen_feierabend_schwester":
                    patientenJeZaehler["Schwester belegt"].Remove(patientId);
                    break;
                case "startet_arzt_behandlung":
                    patientenJeZaehler["Arzt belegt"].Add(patientId);
                    break;
                case "beendet_arzt_behandlung":
                case "bricht_ab_wegen_feierabend_arzt":
                    patientenJeZaehler["Arzt belegt"].Remove(patientId);
                    break;
                case "geht_zum_ausgang":
                case "verlaesst_klinik":
                    foreach (HashSet<int> patienten in patientenJeZaehler.Values)
                        patienten.Remove(patientId);
                    break;
            }
        }

        private static double BerechneZeitbasierteAuslastung(
            double belegteMinuten,
            int kapazitaet,
            double simulationsdauer)
        {
            double verfuegbareKapazitaetsminuten = kapazitaet * simulationsdauer;
            double zeitbasierteAuslastung = verfuegbareKapazitaetsminuten > 0.0
                ? (belegteMinuten / verfuegbareKapazitaetsminuten) * 100.0
                : 0.0;
            return Math.Round(zeitbasierteAuslastung, 2);
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
            return new AuslastungTraceEvent(index, tagIndex, zeit, teile[1], patientId);
        }

        private sealed class BelegungsStatistik
        {
            private double belegteMinuten;
            public double BelegteMinuten => belegteMinuten;

            public void ErfasseDauer(double dauer, int wert)
            {
                if (dauer > 0.0)
                    belegteMinuten += wert * dauer;
            }
        }

        private sealed record AuslastungTraceEvent(
            int Index,
            int TagIndex,
            double ZeitMinuten,
            string EventTyp,
            int PatientId);
    }
}
