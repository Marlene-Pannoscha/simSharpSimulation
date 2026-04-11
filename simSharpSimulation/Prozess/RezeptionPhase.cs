using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    internal static class RezeptionPhase
    {
        /*
         Diese Klasse enthält NUR den Ablauf an der Rezeption.
         Ziel: Der Patient meldet sich an, wartet ggf. kurz, wird bedient und geht weiter.

         Warum als eigene Klasse?
         - Der Code ist übersichtlicher.
         - Jede Phase (Rezeption / Schwester / Arzt) ist sauber getrennt.
         - Änderungen sind später leichter.
        */

        // --- REZEPTION (RECEPTION) PHASE ---
        // Diese Methode beschreibt den kompletten Weg eines Patienten an der Rezeption.
        // Sie läuft als Simulationsprozess und gibt Ereignisse (Events) zurück.
        internal static IEnumerable<Event> DurchlaufeRezeption(
            // env: Simulationsumgebung (enthält die Simulationsuhr)
            Simulation env,
            // patientId: eindeutige Nummer des Patienten (für Logs/Trace)
            int patientId,
            // rezeption: Ressource mit begrenzter Kapazität (z. B. Anzahl Rezeptionisten)
            Resource rezeption,
            // ankunftszeit: Zeitpunkt, an dem der Patient die Klinik betreten hat
            double ankunftszeit,
            // rnd: Zufallsgenerator für die Bedienzeit
            Random rnd,
            // daten: sammelt Kennzahlen und Event-Logs
            SimulationsDaten daten)
        {
            // Request() bedeutet: Patient fordert einen Platz an der Rezeption an.
            // using sorgt dafür, dass der Platz am Ende automatisch wieder freigegeben wird.
            using (var rezeptionAnfrage = rezeption.Request())
            {
                // 1) Patient stellt sich in die Warteschlange an der Rezeption.
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zur_rezeption_warteschlange", patientId);

                // 2) Simulation wartet, bis ein Rezeptionist frei ist.
                yield return rezeptionAnfrage;

                // Aktuelle Simulationszeit in Minuten seit Tagesstart.
                double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

                // Wartezeit bis zum Start der Rezeption = aktuelle Zeit - Ankunftszeit.
                double rezeptionWartezeit = nowMinutes - ankunftszeit;
                daten.RezeptionsWartezeiten.Add(rezeptionWartezeit);

                // 3) Patient verlässt die Warteschlange und wird jetzt direkt bedient.
                daten.LogEvent(nowMinutes, "verlaesst_rezeption_warteschlange", patientId);
                daten.LogEvent(nowMinutes, "geht_zur_rezeption", patientId);

                // 4) Bedienzeit an der Rezeption (zufällig, Exponentialverteilung).
                // Mittelwert kommt aus RezeptionKonfiguration.
                double rezeptionServiceDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / RezeptionKonfiguration.MITTELREZEPTIONSZEIT);

                // Simulation läuft um diese Bedienzeit weiter.
                yield return env.Timeout(TimeSpan.FromMinutes(rezeptionServiceDauer));

                // 5) Bedienung ist fertig, Patient verlässt die Rezeption.
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_rezeption", patientId);
            }
        }
    }
}
