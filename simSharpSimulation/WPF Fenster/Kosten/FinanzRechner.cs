namespace simSharpSimulation;

/// <summary>
/// Kapselt die Finanzlogik fuer einen Praxistag.
/// </summary>
public static class FinanzRechner
{
    private static FinanzKonfigurationJson Finanzen => KonfigurationJsonExport.Finanzen;
    private static double AnteilGesetzlichVersichert => 1.0 - Finanzen.Versicherung.AnteilPrivatversichert;

    public static double BerechneArztlohn(int anzahlAerzte, int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Fixer Stundenlohn pro Arzt pro Tag
        double stundenlohnKomponente = anzahlAerzte
            * Finanzen.Personal.ArztLohnProStunde
            * Finanzen.Personal.ArbeitsstundenProTag;

        // Variable Vergütung pro behandeltetem Patienten (z. B. Fallpauschale oder Bonus)
        double proPatientKomponente = behandeltePatienten * Finanzen.Personal.ArztLohnProPatient;

        return stundenlohnKomponente + proPatientKomponente;
    }

    public static double BerechneSchwesterlohn(int anzahlSchwestern)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlSchwestern, 0);

        return anzahlSchwestern
            * Finanzen.Personal.SchwesterLohnProStunde
            * Finanzen.Personal.ArbeitsstundenProTag;
    }

    public static double BerechneRezeptionlohn(int anzahlRezeptionisten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlRezeptionisten, 0);

        return anzahlRezeptionisten
            * Finanzen.Personal.RezeptionLohnProStunde
            * Finanzen.Personal.ArbeitsstundenProTag;
    }

    public static Versicherungsverteilung BerechneVersicherungsverteilung(int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Verteilt die Patienten anhand der konfigurierten Anteile moeglichst fair auf ganze Zahlen.
        Dictionary<string, int> verteilung = VerteileGanzzahlen(
            behandeltePatienten,
            new[]
            {
                ("Privat", Finanzen.Versicherung.AnteilPrivatversichert),
                ("Gesetzlich", AnteilGesetzlichVersichert)
            });

        return new Versicherungsverteilung(
            verteilung["Privat"],
            verteilung["Gesetzlich"]);
    }

    public static Umsatzverteilung BerechneUmsatzverteilung(Versicherungsverteilung versicherungen)
    {
        // Jeder Versicherungstyp bringt einen anderen Erlos pro Patient ein.
        double umsatzPrivat = versicherungen.PrivatPatienten * Finanzen.Versicherung.EinnahmePrivatpatient;
        double umsatzGesetzlich = versicherungen.GesetzlichPatienten * Finanzen.Versicherung.EinnahmeGesetzlichPatient;

        return new Umsatzverteilung(
            umsatzPrivat,
            umsatzGesetzlich);
    }

    public static Behandlungsmix BerechneBehandlungsmix(int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Bildet aus Wahrscheinlichkeiten eine ganzzahlige Verteilung fuer kurze, mittlere und lange Behandlungen.
        Dictionary<PatientenTyp, int> verteilung = VerteileGanzzahlen(
            behandeltePatienten,
            PatientenKonfiguration.TYPEN_VERTEILUNG.Select(t => (t.Typ, t.Wahrscheinlichkeit)));

        int kurzPatienten = verteilung.GetValueOrDefault(PatientenTyp.Kurz, 0);
        int mittelPatienten = verteilung.GetValueOrDefault(PatientenTyp.Mittel, 0);
        int langPatienten = verteilung.GetValueOrDefault(PatientenTyp.Lang, 0);

        return new Behandlungsmix(
            kurzPatienten,
            mittelPatienten,
            langPatienten);
    }

    public static Tageskosten BerechneTageskosten(int anzahlAerzte, int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Fasst alle Kostenarten eines Praxistages zu einem Gesamtergebnis zusammen.
        Behandlungsmix behandlungsmix = BerechneBehandlungsmix(behandeltePatienten);
        return BerechneKosten(
            anzahlAerzte,
            SchwesterKonfiguration.ANZAHL_SCHWESTERN,
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
            behandeltePatienten);
    }

    internal static double GetMietkostenProQuadratmeterProMonat(double flaeche)
    {
        var MietAufteilung = Finanzen.Fixkosten.MietkostenAufteilung
            .FirstOrDefault(s => flaeche >= s.MinFlaeche && flaeche <= s.MaxFlaeche);

        if (MietAufteilung != null)
        {
            return MietAufteilung.KostenProQm;
        }

        // Fallback, falls keine MietAufteilung passt (sollte durch die Konfiguration nicht passieren)
        return 0.0;
    }

    // BerechneKosten: Implementierung weiter unten mit voller Tageskosten-Signatur.

    public static Tagesergebnis BerechneTagesergebnis(int anzahlAerzte, int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Kombiniert Kosten, Versicherungsstruktur und Behandlungsmix zu einer kompakten Tagesauswertung.
        Behandlungsmix behandlungsmix = BerechneBehandlungsmix(behandeltePatienten);
        Tageskosten kosten = BerechneKosten(
            anzahlAerzte,
            SchwesterKonfiguration.ANZAHL_SCHWESTERN,
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
            behandeltePatienten);
        Versicherungsverteilung versicherungen = BerechneVersicherungsverteilung(behandeltePatienten);
        Umsatzverteilung umsatzverteilung = BerechneUmsatzverteilung(versicherungen);
        double umsatz = umsatzverteilung.Gesamtumsatz;
        Kostenstruktur kostenstruktur = BerechneKostenstruktur(kosten, umsatz);
        BreakEvenPoint breakEven = BerechneBreakEvenPoint(kosten, behandeltePatienten, umsatz / Math.Max(1, behandeltePatienten));
        return ErstelleErgebnis(kosten, versicherungen, umsatzverteilung, behandlungsmix, kostenstruktur, breakEven);
    }

    public static Tagesergebnis BerechneTagesergebnis(
        int anzahlAerzte,
        int anzahlSchwestern,
        int anzahlRezeptionisten,
        int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlSchwestern, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlRezeptionisten, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        Behandlungsmix behandlungsmix = BerechneBehandlungsmix(behandeltePatienten);
        Tageskosten kosten = BerechneKosten(
            anzahlAerzte,
            anzahlSchwestern,
            anzahlRezeptionisten,
            behandeltePatienten);
        Versicherungsverteilung versicherungen = BerechneVersicherungsverteilung(behandeltePatienten);
        Umsatzverteilung umsatzverteilung = BerechneUmsatzverteilung(versicherungen);
        double umsatz = umsatzverteilung.Gesamtumsatz;
        Kostenstruktur kostenstruktur = BerechneKostenstruktur(kosten, umsatz);
        BreakEvenPoint breakEven = BerechneBreakEvenPoint(kosten, behandeltePatienten, umsatz / Math.Max(1, behandeltePatienten));
        return ErstelleErgebnis(kosten, versicherungen, umsatzverteilung, behandlungsmix, kostenstruktur, breakEven);
    }

    public static Tagesergebnis BerechneZeitraumergebnis(int anzahlAerzte, int behandeltePatienten, int arbeitstage)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(arbeitstage, 0);

        Behandlungsmix behandlungsmix = BerechneBehandlungsmix(behandeltePatienten);
        Tageskosten kosten = BerechneKosten(
            anzahlAerzte,
            SchwesterKonfiguration.ANZAHL_SCHWESTERN,
            RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN,
            behandeltePatienten);
        Versicherungsverteilung versicherungen = BerechneVersicherungsverteilung(behandeltePatienten);
        Umsatzverteilung umsatzverteilung = BerechneUmsatzverteilung(versicherungen);
        double umsatz = umsatzverteilung.Gesamtumsatz;
        Kostenstruktur kostenstruktur = BerechneKostenstruktur(kosten, umsatz);
        BreakEvenPoint breakEven = BerechneBreakEvenPoint(kosten, behandeltePatienten, umsatz / Math.Max(1, behandeltePatienten));
        return ErstelleErgebnis(kosten, versicherungen, umsatzverteilung, behandlungsmix, kostenstruktur, breakEven);
    }

    internal static BreakEvenPoint BerechneBreakEvenPoint(
        Tageskosten kosten,
        double behandeltePatientenProTag,
        double durchschnittsumsatzProPatient)
    {
        if (durchschnittsumsatzProPatient <= 0 || behandeltePatientenProTag <= 0)
            return new BreakEvenPoint { Patienten = int.MaxValue, Tage = double.MaxValue };

        double variableKostenProPatient = Finanzen.Personal.ArztLohnProPatient
            + Finanzen.VariableKosten.MedizinischesMaterialProPatient;
        double deckungsbeitragProPatient = durchschnittsumsatzProPatient - variableKostenProPatient;

        if (deckungsbeitragProPatient <= 0)
            return new BreakEvenPoint { Patienten = int.MaxValue, Tage = double.MaxValue };

        double fixeTageskosten = kosten.Gesamtkosten - variableKostenProPatient * behandeltePatientenProTag;
        int breakEvenPatienten = Math.Max(0, (int)Math.Ceiling(fixeTageskosten / deckungsbeitragProPatient));
        double breakEvenTage = breakEvenPatienten / behandeltePatientenProTag;

        return new BreakEvenPoint
        {
            Patienten = breakEvenPatienten,
            Tage = breakEvenTage
        };
    }

    private static Kostenstruktur BerechneKostenstruktur(Tageskosten kosten, double umsatz)
    {
        if (umsatz == 0) return new Kostenstruktur();

        return new Kostenstruktur
        {
            PersonalkostenAnteil = kosten.Personalkosten / umsatz,
            MietkostenAnteil = kosten.Mietkosten / umsatz,
            EnergiekostenAnteil = kosten.Energiekosten / umsatz,
            ReinigungskostenAnteil = kosten.Reinigungskosten / umsatz,
            InfrastrukturkostenAnteil = kosten.Infrastrukturkosten / umsatz,
            ITUndVerwaltungskostenAnteil = kosten.ITUndVerwaltungskosten / umsatz,
            VersicherungskostenAnteil = kosten.Versicherungskosten / umsatz,
            MaterialkostenAnteil = kosten.MedizinischesMaterialkosten / umsatz,
            GeraeteLeasingAnteil = kosten.GeraeteLeasingKosten / umsatz,
            GeraeteWartungAnteil = kosten.GeraeteWartungskosten / umsatz,
            SonstigeFixkostenAnteil = kosten.SonstigeFixkosten / umsatz
        };
    }

    private static Tageskosten BerechneKosten(
        int anzahlAerzte,
        int anzahlSchwestern,
        int anzahlRezeptionisten,
        int behandeltePatienten)
    {
        double arztlohn = BerechneArztlohn(anzahlAerzte, behandeltePatienten);
        double schwesterlohn = BerechneSchwesterlohn(anzahlSchwestern);
        double rezeptionlohn = BerechneRezeptionlohn(anzahlRezeptionisten);
        double personalkosten = arztlohn + schwesterlohn + rezeptionlohn;

        double gesamtflaeche = Finanzen.Fixkosten.AnzahlBehandlungsraeumeSchwester * Finanzen.Fixkosten.FlaecheBehandlungsraumSchwesterQuadratmeter
                             + Finanzen.Fixkosten.AnzahlBehandlungsraeumeArzt * Finanzen.Fixkosten.FlaecheBehandlungsraumArztQuadratmeter
                             + Finanzen.Fixkosten.FlaecheWartezimmerQuadratmeter;

        double mietkostenProQuadratmeterProMonat = GetMietkostenProQuadratmeterProMonat(gesamtflaeche);
        double mietkostenProTag = (mietkostenProQuadratmeterProMonat * gesamtflaeche * 12) / 365.0;
        double energiekostenProTag = (Finanzen.Fixkosten.EnergiekostenProQmProMonat * gesamtflaeche * 12) / 365.0;
        double reinigungskostenProTag = (Finanzen.Fixkosten.ReinigungskostenProQmProMonat * gesamtflaeche * 12) / 365.0;
        double materialkosten = behandeltePatienten * Finanzen.VariableKosten.MedizinischesMaterialProPatient;

        return new Tageskosten(
            arztlohn,
            schwesterlohn,
            rezeptionlohn,
            mietkostenProTag,
            energiekostenProTag,
            reinigungskostenProTag,
            Finanzen.Fixkosten.InfrastrukturProTag,
            Finanzen.Fixkosten.ITUndVerwaltungProTag,
            Finanzen.Fixkosten.VersicherungenProTag,
            materialkosten,
            Finanzen.Fixkosten.GeraeteLeasingProTag,
            Finanzen.Fixkosten.GeraeteWartungProTag,
            Finanzen.Fixkosten.SonstigeFixkostenProTag);
    }

    private static Tagesergebnis ErstelleErgebnis(
        Tageskosten kosten,
        Versicherungsverteilung versicherungen,
        Umsatzverteilung umsatzverteilung,
        Behandlungsmix behandlungsmix,
        Kostenstruktur kostenstruktur,
        BreakEvenPoint breakEven)
    {
        double umsatz = umsatzverteilung.Gesamtumsatz;
        double gewinn = umsatz - kosten.Gesamtkosten;
        return new Tagesergebnis(umsatz, gewinn, kosten, versicherungen, umsatzverteilung, behandlungsmix, kostenstruktur, breakEven);
    }

    private static Dictionary<TKey, int> VerteileGanzzahlen<TKey>(
        int gesamt,
        IEnumerable<(TKey Schluessel, double Anteil)> anteile)
        where TKey : notnull
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gesamt, 0);

        List<(TKey Schluessel, double Anteil)> anteileListe = anteile.ToList();
        Dictionary<TKey, int> basis = new();
        List<(TKey Schluessel, double Restwert)> reste = new();

        // Zuerst werden die ganzzahligen Basiswerte ueber Abrunden bestimmt.
        int summeBasis = 0;
        foreach ((TKey schluessel, double anteil) in anteileListe)
        {
            double roherWert = gesamt * Math.Max(anteil, 0.0);
            int ganzeZahl = (int)Math.Floor(roherWert);
            basis[schluessel] = ganzeZahl;
            reste.Add((schluessel, roherWert - ganzeZahl));
            summeBasis += ganzeZahl;
        }

        // Verbleibende Elemente gehen an die groessten Nachkomma-Reste.
        int verbleibend = gesamt - summeBasis;
        foreach (var restEintrag in reste.OrderByDescending(e => e.Restwert).Take(verbleibend))
            basis[restEintrag.Schluessel]++;

        return basis;
    }
}

public readonly record struct Tageskosten(
    double Arztlohn,
    double Schwesterlohn,
    double Rezeptionlohn,
    double Mietkosten,
    double Energiekosten,
    double Reinigungskosten,
    double Infrastrukturkosten,
    double ITUndVerwaltungskosten,
    double Versicherungskosten,
    double MedizinischesMaterialkosten,
    double GeraeteLeasingKosten,
    double GeraeteWartungskosten,
    double SonstigeFixkosten)
{
    public double Gesamtkosten => Personalkosten + Fixkosten + MedizinischesMaterialkosten;
    public double Personalkosten => Arztlohn + Schwesterlohn + Rezeptionlohn;
    public double Fixkosten => Mietkosten + Energiekosten + Reinigungskosten + Infrastrukturkosten
        + ITUndVerwaltungskosten + Versicherungskosten + GeraeteLeasingKosten
        + GeraeteWartungskosten + SonstigeFixkosten;
}

public readonly record struct Kostenstruktur
{
    public double PersonalkostenAnteil { get; init; }
    public double MietkostenAnteil { get; init; }
    public double EnergiekostenAnteil { get; init; }
    public double ReinigungskostenAnteil { get; init; }
    public double InfrastrukturkostenAnteil { get; init; }
    public double ITUndVerwaltungskostenAnteil { get; init; }
    public double VersicherungskostenAnteil { get; init; }
    public double MaterialkostenAnteil { get; init; }
    public double GeraeteLeasingAnteil { get; init; }
    public double GeraeteWartungAnteil { get; init; }
    public double SonstigeFixkostenAnteil { get; init; }
}

public readonly record struct BreakEvenPoint
{
    public int Patienten { get; init; }
    public double Tage { get; init; }
}

public readonly record struct Tagesergebnis(
    double Umsatz,
    double Gewinn,
    Tageskosten Kosten,
    Versicherungsverteilung Versicherungen,
    Umsatzverteilung Umsatzverteilung,
    Behandlungsmix Behandlungsmix,
    Kostenstruktur Kostenstruktur,
    BreakEvenPoint BreakEven);

public readonly record struct Versicherungsverteilung(
    int PrivatPatienten,
    int GesetzlichPatienten)
{
    // Hilfswert fuer Auswertungen, bei denen nur die Gesamtmenge relevant ist.
    public int GesamtPatienten => PrivatPatienten + GesetzlichPatienten;
}

public readonly record struct Umsatzverteilung(
    double UmsatzPrivat,
    double UmsatzGesetzlich)
{
    // Summiert die Einnahmen beider Versicherungsgruppen.
    public double Gesamtumsatz => UmsatzPrivat + UmsatzGesetzlich;
}

public readonly record struct Behandlungsmix(
    int KurzPatienten,
    int MittelPatienten,
    int LangPatienten)
{
    // Dient fuer aggregierte Auswertungen ueber alle Behandlungsarten hinweg.
    public int GesamtPatienten => KurzPatienten + MittelPatienten + LangPatienten;
}


