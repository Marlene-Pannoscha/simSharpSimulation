using System;
using System.Collections.Generic;
using SimSharp;

namespace simSharpSimulation
{
    // Diese Datei enthält den fachlichen End-to-End-Ablauf eines einzelnen Patienten.
    // Hier werden die Stationsphasen in der richtigen Reihenfolge zusammengesetzt.
    internal sealed partial class PatientenProzess
    {
        /*Schritt P4: Der Weg des Patienten
        Beschreibt exakt, was passiert, von der Tür bis zur Entlassung.
        Prozesslogik eines einzelnen Patienten in der Klinik.
        /// Ablauf: Ankunft -> Rezeption -> (Wartezimmer) -> Schwester -> Arzt -> Abgang
        Hinweis zum Realitätsmodell:
        - Es gibt KEINE harte technische Priorität in den Schwester/Arzt-Queues.
        - Terminpatienten warten im Schnitt kürzer über kürzere Wartezimmerdauer.
        - Patienten ohne Termin warten im Schnitt länger, laufen aber parallel weiter.
        */
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource rezeption, List<PriorityResource> schwestern, List<PriorityResource> aerzte)
        {
            TimeSpan eingangZurRezeptionDauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_EINGANG_ZUR_REZEPTION_SEKUNDEN);
            TimeSpan interneBewegungsdauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_INNERHALB_KLINIK_SEKUNDEN);
            TimeSpan arztZumAusgangDauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_ARZT_ZUM_AUSGANG_SEKUNDEN);
            TimeSpan rezeptionZumAusgangDauer = TimeSpan.FromSeconds(SimulationKonfiguration.BEWEGUNGSZEIT_REZEPTION_ZUM_AUSGANG_SEKUNDEN);

            // Phase P-B: Individueller Patientenablauf.
            // Schritt P4.1: Aktuelle Simulationszeit in Minuten holen.
            double nowMinutes = (env.Now - env.StartDate).TotalMinutes;

            // Schritt P4.2: Patient betritt die Klinik (Startpunkt des individuellen Ablaufs).
            // EREIGNIS 1: Patient betritt die Klinik
            daten.LogEvent(nowMinutes, "betritt_klinik", patientId);

            // Schritt P4.3: Ankunftszeit merken (Basis für Wartezeit-Berechnungen).
            double ankunftszeit = nowMinutes;
            daten.EchteAnkunftszeiten.Add(ankunftszeit);

            // Schritt P4.3B: Patienten-Typ zuweisen basierend auf Verteilung.
            PatientenTyp patientenTyp = WaehlePatientenTyp(rnd);
            daten.ErfassePatientenTyp(patientenTyp);

            // Schritt P4.3A: Terminstatus früh festlegen, damit die Rezeption ihn kennt und loggen kann.
            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;
            double erwarteteRestzeitAbAnkunft =
                eingangZurRezeptionDauer.TotalMinutes +
                RezeptionKonfiguration.MITTELREZEPTIONSZEIT +
                BerechneErwarteteSchwesterRestzeit(
                    hatTermin,
                    interneBewegungsdauer,
                    hatTermin
                        ? PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT
                        : PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT) +
                BerechneRestzeitAbSchwester(interneBewegungsdauer) +
                BerechneErwarteteRestzeitNachArzt(interneBewegungsdauer, rezeptionZumAusgangDauer, arztZumAusgangDauer);
            if (!ErfassePrognoseCheckpoint(env, patientId, "Ankunft", erwarteteRestzeitAbAnkunft))
            {
                foreach (var ev in BrichWegenPrognoseAb(env, patientId, "Ankunft", TimeSpan.Zero))
                    yield return ev;
                yield break;
            }

            // Schritt P4.4: Rezeption durchlaufen.
            // --- REZEPTION (RECEPTION) PHASE ---
            daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zur_rezeption", patientId);
            yield return env.Timeout(eingangZurRezeptionDauer);
            double ankunftszeitRezeption = (env.Now - env.StartDate).TotalMinutes;

            var ersteRezeptionErgebnis = new BehandlungsPhaseErgebnis();
            foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, ankunftszeitRezeption, hatTermin, false, eingangZurRezeptionDauer, rnd, daten, ersteRezeptionErgebnis))
                yield return ev;

            if (ersteRezeptionErgebnis.PatientHatKlinikVerlassen)
                yield break;

            // Schritt P4.5: Entscheidungsvariablen für den weiteren Ablauf vorbereiten.
            bool brauchtVorbereitung = false;
            bool direktZurSchwester = false;
            bool ueberspringeSchwester = false;

            // Schritt P4.6: Prüfen, ob der Patient einen Termin hat.
            if (hatTermin)
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_termin", patientId);

                // Schritt P4.7A: Bei Termin prüfen, ob Schwester-Vorbereitung nötig ist.
                brauchtVorbereitung = rnd.NextDouble() < PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    // Schritt P4.8A: Prüfen, ob sofort eine Schwester frei ist.
                    int users = ErmittleAktiveNutzer(schwestern);
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        // Schritt P4.9A: Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                    }
                    else
                    {
                        // Schritt P4.9B: Keine Schwester frei -> zuerst ins Wartezimmer.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        // Schritt P4.9C: Der Weg ins Wartezimmer ist eine interne Bewegung.
                        yield return env.Timeout(interneBewegungsdauer);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer", patientId);

                        // Schritt P4.10B: Terminpatienten warten im Schnitt kürzer im Wartezimmer.
                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                                1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER));
                        yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                        // Das Wartezimmer wird erst verlassen, wenn eine Schwester frei wird.
                    }
                }
                else
                {
                    // Schritt P4.8B: Termin vorhanden, aber keine Schwester-Vorbereitung nötig.
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "keine_schwester_vorbereitung", patientId);
                    ueberspringeSchwester = true;
                }
            }
            else
            {
                // Schritt P4.7B: Patient hat keinen Termin.
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_keinen_termin", patientId);

                // Auch ohne Termin prüfen, ob eine Schwester-Vorbereitung anfällt.
                brauchtVorbereitung = rnd.NextDouble() < PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    // Prüfen, ob eine Schwester frei ist.
                    int users = ErmittleAktiveNutzer(schwestern);
                    if (users < SchwesterKonfiguration.ANZAHL_SCHWESTERN)
                    {
                        // Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                    }
                    else
                    {
                        // Keine Schwester frei -> zuerst ins Wartezimmer.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer", patientId);

                        // Schritt P4.9D: Auch der Weg ins Wartezimmer ist eine interne Bewegung.
                        yield return env.Timeout(interneBewegungsdauer);
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer", patientId);

                        // Ohne Termin warten Patienten im Schnitt länger im Wartezimmer auf die Schwester.
                        double wartezimmerDauer = MathNet.Numerics.Distributions.Exponential.Sample(
                            rnd,
                            1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_SCHWESTER * PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_SCHWESTER));
                        yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauer));

                        // Das Wartezimmer wird erst verlassen, wenn eine Schwester frei wird.
                    }
                }
                else
                {
                    // Keine Schwester-Vorbereitung nötig -> Schwester wird übersprungen.
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "keine_schwester_vorbereitung", patientId);
                    ueberspringeSchwester = true;
                }
            }

            // Schritt P4.10: Falls Schwester nicht übersprungen wird,
            // Schwester-Phase (Variante mit Prüfung) durchlaufen.
            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "NachRezeption",
                BerechneSchwesterRestzeitNachRezeption(
                    brauchtVorbereitung,
                    direktZurSchwester,
                    hatTermin,
                    interneBewegungsdauer) +
                BerechneRestzeitAbSchwester(interneBewegungsdauer) +
                BerechneErwarteteRestzeitNachArzt(interneBewegungsdauer, rezeptionZumAusgangDauer, arztZumAusgangDauer)))
            {
                foreach (var ev in BrichWegenPrognoseAb(env, patientId, "NachRezeption", rezeptionZumAusgangDauer))
                    yield return ev;
                yield break;
            }

            if (!ueberspringeSchwester)
            {
                if (!ErfassePrognoseCheckpoint(
                    env,
                    patientId,
                    "VorSchwester",
                    interneBewegungsdauer.TotalMinutes +
                    SchwesterKonfiguration.MITTLERE_BEHANDLUNGSDAUER +
                    BerechneRestzeitAbSchwester(interneBewegungsdauer) +
                    BerechneErwarteteRestzeitNachArzt(interneBewegungsdauer, rezeptionZumAusgangDauer, arztZumAusgangDauer)))
                {
                    foreach (var ev in BrichWegenPrognoseAb(env, patientId, "VorSchwester", interneBewegungsdauer))
                        yield return ev;
                    yield break;
                }

                var (schwesterRes, schwesterId) = WaehleRessource(schwestern);
                var schwesterErgebnis = new BehandlungsPhaseErgebnis();
                // --- SCHWESTER (NURSE) PHASE ---
                foreach (var ev in SchwesterPhase.DurchlaufeSchwester(
                    env,
                    patientId,
                    schwesterRes,
                    schwesterId,
                    patientenTyp,
                    ankunftszeit,
                    hatTermin,
                    direktZurSchwester,
                    interneBewegungsdauer,
                    rnd,
                    daten,
                    schwesterErgebnis))
                    yield return ev;

                if (schwesterErgebnis.PatientHatKlinikVerlassen)
                    yield break;

                if (!ErfassePrognoseCheckpoint(
                    env,
                    patientId,
                    "NachSchwester",
                    BerechneRestzeitAbSchwester(interneBewegungsdauer) +
                    BerechneErwarteteRestzeitNachArzt(interneBewegungsdauer, rezeptionZumAusgangDauer, arztZumAusgangDauer)))
                {
                    foreach (var ev in BrichWegenPrognoseAb(env, patientId, "NachSchwester", interneBewegungsdauer))
                        yield return ev;
                    yield break;
                }
            }
            else
            {
                // Schritt P4.10B: Schwester wird in diesem Pfad übersprungen (nur bei Terminpatienten).
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "ueberspringt_schwester", patientId);
            }

            // Schritt P4.11: Wartevorgang für den Arzt.
            // Alle Patienten (mit/ohne Termin, mit/ohne Schwester) kommen hier an, bevor sie zum Arzt gehen.
            daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer_fuer_arzt", patientId);

            // Der Weg ins Arzt-Wartezimmer ist ebenfalls eine interne Bewegung.
            yield return env.Timeout(interneBewegungsdauer);
            daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer_fuer_arzt", patientId);

            double wartezeitFaktor = hatTermin
                ? PatientenKonfiguration.MIT_TERMIN_WARTEZIMMER_FAKTOR_ARZT
                : PatientenKonfiguration.OHNE_TERMIN_WARTEZIMMER_FAKTOR_ARZT;
            double wartezimmerDauerArzt = MathNet.Numerics.Distributions.Exponential.Sample(
                rnd, 1.0 / (PatientenKonfiguration.MITTLERE_WARTEZIMMER_DAUER_ARZT * wartezeitFaktor));
            yield return env.Timeout(TimeSpan.FromMinutes(wartezimmerDauerArzt));

            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "VorArzt",
                interneBewegungsdauer.TotalMinutes +
                ArztKonfiguration.MITTLERE_BEHANDLUNGSDAUER +
                BerechneErwarteteRestzeitNachArzt(interneBewegungsdauer, rezeptionZumAusgangDauer, arztZumAusgangDauer)))
            {
                foreach (var ev in BrichWegenPrognoseAb(env, patientId, "VorArzt", interneBewegungsdauer))
                    yield return ev;
                yield break;
            }

            // Schritt P4.12: Arzt-Phase durchlaufen.
            // --- ARZT (DOCTOR) PHASE ---
            var (arztRes, arztId) = WaehleRessource(aerzte);
            var arztErgebnis = new BehandlungsPhaseErgebnis();
            foreach (var ev in ArztPhase.DurchlaufeArzt(env, patientId, arztRes, arztId, patientenTyp, ankunftszeit, hatTermin, interneBewegungsdauer, rnd, daten, arztErgebnis))
                yield return ev;

            if (arztErgebnis.PatientHatKlinikVerlassen)
                yield break;

            // Schritt P4.13: Nach dem Arzt entscheidet sich, ob der Patient noch einmal zur Rezeption muss.
            bool gehtNachArztZurRezeption = rnd.NextDouble() < WahrscheinlichkeitNachArztZurRezeption;
            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "NachArzt",
                BerechneRestzeitNachArztMitKonkretemPfad(
                    gehtNachArztZurRezeption,
                    interneBewegungsdauer,
                    rezeptionZumAusgangDauer,
                    arztZumAusgangDauer)))
            {
                foreach (var ev in BrichWegenPrognoseAb(env, patientId, "NachArzt", arztZumAusgangDauer))
                    yield return ev;
                yield break;
            }
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, gehtNachArztZurRezeption ? "geht_nach_arzt_zur_rezeption" : "verlaesst_nach_arzt_ohne_rezeption", patientId);

            if (gehtNachArztZurRezeption)
            {
                yield return env.Timeout(interneBewegungsdauer);
                nowMinutes = (env.Now - env.StartDate).TotalMinutes;
                var zweiteRezeptionErgebnis = new BehandlungsPhaseErgebnis();
                foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, nowMinutes, hatTermin, true, interneBewegungsdauer, rnd, daten, zweiteRezeptionErgebnis))
                    yield return ev;

                if (zweiteRezeptionErgebnis.PatientHatKlinikVerlassen)
                    yield break;

                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zum_ausgang", patientId);
                yield return env.Timeout(rezeptionZumAusgangDauer);
            }
            else
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_zum_ausgang", patientId);
                yield return env.Timeout(arztZumAusgangDauer);
            }

            // Schritt P4.14: Patient verlässt die Klinik (Ende des Patientenablaufs).
            // EREIGNIS 10: Patient verlässt die Klinik
            nowMinutes = (env.Now - env.StartDate).TotalMinutes;
            daten.LogEvent(nowMinutes, "verlaesst_klinik", patientId);

            // Gesamtprozesszeit = von Klinik-Eintritt bis Klinik-Austritt.
            double gesamtprozesszeit = nowMinutes - ankunftszeit;
            daten.ErfasseGesamtprozesszeit(gesamtprozesszeit, hatTermin);
            daten.SchliessePrognosen(patientId, nowMinutes);
        }
    }
}
