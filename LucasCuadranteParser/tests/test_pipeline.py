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


def test_calculated_sala_1_1_2():
    """
    Referencia: en dayplanning el lunes el esquema de sala es 1-1-2.
    El cálculo (roles + ventanas) debe dar staff_floor Mediodía=1, Tarde=1, Noche=2.
    """
    text = """
Lunes 9 febrero 	0h 	1h 	Total 	Firma
Sala (referencia 1-1-2)
Manager
10:00 - 16:00
Camarero/a
16:00 - 20:00
Camarero/a
20:00 - 00:00
Camarero/a
20:00 - 01:00
"""
    blocks, _ = segment_by_days(text)
    assert len(blocks) >= 1
    entities_list = extract_entities_from_day_blocks(blocks, year_from_week=2026)
    assert len(entities_list) >= 1
    lucas = to_lucas_week(entities_list)
    shifts = {s["shift_name"]: s for s in lucas[0]["shifts"]}
    assert shifts["Mediodia"]["staff_floor"] == 1, "Mediodía debe tener 1 en sala (Manager 10-16)"
    assert shifts["Tarde"]["staff_floor"] == 1, "Tarde debe tener 1 en sala (Camarero 16-20)"
    assert shifts["Noche"]["staff_floor"] == 2, "Noche debe tener 2 en sala (2 Camarero 20-00/01)"


def test_calculated_sala_1_1_2_role_after_time():
    """Mismo resultado 1-1-2 cuando el PDF pone el horario primero y el rol en la línea siguiente."""
    text = """
Lunes 9 febrero 	0h 	1h 	Total 	Firma
10:00 - 16:00
Manager
16:00 - 20:00
Camarero/a
20:00 - 00:00
Camarero/a
20:00 - 01:00
Camarero/a
"""
    blocks, _ = segment_by_days(text)
    assert len(blocks) >= 1
    entities_list = extract_entities_from_day_blocks(blocks, year_from_week=2026)
    lucas = to_lucas_week(entities_list)
    shifts = {s["shift_name"]: s for s in lucas[0]["shifts"]}
    assert shifts["Mediodia"]["staff_floor"] == 1
    assert shifts["Tarde"]["staff_floor"] == 1
    assert shifts["Noche"]["staff_floor"] == 2


def test_dayplanning_lunes_2_marzo_sala_1_1_2():
    """
    Estructura real DayPlanning.pdf: Lunes 2 marzo.
    Jose Camarero 16:00-00:00 (Tarde+Noche), Santiago Camarero 11:00-16:00 y 19:00-23:00 (Mediodía+Noche).
    Solo cuenta en un turno si hay >= 1h30, así Tarde debe ser 1 (solo Jose), no 2.
    """
    text = """
Lunes 2 marzo 0h 1h 2h Total Firma
Jose Camarero/a 08h00
GARCIA DE LA VEGA 16:00 - 00:00
Santiago Adolfo Camarero/a Camarero/a 09h00
MEJIA PALACIO 11:00 - 16:00 19:00 - 23:00
Ivan Segundo de cocina 09h00
PELEGRINA IGLESIA 14:00 - 23:00
Mizan Cocinero/a Cocinero/a 08h00
SHEK 12:00 - 16:00 20:00 - 00:00
"""
    blocks, _ = segment_by_days(text)
    assert len(blocks) >= 1
    entities_list = extract_entities_from_day_blocks(blocks, year_from_week=2026)
    lucas = to_lucas_week(entities_list)
    shifts = {s["shift_name"]: s for s in lucas[0]["shifts"]}
    assert shifts["Mediodia"]["staff_floor"] == 1
    assert shifts["Tarde"]["staff_floor"] == 1
    assert shifts["Noche"]["staff_floor"] == 2


if __name__ == "__main__":
    test_segment_by_days()
    test_entities_and_normalize()
    print("Tests OK")
