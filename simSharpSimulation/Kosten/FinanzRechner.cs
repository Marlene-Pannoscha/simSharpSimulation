namespace simSharpSimulation;

/// <summary>
/// Kapselt die Finanzlogik fuer einen Praxistag.
/// </summary>
public static class FinanzRechner
{
    public const double EinnahmeProPatient = 100.0;
    public const double ArztLohnProPatient = 30.0;
    public const double ArztLohnProStunde = 50.0;
    public const double MietkostenProTag = 90.0;
    public const double WeitereFixkostenProTag = 200.0;
    public const int ArbeitsstundenProTag = 8;

    public static double BerechneArztlohn(int anzahlAerzte, int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        double grundlohn = anzahlAerzte * ArztLohnProStunde * ArbeitsstundenProTag;
        double variablerAnteil = behandeltePatienten * ArztLohnProPatient;
        return grundlohn + variablerAnteil;
    }

    public static Tageskosten BerechneTageskosten(int anzahlAerzte, int behandeltePatienten)
    {
        double arztlohn = BerechneArztlohn(anzahlAerzte, behandeltePatienten);
        double fixkosten = MietkostenProTag + WeitereFixkostenProTag;
        double gesamtkosten = arztlohn + fixkosten;

        return new Tageskosten(arztlohn, fixkosten, gesamtkosten);
    }

    public static Tagesergebnis BerechneTagesergebnis(int anzahlAerzte, int behandeltePatienten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(anzahlAerzte, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(behandeltePatienten, 0);

        Tageskosten kosten = BerechneTageskosten(anzahlAerzte, behandeltePatienten);
        double umsatz = behandeltePatienten * EinnahmeProPatient;
        double gewinn = umsatz - kosten.Gesamtkosten;

        return new Tagesergebnis(umsatz, gewinn, kosten);
    }
}

public readonly record struct Tageskosten(
    double Arztlohn,
    double Fixkosten,
    double Gesamtkosten);

public readonly record struct Tagesergebnis(
    double Umsatz,
    double Gewinn,
    Tageskosten Kosten);
