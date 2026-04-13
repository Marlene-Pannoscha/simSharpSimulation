using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace simSharpSimulation
{
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
    }
}
