# -*- coding: utf-8 -*-
"""
Normalización: construir JSON por semana compatible con Lucas.

Lógica principal: horario a horario. total_hours_worked = suma de la duración
de cada rango horario del día (11-16 = 5h, 19-23 = 4h, etc.), sin descansos.
Los turnos (Mediodía/Tarde/Noche) se rellenan con el solapamiento de esos rangos.
"""

from dataclasses import dataclass, asdict
from typing import List, Any

from .entities import DayEntities, duration_hours
from .relations import apply_shift_rules, ShiftAggregate


@dataclass
class DayOutput:
    """Un día en formato Lucas."""
    date: str
    total_revenue: float
    total_hours_worked: float
    shifts: List[dict]


def _day_total_hours(day_entities: DayEntities) -> float:
    """
    Total del día = suma de duración de cada rango, sin descansos ni otros establecimientos.
    Si un empleado tiene "Ausencia injustificada XXhYY", se aplica tope: min(suma de sus turnos, XXhYY).
    Se añaden las horas de empleados que solo tienen descanso pero con total > 0 (ej. 00h47) para
    coincidir con la suma del PDF (ej. Martes 32.28).
    """
    from collections import defaultdict
    by_employee: dict = defaultdict(list)
    for ps in day_entities.shifts:
        if ps.is_rest_or_absence or getattr(ps, "is_other_establishment", False):
            continue
        d = duration_hours(ps)
        key = (ps.employee_name or "")
        by_employee[key].append((ps, d))
    total = 0.0
    employees_with_work = set()
    for _emp, pairs in by_employee.items():
        emp_total = sum(d for _ps, d in pairs)
        cap = None
        for ps, _d in pairs:
            cap = getattr(ps, "max_hours_cap", None)
            if cap is not None:
                break
        if cap is not None:
            emp_total = min(emp_total, cap)
        total += emp_total
        if _emp:
            employees_with_work.add(_emp.strip())
    # Empleados que solo tienen descanso pero con total > 0 y < 2h (ej. Descanso semanal 00h47)
    # para coincidir con el PDF (Martes 32.28) sin doble contar en otros días
    for e in day_entities.employees:
        if not e.name or "sin asignar" in e.name.lower() or e.total_hours <= 0 or e.total_hours >= 2:
            continue
        name_key = e.name.strip()
        if name_key not in employees_with_work:
            total += e.total_hours
    return total


def to_lucas_week(day_entities_list: List[DayEntities]) -> List[dict]:
    """
    Convierte la lista de DayEntities en una lista de días en formato Lucas:
    cada día tiene date, total_revenue (0), total_hours_worked, shifts (array de
    { shift_name, staff_floor, staff_kitchen, hours_worked }).
    """
    result: List[dict] = []
    for day_entities in day_entities_list:
        aggregates: List[ShiftAggregate] = apply_shift_rules(day_entities)
        total_hours = _day_total_hours(day_entities)
        shifts_json = [
            {
                "shift_name": a.shift_name,
                "staff_floor": a.staff_floor,
                "staff_kitchen": a.staff_kitchen,
                "hours_worked": round(a.hours_worked, 2),
            }
            for a in aggregates
        ]
        result.append({
            "date": day_entities.date_iso,
            "total_revenue": 0.0,
            "total_hours_worked": round(total_hours, 2),
            "shifts": shifts_json,
        })
    return result
