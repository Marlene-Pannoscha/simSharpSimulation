using System;
using System.Collections.Generic;
using System.Linq;
using SimSharp;
using static simSharpSimulation.SimulationsHelfer;

namespace simSharpSimulation;

/// <summary>
/// Gemeinsame Personalzuteilung fuer Rezeption und Schwester. Jede Person existiert genau einmal.
/// Zuteilung und Raumreservierung erfolgen gemeinsam; es gibt keinen Rollenwechsel bei Belegung.
/// </summary>
public sealed class StationsPersonalPool
{
    private readonly Simulation env;
    private readonly SimulationsDaten daten;
    private readonly RollenwechselOptionen regeln;
    private readonly List<Mitarbeiter> mitarbeiter = new();
    private readonly List<PersonalAnforderung> wartende = new();
    private readonly bool[] schwesterplaetze;
    private readonly int rezeptionAnzahl;
    private readonly int schwesterAnzahl;
    private readonly Action<int, int>? kapazitaetGeaendert;
    private long reihenfolge;
    private bool verteilungGeplant;

    public StationsPersonalPool(Simulation env, int rezeptionAnzahl, int schwesterAnzahl,
        int schwesterraumAnzahl, RollenwechselOptionen regeln, SimulationsDaten daten,
        Action<int, int>? kapazitaetGeaendert = null)
    {
        regeln.Pruefen(schwesterAnzahl);
        if (!regeln.Aktiv || rezeptionAnzahl < 1 || schwesterraumAnzahl < 1)
            throw new ArgumentException("Die gemeinsame Zuteilung benoetigt aktive Wechselregeln, Rezeption und Schwesterraeume.");
        this.env = env;
        this.daten = daten;
        this.regeln = regeln;
        this.rezeptionAnzahl = rezeptionAnzahl;
        this.schwesterAnzahl = schwesterAnzahl;
        this.kapazitaetGeaendert = kapazitaetGeaendert;
        schwesterplaetze = new bool[schwesterraumAnzahl];
        for (int id = 1; id <= rezeptionAnzahl; id++)
            mitarbeiter.Add(new Mitarbeiter($"rezeption_{id}", "Rezeption", id, false));
        for (int id = 1; id <= schwesterAnzahl; id++)
            mitarbeiter.Add(new Mitarbeiter($"schwester_{id}", "Schwester", id, id == regeln.SchwesterId));
        MeldeKapazitaeten();
    }

    public int RezeptionWartend => wartende.Count(a => a.Rolle == "Rezeption");
    public int SchwesterWartend => wartende.Count(a => a.Rolle == "Schwester");
    public bool IstRezeptionFrei => mitarbeiter.Any(m => m.Rolle == "Rezeption" && !m.Belegt && !m.Unterwegs);
    public bool IstSchwesterFrei => schwesterplaetze.Any(belegt => !belegt)
        && mitarbeiter.Any(m => m.Rolle == "Schwester" && !m.Belegt && !m.Unterwegs);

    public PersonalAnforderung Anfordern(string rolle, int patientId, int prioritaet = 0)
    {
        if (rolle != "Rezeption" && rolle != "Schwester") throw new ArgumentException("Unbekannte Stationsrolle.");
        var anforderung = new PersonalAnforderung(this, env, rolle, patientId, prioritaet, reihenfolge++);
        wartende.Add(anforderung);
        NeuVerteilen();
        return anforderung;
    }

    private void NeuVerteilen()
    {
        if (verteilungGeplant) return;
        verteilungGeplant = true;
        env.Process(Verteilen());
    }

    private IEnumerable<Event> Verteilen()
    {
        // Gleiche Zeitpunkte zusammenfassen. Wartende zaehlen erst nach Zuteilung fester Kraefte.
        yield return env.Timeout(TimeSpan.Zero);
        verteilungGeplant = false;
        if (AktuelleSimulationszeitMinuten(env) >= SimulationKonfiguration.SIMULATIONSDAUER) yield break;
        foreach (var person in mitarbeiter.Where(m => !m.Flexibel && !m.Belegt))
            Zuteilen(person);
        var flexibel = mitarbeiter.Single(m => m.Flexibel);
        if (flexibel.Belegt || flexibel.Unterwegs) yield break;
        string ziel = flexibel.Rolle;
        if (flexibel.Rolle == "Schwester")
        {
            if (RezeptionWartend > 0 && (SchwesterWartend == 0
                || (RezeptionWartend >= regeln.RezeptionHilfeAb && SchwesterWartend <= regeln.SchwesterHilfeBis)))
                ziel = "Rezeption";
        }
        else if (SchwesterWartend > 0 && (RezeptionWartend <= regeln.RezeptionRueckkehrBis
            || SchwesterWartend >= regeln.SchwesterRueckkehrAb))
            ziel = "Schwester";
        // Wenn beide Schlangen leer sind, bleibt die Person stehen. Keine Pendelfahrten im Leerlauf.
        if (ziel != flexibel.Rolle)
        {
            flexibel.Unterwegs = true;
            MeldeKapazitaeten();
            env.Process(Wechseln(flexibel, ziel));
        }
        else Zuteilen(flexibel);
    }

    private void Zuteilen(Mitarbeiter person)
    {
        int raum = person.Rolle == "Schwester" ? Array.FindIndex(schwesterplaetze, belegt => !belegt) : -1;
        if (person.Rolle == "Schwester" && raum < 0) return;
        var anforderung = wartende.Where(a => a.Rolle == person.Rolle)
            .OrderBy(a => a.Prioritaet).ThenBy(a => a.Reihenfolge).FirstOrDefault();
        if (anforderung is null) return;
        wartende.Remove(anforderung);
        person.Belegt = true;
        if (raum >= 0) schwesterplaetze[raum] = true;
        anforderung.PersonalId = person.Id;
        anforderung.SchwesterId = person.Stammrolle == "Schwester" ? person.Nummer : null;
        anforderung.ArbeitsplatzId = person.Rolle == "Schwester" ? $"schwester_{raum + 1}"
            : $"rezeption_{(person.Flexibel ? rezeptionAnzahl + 1 : person.Nummer)}";
        anforderung.Freigabe = () =>
        {
            person.Belegt = false;
            if (raum >= 0) schwesterplaetze[raum] = false;
            Logge("personal_aufgabe_ende", anforderung.PatientId, person, person.Rolle, person.Rolle, anforderung.ArbeitsplatzId);
        };
        Logge("personal_aufgabe_start", anforderung.PatientId, person, person.Rolle, person.Rolle, anforderung.ArbeitsplatzId);
        anforderung.Bereit.Succeed();
    }

    private IEnumerable<Event> Wechseln(Mitarbeiter person, string ziel)
    {
        string von = person.Rolle;
        string platz = ziel == "Rezeption" ? $"rezeption_{rezeptionAnzahl + 1}" : $"schwester_{person.Nummer}";
        Logge("personal_wechsel_start", 0, person, von, ziel, platz, regeln.WegzeitSekunden / 60.0);
        yield return env.Timeout(TimeSpan.FromSeconds(regeln.WegzeitSekunden));
        person.Rolle = ziel;
        person.Unterwegs = false;
        Logge("personal_wechsel_ende", 0, person, von, ziel, platz);
        MeldeKapazitaeten();
        NeuVerteilen();
    }

    private void MeldeKapazitaeten()
    {
        var flexibel = mitarbeiter.Single(m => m.Flexibel);
        // Die Prognose darf die flexible Person nicht gleichzeitig fuer beide Rollen ansetzen.
        int rezeption = rezeptionAnzahl + (!flexibel.Unterwegs && flexibel.Rolle == "Rezeption" ? 1 : 0);
        int schwester = schwesterAnzahl - 1 + (!flexibel.Unterwegs && flexibel.Rolle == "Schwester" ? 1 : 0);
        kapazitaetGeaendert?.Invoke(rezeption, Math.Min(schwester, schwesterplaetze.Length));
    }

    private void Logge(string typ, int patientId, Mitarbeiter person, string von, string zu,
        string platz, double? dauer = null) => daten.LogEvent(AktuelleSimulationszeitMinuten(env), typ, patientId,
            schwesterId: person.Stammrolle == "Schwester" ? person.Nummer : null,
            personalId: person.Id, arbeitsplatzId: platz, vonRolle: von, zuRolle: zu, bewegungsdauerMinuten: dauer);

    internal void Freigeben(PersonalAnforderung anforderung)
    {
        wartende.Remove(anforderung);
        anforderung.Freigabe?.Invoke();
        anforderung.Freigabe = null;
        NeuVerteilen();
    }

    private sealed class Mitarbeiter
    {
        public Mitarbeiter(string id, string rolle, int nummer, bool flexibel)
        { Id = id; Stammrolle = Rolle = rolle; Nummer = nummer; Flexibel = flexibel; }
        public string Id { get; }
        public string Stammrolle { get; }
        public string Rolle { get; set; }
        public int Nummer { get; }
        public bool Flexibel { get; }
        public bool Belegt { get; set; }
        public bool Unterwegs { get; set; }
    }
}

/// <summary>Eine wartende oder belegte Person-Raum-Zuteilung. Dispose storniert auch wartende Anfragen.</summary>
public sealed class PersonalAnforderung : IDisposable
{
    private readonly StationsPersonalPool pool;
    private bool freigegeben;
    internal PersonalAnforderung(StationsPersonalPool pool, Simulation env, string rolle, int patientId, int prioritaet, long reihenfolge)
    { this.pool = pool; Bereit = new Event(env); Rolle = rolle; PatientId = patientId; Prioritaet = prioritaet; Reihenfolge = reihenfolge; }
    public Event Bereit { get; }
    public string Rolle { get; }
    public int PatientId { get; }
    public string? PersonalId { get; internal set; }
    public string? ArbeitsplatzId { get; internal set; }
    public int? SchwesterId { get; internal set; }
    internal int Prioritaet { get; }
    internal long Reihenfolge { get; }
    internal Action? Freigabe { get; set; }
    public void Dispose()
    {
        if (freigegeben) return;
        freigegeben = true;
        pool.Freigeben(this);
    }
}
