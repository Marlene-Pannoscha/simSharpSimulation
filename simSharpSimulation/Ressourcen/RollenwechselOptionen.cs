using System;

namespace simSharpSimulation;

/// <summary>Regeln fuer genau eine Schwester mit zusaetzlicher Rezeptionsqualifikation.</summary>
public sealed record RollenwechselOptionen
{
    public bool Aktiv { get; init; }
    public int SchwesterId { get; init; } = 2;
    public int RezeptionHilfeAb { get; init; } = 4;
    public int SchwesterHilfeBis { get; init; } = 3;
    public int RezeptionRueckkehrBis { get; init; } = 2;
    public int SchwesterRueckkehrAb { get; init; } = 4;
    public double WegzeitSekunden { get; init; } = 10;

    public void Pruefen(int anzahlSchwestern)
    {
        if (!Aktiv) return;
        if (SchwesterId < 1 || SchwesterId > anzahlSchwestern || anzahlSchwestern < 2)
            throw new ArgumentException("Rollenwechsel benoetigt mindestens zwei Schwestern und eine gueltige flexible Schwester-ID.");
        if (RezeptionHilfeAb <= RezeptionRueckkehrBis || RezeptionRueckkehrBis < 0
            || SchwesterHilfeBis < 0 || SchwesterRueckkehrAb <= SchwesterHilfeBis)
            throw new ArgumentException("Die Ein- und Rueckkehrschwellen muessen eine positive Hysterese bilden.");
        if (!double.IsFinite(WegzeitSekunden) || WegzeitSekunden <= 0)
            throw new ArgumentException("Die Wegzeit fuer Rollenwechsel muss groesser als null sein.");
    }
}
