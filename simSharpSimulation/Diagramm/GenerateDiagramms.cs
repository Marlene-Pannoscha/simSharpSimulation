using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace simSharpSimulation
{
    // Diese Klasse ist verantwortlich für die Erstellung von Diagrammen aus den Simulationsdaten.
    // Sie verwendet die ScottPlot-Bibliothek, um verschiedene Aspekte der Simulation zu visualisieren,
    // wie z.B. Ankunftszeiten und Wartezeiten.
    internal static class GenerateDiagramme
    {
        private const string ImagesOrdner = "images";

        // Erstellt alle Diagramme analog zur SimPy-Version:
        // 1) Theorie: PDF + CDF der Normalverteilung für Patientenankünfte.
        // 2) Simulation vs. Theorie: Ein Histogramm der tatsächlichen Ankünfte im Vergleich zur theoretischen Verteilung.
        // 3) Histogramm der Wartezeiten beim Arzt.
        // 4) Histogramm der Wartezeiten bei der Schwester.
        // 5) Ein vergleichendes Diagramm der Wartezeiten (Arzt vs. Schwester).
        // 6) Histogramm der Gesamtprozesszeit (Eintritt bis Austritt).
        // 7) Zeitachse für einen einzelnen Patienten (vom Eintritt bis Austritt).
        // 8) Vergleichs-Zeitachse für 3-5 Patienten mit unterschiedlichen Prozesspfaden.
        public static void GeneriereDiagramme(
            IReadOnlyList<double> echteAnkunftszeiten,
            IReadOnlyList<double> wartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            IReadOnlyList<double> gesamtprozesszeiten,
            IReadOnlyList<string> traceData,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            Console.WriteLine("Generiere Diagramme (Wartezeiten, Ankünfte, Theorie, Gesamtprozesszeit)...");

            // Erzeugt eine lineare Sequenz von Zeitpunkten für die X-Achse der theoretischen Kurven.
            double[] x = Linspace(0, simulationsdauer, 500);

            // Berechnet die Wahrscheinlichkeitsdichtefunktion (PDF) für jeden Zeitpunkt.
            double[] pdf = x
                .Select(v => MathNet.Numerics.Distributions.Normal.PDF(erwartungswert, standardabweichung, v))
                .ToArray();

            // Berechnet die kumulative Verteilungsfunktion (CDF) für jeden Zeitpunkt.
            double[] cdf = x
                .Select(v => MathNet.Numerics.Distributions.Normal.CDF(erwartungswert, standardabweichung, v))
                .ToArray();

            // Ruft die Methoden zur Erstellung der einzelnen Diagramme auf.
            // [Diagramm 1] Theorie: PDF + CDF
            ErzeugeTheorieDiagramm(x, pdf, cdf, erwartungswert, standardabweichung);
            // [Diagramm 2] Simulation vs. Theorie: Ankünfte
            ErzeugeAnkuenfteVergleichsDiagramm(echteAnkunftszeiten, x, pdf, cdf, simulationsdauer, erwartungswert, standardabweichung);
            // [Diagramm 3] Wartezeiten beim Arzt
            ErzeugeWartezeitenDiagramm(wartezeiten, anzahlAerzte);
            // [Diagramm 4] Wartezeiten bei der Schwester
            ErzeugeSchwesternWartezeitenDiagramm(schwesternWartezeiten, anzahlSchwestern);
            // [Diagramm 5] Vergleich Arzt vs. Schwester
            ErzeugeWartezeitenVergleichsDiagramm(wartezeiten, schwesternWartezeiten, anzahlAerzte, anzahlSchwestern);
            // [Diagramm 6] Gesamtprozesszeit
            ErzeugeGesamtprozesszeitDiagramm(gesamtprozesszeiten);
            // [Diagramm 7] Zeitachse eines Patienten
            ErzeugePatientenZeitachsenDiagramm(traceData);
            // [Diagramm 8] Vergleich 3-5 Patienten (verschiedene Pfade)
            ErzeugeMehrpatientenVergleichsZeitachse(traceData);
        }

        // ============================
        // Diagramm 1: Theorie (PDF/CDF)
        // ============================
        // Erstellt ein Diagramm, das die theoretische PDF und CDF der Ankunftsverteilung zeigt.
        private static void ErzeugeTheorieDiagramm(double[] x, double[] pdf, double[] cdf, double erwartungswert, double standardabweichung)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            // Fügt die PDF-Kurve hinzu.
            plot.AddScatter(x, pdf, color: Color.RoyalBlue, lineWidth: 2, label: "PDF (Dichtefunktion)");
            // Fügt eine zweite Y-Achse für die CDF-Kurve hinzu.
            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            // Fügt die CDF-Kurve hinzu und weist sie der rechten Y-Achse zu.
            var cdfLine = plot.AddScatter(x, cdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 2, label: "CDF (Verteilungsfunktion)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;
            plot.Title($"Theoretische Patientenankünfte (Normalverteilung)\n(Erwartungswert: {erwartungswert}, StdAbw: {standardabweichung})");
            plot.XLabel("Zeit in Minuten (0 bis 480)");
            plot.YLabel("Wahrscheinlichkeitsdichte (PDF)");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            string outputPath = ErzeugeOutputPfad("ankunftsverteilung_theorie_pdf_cdf.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 1 gespeichert: {outputPath} ---");
        }

        // Erstellt ein Diagramm, das das Histogramm der tatsächlichen Ankünfte mit der theoretischen Verteilung vergleicht.
        // ============================
        // Diagramm 2: Ankünfte Vergleich
        // ============================
        private static void ErzeugeAnkuenfteVergleichsDiagramm(
            IReadOnlyList<double> echteAnkunftszeiten,
            double[] x,
            double[] pdf,
            double[] cdf,
            double simulationsdauer,
            double erwartungswert,
            double standardabweichung)
        {
            int anzahlBins = 24; // Anzahl der Balken im Histogramm.
            // Erstellt die Histogrammdaten aus den tatsächlichen Ankunftszeiten.
            var (ankunftCounts, ankunftCenters, balkenBreite) = BuildHistogram(echteAnkunftszeiten, anzahlBins, 0, simulationsdauer);
            var plot = new ScottPlot.Plot(1000, 600);
            // Fügt das Balkendiagramm (Histogramm) hinzu.
            var bars = plot.AddBar(ankunftCounts, ankunftCenters);
            bars.BarWidth = balkenBreite * 0.9;
            bars.FillColor = Color.LightBlue;
            bars.BorderColor = Color.Black;
            bars.Label = "Tatsächliche Ankünfte (Histogramm)";
            // Skaliert die PDF, damit sie über das Histogramm gelegt werden kann.
            double[] skaliertePdf = pdf.Select(v => v * echteAnkunftszeiten.Count * balkenBreite).ToArray();
            plot.AddScatter(x, skaliertePdf, color: Color.DarkBlue, lineWidth: 3, label: "PDF (Dichtefunktion)");
            // Fügt die CDF-Kurve mit einer eigenen Achse hinzu.
            var axisRight = plot.AddAxis(ScottPlot.Renderable.Edge.Right);
            var cdfLine = plot.AddScatter(x, cdf, color: Color.Red, lineStyle: ScottPlot.LineStyle.Dash, lineWidth: 3, label: "CDF (Verteilungsfunktion)");
            cdfLine.YAxisIndex = axisRight.AxisIndex;
            plot.Title($"Simulation vs. Theorie: Ankünfte\n(Mittelwert: {erwartungswert}, StdAbw: {standardabweichung})");
            plot.XLabel("Zeit der Simulation in Minuten (0 bis 480)");
            plot.YLabel("Anzahl der Patienten");
            axisRight.Label("Kumulierte Wahrscheinlichkeit (CDF)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            string outputPath = ErzeugeOutputPfad("ankuenfte_simulation_vs_theorie.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 2 gespeichert: {outputPath} ---");
        }

        // ===============================
        // Diagramm 3: Wartezeiten (Arzt)
        // ===============================
        // Erstellt ein Histogramm der Wartezeiten der Patienten auf einen Arzt.
        private static void ErzeugeWartezeitenDiagramm(IReadOnlyList<double> wartezeiten, int anzahlAerzte)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            if (wartezeiten.Count > 0)
            {
                int bins = 20;
                double maxWartezeit = Math.Max(wartezeiten.Max(), 1.0);
                // Erstellt die Histogrammdaten.
                var (counts, centers, binWidth) = BuildHistogram(wartezeiten, bins, 0, maxWartezeit);
                var bars = plot.AddBar(counts, centers);
                bars.BarWidth = binWidth * 0.9;
                bars.FillColor = Color.Teal;
                bars.BorderColor = Color.Black;
            }
            plot.Title($"Verteilung der Wartezeiten (Ärzte: {anzahlAerzte})");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            string outputPath = ErzeugeOutputPfad("wartezeiten_histogramm.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 3 gespeichert: {outputPath} ---");
        }

        // ====================================
        // Diagramm 4: Wartezeiten (Schwester)
        // ====================================
        // Erstellt ein Histogramm der Wartezeiten der Patienten auf eine Schwester.
        private static void ErzeugeSchwesternWartezeitenDiagramm(IReadOnlyList<double> schwesternWartezeiten, int anzahlSchwestern)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            if (schwesternWartezeiten.Count > 0)
            {
                int bins = 20;
                double maxWartezeit = Math.Max(schwesternWartezeiten.Max(), 1.0);
                // Erstellt die Histogrammdaten.
                var (counts, centers, binWidth) = BuildHistogram(schwesternWartezeiten, bins, 0, maxWartezeit);
                var bars = plot.AddBar(counts, centers);
                bars.BarWidth = binWidth * 0.9;
                bars.FillColor = Color.PeachPuff;
                bars.BorderColor = Color.Black;
            }
            plot.Title($"Verteilung der Wartezeiten bei den Schwestern (Schwestern: {anzahlSchwestern})");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            string outputPath = ErzeugeOutputPfad("wartezeiten_schwester_histogramm.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 4 gespeichert: {outputPath} ---");
        }

        // ============================================
        // Diagramm 5: Vergleich Arzt vs. Schwester
        // ============================================
        // Erstellt ein Diagramm, das die Wartezeiten bei Ärzten und Schwestern vergleicht.
        private static void ErzeugeWartezeitenVergleichsDiagramm(
            IReadOnlyList<double> wartezeiten,
            IReadOnlyList<double> schwesternWartezeiten,
            int anzahlAerzte,
            int anzahlSchwestern)
        {
            var plot = new ScottPlot.Plot(1000, 600);
            int bins = 20;
            // Bestimmt die maximale Wartezeit, um die Achsen beider Histogramme zu synchronisieren.
            double maxWartezeit = Math.Max(
                wartezeiten.Count > 0 ? wartezeiten.Max() : 0,
                schwesternWartezeiten.Count > 0 ? schwesternWartezeiten.Max() : 0);
            maxWartezeit = Math.Max(maxWartezeit, 1.0);
            // Erstellt Histogrammdaten für Ärzte und Schwestern.
            var (doctorCounts, centers, binWidth) = BuildHistogram(wartezeiten, bins, 0, maxWartezeit);
            var (nurseCounts, _, _) = BuildHistogram(schwesternWartezeiten, bins, 0, maxWartezeit);
            // Verschiebt die Balken leicht, damit sie nebeneinander und nicht übereinander liegen.
            double offset = binWidth * 0.2;
            // Fügt die Balken für Arzt-Wartezeiten hinzu.
            var doctorBars = plot.AddBar(doctorCounts, centers.Select(c => c - offset).ToArray());
            doctorBars.BarWidth = binWidth * 0.35;
            doctorBars.FillColor = Color.Teal;
            doctorBars.BorderColor = Color.Black;
            doctorBars.Label = "Arzt-Wartezeiten";
            // Fügt die Balken für Schwester-Wartezeiten hinzu.
            var nurseBars = plot.AddBar(nurseCounts, centers.Select(c => c + offset).ToArray());
            nurseBars.BarWidth = binWidth * 0.35;
            nurseBars.FillColor = Color.PeachPuff;
            nurseBars.BorderColor = Color.Black;
            nurseBars.Label = "Schwester-Wartezeiten";
            plot.Title($"Wartezeitenvergleich: Ärzte vs. Schwestern\n(Ärzte: {anzahlAerzte}, Schwestern: {anzahlSchwestern})");
            plot.XLabel("Wartezeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Legend(location: ScottPlot.Alignment.UpperRight);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            string outputPath = ErzeugeOutputPfad("wartezeiten_vergleich_arzt_schwester.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 5 gespeichert: {outputPath} ---");
        }

        // ============================================
        // Diagramm 6: Gesamtprozesszeit
        // ============================================
        // Erstellt ein Histogramm der gesamten Prozesszeit (Eintritt bis Austritt).
        private static void ErzeugeGesamtprozesszeitDiagramm(IReadOnlyList<double> gesamtprozesszeiten)
        {
            var plot = new ScottPlot.Plot(1000, 600);

            if (gesamtprozesszeiten.Count > 0)
            {
                int bins = 20;
                double maxProzesszeit = Math.Max(gesamtprozesszeiten.Max(), 1.0);
                var (counts, centers, binWidth) = BuildHistogram(gesamtprozesszeiten, bins, 0, maxProzesszeit);
                var bars = plot.AddBar(counts, centers);
                bars.BarWidth = binWidth * 0.9;
                bars.FillColor = Color.MediumPurple;
                bars.BorderColor = Color.Black;
            }

            plot.Title("Verteilung der Gesamtprozesszeit (Klinik-Eintritt bis Austritt)");
            plot.XLabel("Gesamtprozesszeit in Minuten");
            plot.YLabel("Anzahl der Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("gesamtprozesszeit_histogramm.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 6 gespeichert: {outputPath} ---");
        }

        // ============================================
        // Diagramm 7: Zeitachse eines Patienten
        // ============================================
        // Erstellt eine Zeitachse (Schritt-für-Schritt) für einen einzelnen Patienten auf Basis des Trace-Logs.
        private static void ErzeugePatientenZeitachsenDiagramm(IReadOnlyList<string> traceData)
        {
            // Trace-Zeile: "Zeit;EventTyp;PatientId"
            var events = new List<(double Zeit, string EventTyp, int PatientId)>();
            foreach (string line in traceData)
            {
                string[] parts = line.Split(';');
                if (parts.Length != 3)
                    continue;

                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zeit))
                    continue;
                if (!int.TryParse(parts[2], out int patientId))
                    continue;

                events.Add((zeit, parts[1], patientId));
            }

            if (events.Count == 0)
                return;

            // Wähle den kleinsten PatientId mit vollständigem Ablauf (Eintritt und Austritt vorhanden).
            int? zielPatientId = events
                .GroupBy(e => e.PatientId)
                .OrderBy(g => g.Key)
                .Where(g => g.Any(x => x.EventTyp == "betritt_klinik") && g.Any(x => x.EventTyp == "verlaesst_klinik"))
                .Select(g => (int?)g.Key)
                .FirstOrDefault();

            if (zielPatientId is null)
                return;

            var patientEvents = events
                .Where(e => e.PatientId == zielPatientId.Value)
                .OrderBy(e => e.Zeit)
                .ToList();

            if (patientEvents.Count == 0)
                return;

            double[] x = patientEvents.Select(e => e.Zeit).ToArray();
            double[] y = Enumerable.Range(0, patientEvents.Count).Select(i => (double)i).ToArray();

            var plot = new ScottPlot.Plot(1400, 800);
            var timelineScatter = plot.AddScatter(x, y, color: Color.DarkSlateBlue, lineWidth: 2, markerSize: 8, label: $"Patient {zielPatientId.Value}");
            timelineScatter.MarkerShape = ScottPlot.MarkerShape.filledCircle;

            for (int i = 0; i < patientEvents.Count; i++)
            {
                string label = patientEvents[i].EventTyp.Replace('_', ' ');
                plot.AddText(label, x[i] + 1.5, y[i], color: Color.Black);
            }

            plot.Title($"Zeitachse eines Patienten (ID: {zielPatientId.Value})");
            plot.XLabel("Zeit in Minuten seit Tagesbeginn");
            plot.YLabel("Prozessschritt (chronologisch)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("patienten_zeitachse.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 7 gespeichert: {outputPath} ---");
        }

        // =========================================================
        // Diagramm 8: Vergleichs-Zeitachse für 3-10 Patienten
        // =========================================================
        // Zeigt mehrere Patienten in separaten Zeilen (mit/ohne Termin, mit/ohne Schwester-Vorbereitung).
        private static void ErzeugeMehrpatientenVergleichsZeitachse(IReadOnlyList<string> traceData)
        {
            const int minVergleichPatienten = 3;
            const int maxVergleichPatienten = 10;

            var events = new List<(double Zeit, string EventTyp, int PatientId)>();
            foreach (string line in traceData)
            {
                string[] parts = line.Split(';');
                if (parts.Length != 3)
                    continue;

                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zeit))
                    continue;
                if (!int.TryParse(parts[2], out int patientId))
                    continue;

                events.Add((zeit, parts[1], patientId));
            }

            if (events.Count == 0)
                return;

            static double? FindeZeit(List<(double Zeit, string EventTyp, int PatientId)> evs, string eventTyp)
            {
                var hit = evs.FirstOrDefault(x => x.EventTyp == eventTyp);
                return hit.EventTyp is null ? null : hit.Zeit;
            }

            var byPatient = events
                .GroupBy(e => e.PatientId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var patientEvents = g.OrderBy(x => x.Zeit).ToList();
                    var betritt = FindeZeit(patientEvents, "betritt_klinik");
                    var verlaesst = FindeZeit(patientEvents, "verlaesst_klinik");
                    return new PatientenPfadInfo
                    {
                        PatientId = g.Key,
                        HatTermin = g.Any(x => x.EventTyp == "hat_termin"),
                        HatSchwesterVorbereitung = g.Any(x => x.EventTyp == "startet_vorbereitung_schwester" || x.EventTyp == "benoetigt_schwester_vorbereitung"),
                        Betritt = betritt,
                        StartRezeption = FindeZeit(patientEvents, "startet_rezeption"),
                        StartSchwester = FindeZeit(patientEvents, "startet_schwester_prozess"),
                        StartArzt = FindeZeit(patientEvents, "startet_arzt_behandlung"),
                        Verlaesst = verlaesst
                    };
                })
                .Where(p => p.Betritt.HasValue && p.Verlaesst.HasValue)
                .ToList();

            if (byPatient.Count == 0)
                return;

            // Ziel: möglichst verschiedene Kombinationen abdecken.
            var kandidaten = new List<PatientenPfadInfo>();

            void AddFirstMatch(bool hatTermin, bool hatVorbereitung)
            {
                PatientenPfadInfo? match = byPatient.FirstOrDefault(p =>
                    p.HatTermin == hatTermin &&
                    p.HatSchwesterVorbereitung == hatVorbereitung &&
                    !kandidaten.Any(c => c.PatientId == p.PatientId));
                if (match != null)
                    kandidaten.Add(match);
            }

            AddFirstMatch(true, true);
            AddFirstMatch(true, false);
            AddFirstMatch(false, true);
            AddFirstMatch(false, false);

            foreach (var p in byPatient)
            {
                if (kandidaten.Count >= maxVergleichPatienten)
                    break;
                if (!kandidaten.Any(c => c.PatientId == p.PatientId))
                    kandidaten.Add(p);
            }

            // Mindestens 3 Patienten, falls vorhanden.
            if (kandidaten.Count < Math.Min(minVergleichPatienten, byPatient.Count))
            {
                foreach (var p in byPatient)
                {
                    if (kandidaten.Count >= Math.Min(minVergleichPatienten, byPatient.Count))
                        break;
                    if (!kandidaten.Any(c => c.PatientId == p.PatientId))
                        kandidaten.Add(p);
                }
            }

            if (kandidaten.Count == 0)
                return;

            double minZeit = kandidaten.Min(p => p.Betritt!.Value);
            double maxZeit = kandidaten.Max(p => p.Verlaesst!.Value);
            double zeitSpanne = Math.Max(maxZeit - minZeit, 1.0);
            double linkerPuffer = Math.Max(1.0, zeitSpanne * 0.05);
            double rechterPuffer = Math.Max(8.0, zeitSpanne * 0.40);

            int plotHoehe = Math.Min(1600, Math.Max(900, 220 + kandidaten.Count * 95));
            var plot = new ScottPlot.Plot(1500, plotHoehe);

            for (int i = 0; i < kandidaten.Count; i++)
            {
                var p = kandidaten[i];
                double y = i + 1;

                // Grundlinie von Eintritt bis Austritt
                plot.AddScatter(
                    new[] { p.Betritt!.Value, p.Verlaesst!.Value },
                    new[] { y, y },
                    color: Color.Gray,
                    lineWidth: 2,
                    markerSize: 0,
                    label: null);

                // Meilensteine
                plot.AddPoint(p.Betritt!.Value, y, color: Color.DimGray, size: 10, label: null);
                if (p.StartRezeption.HasValue)
                    plot.AddPoint(p.StartRezeption.Value, y, color: Color.SteelBlue, size: 9, label: null);
                if (p.StartSchwester.HasValue)
                    plot.AddPoint(p.StartSchwester.Value, y, color: Color.MediumVioletRed, size: 9, label: null);
                if (p.StartArzt.HasValue)
                    plot.AddPoint(p.StartArzt.Value, y, color: Color.DarkGreen, size: 9, label: null);
                plot.AddPoint(p.Verlaesst!.Value, y, color: Color.Black, size: 10, label: null);

                string laneLabel = $"ID {p.PatientId} | {(p.HatTermin ? "mit Termin" : "ohne Termin")} | {(p.HatSchwesterVorbereitung ? "mit Schwester-Vorbereitung" : "ohne Schwester-Vorbereitung")}";
                plot.AddText(laneLabel, p.Verlaesst!.Value + 1.0, y, color: Color.Black);
            }

            double[] laneYs = Enumerable.Range(1, kandidaten.Count).Select(v => (double)v).ToArray();
            string[] laneLabels = kandidaten
                .Select(p => $"P{p.PatientId}")
                .ToArray();
            plot.YTicks(laneYs, laneLabels);

            // Kleine Legende als Text im Plot
            plot.AddText("Marker: ♦ Eintritt | ● Rezeption | ■ Schwester | ▲ Arzt | ○ Austritt", minZeit - linkerPuffer + 1.0, kandidaten.Count + 0.6, color: Color.Black);

            plot.Title("Vergleich 10 Patienten: Prozesspfade (Rezeption → Schwester → Arzt)");
            plot.XLabel("Zeit in Minuten seit Tagesbeginn");
            plot.YLabel("Patienten");
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.SetAxisLimits(xMin: minZeit - linkerPuffer, xMax: maxZeit + rechterPuffer, yMin: 0.7, yMax: kandidaten.Count + 1.1);

            string outputPath = ErzeugeOutputPfad("patienten_vergleich_zeitachse.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 8 gespeichert: {outputPath} ---");
        }

        private sealed class PatientenPfadInfo
        {
            public int PatientId { get; set; }
            public bool HatTermin { get; set; }
            public bool HatSchwesterVorbereitung { get; set; }
            public double? Betritt { get; set; }
            public double? StartRezeption { get; set; }
            public double? StartSchwester { get; set; }
            public double? StartArzt { get; set; }
            public double? Verlaesst { get; set; }
        }

        // Stellt sicher, dass der Ausgabeordner für die Bilder existiert und gibt den vollständigen Pfad für eine Datei zurück.
        private static string ErzeugeOutputPfad(string dateiname)
        {
            string imagesPfad = Path.Combine(Directory.GetCurrentDirectory(), ImagesOrdner);
            Directory.CreateDirectory(imagesPfad);
            return Path.Combine(imagesPfad, dateiname);
        }

        // Erzeugt eine Sequenz von gleichmäßig verteilten Zahlen über ein angegebenes Intervall.
        // Ähnlich wie np.linspace in Python/NumPy.
        private static double[] Linspace(double start, double end, int count)
        {
            if (count < 2)
                return new[] { start };
            double step = (end - start) / (count - 1);
            return Enumerable.Range(0, count).Select(i => start + i * step).ToArray();
        }

        // Berechnet die Daten für ein Histogramm aus einer Liste von Werten.
        private static (double[] counts, double[] centers, double binWidth) BuildHistogram(
            IReadOnlyList<double> values,
            int binCount,
            double min,
            double max)
        {
            double binWidth = (max - min) / binCount;
            double[] counts = new double[binCount];
            double[] centers = new double[binCount];
            // Berechnet die Mittelpunkte der Bins für die X-Achse.
            for (int i = 0; i < binCount; i++)
                centers[i] = min + (i + 0.5) * binWidth;
            // Zählt, wie viele Werte in jeden Bin fallen.
            foreach (double value in values)
            {
                if (value < min || value > max)
                    continue; // Ignoriert Werte außerhalb des Bereichs.
                int index = (int)((value - min) / binWidth);
                if (index == binCount)
                    index = binCount - 1; // Ordnet den Maximalwert dem letzten Bin zu.
                counts[index]++;
            }
            return (counts, centers, binWidth);
        }
    }
}
