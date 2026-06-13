#!/usr/bin/env python3
import json
import sys
from collections import defaultdict
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt


def _tag_label(value):
    if not value:
        return "Tag"
    return str(value).split("T", 1)[0]


def _load(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def _aggregate(data):
    pruefungen = data.get("AufnahmeprognosePruefungen", [])
    entscheidungen = data.get("AufnahmeprognoseEntscheidungen", [])

    tags = []
    freeze_selection_by_tag = {}
    allowed_by_tag = defaultdict(int)
    rejected_by_tag = defaultdict(int)

    for item in pruefungen:
        tag = _tag_label(item.get("Tag"))
        if tag not in tags:
            tags.append(tag)
        freeze_selection_by_tag[tag] = int(item.get("AufnahmeKapazitaet", 0))

    for item in entscheidungen:
        tag = _tag_label(item.get("Tag"))
        if tag not in tags:
            tags.append(tag)
        if item.get("Zugelassen"):
            allowed_by_tag[tag] += 1
        else:
            rejected_by_tag[tag] += 1

    return tags, freeze_selection_by_tag, allowed_by_tag, rejected_by_tag, pruefungen, entscheidungen


def create_plot(json_path, output_path):
    data = _load(json_path)
    tags, freeze_selection_by_tag, allowed_by_tag, rejected_by_tag, pruefungen, entscheidungen = _aggregate(data)

    output = Path(output_path)
    output.parent.mkdir(parents=True, exist_ok=True)

    fig, axes = plt.subplots(
        2,
        1,
        figsize=(13, 8),
        gridspec_kw={"height_ratios": [1.0, 1.25]},
        constrained_layout=True,
    )
    fig.suptitle("Queue-Freeze und Aufnahmeprognose eine Stunde vor Praxisschluss", fontsize=16, fontweight="bold")

    if not tags:
        axes[0].axis("off")
        axes[1].axis("off")
        axes[0].text(
            0.5,
            0.5,
            "Keine Aufnahmeprognose-Daten vorhanden.\nSimulation zuerst mit aktueller Logik ausfuehren.",
            ha="center",
            va="center",
            fontsize=14,
        )
        fig.savefig(output, dpi=160)
        plt.close(fig)
        return

    x = list(range(len(tags)))
    width = 0.25
    freeze_selection = [freeze_selection_by_tag.get(tag, 0) for tag in tags]
    allowed = [allowed_by_tag.get(tag, 0) for tag in tags]
    rejected = [rejected_by_tag.get(tag, 0) for tag in tags]

    axes[0].bar([v - width for v in x], freeze_selection, width, label="Freeze-Auswahl (Engpassbudget)", color="#4f7cac")
    axes[0].bar(x, allowed, width, label="Tatsaechlich zugelassen", color="#2e8b57")
    axes[0].bar([v + width for v in x], rejected, width, label="Abgewiesen", color="#c95f5f")
    axes[0].set_ylabel("Patienten")
    axes[0].set_title("Bei Minute 420 werden nur bereits aktive Patienten gegen Rezeption-, Schwester- und Arztbudget geprueft.")
    axes[0].set_xticks(x, tags, rotation=25, ha="right")
    axes[0].grid(axis="y", linestyle=":", alpha=0.55)
    axes[0].legend(loc="upper right")

    for values, offset in ((freeze_selection, -width), (allowed, 0), (rejected, width)):
        for idx, value in enumerate(values):
            if value > 0:
                axes[0].text(idx + offset, value, str(value), ha="center", va="bottom", fontsize=9)

    tag_to_y = {tag: idx for idx, tag in enumerate(tags)}
    allowed_points = [e for e in entscheidungen if e.get("Zugelassen")]
    rejected_points = [e for e in entscheidungen if not e.get("Zugelassen")]

    if allowed_points:
        axes[1].scatter(
            [e.get("ZeitpunktMinuten", 0) for e in allowed_points],
            [tag_to_y[_tag_label(e.get("Tag"))] for e in allowed_points],
            s=46,
            color="#2e8b57",
            label="Zugelassen",
            alpha=0.85,
        )
    if rejected_points:
        axes[1].scatter(
            [e.get("ZeitpunktMinuten", 0) for e in rejected_points],
            [tag_to_y[_tag_label(e.get("Tag"))] for e in rejected_points],
            s=54,
            color="#c95f5f",
            marker="x",
            label="Abgewiesen",
            alpha=0.9,
        )

    pruefzeiten = [p.get("ZeitpunktMinuten", 0) for p in pruefungen]
    pruefzeit = min(pruefzeiten) if pruefzeiten else 420
    schliesszeit = pruefzeit + 60
    axes[1].axvline(pruefzeit, color="#4f7cac", linestyle="--", linewidth=1.5, label="Queue-Freeze")
    axes[1].axvline(schliesszeit, color="#555555", linestyle=":", linewidth=1.5, label="Praxisschluss")

    axes[1].set_yticks(x, tags)
    axes[1].set_xlabel("Zeitpunkt im Tag (Minuten seit Tagesstart)")
    axes[1].set_ylabel("Simulationstag")
    axes[1].set_title("Nach dem Freeze werden neue Ankuenfte vor der Klinik abgewiesen; bereits aktive Abweisungen bleiben sichtbar.")
    axes[1].grid(axis="x", linestyle=":", alpha=0.55)
    axes[1].legend(loc="upper right")

    fig.savefig(output, dpi=160)
    plt.close(fig)


def main():
    if len(sys.argv) != 3:
        print("Usage: aufnahmeprognose_matplotlib.py <prognose_daten.json> <output.png>", file=sys.stderr)
        return 2

    create_plot(sys.argv[1], sys.argv[2])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
