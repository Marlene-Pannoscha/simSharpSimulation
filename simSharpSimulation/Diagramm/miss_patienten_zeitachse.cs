using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 13 - Zeitachse eines besonderen Miss-Patienten
    // (Abbruch wegen zu langer Wartezeit oder Verschiebung durch Schlussplanung).
    internal static partial class GenerateDiagramme
    {
        // Diagramm 13
        private static void ErzeugeMissPatientenZeitachsenDiagramm(IReadOnlyList<string> traceData)
        {
            var events = new List<(double Zeit, string EventTyp, int PatientId)>();
            foreach (string line in traceData)
            {
                string[] parts = line.Split(';');
                if (parts.Length < 5)
                    continue;

                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double zeit))
                    continue;
                if (!int.TryParse(parts[4], out int patientId))
                    continue;

                events.Add((zeit, parts[1], patientId));
            }

            if (events.Count == 0)
                return;

            string[] priorisierteMissEvents =
            {
                "bricht_ab_und_verlaesst_klinik_wegen_wartezeit",
                "wird_auf_naechsten_tag_verschoben_arzt",
                "erhaelt_festen_termin_am_naechsten_vormittag_arzt",
                "wird_auf_naechsten_tag_verschoben_schwester",
                "erhaelt_festen_termin_am_naechsten_vormittag_schwester",
                "wird_auf_naechsten_tag_verschoben_rezeption",
                "erhaelt_festen_termin_am_naechsten_vormittag_rezeption"
            };

            int? zielPatientId = priorisierteMissEvents
                .SelectMany(eventTyp => events
                    .Where(e => e.EventTyp == eventTyp)
                    .OrderBy(e => e.Zeit)
                    .Select(e => (int?)e.PatientId))
                .FirstOrDefault();

            if (zielPatientId is null)
                return;

            var patientEvents = events
                .Where(e => e.PatientId == zielPatientId.Value)
                .OrderBy(e => e.Zeit)
                .ToList();

            if (patientEvents.Count == 0)
                return;

            string missEventTyp = patientEvents
                .Select(e => e.EventTyp)
                .FirstOrDefault(priorisierteMissEvents.Contains) ?? "miss";

            string missGrund = missEventTyp.Contains("naechsten_vormittag")
                ? "   Verschoben mit festem Vormittagstermin"
                : missEventTyp.Contains("verschoben")
                ? "   Verschoben auf den naechsten Tag"
                : "   Abbruch wegen zu langer Wartezeit";

            Color linienFarbe = missEventTyp.Contains("naechsten_vormittag") || missEventTyp.Contains("verschoben")
                ? Color.DarkOrange
                : Color.Crimson;

            double[] x = patientEvents.Select(e => e.Zeit).ToArray();
            double[] y = Enumerable.Range(0, patientEvents.Count).Select(i => (double)i).ToArray();
            const double labelOffsetX = 1.5;
            const float eventTextGroesse = 18f;
            const float hinweisTextGroesse = 20f;

            var plot = new ScottPlot.Plot(1800, 1000);
            var timelineScatter = plot.AddScatter(
                x,
                y,
                color: linienFarbe,
                lineWidth: 3,
                markerSize: 10,
                label: $"Miss-Patient {zielPatientId.Value}");
            timelineScatter.MarkerShape = ScottPlot.MarkerShape.filledCircle;

            for (int i = 0; i < patientEvents.Count; i++)
            {
                double? naechsteZeit = i < patientEvents.Count - 1 ? patientEvents[i + 1].Zeit : null;
                string label = FormatiereEventLabel(patientEvents[i].EventTyp, patientEvents[i].Zeit, naechsteZeit);
                plot.AddText(label, x[i] + labelOffsetX, y[i], eventTextGroesse, Color.Black);
            }

            double minZeit = x.Min();
            double maxZeit = x.Max();
            double zeitSpanne = Math.Max(maxZeit - minZeit, 1.0);
            double linkerPuffer = Math.Max(1.0, zeitSpanne * 0.05);
            double hinweisOffsetX = Math.Max(2.5, zeitSpanne * 0.06);
            int maxLabelLaenge = patientEvents
                .Select(e => e.EventTyp.Replace('_', ' ').Length)
                .DefaultIfEmpty(0)
                .Max();

            int maxTextLaenge = Math.Max(maxLabelLaenge, missGrund.Length);
            double textPuffer = 4.0 + (maxTextLaenge * 0.45);
            double rechterPuffer = Math.Max(12.0, textPuffer);

            plot.SetAxisLimits(
                xMin: minZeit - linkerPuffer,
                xMax: maxZeit + rechterPuffer + labelOffsetX,
                yMin: -0.8,
                yMax: patientEvents.Count - 1 + 0.8);

            plot.Title($"Zeitachse eines Miss-Patienten (ID: {zielPatientId.Value})");
            plot.XLabel("Zeit in Minuten seit Tagesbeginn");
            plot.YLabel("Prozessschritt (chronologisch)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);
            plot.AddText(missGrund, minZeit + hinweisOffsetX, patientEvents.Count - 0.2, hinweisTextGroesse, linienFarbe);

            string outputPath = ErzeugeOutputPfad("miss_patienten_zeitachse.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 13 gespeichert: {outputPath} ---");
        }
    }
}
