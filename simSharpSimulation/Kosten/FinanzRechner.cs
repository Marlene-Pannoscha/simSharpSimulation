namespace simSharpSimulation;

/// <summary>
/// Kapselt die Finanzlogik fuer einen Praxistag.
/// </summary>
public static class FinanzRechner
{
    public static double BerechneArztlohn(int anzahlAerzte, int behandeltePatienten)
    {
        // Laedt die Finanzparameter vor jeder Berechnung neu aus der Konfiguration.
        KonfigurationJsonExport.LadeFinanzen();

        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        double grundlohn = anzahlAerzte * FinanzKonfiguration.ARZT_LOHN_PRO_STUNDE * FinanzKonfiguration.ARBEITSSTUNDEN_PRO_TAG;
        double variablerAnteil = behandeltePatienten * FinanzKonfiguration.ARZT_LOHN_PRO_PATIENT;
        return grundlohn + variablerAnteil;
    }

    public static double BerechneSchwesterlohn(int anzahlSchwestern)
    {
        KonfigurationJsonExport.LadeFinanzen();

        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlSchwestern, 0);

        return anzahlSchwestern
            * FinanzKonfiguration.SCHWESTER_LOHN_PRO_STUNDE
            * FinanzKonfiguration.ARBEITSSTUNDEN_PRO_TAG;
    }

    public static double BerechneRezeptionlohn(int anzahlRezeptionisten)
    {
        KonfigurationJsonExport.LadeFinanzen();

        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlRezeptionisten, 0);

        return anzahlRezeptionisten
            * FinanzKonfiguration.REZEPTION_LOHN_PRO_STUNDE
            * FinanzKonfiguration.ARBEITSSTUNDEN_PRO_TAG;
    }

    public static Versicherungsverteilung BerechneVersicherungsverteilung(int behandeltePatienten)
    {
        KonfigurationJsonExport.LadeFinanzen();

        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Verteilt die Patienten anhand der konfigurierten Anteile moeglichst fair auf ganze Zahlen.
        Dictionary<string, int> verteilung = VerteileGanzzahlen(
            behandeltePatienten,
            new[]
            {
                ("Privat", FinanzKonfiguration.ANTEIL_PRIVATVERSICHERT),
                ("Gesetzlich", FinanzKonfiguration.ANTEIL_GESETZLICH_VERSICHERT)
            });

        return new Versicherungsverteilung(
            verteilung["Privat"],
            verteilung["Gesetzlich"]);
    }

    public static Umsatzverteilung BerechneUmsatzverteilung(Versicherungsverteilung versicherungen)
    {
        KonfigurationJsonExport.LadeFinanzen();

        // Jeder Versicherungstyp bringt einen anderen Erlos pro Patient ein.
        double umsatzPrivat = versicherungen.PrivatPatienten * FinanzKonfiguration.EINNAHME_PRIVATPATIENT;
        double umsatzGesetzlich = versicherungen.GesetzlichPatienten * FinanzKonfiguration.EINNAHME_GESETZLICH_PATIENT;

        return new Umsatzverteilung(
            umsatzPrivat,
            umsatzGesetzlich);
    }

    public static Behandlungsmix BerechneBehandlungsmix(int behandeltePatienten)
    {
        KonfigurationJsonExport.LadeFinanzen();

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
            langPatienten,
            kurzPatienten * FinanzKonfiguration.BEHANDLUNGSKOSTEN_KURZ,
            mittelPatienten * FinanzKonfiguration.BEHANDLUNGSKOSTEN_MITTEL,
            langPatienten * FinanzKonfiguration.BEHANDLUNGSKOSTEN_LANG);
    }

    public static Tageskosten BerechneTageskosten(int anzahlAerzte, int behandeltePatienten)
    {
        KonfigurationJsonExport.LadeFinanzen();

        // Fasst alle Kostenarten eines Praxistages zu einem Gesamtergebnis zusammen.
        double arztlohn = BerechneArztlohn(anzahlAerzte, behandeltePatienten);
        double schwesterlohn = BerechneSchwesterlohn(SchwesterKonfiguration.ANZAHL_SCHWESTERN);
        double rezeptionlohn = BerechneRezeptionlohn(RezeptionKonfiguration.ANZAHL_REZEPTIONISTEN);
        double fixkosten = FinanzKonfiguration.MIETKOSTEN_PRO_TAG + FinanzKonfiguration.WEITERE_FIXKOSTEN_PRO_TAG;
        Behandlungsmix behandlungsmix = BerechneBehandlungsmix(behandeltePatienten);
        double personalGesamt = arztlohn + schwesterlohn + rezeptionlohn;
        double gesamtkosten = personalGesamt + fixkosten + behandlungsmix.Gesamtkosten;

        return new Tageskosten(
            arztlohn,
            schwesterlohn,
            rezeptionlohn,
            fixkosten,
            behandlungsmix.Gesamtkosten,
            gesamtkosten);
    }

    public static Tagesergebnis BerechneTagesergebnis(int anzahlAerzte, int behandeltePatienten)
    {
        KonfigurationJsonExport.LadeFinanzen();

        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        // Kombiniert Kosten, Versicherungsstruktur und Behandlungsmix zu einer kompakten Tagesauswertung.
        Tageskosten kosten = BerechneTageskosten(anzahlAerzte, behandeltePatienten);
        Versicherungsverteilung versicherungen = BerechneVersicherungsverteilung(behandeltePatienten);
        Umsatzverteilung umsatzverteilung = BerechneUmsatzverteilung(versicherungen);
        Behandlungsmix behandlungsmix = BerechneBehandlungsmix(behandeltePatienten);
        double umsatz = umsatzverteilung.Gesamtumsatz;
        double gewinn = umsatz - kosten.Gesamtkosten;

        return new Tagesergebnis(umsatz, gewinn, kosten, versicherungen, umsatzverteilung, behandlungsmix);
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
    double Fixkosten,
    double Behandlungskosten,
    double Gesamtkosten);

public readonly record struct Tagesergebnis(
    double Umsatz,
    double Gewinn,
    Tageskosten Kosten,
    Versicherungsverteilung Versicherungen,
    Umsatzverteilung Umsatzverteilung,
    Behandlungsmix Behandlungsmix);

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
    int LangPatienten,
    double KurzKosten,
    double MittelKosten,
    double LangKosten)
{
    // Dient fuer aggregierte Auswertungen ueber alle Behandlungsarten hinweg.
    public int GesamtPatienten => KurzPatienten + MittelPatienten + LangPatienten;
    public double Gesamtkosten => KurzKosten + MittelKosten + LangKosten;
}
