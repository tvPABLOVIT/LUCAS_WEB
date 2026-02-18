# -*- coding: utf-8 -*-
"""Pruebas básicas del pipeline (segmentación, entidades, reglas, normalización)."""

import sys
from pathlib import Path

# Raíz del proyecto
ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from pipeline.segment import segment_by_days, DayBlock
from pipeline.entities import extract_entities_from_day_blocks, DayEntities
from pipeline.relations import apply_shift_rules
from pipeline.normalize import to_lucas_week


def test_segment_by_days():
    text = """
Horarios de BETLEM del 09/02/2026 al 15/02/2026

Lunes 9 febrero 	0h 	1h 	... Total 	Firma
Guillermo
DEO MARTÍN 08h00
Total empleados 	0 0 ...
Puestos: 	Camarero…
Martes 10 febrero 	0h 	1h 	... Total 	Firma
Leonel
VITALE 07h13
Total empleados 	0 0 ...
"""
    blocks, info = segment_by_days(text)
    assert len(blocks) >= 2
    assert blocks[0].day_name.lower() == "lunes"
    assert blocks[0].day_num == 9
    assert blocks[1].day_name.lower() == "martes"
    assert blocks[1].day_num == 10
    if info.get("week_start_dd_mm_yyyy"):
        d, m, y = info["week_start_dd_mm_yyyy"]
        assert y == 2026


def test_entities_and_normalize():
    text = """
Lunes 9 febrero 	0h 	1h 	Total 	Firma
Guillermo
DEO MARTÍN 08h00
Total empleados 	0 0
Puestos: 	Camarero…
Equipos: sala, cocina
Empleados:
Manager
10:00 - 16:00
Camarero/a
16:00 - 20:00
"""
    blocks, _ = segment_by_days(text)
    assert len(blocks) == 1
    entities_list = extract_entities_from_day_blocks(blocks, year_from_week=2026)
    assert len(entities_list) == 1
    day = entities_list[0]
    assert day.date_iso.startswith("2026-")
    lucas = to_lucas_week(entities_list)
    assert len(lucas) == 1
    assert "date" in lucas[0]
    assert "shifts" in lucas[0]
    assert len(lucas[0]["shifts"]) == 3
    shift_names = {s["shift_name"] for s in lucas[0]["shifts"]}
    assert shift_names == {"Mediodia", "Tarde", "Noche"}


if __name__ == "__main__":
    test_segment_by_days()
    test_entities_and_normalize()
    print("Tests OK")
