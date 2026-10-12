using System;
using SimSharp;

namespace simSharpSimulation
{
    internal static class SimulationsHelfer
    {
        // Alle Zeitwerte beziehen sich auf den Beginn des jeweiligen Simulationstags.
        internal static double AktuelleSimulationszeitMinuten(Simulation env)
        {
            return (env.Now - env.StartDate).TotalMinutes;
        }

        // SimSharp bevorzugt kleinere Prioritaetswerte; bei Gleichstand gilt die Anforderungsreihenfolge.
        internal static int ErmittlePatientenPrioritaet(PatientenTyp typ)
        {
            return typ switch
            {
                PatientenTyp.Kurz => 1,
                PatientenTyp.Mittel => 2,
                PatientenTyp.Lang => 3,
                _ => 3
            };
        }

        internal static Event WarteAufRessourceOderSchichtende(
            Simulation env, Request anforderung, double schichtendeMinuten)
        {
            double restMinuten = schichtendeMinuten - AktuelleSimulationszeitMinuten(env);
            Event schichtende = env.Timeout(TimeSpan.FromMinutes(restMinuten));
            // Der Prozess wird fortgesetzt, sobald die Ressource oder das Schichtende verarbeitet wird.
            return anforderung | schichtende;
        }

        internal static bool MussWartenAbbrechen(
            Simulation env, Request anforderung, double schichtendeMinuten)
        {
            // Genau am Schichtende darf auch eine gleichzeitig zugeteilte Ressource keine Behandlung mehr starten.
            return !anforderung.IsProcessed || AktuelleSimulationszeitMinuten(env) >= schichtendeMinuten;
        }

        internal static void GibVerarbeiteteAnforderungFrei(Request anforderung)
        {
            // Die bestehende Freigabelogik betrifft nur verarbeitete Anforderungen.
            // Noch wartende Anforderungen werden hier nicht storniert.
            if (anforderung.IsProcessed)
                anforderung.Dispose();
        }
    }
}
