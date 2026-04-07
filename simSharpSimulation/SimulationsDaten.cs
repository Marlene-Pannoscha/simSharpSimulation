using System.Collections.Generic;
using System.Globalization;

namespace simSharpSimulation
{
    /// <summary>
    /// Hält alle während der Simulation gesammelten Daten (Trace, Wartezeiten, Ankünfte).
    /// Diese Klasse entkoppelt Datenspeicherung von der eigentlichen Simulationslogik.
    /// </summary>
    internal sealed class SimulationsDaten
    {
        public List<string> TraceData { get; } = new();
        public List<double> Wartezeiten { get; } = new();
        public List<double> SchwesternWartezeiten { get; } = new();
        public List<double> EchteAnkunftszeiten { get; } = new();

        /// <summary>
        /// Speichert ein Ereignis im Trace-Format: "Zeit;EventTyp;PatientId".
        /// </summary>
        public void LogEvent(double zeit, string eventTyp, int patientId)
        {
            string timeStr = zeit.ToString("000.00", CultureInfo.InvariantCulture);
            string logEntry = $"{timeStr};{eventTyp};{patientId}";
            TraceData.Add(logEntry);
        }
    }
}
