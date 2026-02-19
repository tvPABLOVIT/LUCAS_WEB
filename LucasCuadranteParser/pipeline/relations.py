# -*- coding: utf-8 -*-
"""
Reglas: horas en ventana ≥1h30, clasificación sala/cocina, agregados por turno.

Ventanas: Mediodía 10:00–16:00, Tarde 16:01–20:00, Noche 20:00–01:00 (día siguiente).
Si una persona trabaja al menos 1h30 en un turno, cuenta como 1 en ese turno (sala o cocina).
"""

import re
from dataclasses import dataclass
from typing import List, Dict

import config
from .entities import DayEntities, ParsedShift


@dataclass
class ShiftAggregate:
    """Agregado por (fecha, turno): staff_floor, staff_kitchen, hours_worked."""
    shift_name: str
    staff_floor: int
    staff_kitchen: int
    hours_worked: float


def _role_to_area(role: str) -> str | None:
    """Devuelve 'sala', 'cocina' o None si no se reconoce."""
    if not role:
        return None
    r = role.lower().strip()
    # Normalizar: quitar espacios extra, guiones, etc.
    r_normalized = re.sub(r'\s+', ' ', r).strip()
    # Buscar coincidencia exacta primero, luego parcial
    for key, area in config.ROLE_TO_AREA.items():
        key_normalized = key.lower().strip()
        # Coincidencia exacta o parcial más flexible
        if key_normalized == r_normalized or key_normalized in r_normalized or r_normalized in key_normalized:
            return area
        # También buscar sin espacios ni guiones
        if key_normalized.replace(' ', '').replace('-', '') in r_normalized.replace(' ', '').replace('-', ''):
            return area
    return None


def _overlap_hours(
    start_h: int, start_m: int, end_h: int, end_m: int,
    crosses_midnight: bool,
    win_start_h: int, win_start_m: int, win_end_h: int, win_end_m: int,
    win_crosses_midnight: bool,
) -> float:
    """
    Calcula solapamiento en horas entre un turno (start-end) y una ventana (win_*).
    Horas en minutos para comparar: start_min = start_h*60+start_m, etc.
    """
    def to_min(h: int, m: int, next_day: bool = False) -> float:
        return (h * 60 + m) + (1440 if next_day else 0)

    start_min = to_min(start_h, start_m)
    end_min = to_min(end_h, end_m)
    if crosses_midnight:
        end_min += 1440
    win_s = to_min(win_start_h, win_start_m)
    win_e = to_min(win_end_h, win_end_m)
    if win_crosses_midnight:
        win_e += 1440
    # Normalizar turno que pasa medianoche: end > start
    if end_min <= start_min:
        end_min += 1440
    if win_e <= win_s:
        win_e += 1440
    overlap_start = max(start_min, win_s)
    overlap_end = min(end_min, win_e)
    if overlap_end <= overlap_start:
        return 0.0
    return (overlap_end - overlap_start) / 60.0


def _hours_in_shift_window(ps: ParsedShift, shift_name: str) -> float:
    """Horas del turno ps que caen dentro de la ventana del turno shift_name. Otros establecimientos no cuentan."""
    if ps.is_rest_or_absence or getattr(ps, "is_other_establishment", False):
        return 0.0
    win = config.SHIFT_WINDOWS.get(shift_name)
    if not win:
        return 0.0
    (t0, t1) = win
    win_start_h, win_start_m = t0.hour, t0.minute
    win_end_h, win_end_m = t1.hour, t1.minute
    win_crosses = shift_name == "Noche"
    return _overlap_hours(
        ps.start_h, ps.start_m, ps.end_h, ps.end_m, ps.crosses_midnight,
        win_start_h, win_start_m, win_end_h, win_end_m, win_crosses,
    )


def apply_shift_rules(day_entities: DayEntities) -> List[ShiftAggregate]:
    """
    Para cada turno nominal (Mediodia, Tarde, Noche): cuenta personas en sala/cocina
    (cada trabajo con ≥ MIN_HOURS_IN_SHIFT en esa ventana cuenta 1) y suma horas.
    """
    result: List[ShiftAggregate] = []
    for shift_name in config.SHIFT_NAMES:
        staff_floor = 0
        staff_kitchen = 0
        hours_worked = 0.0
        for ps in day_entities.shifts:
            if ps.is_rest_or_absence or getattr(ps, "is_other_establishment", False):
                continue
            h = _hours_in_shift_window(ps, shift_name)
            hours_worked += h
            # Contar personal: si tiene ≥ MIN_HOURS_IN_SHIFT cuenta como 1, pero también
            # si tiene > 0 horas (aunque sean menos) y el rol se reconoce, contar como 0.5
            # para no perder personal en turnos cortos
            if h > 0:
                area = _role_to_area(ps.role)
                if h >= config.MIN_HOURS_IN_SHIFT:
                    # Turno completo: cuenta como 1
                    if area == "sala":
                        staff_floor += 1
                    elif area == "cocina":
                        staff_kitchen += 1
                elif h >= 0.5 and area:  # Turno corto pero reconocible: cuenta como 0.5
                    # Redondeamos al final, así que 0.5 se suma y luego se redondea a 1 si hay suficientes
                    if area == "sala":
                        staff_floor += 0.5
                    elif area == "cocina":
                        staff_kitchen += 0.5
        # Redondear personal: si hay 0.5 o más, redondear hacia arriba
        staff_floor_final = int(round(staff_floor))
        staff_kitchen_final = int(round(staff_kitchen))
        result.append(
            ShiftAggregate(
                shift_name=shift_name,
                staff_floor=staff_floor_final,
                staff_kitchen=staff_kitchen_final,
                hours_worked=round(hours_worked, 2),
            )
        )
    return result
