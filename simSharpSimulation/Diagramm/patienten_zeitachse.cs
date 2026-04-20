using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
    // Dateirolle: Diagramm 7 - Zeitachse eines einzelnen Patienten aus den Trace-Ereignissen.
    internal static partial class GenerateDiagramme
    {
        // Diagramm 7
        private static void ErzeugePatientenZeitachsenDiagramm(IReadOnlyList<string> traceData)
        {
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
            const double labelOffsetX = 1.5;

            var plot = new ScottPlot.Plot(1400, 800);
            var timelineScatter = plot.AddScatter(x, y, color: Color.DarkSlateBlue, lineWidth: 2, markerSize: 8, label: $"Patient {zielPatientId.Value}");
            timelineScatter.MarkerShape = ScottPlot.MarkerShape.filledCircle;

            for (int i = 0; i < patientEvents.Count; i++)
            {
                string label = patientEvents[i].EventTyp.Replace('_', ' ');
                plot.AddText(label, x[i] + labelOffsetX, y[i], color: Color.Black);
            }

            double minZeit = x.Min();
            double maxZeit = x.Max();
            double zeitSpanne = Math.Max(maxZeit - minZeit, 1.0);
            double linkerPuffer = Math.Max(1.0, zeitSpanne * 0.05);
            int maxLabelLaenge = patientEvents
                .Select(e => e.EventTyp.Replace('_', ' ').Length)
                .DefaultIfEmpty(0)
                .Max();

            // Kompakter rechter Puffer:
            // - genug Platz für den längsten Text
            // - deutlich weniger Leerraum als zuvor
            double textPuffer = 1.5 + (maxLabelLaenge * 0.22);
            double rechterPuffer = Math.Max(4.0, Math.Min(textPuffer, 10.0));

            // Explizite Achsenlimits verhindern, dass der letzte Prozess-Text rechts abgeschnitten wird.
            plot.SetAxisLimits(
                xMin: minZeit - linkerPuffer,
                xMax: maxZeit + rechterPuffer + labelOffsetX,
                yMin: -0.8,
                yMax: patientEvents.Count - 1 + 0.8);

            plot.Title($"Zeitachse eines Patienten (ID: {zielPatientId.Value})");
            plot.XLabel("Zeit in Minuten seit Tagesbeginn");
            plot.YLabel("Prozessschritt (chronologisch)");
            plot.Legend(location: ScottPlot.Alignment.UpperLeft);
            plot.Grid(enable: true, lineStyle: ScottPlot.LineStyle.Dot);

            string outputPath = ErzeugeOutputPfad("patienten_zeitachse.png");
            plot.SaveFig(outputPath);
            Console.WriteLine($"--- Diagramm 7 gespeichert: {outputPath} ---");
        }
    }
}
