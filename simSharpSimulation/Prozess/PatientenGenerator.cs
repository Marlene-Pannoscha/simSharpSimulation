using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;

namespace simSharpSimulation
{
    // Diese Klasse erzeugt die Patienten-Ankünfte für einen Tag.
    // Sie entscheidet nur WANN Patienten ankommen –
    // was danach passiert, übernimmt der eigentliche Patientenprozess.
    internal static class PatientenGenerator
    {
        public static IEnumerable<Event> Generiere(
            Simulation env,
            Resource rezeption,
            BeweglicherArztPool aerzte,
            BeweglicherSchwesterPool schwestern,
            Random rnd,
            SimulationsDaten daten,
            int patientIdStart,
            Func<Simulation, int, Resource, BeweglicherSchwesterPool, BeweglicherArztPool, IEnumerable<Event>> patientFactory)
        {
            // Hier sammeln wir alle geplanten Ankunftszeitpunkte (in Minuten ab Tagesstart).
            // drawIndex sorgt bei gleichen Zeiten für eine stabile (FIFO-)Reihenfolge.
            var ankunftszeiten = new List<(double zeit, int drawIndex)>();

            // Wir ziehen zunächst viele mögliche Ankunftszeiten aus einer Normalverteilung.
            // Idee: Die meisten Patienten kommen um den Mittelwert herum,
            // wenige sehr früh oder sehr spät.
            for (int i = 0; i < PatientenKonfiguration.ANZAHL_PATIENTEN_TAG; i++)
            {
                double z = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG);

                // Nur Ankünfte innerhalb des Simulationstages übernehmen.
                // Alles darüber liegt außerhalb der Tagesdauer und wird verworfen.
                if (z <= SimulationKonfiguration.SIMULATIONSDAUER)
                    ankunftszeiten.Add((z, i));
            }

            // Wichtig: chronologische Reihenfolge, damit die Simulation realistisch abläuft.
            ankunftszeiten = ankunftszeiten
                .OrderBy(x => x.zeit)
                .ThenBy(x => x.drawIndex)
                .ToList();

            // Vor Öffnungszeit (t < 0) kommende Patienten warten in einer expliziten FIFO-Warteschlange.
            // Bei Öffnung (t = 0) werden sie in genau dieser Reihenfolge in den Prozess gegeben.
            var warteschlangeVorOeffnung = ankunftszeiten
                .Where(x => x.zeit < 0)
                .ToList();

            var ankuenfteAbOeffnung = ankunftszeiten
                .Where(x => x.zeit >= 0)
                .ToList();

            int patientCount = patientIdStart;
            foreach (var eintrag in warteschlangeVorOeffnung)
            {
                // Optionales Trace-Event: Patient ist vor Öffnungszeit da und wartet.
                daten.LogEvent(eintrag.zeit, "wartet_vor_oeffnung", patientCount);

                // Bei Öffnung werden wartende Patienten nacheinander in FIFO-Reihenfolge gestartet.
                env.Process(patientFactory(env, patientCount, rezeption, schwestern, aerzte));
                patientCount++;
            }

            foreach (var eintrag in ankuenfteAbOeffnung)
            {
                double ankunftszeit = eintrag.zeit;

                // Berechnet, wie lange die Simulation noch warten muss,
                // bis der nächste Patient "ankommt".
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;

                // "yield return" pausiert den Generator bis der Timeout vorbei ist.
                // Danach läuft er hier weiter und erzeugt den nächsten Prozess.
                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                // Startet den individuellen Ablauf für genau diesen Patienten.
                env.Process(patientFactory(env, patientCount, rezeption, schwestern, aerzte));
                patientCount++;
            }
        }
    }
}
