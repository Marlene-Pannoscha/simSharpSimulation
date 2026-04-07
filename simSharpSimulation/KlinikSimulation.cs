using System;
using System.Collections.Generic;
using SimSharp;

// Ein 'namespace' (Namensraum) ist wie ein Ordner für Klassen, um den Code zu organisieren und Namenskonflikte zu vermeiden.
namespace simSharpSimulation
{
    /* Enthält die komplette Ablauf-Logik der Simulation:
     - Patientenprozess
     - Generator für Ankünfte
     - Start/Run der SimSharp-Umgebung
    */
    /* internal: Klasse ist nur innerhalb dieses Projekts sichtbar.
    sealed: Keine andere Klasse darf von dieser Klasse erben (sie ist "versiegelt").
    class: Der Bauplan für die Klinik-Simulation. */
    internal sealed class KlinikSimulation
    {
        private readonly Random rnd;
        private readonly SimulationsDaten daten;

        // Schritt 1: Vorbereitung (Der Konstruktor)
        // Erhält einen Startwert für den Zufallsgenerator und ein Objekt zum Speichern der Ergebnisse.
        public KlinikSimulation(int randomSeed, SimulationsDaten daten)
        {
            this.rnd = new Random(randomSeed);
            this.daten = daten;
        }

        // Schritt 2: Der Start
        // Richtet die Simulationsuhr und die Ärzte ein und startet den Ablauf.
        public void FuehreAus()
        {
            // Wir simulieren eine Arbeitswoche: 5 Tage (Montag bis Freitag).
            // Der 3. Januar 2000 war ein Montag.
            DateTime startDatum = new DateTime(2000, 1, 3);

            for (int tag = 0; tag < 5; tag++) // 0: Montag, 1: Dienstag, ... 4: Freitag
            {
                // Jeder Tag bekommt seine eigene Simulations-Umgebung (Uhr) und neue Ärzte.
                // Das Datum wird für jeden Durchlauf um 'tag' Tage erhöht.
                var env = new Simulation(startDatum.AddDays(tag));
                var arzt = new Resource(env, capacity: SimulationKonfiguration.ANZAHL_AERZTE);
            var schwester = new Resource(env, capacity: SimulationKonfiguration.ANZAHL_SCHWESTERN);

                // Schritt 3: PatientenGenerator für den jeweiligen Tag starten
                env.Process(PatientenGenerator(env, arzt, schwester)); 
                
                // Simulation für diesen einen Tag laufen lassen (z.B. 8 Stunden / 480 Minuten)
                env.Run(TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER));
            }
        }

        /*Schritt 4: Der Weg des Patienten
        Beschreibt exakt, was passiert, von der Tür bis zur Entlassung.
        Prozesslogik eines einzelnen Patienten in der Klinik.
        /// Ablauf: Ankunft -> Schwester -> Arzt -> Abgang
        */
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource schwester, Resource arzt)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            // --- SCHWESTER (NURSE) PHASE ---
            using (var schwesterAnfrage = schwester.Request())
            {
                // EREIGNIS 2: Patient geht zur Schwester-Warteschlange
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_schwester_warteschlange", patientId);
                yield return schwesterAnfrage;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double schwesterWartezeit = nowMinutes - ankunftszeit;
                daten.SchwesternWartezeiten.Add(schwesterWartezeit);

                // EREIGNIS 3: Patient verlässt die Schwester-Warteschlange
                daten.LogEvent(nowMinutes, "verlaesst_schwester_warteschlange", patientId);
                // EREIGNIS 4: Patient geht zur Schwester
                daten.LogEvent(nowMinutes, "geht_zur_schwester", patientId);

                double schwesternBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / SimulationKonfiguration.MITTLERE_SCHWESTER_ZEIT);

                yield return env.Timeout(TimeSpan.FromMinutes(schwesternBehandlungsdauer));

                // EREIGNIS 5: Patient verlässt die Schwester
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_schwester", patientId);
            }

            // --- ARZT (DOCTOR) PHASE ---
            using (var arztAnfrage = arzt.Request())
            {
                // EREIGNIS 6: Patient geht zur Arzt-Warteschlange
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_arzt_warteschlange", patientId);
                yield return arztAnfrage;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double arztWartezeit = nowMinutes - ankunftszeit;
                daten.Wartezeiten.Add(arztWartezeit);

                // EREIGNIS 7: Patient verlässt die Arzt-Warteschlange
                daten.LogEvent(nowMinutes, "verlaesst_arzt_warteschlange", patientId);
                // EREIGNIS 8: Patient geht zum Arzt
                daten.LogEvent(nowMinutes, "geht_zu_arzt", patientId);

                double arztBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / SimulationKonfiguration.MITTLERE_BEHANDLUNGSZEIT);
                    // Behandlungsdauer wird als Exponentialverteilung modelliert, 
                    // da sie oft für Wartezeiten und Servicezeiten in Warteschlangensystemen verwendet wird.
                    // Lambda (Rate) = 1 / der mittleren Behandlungszeit, 
                    // ..rechnet der Code: 1.0 / 5.0 = 0.2 Das bedeutet: Der Arzt schafft durchschnittlich 0,2 Patienten pro Minute

                yield return env.Timeout(TimeSpan.FromMinutes(arztBehandlungsdauer));

                // EREIGNIS 9: Patient verlässt den Arzt
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_arzt", patientId);
            }

            // EREIGNIS 10: Patient verlässt die Klinik
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);
        }

        /* Schritt 3: Patienten zur Klinik kommen
        Erzeugt alle Patientenankünfte über den Tag und startet deren Prozesse.
        Ankunftszeiten werden dabei per Zufall (Normalverteilung) berechnet.
        */
        private IEnumerable<Event> PatientenGenerator(Simulation env, Resource arzt, Resource schwester)
        {
            var ankunftszeiten = new List<double>();

            // 1) Stichprobe von Ankunftszeiten aus Normalverteilung ziehen.
            for (int i = 0; i < SimulationKonfiguration.ANZAHL_PATIENTEN_TAG; i++)
            {
                double z = MathNet.Numerics.Distributions.Normal.Sample(
                    rnd,
                    SimulationKonfiguration.ERWARTUNGSWERT,
                    SimulationKonfiguration.STANDARDABWEICHUNG);

                // 2) Vor der Öffnung (z < 0) -> Warten an der Tür und werden um Punkt 0 Uhr simulativ eingelassen.
                // Innerhalb der Öffnungsdauer (0 <= z <= SIMULATIONSDAUER) -> Betreten die Klinik normal.
                // Nach der Öffnungsdauer (z > SIMULATIONSDAUER) -> Werden weggeworfen.
                if (z <= SimulationKonfiguration.SIMULATIONSDAUER)
                    ankunftszeiten.Add(z);
            }

            // 3) Sortieren ist wichtig, damit Ereignisse zeitlich korrekt nacheinander laufen.
            ankunftszeiten.Sort();

            int patientCount = 1;
            foreach (double ankunftszeit in ankunftszeiten)
            {
                // Warte bis zur nächsten geplanten Ankunftszeit (Simulationszeit, nicht Echtzeit).
                double warteBisAnkunft = ankunftszeit - (env.Now - env.StartDate).TotalMinutes;
                if (warteBisAnkunft > 0)
                    yield return env.Timeout(TimeSpan.FromMinutes(warteBisAnkunft));

                env.Process(Patient(env, patientCount, schwester, arzt));
                patientCount++;
            }
        }
    }
}
