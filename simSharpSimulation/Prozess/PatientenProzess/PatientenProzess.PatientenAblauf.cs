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
        - Schwester/Arzt nutzen gemeinsame Pools und priorisieren nach Patientenbedarf.
        - Terminpatienten warten im Schnitt kürzer über kürzere Wartezimmerdauer.
        - Patienten ohne Termin warten im Schnitt länger, laufen aber parallel weiter.
        */
        private IEnumerable<Event> Patient(Simulation env, int patientId, Resource rezeption, BeweglicherSchwesterPool schwestern, BeweglicherArztPool aerzte)
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
            PatientenTyp patientenTyp = PatientenKonfiguration.WaehlePatientenTyp(rnd);
            daten.ErfassePatientenTyp(patientenTyp);

            // Schritt P4.3A: Terminstatus früh festlegen, damit die Rezeption ihn kennt und loggen kann.
            bool hatTermin = rnd.NextDouble() < PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT;
            double ersteRezeptionsdauer = ZieheRezeptionsdauer(rnd);
            double zweiteRezeptionsdauer = ZieheRezeptionsdauer(rnd);
            double schwesterBehandlungsdauer = ZieheSchwesterBehandlungsdauer(patientenTyp, rnd);
            double arztBehandlungsdauer = ZieheArztBehandlungsdauer(patientenTyp, rnd);
            double schwesterWartezimmerdauer = ZieheSchwesterWartezimmerdauer(hatTermin, rnd);
            double arztWartezimmerdauer = ZieheArztWartezimmerdauer(hatTermin, rnd);
            double vorbereitungsWahrscheinlichkeit = hatTermin
                ? PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT
                : PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT;
            bool brauchtVorbereitung = rnd.NextDouble() < vorbereitungsWahrscheinlichkeit;
            bool gehtNachArztZurRezeption = rnd.NextDouble() < WahrscheinlichkeitNachArztZurRezeption;
            double prognoseBearbeitungsRestzeitAbAnkunft =
                ersteRezeptionsdauer +
                (brauchtVorbereitung ? schwesterBehandlungsdauer : 0.0) +
                arztBehandlungsdauer +
                (gehtNachArztZurRezeption ? zweiteRezeptionsdauer : 0.0);
            PlaneRezeptionsAnkunft(
                env,
                patientId,
                eingangZurRezeptionDauer.TotalMinutes,
                ersteRezeptionsdauer);
            double erwarteteRestzeitAbAnkunft =
                eingangZurRezeptionDauer.TotalMinutes +
                SchaetzeRezeptionsQueueWartezeit(env, patientId, ersteRezeptionsdauer, eingangZurRezeptionDauer.TotalMinutes) +
                ersteRezeptionsdauer +
                (brauchtVorbereitung
                    ? ((2.0 * interneBewegungsdauer.TotalMinutes) + schwesterWartezimmerdauer + schwesterBehandlungsdauer)
                    : 0.0) +
                (brauchtVorbereitung ? SchaetzeSchwesterQueueWartezeit(
                    env,
                    patientId,
                    schwesterBehandlungsdauer,
                    patientenTyp,
                    eingangZurRezeptionDauer.TotalMinutes + ersteRezeptionsdauer + interneBewegungsdauer.TotalMinutes + schwesterWartezimmerdauer) : 0.0) +
                BerechneRestzeitAbSchwester(interneBewegungsdauer, arztBehandlungsdauer, arztWartezimmerdauer) +
                SchaetzeArztQueueWartezeit(
                    env,
                    patientId,
                    arztBehandlungsdauer,
                    patientenTyp,
                    eingangZurRezeptionDauer.TotalMinutes +
                    ersteRezeptionsdauer +
                    (brauchtVorbereitung ? ((2.0 * interneBewegungsdauer.TotalMinutes) + schwesterWartezimmerdauer + schwesterBehandlungsdauer) : 0.0) +
                    interneBewegungsdauer.TotalMinutes +
                    arztWartezimmerdauer) +
                (gehtNachArztZurRezeption ? SchaetzeRezeptionsQueueWartezeit(env, patientId, zweiteRezeptionsdauer) : 0.0) +
                BerechneRestzeitNachArztMitKonkretemPfad(
                    gehtNachArztZurRezeption,
                    interneBewegungsdauer,
                    rezeptionZumAusgangDauer,
                    arztZumAusgangDauer,
                    zweiteRezeptionsdauer);

            if (!DarfPatientNachAufnahmeprognoseNochRein(env, patientId))
            {
                foreach (var ev in WeiseWegenAufnahmeprognoseAb(env, patientId, TimeSpan.Zero))
                    yield return ev;
                yield break;
            }

            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "Ankunft",
                erwarteteRestzeitAbAnkunft,
                prognoseBearbeitungsRestzeitAbAnkunft,
                0.0))
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
            foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, ankunftszeitRezeption, hatTermin, false, rezeptionZumAusgangDauer, ersteRezeptionsdauer, daten, ersteRezeptionErgebnis, rezeptionStatus))
                yield return ev;

            if (ersteRezeptionErgebnis.PatientHatKlinikVerlassen)
            {
                EntferneAktivePatientenPrognose(patientId);
                yield break;
            }

            // Schritt P4.5: Entscheidungsvariablen für den weiteren Ablauf vorbereiten.
            bool direktZurSchwester = false;
            bool ueberspringeSchwester = false;

            // Schritt P4.6: Prüfen, ob der Patient einen Termin hat.
            if (hatTermin)
            {
                daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "hat_termin", patientId);

                // Schritt P4.7A: Bei Termin prüfen, ob Schwester-Vorbereitung nötig ist.
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    // Schritt P4.8A: Prüfen, ob sofort eine Schwester frei ist.
                    if (schwestern.IstFrei)
                    {
                        // Schritt P4.9A: Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                    }
                    else
                    {
                        // Keine Schwester frei: Der Weg ins Wartezimmer folgt nach dem NachRezeption-Checkpoint.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);

                        // Schritt P4.9C: Der Weg ins Wartezimmer ist eine interne Bewegung.

                        // Schritt P4.10B: Terminpatienten warten im Schnitt kürzer im Wartezimmer.

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
                if (brauchtVorbereitung)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "benoetigt_schwester_vorbereitung", patientId);

                    // Prüfen, ob eine Schwester frei ist.
                    if (schwestern.IstFrei)
                    {
                        // Schwester ist frei -> direkt ins Schwesterzimmer.
                        direktZurSchwester = true;
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_frei", patientId);
                    }
                    else
                    {
                        // Keine Schwester frei: Der Weg ins Wartezimmer folgt nach dem NachRezeption-Checkpoint.
                        daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "schwester_nicht_frei", patientId);

                        // Schritt P4.9D: Auch der Weg ins Wartezimmer ist eine interne Bewegung.

                        // Ohne Termin warten Patienten im Schnitt länger im Wartezimmer auf die Schwester.

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
            double prognoseBearbeitungsRestzeitNachRezeption =
                (brauchtVorbereitung ? schwesterBehandlungsdauer : 0.0) +
                arztBehandlungsdauer +
                (gehtNachArztZurRezeption ? zweiteRezeptionsdauer : 0.0);
            double prognoseBearbeitungsRestzeitVorSchwester =
                schwesterBehandlungsdauer +
                arztBehandlungsdauer +
                (gehtNachArztZurRezeption ? zweiteRezeptionsdauer : 0.0);
            double prognoseBearbeitungsRestzeitNachSchwester =
                arztBehandlungsdauer +
                (gehtNachArztZurRezeption ? zweiteRezeptionsdauer : 0.0);
            double verbrauchteBearbeitungszeitNachRezeption = ersteRezeptionsdauer;
            double verbrauchteBearbeitungszeitNachSchwester =
                ersteRezeptionsdauer +
                (brauchtVorbereitung ? schwesterBehandlungsdauer : 0.0);

            if (brauchtVorbereitung)
            {
                PlaneSchwesterAnkunft(
                    env,
                    patientId,
                    direktZurSchwester ? 0.0 : interneBewegungsdauer.TotalMinutes + schwesterWartezimmerdauer,
                    schwesterBehandlungsdauer,
                    patientenTyp);
            }
            else
            {
                PlaneArztAnkunft(
                    env,
                    patientId,
                    interneBewegungsdauer.TotalMinutes + arztWartezimmerdauer,
                    arztBehandlungsdauer,
                    patientenTyp);
            }

            if (IstDurchAufnahmeprognoseAbgewiesen(env, patientId))
            {
                foreach (var ev in WeiseWegenAufnahmeprognoseAb(env, patientId, rezeptionZumAusgangDauer))
                    yield return ev;
                yield break;
            }

            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "NachRezeption",
                BerechneSchwesterRestzeitNachRezeption(
                    brauchtVorbereitung,
                    direktZurSchwester,
                    hatTermin,
                    interneBewegungsdauer,
                    schwesterBehandlungsdauer,
                    schwesterWartezimmerdauer) +
                (brauchtVorbereitung ? SchaetzeSchwesterQueueWartezeit(
                    env,
                    patientId,
                    schwesterBehandlungsdauer,
                    patientenTyp,
                    direktZurSchwester ? 0.0 : interneBewegungsdauer.TotalMinutes + schwesterWartezimmerdauer) : 0.0) +
                BerechneRestzeitAbSchwester(interneBewegungsdauer, arztBehandlungsdauer, arztWartezimmerdauer) +
                SchaetzeArztQueueWartezeit(
                    env,
                    patientId,
                    arztBehandlungsdauer,
                    patientenTyp,
                    brauchtVorbereitung
                        ? ((direktZurSchwester ? 0.0 : interneBewegungsdauer.TotalMinutes + schwesterWartezimmerdauer) +
                           schwesterBehandlungsdauer +
                           interneBewegungsdauer.TotalMinutes +
                           arztWartezimmerdauer)
                        : interneBewegungsdauer.TotalMinutes + arztWartezimmerdauer) +
                (gehtNachArztZurRezeption ? SchaetzeRezeptionsQueueWartezeit(env, patientId, zweiteRezeptionsdauer) : 0.0) +
                BerechneRestzeitNachArztMitKonkretemPfad(
                    gehtNachArztZurRezeption,
                    interneBewegungsdauer,
                    rezeptionZumAusgangDauer,
                    arztZumAusgangDauer,
                    zweiteRezeptionsdauer),
                prognoseBearbeitungsRestzeitNachRezeption,
                verbrauchteBearbeitungszeitNachRezeption))
            {
                foreach (var ev in BrichWegenPrognoseAb(env, patientId, "NachRezeption", rezeptionZumAusgangDauer))
                    yield return ev;
                yield break;
            }

            if (!ueberspringeSchwester)
            {
                if (!direktZurSchwester)
                {
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "geht_ins_wartezimmer_schwester", patientId);
                    yield return env.Timeout(interneBewegungsdauer);
                    daten.LogEvent((env.Now - env.StartDate).TotalMinutes, "betritt_wartezimmer_schwester", patientId);
                    yield return env.Timeout(TimeSpan.FromMinutes(schwesterWartezimmerdauer));
                }

                if (!ErfassePrognoseCheckpoint(
                    env,
                    patientId,
                    "VorSchwester",
                    interneBewegungsdauer.TotalMinutes +
                    schwesterBehandlungsdauer +
                    SchaetzeSchwesterQueueWartezeit(env, patientId, schwesterBehandlungsdauer, patientenTyp) +
                    BerechneRestzeitAbSchwester(interneBewegungsdauer, arztBehandlungsdauer, arztWartezimmerdauer) +
                    SchaetzeArztQueueWartezeit(
                        env,
                        patientId,
                        arztBehandlungsdauer,
                        patientenTyp,
                        schwesterBehandlungsdauer + interneBewegungsdauer.TotalMinutes + arztWartezimmerdauer) +
                    (gehtNachArztZurRezeption ? SchaetzeRezeptionsQueueWartezeit(env, patientId, zweiteRezeptionsdauer) : 0.0) +
                    BerechneRestzeitNachArztMitKonkretemPfad(
                        gehtNachArztZurRezeption,
                        interneBewegungsdauer,
                        rezeptionZumAusgangDauer,
                        arztZumAusgangDauer,
                        zweiteRezeptionsdauer),
                    prognoseBearbeitungsRestzeitVorSchwester,
                    verbrauchteBearbeitungszeitNachRezeption))
                {
                    foreach (var ev in BrichWegenPrognoseAb(env, patientId, "VorSchwester", interneBewegungsdauer))
                        yield return ev;
                    yield break;
                }

                var schwesterErgebnis = new BehandlungsPhaseErgebnis();
                // --- SCHWESTER (NURSE) PHASE ---
                foreach (var ev in SchwesterPhase.DurchlaufeSchwester(
                    env,
                    patientId,
                    schwestern,
                    patientenTyp,
                    ankunftszeit,
                    hatTermin,
                    direktZurSchwester,
                    interneBewegungsdauer,
                    schwesterBehandlungsdauer,
                    daten,
                    schwesterErgebnis,
                    schwesterStatus))
                    yield return ev;

                if (schwesterErgebnis.PatientHatKlinikVerlassen)
                {
                    EntferneAktivePatientenPrognose(patientId);
                    yield break;
                }

                if (IstDurchAufnahmeprognoseAbgewiesen(env, patientId))
                {
                    foreach (var ev in WeiseWegenAufnahmeprognoseAb(env, patientId, interneBewegungsdauer))
                        yield return ev;
                    yield break;
                }

                PlaneArztAnkunft(
                    env,
                    patientId,
                    interneBewegungsdauer.TotalMinutes + arztWartezimmerdauer,
                    arztBehandlungsdauer,
                    patientenTyp);

                if (!ErfassePrognoseCheckpoint(
                    env,
                    patientId,
                    "NachSchwester",
                    BerechneRestzeitAbSchwester(interneBewegungsdauer, arztBehandlungsdauer, arztWartezimmerdauer) +
                    SchaetzeArztQueueWartezeit(
                        env,
                        patientId,
                        arztBehandlungsdauer,
                        patientenTyp,
                        interneBewegungsdauer.TotalMinutes + arztWartezimmerdauer) +
                    (gehtNachArztZurRezeption ? SchaetzeRezeptionsQueueWartezeit(env, patientId, zweiteRezeptionsdauer) : 0.0) +
                    BerechneRestzeitNachArztMitKonkretemPfad(
                        gehtNachArztZurRezeption,
                        interneBewegungsdauer,
                        rezeptionZumAusgangDauer,
                        arztZumAusgangDauer,
                        zweiteRezeptionsdauer),
                    prognoseBearbeitungsRestzeitNachSchwester,
                    verbrauchteBearbeitungszeitNachSchwester))
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

            yield return env.Timeout(TimeSpan.FromMinutes(arztWartezimmerdauer));

            if (IstDurchAufnahmeprognoseAbgewiesen(env, patientId))
            {
                foreach (var ev in WeiseWegenAufnahmeprognoseAb(env, patientId, interneBewegungsdauer))
                    yield return ev;
                yield break;
            }

            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "VorArzt",
                interneBewegungsdauer.TotalMinutes +
                arztBehandlungsdauer +
                SchaetzeArztQueueWartezeit(env, patientId, arztBehandlungsdauer, patientenTyp) +
                (gehtNachArztZurRezeption ? SchaetzeRezeptionsQueueWartezeit(env, patientId, zweiteRezeptionsdauer) : 0.0) +
                BerechneRestzeitNachArztMitKonkretemPfad(
                    gehtNachArztZurRezeption,
                    interneBewegungsdauer,
                    rezeptionZumAusgangDauer,
                    arztZumAusgangDauer,
                    zweiteRezeptionsdauer),
                prognoseBearbeitungsRestzeitNachSchwester,
                verbrauchteBearbeitungszeitNachSchwester))
            {
                foreach (var ev in BrichWegenPrognoseAb(env, patientId, "VorArzt", interneBewegungsdauer))
                    yield return ev;
                yield break;
            }

            // Schritt P4.12: Arzt-Phase durchlaufen.
            // --- ARZT (DOCTOR) PHASE ---
            var arztErgebnis = new BehandlungsPhaseErgebnis();
            foreach (var ev in ArztPhase.DurchlaufeArzt(env, patientId, aerzte, patientenTyp, ankunftszeit, hatTermin, interneBewegungsdauer, arztBehandlungsdauer, daten, arztErgebnis, arztStatus))
                yield return ev;

            if (arztErgebnis.PatientHatKlinikVerlassen)
            {
                EntferneAktivePatientenPrognose(patientId);
                yield break;
            }

            if (IstDurchAufnahmeprognoseAbgewiesen(env, patientId))
            {
                foreach (var ev in WeiseWegenAufnahmeprognoseAb(env, patientId, arztZumAusgangDauer))
                    yield return ev;
                yield break;
            }

            // Schritt P4.13: Nach dem Arzt entscheidet sich, ob der Patient noch einmal zur Rezeption muss.
            double prognoseBearbeitungsRestzeitNachArzt =
                gehtNachArztZurRezeption ? zweiteRezeptionsdauer : 0.0;
            double verbrauchteBearbeitungszeitNachArzt =
                verbrauchteBearbeitungszeitNachSchwester +
                arztBehandlungsdauer;
            if (gehtNachArztZurRezeption)
            {
                PlaneRezeptionsAnkunft(
                    env,
                    patientId,
                    interneBewegungsdauer.TotalMinutes,
                    zweiteRezeptionsdauer);
            }

            if (!ErfassePrognoseCheckpoint(
                env,
                patientId,
                "NachArzt",
                BerechneRestzeitNachArztMitKonkretemPfad(
                    gehtNachArztZurRezeption,
                    interneBewegungsdauer,
                    rezeptionZumAusgangDauer,
                    arztZumAusgangDauer,
                    zweiteRezeptionsdauer) +
                (gehtNachArztZurRezeption ? SchaetzeRezeptionsQueueWartezeit(env, patientId, zweiteRezeptionsdauer, interneBewegungsdauer.TotalMinutes) : 0.0),
                prognoseBearbeitungsRestzeitNachArzt,
                verbrauchteBearbeitungszeitNachArzt))
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
                foreach (var ev in RezeptionPhase.DurchlaufeRezeption(env, patientId, rezeption, nowMinutes, hatTermin, true, rezeptionZumAusgangDauer, zweiteRezeptionsdauer, daten, zweiteRezeptionErgebnis, rezeptionStatus))
                    yield return ev;

                if (zweiteRezeptionErgebnis.PatientHatKlinikVerlassen)
                {
                    EntferneAktivePatientenPrognose(patientId);
                    yield break;
                }

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
            double gesampelteBearbeitungszeitGesamt =
                ersteRezeptionsdauer +
                (brauchtVorbereitung ? schwesterBehandlungsdauer : 0.0) +
                arztBehandlungsdauer +
                (gehtNachArztZurRezeption ? zweiteRezeptionsdauer : 0.0);
            daten.ErfasseGesamtprozesszeit(gesamtprozesszeit, hatTermin);
            daten.SchliessePrognosen(patientId, nowMinutes, gesampelteBearbeitungszeitGesamt);
            EntferneAktivePatientenPrognose(patientId);
        }
    }
}
