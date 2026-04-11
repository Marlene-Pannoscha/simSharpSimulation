using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    internal static class PatientenGenerator
    {
        /* Diese Methode erzeugt alle Patienten-Ankünfte für einen Tag.
        1) Es werden Ankunftszeiten zufällig erzeugt (Normalverteilung)
        2) Zeiten außerhalb der Simulationsdauer werden ignoriert
        3) Zeiten werden sortiert, damit Patienten in richtiger Reihenfolge kommen
        4) Zu jeder Zeit wird ein neuer Patienten-Prozess gestartet
        */

        // public: von überall im Projekt aufrufbar
        // static: gehört zur Klasse, kein Objekt nötig
        // IEnumerable<Event>: gibt eine Folge von SimSharp-Ereignissen zurück
        // Generiere(...): Name der Methode
        public static IEnumerable<Event> Generiere(
            // env = Simulations-Umgebung (Simulationsuhr + Engine)
            Simulation env,
            // rezeption = Ressource für den Empfang
            Resource rezeption,
            // arzt = Ressource für die Ärzte
            Resource arzt,
            // schwester = Ressource für die Schwestern
            Resource schwester,
            // rnd = Zufallsgenerator für zufällige Zeiten
            Random rnd,
            // daten = Objekt, in dem Statistik und Trace gesammelt werden
            SimulationsDaten daten,
            // patientFactory = Funktion, die den Ablauf eines einzelnen Patienten erstellt, 
            // wird der konkrete Patienten-Ablauf erzeugt und dann in der Simulation gestartet.
            Func<Simulation, int, Resource, Resource, Resource, IEnumerable<Event>> patientFactory)
        {
            var ankunftszeiten = new List<double>();

            for (int i = 0; i < PatientenKonfiguration.ANZAHL_PATIENTEN_TAG; i++)
            {
                // z = geplanter Ankunftszeitpunkt (in Minuten seit Tagesstart)
                double z = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    PatientenKonfiguration.ERWARTUNGSWERT,
                    PatientenKonfiguration.STANDARDABWEICHUNG);

                if (z <= SimulationKonfiguration.SIMULATIONSDAUER)
                    ankunftszeiten.Add(z);
            }

            // Wichtig: Ohne Sortierung kämen Patienten ggf. in falscher Reihenfolge an.
            ankunftszeiten.Sort();

            int patientCount = 1;
            foreach (double ankunftszeit in ankunftszeiten)
            {
                // Warte in der Simulationsuhr bis zur nächsten Ankunft.
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;
                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                // Startet den eigentlichen Ablauf des Patienten
                // (definiert in PatientenProzess.Patient).
                env.Process(patientFactory(env, patientCount, rezeption, schwester, arzt));
                patientCount++;
            }
        }
    }
}
