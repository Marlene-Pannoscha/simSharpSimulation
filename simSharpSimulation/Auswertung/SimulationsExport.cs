using System.IO;
using System.Linq;

namespace simSharpSimulation;

internal static class SimulationsExport
{
    private const string TraceKopfzeile = "Zeit;EventTyp;VonZustand;ZuZustand;PatientId;ArztId;SchwesterId";

    internal static void Schreibe(SimulationsDaten daten, TextWriter meldungen)
    {
        const string tracePfad = "klinik_trace.txt";
        meldungen.WriteLine($"--- Speichere Trace-File: {tracePfad} ---");
        File.WriteAllLines(tracePfad, daten.TraceData.Prepend(TraceKopfzeile));
        meldungen.WriteLine("--- Trace-File erfolgreich gespeichert. ---");

        const string prognosePfad = "prognose_report.txt";
        daten.SchreibePrognoseReport(prognosePfad);
        meldungen.WriteLine($"--- Prognose-Report gespeichert: {prognosePfad} ---");

        const string prognoseJsonPfad = "prognose_daten.json";
        daten.SchreibePrognoseDatenJson(prognoseJsonPfad);
        meldungen.WriteLine($"--- Prognose-Daten gespeichert: {prognoseJsonPfad} ---");
    }
}
