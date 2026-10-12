using System.Collections.Generic;

namespace simSharpSimulation
{
    internal static partial class GenerateDiagramme
    {
        // Unvollstaendige Zeilen und Kopfzeilen werden wie bisher uebersprungen.
        private static List<(double Zeit, string EventTyp, int PatientId)> LesePatientenTraceEreignisse(
            IReadOnlyList<string> traceDaten)
        {
            var events = new List<(double Zeit, string EventTyp, int PatientId)>();
            foreach (string line in traceDaten)
            {
                string[] parts = line.Split(';');
                if (parts.Length < 5)
                    continue;

                if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zeit))
                    continue;
                if (!int.TryParse(parts[4], out int patientId))
                    continue;

                events.Add((zeit, parts[1], patientId));
            }

            return events;
        }
    }
}
