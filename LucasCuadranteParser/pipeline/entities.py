# -*- coding: utf-8 -*-
"""
Entidades: fechas, empleados y turnos (rol + rango) a partir de bloques de día.

Lógica principal: horario a horario. Las horas se calculan desde cada rango
(11:00-16:00 = 5h, 19:00-23:00 = 4h, 20:00-01:00 = 5h); el total del día es
la suma de esas duraciones (sin contar descansos/ausencias).
"""

import re
from dataclasses import dataclass
from datetime import date, datetime
from typing import List, Optional

from .segment import DayBlock

# Puestos conocidos (sala/cocina) para no usar nombres de empleados como rol
KNOWN_ROLE_KEYWORDS = (
    "manager", "camarero", "camarera", "supervisor",
    "jefe de sala", "segundo de sala",
    "jefe de cocina", "jefe de co", "segundo de cocina", "segundo",
    "cocinero", "cocinera", "chef oper", "chef operativo", "soporte",
)

RE_TOTAL_HOURS = re.compile(r"(\d{1,2})h(\d{2})\s*$", re.IGNORECASE)
RE_TIME_RANGE = re.compile(r"(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})", re.IGNORECASE)
RE_DURATION_ONLY = re.compile(r"\((\d+)h\)\s*$", re.IGNORECASE)


@dataclass
class ParsedEmployee:
    """Empleado con horas totales del día."""
    name: str
    total_hours: float


@dataclass
class ParsedShift:
    """Turno: rol + rango horario (o descanso/ausencia)."""
    role: str
    start_h: int
    start_m: int
    end_h: int
    end_m: int
    crosses_midnight: bool = False
    is_rest_or_absence: bool = False
    is_other_establishment: bool = False  # True si trabajó en otro restaurante (ej. " - CENTRIC", " - MOLINA")
    duration_hours: Optional[float] = None
    employee_name: Optional[str] = None  # persona a la que pertenece (orden PDF)
    # Si la línea tiene "Ausencia injustificada XXhYY", tope de horas a contar para ese empleado ese día
    max_hours_cap: Optional[float] = None


@dataclass
class DayEntities:
    """Entidades extraídas de un día."""
    date_iso: str
    employees: List[ParsedEmployee]
    shifts: List[ParsedShift]  # lista plana de turnos (rol + rango) para ese día


def _spanish_month_to_num(month_name: str) -> int:
    meses = {
        "enero": 1, "febrero": 2, "marzo": 3, "abril": 4, "mayo": 5, "junio": 6,
        "julio": 7, "agosto": 8, "septiembre": 9, "octubre": 10, "noviembre": 11, "diciembre": 12,
    }
    return meses.get(month_name.lower().strip(), 0)


def _build_date(day_num: int, month_name: str, year: int) -> date:
    m = _spanish_month_to_num(month_name)
    if m == 0:
        m = 1
    return date(year, m, day_num)


def _parse_total_hours(s: str) -> float:
    m = RE_TOTAL_HOURS.search(s.strip())
    if not m:
        return 0.0
    h, mm = int(m.group(1)), int(m.group(2))
    return h + mm / 60.0


def _is_rest_or_absence(role: str) -> bool:
    r = role.lower().strip()
    for p in ["descanso semanal", "descanso compensatorio", "recuperación festivo", "abs", "ausencia injustificada", "ausencia"]:
        if p in r:
            return True
    return False


def _line_indicates_absence(line: str) -> bool:
    """
    True si la línea indica claramente ausencia (frases completas), no solo 'abs'.
    Evita marcar como ausencia líneas como 'Santiago Adolfo Abs Camarero/a' (trabaja como Camarero).
    """
    if not line:
        return False
    r = line.lower().strip()
    # Solo frases que indican ausencia explícita; no 'abs' aislado (puede ser código o abreviatura)
    for p in ["ausencia injustificada", "descanso semanal", "descanso compensatorio", "recuperación festivo"]:
        if p in r:
            return True
    return False


def _is_other_establishment(role: str) -> bool:
    """
    True si el rol indica que trabajó en otro restaurante (ej. "Camarero/a - CENTRIC", "Chef Operativo - MOLINA").
    El nombre después de " - " es el establecimiento; esas horas no cuentan para BETLEM.
    """
    if not role or " - " not in role:
        return False
    # Tras el primer " - " puede venir "CENTRIC", "MOLINA" o "CENTRIC 16:00 - 00:00"; tomamos la primera palabra
    part = role.split(" - ", 1)[-1].strip()
    first_word = part.split()[0] if part.split() else ""
    if not first_word or len(first_word) > 30:
        return False
    # Nombre de establecimiento: solo letras (ej. CENTRIC, MOLINA)
    if re.match(r"^[A-Za-záéíóúÁÉÍÓÚñÑ]+$", first_word) and len(first_word) >= 2:
        return True
    return False


def duration_hours(ps: "ParsedShift") -> float:
    """Horario a horario: duración del rango (11-16 = 5h, 19-23 = 4h; si pasa medianoche, ej. 20-01 = 5h). Descansos y otros establecimientos = 0."""
    if ps.is_rest_or_absence or getattr(ps, "is_other_establishment", False):
        return 0.0
    start_min = ps.start_h * 60 + ps.start_m
    end_min = ps.end_h * 60 + ps.end_m
    if ps.crosses_midnight or end_min <= start_min:
        end_min += 24 * 60
    return (end_min - start_min) / 60.0


def extract_entities_from_day_blocks(
    day_blocks: List[DayBlock],
    year_from_week: Optional[int] = None,
) -> List[DayEntities]:
    """
    Extrae de cada DayBlock: fecha, empleados con horas totales, y lista plana de
    turnos (rol + rango horario o descanso). year_from_week puede venir del rango
    de la cabecera del PDF.
    """
    current_year = year_from_week or datetime.now().year
    result: List[DayEntities] = []

    for block in day_blocks:
        try:
            d = _build_date(block.day_num, block.month_name, current_year)
        except ValueError:
            d = date(current_year, 1, 1)
        date_iso = d.isoformat()
        lines = [ln.strip() for ln in block.raw_text.split("\n") if ln.strip()]

        # Empleados: pares Nombre / Apellidos HHhMM o una sola línea "Nombre ... XXhYY". No parar en
        # "Total empleados" porque hay más empleados en la página siguiente (mismo día).
        RE_COUNT_ROW = re.compile(r"^[\d\s]+$")
        RE_SAME_LINE_HOURS = re.compile(r"^(.+?)\s+(\d{1,2})h(\d{2})\s*$", re.IGNORECASE)
        employees: List[ParsedEmployee] = []
        employee_line_names: List[tuple] = []  # (line_index, name) para asociar rangos a persona
        i = 0
        while i < len(lines):
            line = lines[i]
            if "Total empleados" in line:
                i += 1
                # Números pueden estar en la misma línea ("Total empleados 0 0 ...") o en la siguiente
                if i < len(lines) and RE_COUNT_ROW.match(lines[i].strip()):
                    i += 1
                continue
            # Cabecera del día o tabla: "Lunes 9 febrero 0h 1h ..." (Mi.?rcoles / S.?bado por encoding PDF)
            if re.match(r"^(Lunes|Martes|Mi.?rcoles|Jueves|Viernes|S.?bado|Domingo)\s+\d", line, re.I):
                i += 1
                continue
            if "\t" in line and "Total" in line:
                i += 1
                continue
            # Una sola línea con XXhYY al final (ej. "Ivan Descanso semanal 00h00", "Chef Oper Chef Operativo 08h00").
            # No tratar como single-line si la línea mezcla turnos con totales (rango horario o línea larga con "(8h)").
            m_single = RE_SAME_LINE_HOURS.match(line)
            if m_single and (
                re.search(r"\d{1,2}:\d{2}\s*-\s*\d{1,2}:\d{2}", line)
                or (len(m_single.group(1)) > 40 and re.search(r"\(\d+h\)", line))
            ):
                m_single = None
            if m_single and "Total empleados" not in line and "Sin asignar" not in line:
                pre, hh, mm = m_single.group(1).strip(), int(m_single.group(2)), int(m_single.group(3))
                total_h = hh + mm / 60.0
                # Nombre: primeros tokens antes de rol conocido
                tokens = pre.split()
                name_tokens = []
                for t in tokens:
                    if t.lower() in ("descanso", "semanal", "compensatorio", "abs", "ausencia", "chef", "operativo", "oper", "manager", "camarero", "camarera", "cocinero", "cocinera", "jefe", "segundo", "supervisor", "soporte") and len(name_tokens) >= 1:
                        break
                    name_tokens.append(t)
                name = " ".join(name_tokens) if name_tokens else (pre.split()[0] if pre.split() else pre)
                if name and len(name) < 50:
                    employees.append(ParsedEmployee(name=name, total_hours=total_h))
                    employee_line_names.append((i, name))
                i += 1
                continue
            # Par de líneas: Nombre / Apellidos XXhYY. No emparejar si first es un turno (rango horario o (8h)).
            if i + 1 >= len(lines):
                i += 1
                continue
            first, second = lines[i], lines[i + 1]
            if "," in first and len(first) > 50:
                i += 1
                continue
            if re.search(r"\d{1,2}:\d{2}\s*-\s*\d{1,2}:\d{2}", first) or re.search(r"\(\d+h\)", first):
                i += 1
                continue
            total_h = _parse_total_hours(second)
            if re.search(r"\d{1,2}h\d{2}", second):
                # Python 3.11 no permite backslashes dentro de la expresión de un f-string;
                # limpiamos la segunda línea primero y luego construimos el nombre.
                clean_second = re.sub(r"\s*\d{1,2}h\d{2}\s*$", "", second, flags=re.I).strip()
                name = f"{first} {clean_second}".strip()
                if name and not name.startswith("Total empleados") and "Sin asignar" not in name and len(name) < 80:
                    employees.append(ParsedEmployee(name=name, total_hours=total_h))
                    employee_line_names.append((i, name))
                i += 2
                continue
            i += 1

        # Última persona "activa" por línea (para asociar cada rango horario a la persona correcta)
        employee_at_line: List[Optional[str]] = [None] * len(lines)
        current_name: Optional[str] = None
        for idx in range(len(lines)):
            for (j, name) in reversed(employee_line_names):
                if j <= idx:
                    current_name = name
                    break
            employee_at_line[idx] = current_name

        # Turnos: en el PDF extraído por pdfplumber, rol+hora están entremezclados con la lista de
        # empleados (línea "Nombre Rol 08h00", siguiente "APELLIDOS 16:00 - 00:00"). También pueden
        # aparecer después de "Equipos:" / "Empleados:". Recorremos todo el bloque desde la línea 1
        # (tras la cabecera del día) para capturar todos los rangos horarios.
        shifts: List[ParsedShift] = []
        parse_start = 1  # saltar línea 0 (cabecera "Lunes 9 febrero 0h 1h ...")
        def _is_time_or_duration(ln: str) -> bool:
            return bool(RE_TIME_RANGE.search(ln)) or bool(RE_DURATION_ONLY.search(ln))

        def _normalize_role_candidate(cand: str) -> str:
            """Quita sufijo HHhMM de líneas tipo 'Manager 08h00' para usar solo el rol."""
            s = re.sub(r"\s*\d{1,2}h\d{2}\s*$", "", cand.strip(), flags=re.I).strip()
            return s or cand.strip()

        def _extract_known_role(ln: str) -> str:
            """De una línea tipo 'Guillermo Manager 08h00' extrae solo el puesto conocido ('Manager')."""
            s = _normalize_role_candidate(ln).lower()
            if not s:
                return ""
            best = ""
            for kw in sorted(KNOWN_ROLE_KEYWORDS, key=len, reverse=True):
                if kw in s:
                    idx = s.find(kw)
                    orig = ln.lower()
                    orig_idx = orig.find(kw)
                    if orig_idx >= 0:
                        best = ln[orig_idx : orig_idx + len(kw)]
                        if best:
                            return best.strip()
            return _normalize_role_candidate(ln)

        def _looks_like_role(ln: str) -> bool:
            """True si la línea parece un puesto conocido (Manager, Camarero/a, etc.)."""
            s = _normalize_role_candidate(ln).lower()
            if not s:
                return False
            return any(kw in s for kw in KNOWN_ROLE_KEYWORDS)

        def _find_role_line(idx: int) -> str:
            """Busca hacia atrás la última línea que sea un rol conocido (no hora, duración ni nombre)."""
            for j in range(idx - 1, -1, -1):
                cand = lines[j].strip()
                if not cand or cand.startswith("Notas") or re.match(r"^--\s*\d+ of \d+", cand):
                    continue
                if _is_time_or_duration(cand):
                    continue
                if RE_TOTAL_HOURS.search(cand) and not _looks_like_role(cand):
                    continue
                if not _looks_like_role(cand):
                    continue
                return _extract_known_role(cand)
            return ""

        i = parse_start
        while i < len(lines):
            line = lines[i]
            if not line or line.startswith("Notas") or re.match(r"^--\s*\d+ of \d+", line):
                i += 1
                continue
            role = _find_role_line(i)
            # Turno partido: una línea puede tener varios rangos (ej. "12:00 - 16:00 20:00 - 00:00")
            # = misma persona hace 2 turnos el mismo día; creamos un ParsedShift por cada rango
            emp_name = employee_at_line[i] if i < len(employee_at_line) else None
            # Para detectar "otro establecimiento" (ej. "Camarero/a - CENTRIC") usamos la línea del rol y la actual
            role_line_full = (lines[i - 1].strip() if i > 0 else "") + " " + line
            role_line_prev = (lines[i - 1].strip() if i > 0 else "")
            # Si "Ausencia injustificada XXhYY", usar XXhYY como tope de horas para ese empleado (no excluir)
            cap_hours: Optional[float] = None
            if "ausencia injustificada" in role_line_prev.lower():
                cap_m = RE_TOTAL_HOURS.search(role_line_prev)
                if cap_m:
                    cap_hours = int(cap_m.group(1)) + int(cap_m.group(2)) / 60.0
            for tr in RE_TIME_RANGE.finditer(line):
                sh, sm = int(tr.group(1)), int(tr.group(2))
                eh, em = int(tr.group(3)), int(tr.group(4))
                crosses = (eh < sh) or (eh == 0 and em == 0)
                use_role = role or (lines[i - 1].strip() if i > 0 else line)
                # Ausencia: comprobar también la línea del rol; usar frases completas para la línea previa
                # (ej. "Guillermo Manager Ausencia injustificada 05h30") para no marcar "Abs Camarero/a" como ausencia
                rest = _is_rest_or_absence(use_role) or _line_indicates_absence(role_line_full) or _line_indicates_absence(role_line_prev)
                # Si hay tope por "Ausencia injustificada XXhYY", no marcar como rest (contamos con tope)
                if cap_hours is not None:
                    rest = False
                # "Sin asignar" no cuenta (ej. "Sin asignar Supervisor 18:00-23:00")
                if "sin asignar" in role_line_full.lower() or "sin asignar" in line.lower():
                    rest = True
                other_est = _is_other_establishment(role_line_full) or _is_other_establishment(line) or _is_other_establishment(use_role)
                shifts.append(ParsedShift(
                    role=use_role,
                    start_h=sh, start_m=sm, end_h=eh, end_m=em,
                    crosses_midnight=crosses,
                    is_rest_or_absence=rest,
                    is_other_establishment=other_est,
                    employee_name=emp_name,
                    max_hours_cap=cap_hours,
                ))
            duration_m = RE_DURATION_ONLY.search(line)
            if duration_m and not RE_TIME_RANGE.search(line):
                use_role = role or (lines[i - 1].strip() if i > 0 else re.sub(r"\s*\(\d+h\)\s*$", "", line, flags=re.I).strip())
                other_est = _is_other_establishment(use_role)
                shifts.append(ParsedShift(
                    role=use_role,
                    start_h=0, start_m=0, end_h=0, end_m=0,
                    is_rest_or_absence=True,
                    is_other_establishment=other_est,
                    duration_hours=float(duration_m.group(1)),
                    employee_name=emp_name,
                ))
            i += 1

        result.append(DayEntities(date_iso=date_iso, employees=employees, shifts=shifts))
    return result
