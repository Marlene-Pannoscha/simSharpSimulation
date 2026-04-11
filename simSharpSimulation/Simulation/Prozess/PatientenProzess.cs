using System;
using System.Collections.Generic;
using System.Reflection;
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
    internal sealed class PatientenProzess
    {
        private readonly Random rnd;
        private readonly SimulationsDaten daten;

        // Schritt 1: Vorbereitung (Der Konstruktor)
        // Erhält einen Startwert für den Zufallsgenerator und ein Objekt zum Speichern der Ergebnisse.
        public PatientenProzess(int randomSeed, SimulationsDaten daten)
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
                // Jeder Tag bekommt seine eigene Simulations-Umgebung (Uhr) und neue Ressourcen.
                // Das Datum wird für jeden Durchlauf um 'tag' Tage erhöht.
                var env = new Simulation(startDatum.AddDays(tag));
                var arzt = new Resource(env, capacity: ArztKonfiguration.ANZAHL_AERZTE);
                var schwester = new Resource(env, capacity: SchwesterKonfiguration.ANZAHL_SCHWESTERN);
                var rezeption = new Resource(env, capacity: RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);

                // Schritt 3: PatientenGenerator für den jeweiligen Tag starten
                env.Process(PatientenGenerator.Generiere(env, rezeption, arzt, schwester, rnd, daten, Patient));
                // Simulation für diesen einen Tag laufen lassen (z.B. 8 Stunden / 480 Minuten)
                env.Run(TimeSpan.FromMinutes(SimulationKonfiguration.SIMULATIONSDAUER));
            }
        }

        /*Schritt 4: Der Weg des Patienten
        Beschreibt exakt, was passiert, von der Tür bis zur Entlassung.
        Prozesslogik eines einzelnen Patienten in der Klinik.
        /// Ablauf: Ankunft -> Schwester -> Arzt -> Abgang
        */
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource rezeption, Resource schwester, Resource arzt)
        {
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            // --- REZEPTION (RECEPTION) PHASE ---
            using (var rezeptionAnfrage = rezeption.Request())
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zur_rezeption_warteschlange", patientId);
                yield return rezeptionAnfrage;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double rezeptionWartezeit = nowMinutes - ankunftszeit;
                daten.RezeptionsWartezeiten.Add(rezeptionWartezeit);

                daten.LogEvent(nowMinutes, "verlaesst_rezeption_warteschlange", patientId);
                daten.LogEvent(nowMinutes, "geht_zur_rezeption", patientId);

                double rezeptionServiceDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / RezeptionKonfiguration.MITTELREZEPTIONSZEIT);

                yield return env.Timeout(TimeSpan.FromMinutes(rezeptionServiceDauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_rezeption", patientId);
            }

            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;
            bool brauchtVorbereitung = false;
            bool direktZurSchwester = false;
            bool ueberspringeSchwester = false;

            if (hatTermin)
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_termin", patientId);
                brauchtVorbereitung = rnd.NextDouble() < PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    var usersProperty = typeof(Resource).GetProperty("Users", BindingFlags.NonPublic | BindingFlags.Instance);
                    var usersCollection = usersProperty.GetValue(schwester) as System.Collections.Generic.IReadOnlyCollection<SimSharp.Request>;
                    int users = usersCollection.Count;
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_schwester_zimmer", patientId);
                    }
                    else
                    {
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER);
                        yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
                    }
                }
                else
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "keine_schwester_vorbereitung", patientId);
                    ueberspringeSchwester = true;
                }
            }
            else
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_keinen_termin", patientId);
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER);
                yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                daten.LogEvent(nowMinutes, "verlaesst_wartezimmer", patientId);
            }

            if (!ueberspringeSchwester)
            {
                // --- SCHWESTER (NURSE) PHASE ---
                using (var schwesterAnfrage = schwester.Request())
                {
                    if (!direktZurSchwester)
                    {
                        // EREIGNIS 4: Patient geht zur Schwester-Warteschlange
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_schwester_warteschlange", patientId);
                    }

                    yield return schwesterAnfrage;

                    nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                    double schwesterWartezeit = nowMinutes - ankunftszeit;
                    daten.SchwesternWartezeiten.Add(schwesterWartezeit);

                    if (!direktZurSchwester)
                    {
                        daten.LogEvent(nowMinutes, "verlaesst_schwester_warteschlange", patientId);
                    }

                    // EREIGNIS 4: Patient geht zur Schwester
                    daten.LogEvent(nowMinutes, "geht_zur_schwester", patientId);

                    bool brauchtVorbereitungNachZimmer = rnd.NextDouble() < PatientenKonfiguration.SCHWESTERZIMMER_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                    if (brauchtVorbereitungNachZimmer)
                    {
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "braucht_vorbereitung_nach_schwesterzimmer", patientId);

                        double schwesternBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT);
                        yield return env.Timeout(TimeSpan.FromMinutes(schwesternBehandlungsdauer));

                        nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                        daten.LogEvent(nowMinutes, "verlaesst_schwester", patientId);
                    }
                    else
                    {
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ohne_vorbereitung_zum_arzt", patientId);
                    }
                }
            }
            else
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "ueberspringt_schwester", patientId);
            }

            // --- ARZT (DOCTOR) PHASE ---
            using (var schwesterAnfrage = schwester.Request())
            {
                if (!direktZurSchwester)
                {
                    // EREIGNIS 4: Patient geht zur Schwester-Warteschlange
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zu_schwester_warteschlange", patientId);
                }

                yield return schwesterAnfrage;

                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                double schwesterWartezeit = nowMinutes - ankunftszeit;
                daten.SchwesternWartezeiten.Add(schwesterWartezeit);

                if (!direktZurSchwester)
                {
                    daten.LogEvent(nowMinutes, "verlaesst_schwester_warteschlange", patientId);
                }

                // EREIGNIS 4: Patient geht zur Schwester
                daten.LogEvent(nowMinutes, "geht_zur_schwester", patientId);

                double schwesternBehandlungsdauer = MathNet.Numerics.Distributions.Exponential.Sample(
                    rnd,
                    1.0 / SchwesterKonfiguration.MITTLERE_SCHWESTER_ZEIT);

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
                    1.0 / ArztKonfiguration.MITTLERE_BEHANDLUNGSZEIT);
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

    }
}
