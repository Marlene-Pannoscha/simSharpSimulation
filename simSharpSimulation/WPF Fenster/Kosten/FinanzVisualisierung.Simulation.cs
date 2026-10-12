using System.Globalization;
using MathNet.Numerics.Distributions;

namespace simSharpSimulation;

internal static partial class FinanzVisualisierung
{
    public static FinanzErgebnis Simuliere(int anzahlAerzte, int anzahlSchwestern, string zeitraum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlSchwestern, 1);

        // Im Jahresmodus werden Tageswerte spaeter zu Monatswerten verdichtet.
        string normalisierterZeitraum = NormalisiereZeitraum(zeitraum);
        bool aggregiereNachMonat = string.Equals(normalisierterZeitraum, "Jahr", StringComparison.OrdinalIgnoreCase);

        Random rnd = new(42);
        List<int> tage = ErmittleTageFuerZeitraum(normalisierterZeitraum);
        Dictionary<string, FinanzAggregat> aggregierteWerte = new();
        List<FinanzTagespunkt> tagespunkte = new();

        Dictionary<string, double> saisonGewinn = new()
        {
            ["Winter"] = 0.0,
            ["Fruehling"] = 0.0,
            ["Sommer"] = 0.0,
            ["Herbst"] = 0.0,
        };

        int gesamteNachfrage = 0;
        int gesamtBehandelt = 0;

        // Einzelne Tagesergebnisse behalten, damit Kostenkomponenten monatsweise korrekt summiert werden.
        List<Tagesergebnis> alleTagesergebnisse = new();

        // Jeder Tag wird separat simuliert und danach je nach Zeitraum direkt gespeichert oder aggregiert.
        for (int index = 0; index < tage.Count; index++)
        {
            int tagImJahr = tage[index];
            string saison = ErmittleSaisonFuerTag(tagImJahr);

            int nachfrage = SimuliereTaeglicheNachfrage(tagImJahr, rnd);
            int behandeltePatientenTag = BerechneBehandeltePatienten(anzahlAerzte, anzahlSchwestern, nachfrage, rnd);
            Tagesergebnis tagesergebnis = FinanzRechner.BerechneTagesergebnis(
                anzahlAerzte,
                anzahlSchwestern,
                RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
                behandeltePatientenTag);

            // Tageswerte fuer die anschliessende Monatsaggregation aufbewahren.
            alleTagesergebnisse.Add(tagesergebnis);

            FinanzTagespunkt tagespunkt = new(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                behandeltePatientenTag,
                tagesergebnis.Umsatz,
                tagesergebnis.Kosten.Gesamtkosten,
                tagesergebnis.Gewinn,
                tagesergebnis.Versicherungen,
                tagesergebnis.Behandlungsmix);

            if (aggregiereNachMonat)
            {
                string monat = ErmittleMonatFuerTag(tagImJahr);
                if (!aggregierteWerte.TryGetValue(monat, out FinanzAggregat? aggregat))
                {
                    aggregat = new FinanzAggregat();
                    aggregierteWerte[monat] = aggregat;
                }

                aggregat.Add(tagespunkt);
            }
            else
            {
                tagespunkte.Add(tagespunkt);
            }

            saisonGewinn[saison] += tagesergebnis.Gewinn;
            gesamteNachfrage += nachfrage;
            gesamtBehandelt += behandeltePatientenTag;
        }

        if (aggregiereNachMonat)
        {
            // Fuer die Diagramme bleibt die Monatsreihenfolge auch dann stabil, wenn ein Monat leer ist.
            string[] monate =
            {
                "Januar", "Februar", "Maerz", "April", "Mai", "Juni",
                "Juli", "August", "September", "Oktober", "November", "Dezember"
            };

            foreach (string monat in monate)
            {
                FinanzAggregat aggregat = aggregierteWerte.GetValueOrDefault(monat) ?? new FinanzAggregat();
                tagespunkte.Add(aggregat.ToTagespunkt(monat));
            }
        }

        double gesamtgewinn = tagespunkte.Sum(p => p.Gewinn);
        int intervalle = tagespunkte.Count;
        double durchschnittGewinn = intervalle > 0 ? gesamtgewinn / intervalle : 0.0;

        var fixkosten = KonfigurationJsonExport.Finanzen.Fixkosten;
        double gesamtflaeche = fixkosten.AnzahlBehandlungsraeumeSchwester * fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter
                             + fixkosten.AnzahlBehandlungsraeumeArzt * fixkosten.FlaecheBehandlungsraumArztQuadratmeter
                             + fixkosten.FlaecheWartezimmerQuadratmeter;

        double mietkostenProQm = FinanzRechner.ErmittleMietkostenProQuadratmeterProMonat(gesamtflaeche);
        double gesamtMietkostenMonat = mietkostenProQm * gesamtflaeche;
        double gesamtMietkostenProTag = (gesamtMietkostenMonat * 12) / 365.0;
        double energiekostenProTag = (fixkosten.EnergiekostenProQmProMonat * gesamtflaeche * 12) / 365.0;
        double reinigungskostenProTag = (fixkosten.ReinigungskostenProQmProMonat * gesamtflaeche * 12) / 365.0;
        double gesamtkostenFix = gesamtMietkostenProTag + energiekostenProTag + reinigungskostenProTag
            + fixkosten.InfrastrukturProTag + fixkosten.ITUndVerwaltungProTag + fixkosten.VersicherungenProTag
            + fixkosten.GeraeteLeasingProTag + fixkosten.GeraeteWartungProTag + fixkosten.SonstigeFixkostenProTag;

        List<Tagesergebnis> tagesergebnisse;
        if (aggregiereNachMonat)
        {
            // Kosten, Umsatz und Patientenstruktur aus den Tagesergebnissen monatsweise zusammenfassen.
            string[] monate =
            {
                "Januar", "Februar", "Maerz", "April", "Mai", "Juni",
                "Juli", "August", "September", "Oktober", "November", "Dezember"
            };

            tagesergebnisse = new List<Tagesergebnis>(monate.Length);
            for (int m = 0; m < monate.Length; m++)
            {
                string monat = monate[m];
                // Tagesindizes des jeweiligen Monats bestimmen.
                var indices = Enumerable.Range(0, tage.Count).Where(i => ErmittleMonatFuerTag(tage[i]) == monat).ToList();

                if (!indices.Any())
                {
                    // Auch ein leerer Monat erhaelt einen Eintrag fuer eine stabile Monatsachse.
                    Tageskosten zeroKosten = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    tagesergebnisse.Add(new Tagesergebnis(0, 0, zeroKosten, new Versicherungsverteilung(0, 0), new Umsatzverteilung(0, 0), new Behandlungsmix(0, 0, 0), new Kostenstruktur(), new BreakEvenPoint()));
                    continue;
                }

                double sumUmsatz = 0.0;
                double sumGewinn = 0.0;
                double sumArzt = 0.0;
                double sumSchwester = 0.0;
                double sumRezeption = 0.0;
                double sumMiete = 0.0;
                double sumEnergie = 0.0;
                double sumReinigung = 0.0;
                double sumInfrastruktur = 0.0;
                double sumITVerwaltung = 0.0;
                double sumVersicherungen = 0.0;
                double sumMaterial = 0.0;
                double sumLeasing = 0.0;
                double sumWartung = 0.0;
                double sumSonstige = 0.0;
                int sumPrivat = 0;
                int sumGesetzlich = 0;
                double sumUmsatzPrivat = 0.0;
                double sumUmsatzGesetzlich = 0.0;
                int sumKurz = 0; int sumMittel = 0; int sumLang = 0;

                foreach (int idx in indices)
                {
                    var tagesergebnis = alleTagesergebnisse[idx];
                    sumUmsatz += tagesergebnis.Umsatz;
                    sumGewinn += tagesergebnis.Gewinn;
                    sumArzt += tagesergebnis.Kosten.Arztlohn;
                    sumSchwester += tagesergebnis.Kosten.Schwesterlohn;
                    sumRezeption += tagesergebnis.Kosten.Rezeptionlohn;
                    sumMiete += tagesergebnis.Kosten.Mietkosten;
                    sumEnergie += tagesergebnis.Kosten.Energiekosten;
                    sumReinigung += tagesergebnis.Kosten.Reinigungskosten;
                    sumInfrastruktur += tagesergebnis.Kosten.Infrastrukturkosten;
                    sumITVerwaltung += tagesergebnis.Kosten.ITUndVerwaltungskosten;
                    sumVersicherungen += tagesergebnis.Kosten.Versicherungskosten;
                    sumMaterial += tagesergebnis.Kosten.MedizinischesMaterialkosten;
                    sumLeasing += tagesergebnis.Kosten.GeraeteLeasingKosten;
                    sumWartung += tagesergebnis.Kosten.GeraeteWartungskosten;
                    sumSonstige += tagesergebnis.Kosten.SonstigeFixkosten;
                    sumPrivat += tagesergebnis.Versicherungen.PrivatPatienten;
                    sumGesetzlich += tagesergebnis.Versicherungen.GesetzlichPatienten;
                    sumUmsatzPrivat += tagesergebnis.Umsatzverteilung.UmsatzPrivat;
                    sumUmsatzGesetzlich += tagesergebnis.Umsatzverteilung.UmsatzGesetzlich;
                    sumKurz += tagesergebnis.Behandlungsmix.KurzPatienten;
                    sumMittel += tagesergebnis.Behandlungsmix.MittelPatienten;
                    sumLang += tagesergebnis.Behandlungsmix.LangPatienten;
                }

                Tageskosten gesamtKosten = new(
                    sumArzt,
                    sumSchwester,
                    sumRezeption,
                    sumMiete,
                    sumEnergie,
                    sumReinigung,
                    sumInfrastruktur,
                    sumITVerwaltung,
                    sumVersicherungen,
                    sumMaterial,
                    sumLeasing,
                    sumWartung,
                    sumSonstige);

                Versicherungsverteilung versicherungen = new(sumPrivat, sumGesetzlich);
                Umsatzverteilung umsatzverteilung = new(sumUmsatzPrivat, sumUmsatzGesetzlich);
                Behandlungsmix behandlungsmix = new(sumKurz, sumMittel, sumLang);
                // Berechne Kostenstruktur-Anteile manuell (FinanzRechner.BerechneKostenstruktur ist nicht öffentlich)
                double sumPersonalkosten = sumArzt + sumSchwester + sumRezeption;
                Kostenstruktur kostenstruktur = new()
                {
                    PersonalkostenAnteil = sumUmsatz > 0 ? sumPersonalkosten / sumUmsatz : 0.0,
                    MietkostenAnteil = sumUmsatz > 0 ? sumMiete / sumUmsatz : 0.0,
                    EnergiekostenAnteil = sumUmsatz > 0 ? sumEnergie / sumUmsatz : 0.0,
                    ReinigungskostenAnteil = sumUmsatz > 0 ? sumReinigung / sumUmsatz : 0.0,
                    InfrastrukturkostenAnteil = sumUmsatz > 0 ? sumInfrastruktur / sumUmsatz : 0.0,
                    ITUndVerwaltungskostenAnteil = sumUmsatz > 0 ? sumITVerwaltung / sumUmsatz : 0.0,
                    VersicherungskostenAnteil = sumUmsatz > 0 ? sumVersicherungen / sumUmsatz : 0.0,
                    MaterialkostenAnteil = sumUmsatz > 0 ? sumMaterial / sumUmsatz : 0.0,
                    GeraeteLeasingAnteil = sumUmsatz > 0 ? sumLeasing / sumUmsatz : 0.0,
                    GeraeteWartungAnteil = sumUmsatz > 0 ? sumWartung / sumUmsatz : 0.0,
                    SonstigeFixkostenAnteil = sumUmsatz > 0 ? sumSonstige / sumUmsatz : 0.0
                };

                tagesergebnisse.Add(new Tagesergebnis(sumUmsatz, sumGewinn, gesamtKosten, versicherungen, umsatzverteilung, behandlungsmix, kostenstruktur, new BreakEvenPoint()));
            }
        }
        else
        {
            tagesergebnisse = new List<Tagesergebnis>(alleTagesergebnisse);
        }

        return new FinanzErgebnis(
            normalisierterZeitraum,
            tage.Count,
            tagespunkte,
            tagesergebnisse,
            gesamteNachfrage,
            gesamtBehandelt,
            gesamtgewinn,
            durchschnittGewinn,
            saisonGewinn,
            gesamtflaeche,
            mietkostenProQm,
            gesamtMietkostenProTag,
            gesamtkostenFix);
    }

    private static string NormalisiereZeitraum(string? zeitraum)
    {
        // Faengt freie oder leere Eingaben robust ab und nutzt dann den Standardwert.
        if (string.IsNullOrWhiteSpace(zeitraum))
            return "Jahr";

        return ZeitraumOptionen
            .FirstOrDefault(z => string.Equals(z, zeitraum.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? "Jahr";
    }

    public static List<int> ErmittleTageFuerZeitraum(string zeitraum)
    {
        return zeitraum switch
        {
            "Jahr" => Enumerable.Range(0, 365).ToList(),
            "Winter" => Enumerable.Range(334, 31).Concat(Enumerable.Range(0, 59)).ToList(),
            "Fruehling" => Enumerable.Range(59, 92).ToList(),
            "Sommer" => Enumerable.Range(151, 92).ToList(),
            "Herbst" => Enumerable.Range(243, 91).ToList(),
            _ => Enumerable.Range(0, 365).ToList(),
        };
    }

    public static string ErmittleSaisonFuerTag(int tagImJahr)
    {
        if (tagImJahr >= 334 || tagImJahr < 59)
            return "Winter";
        if (tagImJahr < 151)
            return "Fruehling";
        if (tagImJahr < 243)
            return "Sommer";
        return "Herbst";
    }

    public static string ErmittleMonatFuerTag(int tagImJahr)
    {
        (string Monat, int Start, int Ende)[] monate =
        {
            ("Januar", 0, 31),
            ("Februar", 31, 59),
            ("Maerz", 59, 90),
            ("April", 90, 120),
            ("Mai", 120, 151),
            ("Juni", 151, 181),
            ("Juli", 181, 212),
            ("August", 212, 243),
            ("September", 243, 273),
            ("Oktober", 273, 304),
            ("November", 304, 334),
            ("Dezember", 334, 365)
        };

        foreach ((string monat, int start, int ende) in monate)
        {
            if (tagImJahr >= start && tagImJahr < ende)
                return monat;
        }

        return "Dezember";
    }

    private static int SimuliereTaeglicheNachfrage(int tagImJahr, Random rnd)
    {
        string saison = ErmittleSaisonFuerTag(tagImJahr);
        var saisonfaktoren = KonfigurationJsonExport.Finanzen.Saisonfaktoren;
        // Saisonfaktoren verschieben die erwartete Nachfrage ueber das Jahr hinweg.
        double saisonfaktor = saison switch
        {
            "Winter" => saisonfaktoren.Winter,
            "Fruehling" => saisonfaktoren.Fruehling,
            "Sommer" => saisonfaktoren.Sommer,
            "Herbst" => saisonfaktoren.Herbst,
            _ => 1.0,
        };

        double basisNachfrage = PatientenKonfiguration.BerechneErwarteteAnkuenfte(
            SimulationKonfiguration.SIMULATIONSDAUER);
        double mean = basisNachfrage * saisonfaktor;
        // Die exponentiellen Zwischenankunftszeiten erzeugen eine Poisson-verteilte Tagesanzahl.
        int nachfrage = Poisson.Sample(rnd, mean);
        return Math.Max(0, nachfrage);
    }

    private static int BerechneBehandeltePatienten(
        int anzahlAerzte,
        int anzahlSchwestern,
        int nachfrage,
        Random rnd)
    {
        // Die Tageskapazitaet wird durch den engeren Engpass aus Arzt- und Schwesterzeit begrenzt.
        double erwartungswertArzt = PatientenKonfiguration.TYPEN_VERTEILUNG
            .Sum(t => t.Wahrscheinlichkeit * t.BehandlungszeitArzt);
        double erwartungswertSchwester = PatientenKonfiguration.TYPEN_VERTEILUNG
            .Sum(t => t.Wahrscheinlichkeit * t.BehandlungszeitSchwester);

        double arztKapazitaet = anzahlAerzte * (8.0 * 60.0 / Math.Max(erwartungswertArzt, 1.0));
        double schwesterKapazitaet = anzahlSchwestern * (8.0 * 60.0 / Math.Max(erwartungswertSchwester, 1.0));

        double brauchtSchwesterWahrscheinlichkeit =
            (PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT * PatientenKonfiguration.TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT) +
            ((1.0 - PatientenKonfiguration.TERMIN_WAHRSCHEINLICHKEIT) * PatientenKonfiguration.OHNE_TERMIN_VORBEREITUNG_WAHRSCHEINLICHKEIT);

        brauchtSchwesterWahrscheinlichkeit = Math.Max(brauchtSchwesterWahrscheinlichkeit, 0.001);

        // Ein kleiner Tagesfaktor simuliert produktivere oder schwaechere Arbeitstage.
        double schwesterKapazitaetFuerAllePatienten = schwesterKapazitaet / brauchtSchwesterWahrscheinlichkeit;
        double basisKapazitaet = Math.Min(arztKapazitaet, schwesterKapazitaetFuerAllePatienten);
        double tagesfaktor = 0.90 + rnd.NextDouble() * 0.20;

        int kapazitaetHeute = (int)Math.Round(basisKapazitaet * tagesfaktor);
        return Math.Min(nachfrage, Math.Max(0, kapazitaetHeute));
    }

    private sealed class FinanzAggregat
    {
        // Sammelobjekt fuer Tageswerte, die im Jahresmodus monatsweise zusammengefasst werden.
        public int BehandeltePatienten { get; private set; }
        public double Umsatz { get; private set; }
        public double Kosten { get; private set; }
        public double Gewinn { get; private set; }
        public int PrivatPatienten { get; private set; }
        public int GesetzlichPatienten { get; private set; }
        public int KurzPatienten { get; private set; }
        public int MittelPatienten { get; private set; }
        public int LangPatienten { get; private set; }

        public void Add(FinanzTagespunkt punkt)
        {
            // Addiert alle relevanten Kennzahlen des Tages in das laufende Aggregat.
            BehandeltePatienten += punkt.BehandeltePatienten;
            Umsatz += punkt.Umsatz;
            Kosten += punkt.Kosten;
            Gewinn += punkt.Gewinn;
            PrivatPatienten += punkt.Versicherungen.PrivatPatienten;
            GesetzlichPatienten += punkt.Versicherungen.GesetzlichPatienten;
            KurzPatienten += punkt.Behandlungsmix.KurzPatienten;
            MittelPatienten += punkt.Behandlungsmix.MittelPatienten;
            LangPatienten += punkt.Behandlungsmix.LangPatienten;
        }

        public FinanzTagespunkt ToTagespunkt(string label)
        {
            return new FinanzTagespunkt(
                label,
                BehandeltePatienten,
                Umsatz,
                Kosten,
                Gewinn,
                new Versicherungsverteilung(PrivatPatienten, GesetzlichPatienten),
                new Behandlungsmix(KurzPatienten, MittelPatienten, LangPatienten));
        }
    }
}
