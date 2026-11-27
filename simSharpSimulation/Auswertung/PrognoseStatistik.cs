using System;
using System.Collections.Generic;

namespace simSharpSimulation
{
    internal sealed partial class PrognoseStatistik
    {
        private const double PrognoseTrefferToleranzAnteil = 0.30;
        private const double PrognoseTrefferMindestToleranzMinuten = 5.0;
        private const int PrognoseKalibrierungMindestStichprobe = 12;
        private const double PrognoseKalibrierungLernrate = 0.04;
        private const double PrognoseKalibrierungMaxKorrekturMinuten = 12.0;
        private static readonly bool PrognoseRestzeitKalibrierungAktiv = true;

        private readonly Dictionary<int, List<PrognosePruefung>> prognoseOffen = new();
        private readonly List<PrognoseErgebnis> prognoseErgebnisse = new();
        private readonly List<PrognoseAbbruchPunkt> prognoseAbbrueche = new();
        private readonly List<PrognoseAufnahmePruefung> prognoseAufnahmePruefungen = new();
        private readonly List<PrognoseAufnahmeEntscheidung> prognoseAufnahmeEntscheidungen = new();
        private readonly Dictionary<string, PrognoseKalibrierung> prognoseKalibrierungNachPhase = new();

        public int AnzahlPrognosePruefungen { get; private set; }
        public int AnzahlPrognoseRichtig { get; private set; }
        public int AnzahlPrognoseAbbruch { get; private set; }
        public int AnzahlPrognoseAufnahmeAbgewiesen { get; private set; }

        public double PrognoseTrefferquote => AnzahlPrognosePruefungen > 0
            ? (AnzahlPrognoseRichtig / (double)AnzahlPrognosePruefungen) * 100.0
            : 0.0;

        public void ErfassePrognoseAbbruch(double zeitpunktMinuten, string phase)
        {
            AnzahlPrognoseAbbruch++;
            prognoseAbbrueche.Add(new PrognoseAbbruchPunkt(zeitpunktMinuten, phase));
        }

        public void ErfassePrognoseAufnahmepruefung(
            DateTime tag,
            double zeitpunktMinuten,
            int aufnahmeKapazitaet)
        {
            prognoseAufnahmePruefungen.Add(new PrognoseAufnahmePruefung(
                tag.Date,
                zeitpunktMinuten,
                aufnahmeKapazitaet));
        }

        public void ErfassePrognoseAufnahmeAbgewiesen(
            DateTime tag,
            double zeitpunktMinuten,
            int patientId)
        {
            AnzahlPrognoseAufnahmeAbgewiesen++;
            prognoseAufnahmeEntscheidungen.Add(new PrognoseAufnahmeEntscheidung(
                tag.Date,
                zeitpunktMinuten,
                patientId,
                Zugelassen: false,
                RestKapazitaet: 0));
        }

        public void ErfassePrognosePruefung(
            int patientId,
            string phase,
            double zeitpunktMinuten,
            double prognoseRestMinuten,
            double prognoseKorrekturMinuten,
            double prognoseBearbeitungsRestMinuten,
            double verbrauchteBearbeitungsMinuten,
            bool prognoseFertigBisSchichtende)
        {
            if (!prognoseOffen.TryGetValue(patientId, out List<PrognosePruefung>? liste))
            {
                liste = new List<PrognosePruefung>();
                prognoseOffen[patientId] = liste;
            }

            liste.Add(new PrognosePruefung(
                patientId,
                phase,
                zeitpunktMinuten,
                prognoseRestMinuten,
                prognoseKorrekturMinuten,
                prognoseBearbeitungsRestMinuten,
                verbrauchteBearbeitungsMinuten,
                prognoseFertigBisSchichtende));
            AnzahlPrognosePruefungen++;

            if (AnzahlPrognosePruefungen == 1)
            {
                Console.WriteLine($"[Prognose] Erste Prüfung: Patient {patientId}, Phase {phase}, t={zeitpunktMinuten:F2}, Rest={prognoseRestMinuten:F2}");
            }
        }

        public double ErmittlePrognoseRestzeitKorrektur(string phase)
        {
            if (!PrognoseRestzeitKalibrierungAktiv)
            {
                return 0.0;
            }

            if (!prognoseKalibrierungNachPhase.TryGetValue(phase, out var kalibrierung))
            {
                return 0.0;
            }

            return kalibrierung.Anzahl >= PrognoseKalibrierungMindestStichprobe
                ? kalibrierung.GekappteKorrekturMinuten
                : 0.0;
        }

        public void SchliessePrognosen(
            int patientId,
            double endZeitpunktMinuten,
            double? gesampelteBearbeitungszeitGesamt = null)
        {
            if (!prognoseOffen.TryGetValue(patientId, out List<PrognosePruefung>? liste) || liste.Count == 0)
                return;

            foreach (var pruefung in liste)
            {
                double istRestMinuten = Math.Max(0.0, endZeitpunktMinuten - pruefung.ZeitpunktMinuten);
                double abweichungMinuten = pruefung.PrognoseRestMinuten - istRestMinuten;
                double? abweichungProzent = istRestMinuten > 0.0001
                    ? (abweichungMinuten / istRestMinuten) * 100.0
                    : null;
                double? gesampelteBearbeitungsRestMinuten = gesampelteBearbeitungszeitGesamt.HasValue
                    ? Math.Max(0.0, gesampelteBearbeitungszeitGesamt.Value - pruefung.VerbrauchteBearbeitungsMinuten)
                    : null;
                double? abweichungBearbeitungMinuten = gesampelteBearbeitungsRestMinuten.HasValue
                    ? pruefung.PrognoseBearbeitungsRestMinuten - gesampelteBearbeitungsRestMinuten.Value
                    : null;
                double? abweichungBearbeitungProzent =
                    gesampelteBearbeitungsRestMinuten.HasValue && gesampelteBearbeitungsRestMinuten.Value > 0.0001
                        ? (abweichungBearbeitungMinuten!.Value / gesampelteBearbeitungsRestMinuten.Value) * 100.0
                        : null;
                bool korrekt;

                if (istRestMinuten <= 0.0001)
                {
                    korrekt = pruefung.PrognoseRestMinuten <= 0.0001;
                }
                else
                {
                    double toleranz = BerechnePrognoseTrefferToleranz(istRestMinuten);
                    korrekt = Math.Abs(pruefung.PrognoseRestMinuten - istRestMinuten) <= toleranz;
                }

                if (korrekt)
                    AnzahlPrognoseRichtig++;

                double basisPrognoseRestMinuten = pruefung.PrognoseRestMinuten - pruefung.PrognoseKorrekturMinuten;
                AktualisierePrognoseKalibrierung(pruefung.Phase, istRestMinuten - basisPrognoseRestMinuten);
                prognoseErgebnisse.Add(new PrognoseErgebnis(
                    pruefung.PatientId,
                    pruefung.Phase,
                    pruefung.ZeitpunktMinuten,
                    pruefung.PrognoseRestMinuten,
                    pruefung.PrognoseBearbeitungsRestMinuten,
                    gesampelteBearbeitungsRestMinuten,
                    abweichungBearbeitungMinuten,
                    abweichungBearbeitungProzent,
                    istRestMinuten,
                    abweichungMinuten,
                    abweichungProzent,
                    korrekt,
                    pruefung.PrognoseFertigBisSchichtende));
            }

            prognoseOffen.Remove(patientId);
        }

        private void AktualisierePrognoseKalibrierung(string phase, double abweichungMinuten)
        {
            if (!prognoseKalibrierungNachPhase.TryGetValue(phase, out var kalibrierung))
            {
                kalibrierung = new PrognoseKalibrierung();
                prognoseKalibrierungNachPhase[phase] = kalibrierung;
            }

            kalibrierung.Aktualisiere(abweichungMinuten);
        }

        private static double BerechnePrognoseTrefferToleranz(double istRestMinuten)
        {
            return Math.Max(
                PrognoseTrefferMindestToleranzMinuten,
                istRestMinuten * PrognoseTrefferToleranzAnteil);
        }

        private sealed class PrognoseKalibrierung
        {
            public int Anzahl { get; set; }
            public double GeglaetteteAbweichungMinuten { get; set; }
            public double GekappteKorrekturMinuten => Math.Clamp(
                GeglaetteteAbweichungMinuten,
                -PrognoseKalibrierungMaxKorrekturMinuten,
                PrognoseKalibrierungMaxKorrekturMinuten);

            public void Aktualisiere(double abweichungMinuten)
            {
                Anzahl++;
                if (Anzahl == 1)
                {
                    GeglaetteteAbweichungMinuten = abweichungMinuten;
                    return;
                }

                GeglaetteteAbweichungMinuten =
                    ((1.0 - PrognoseKalibrierungLernrate) * GeglaetteteAbweichungMinuten) +
                    (PrognoseKalibrierungLernrate * abweichungMinuten);
            }
        }

        private sealed record PrognosePruefung(
            int PatientId,
            string Phase,
            double ZeitpunktMinuten,
            double PrognoseRestMinuten,
            double PrognoseKorrekturMinuten,
            double PrognoseBearbeitungsRestMinuten,
            double VerbrauchteBearbeitungsMinuten,
            bool PrognoseFertigBisSchichtende);

        private sealed record PrognoseErgebnis(
            int PatientId,
            string Phase,
            double ZeitpunktMinuten,
            double PrognoseRestMinuten,
            double PrognoseBearbeitungsRestMinuten,
            double? GesampelteBearbeitungsRestMinuten,
            double? AbweichungBearbeitungMinuten,
            double? AbweichungBearbeitungProzent,
            double IstRestMinuten,
            double AbweichungMinuten,
            double? AbweichungProzent,
            bool Korrekt,
            bool PrognoseFertigBisSchichtende);

        private sealed record PrognoseAbbruchPunkt(
            double ZeitpunktMinuten,
            string Phase);

        private sealed record PrognoseAufnahmePruefung(
            DateTime Tag,
            double ZeitpunktMinuten,
            int AufnahmeKapazitaet);

        private sealed record PrognoseAufnahmeEntscheidung(
            DateTime Tag,
            double ZeitpunktMinuten,
            int PatientId,
            bool Zugelassen,
            int RestKapazitaet);
    }

}
