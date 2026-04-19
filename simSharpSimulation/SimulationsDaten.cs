using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace simSharpSimulation
{
    /// <summary>
    /// Hält alle während der Simulation gesammelten Daten (Trace, Wartezeiten, Ankünfte).
    /// Diese Klasse entkoppelt Datenspeicherung von der eigentlichen Simulationslogik.
    /// </summary>
    public sealed class SimulationsDaten
    {
        public List<string> TraceData { get; } = new();
        public List<double> Wartezeiten { get; } = new();
        public List<double> SchwesternWartezeiten { get; } = new();
        public List<double> RezeptionsWartezeiten { get; } = new();
        public List<double> Gesamtprozesszeiten { get; } = new();
        public List<double> EchteAnkunftszeiten { get; } = new();

        public double DurchschnittlicheWartezeitArzt => Wartezeiten.Count > 0 ? Wartezeiten.Average() : 0;
        public double DurchschnittlicheWartezeitSchwester => SchwesternWartezeiten.Count > 0 ? SchwesternWartezeiten.Average() : 0;
        public double DurchschnittlicheWartezeitRezeption => RezeptionsWartezeiten.Count > 0 ? RezeptionsWartezeiten.Average() : 0;
        public double DurchschnittlicheGesamtprozesszeit => Gesamtprozesszeiten.Count > 0 ? Gesamtprozesszeiten.Average() : 0;

        /// <summary>
        /// Speichert ein Ereignis im Trace-Format: "Zeit;EventTyp;PatientId;ArztId;SchwesterId".
        /// </summary>
        public void LogEvent(double zeit, string eventTyp, int patientId, int? arztId = null, int? schwesterId = null)
        {
            string timeStr = zeit.ToString("000.00", CultureInfo.InvariantCulture);
            string arztStr = arztId.HasValue ? arztId.Value.ToString() : "";
            string schwesterStr = schwesterId.HasValue ? schwesterId.Value.ToString() : "";
            string logEntry = $"{timeStr};{eventTyp};{patientId};{arztStr};{schwesterStr}";
            TraceData.Add(logEntry);
        }
    }
}
