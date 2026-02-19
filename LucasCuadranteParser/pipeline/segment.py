# -*- coding: utf-8 -*-
"""Segmentación: detectar cabecera de semana y cortar por día."""

import re
from dataclasses import dataclass
from typing import List

# Patrón: "Lunes 9 febrero", "Martes 10 febrero", etc. (día nombre + número + mes)
# Mi.?rcoles / S.?bado: PDF puede dar Miércoles (é) o Mircoles (FFFD); Sábado (á) o Sabado/Sbado
# Mejorado: más variaciones para Miércoles (Miercoles, Mircoles, Miércoles, Mi?rcoles, etc.)
DAY_HEADER = re.compile(
    r"^(Lunes|Martes|Mi[ée]?rcoles|Mircoles|Mi.?rcoles|Jueves|Viernes|S[áa]?bado|Sabado|Sbado|S.?bado|Domingo)\s+(\d{1,2})\s+(\w+)\s*",
    re.IGNORECASE,
)


def _normalize_day_name(day_name: str) -> str:
    """Unifica nombre del día (Mircoles/Miercoles -> Miércoles, Sbado/Sabado/Sábado -> Sábado)."""
    d = day_name.strip().lower()
    # Más variaciones para Miércoles: puede venir como "Miercoles", "Mircoles", "Miércoles", "Mi?rcoles", etc.
    if (d.startswith("mi") and ("rcoles" in d or "rcol" in d)) or d in ("miercoles", "mircoles", "miércoles", "mi?rcoles"):
        return "Miércoles"
    if (d.startswith("s") and "bado" in d) or d in ("sabado", "sbado", "sábado"):
        return "Sábado"
    # Capitalizar primera letra para consistencia
    return day_name.strip().capitalize()

# Rango de semana en cabecera: "del 09/02/2026 al 15/02/2026" o "del 09/02/2026 al 15/02/2026"
WEEK_RANGE = re.compile(
    r"del\s+(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})\s+al\s+(\d{1,2})[/\-](\d{1,2})[/\-](\d{4})",
    re.IGNORECASE,
)


@dataclass
class DayBlock:
    """Bloque de texto correspondiente a un día del cuadrante."""
    day_name: str
    day_num: int
    month_name: str
    raw_text: str
    start_line: int


def _parse_week_range(text: str) -> tuple | None:
    """Devuelve (día_inicio, mes, año) del inicio de semana si se encuentra."""
    m = WEEK_RANGE.search(text)
    if not m:
        return None
    d1, m1, y1 = int(m.group(1)), int(m.group(2)), int(m.group(3))
    return (d1, m1, y1)


def _month_spanish_to_num(name: str) -> int:
    meses = {
        "enero": 1, "febrero": 2, "marzo": 3, "abril": 4, "mayo": 5, "junio": 6,
        "julio": 7, "agosto": 8, "septiembre": 9, "octubre": 10, "noviembre": 11, "diciembre": 12,
    }
    return meses.get(name.lower().strip(), 0)


def segment_by_days(full_text: str) -> tuple[List[DayBlock], dict]:
    """
    Segmenta el texto en bloques por día.
    Retorna (lista de DayBlock, info) donde info puede contener week_start para fechas.
    """
    lines = full_text.split("\n")
    day_blocks: List[DayBlock] = []
    info = {}

    week_start = _parse_week_range(full_text)
    if week_start:
        info["week_start_dd_mm_yyyy"] = week_start

    i = 0
    while i < len(lines):
        line = lines[i]
        # Intentar match estricto primero
        m = DAY_HEADER.match(line.strip())
        if not m:
            # Fallback: buscar patrón más flexible para días que pueden tener encoding raro
            # Buscar línea que empiece con nombre de día seguido de número
            day_pattern_flexible = re.compile(
                r"^(Lunes|Martes|Mi[ée]?rcoles|Mircoles|Mi.?rcoles|Jueves|Viernes|S[áa]?bado|Sabado|Sbado|S.?bado|Domingo)\s*[:\-]?\s*(\d{1,2})\s+(\w+)",
                re.IGNORECASE,
            )
            m = day_pattern_flexible.search(line.strip())
        if m:
            day_name = _normalize_day_name(m.group(1))
            try:
                day_num = int(m.group(2))
                month_name = m.group(3)
            except (ValueError, IndexError):
                i += 1
                continue
            # Acumular líneas hasta el siguiente día o fin
            chunk = [line]
            j = i + 1
            while j < len(lines):
                next_line = lines[j]
                next_stripped = next_line.strip()
                if DAY_HEADER.match(next_stripped) or day_pattern_flexible.search(next_stripped):
                    break
                chunk.append(next_line)
                j += 1
            raw = "\n".join(chunk)
            day_blocks.append(
                DayBlock(
                    day_name=day_name,
                    day_num=day_num,
                    month_name=month_name,
                    raw_text=raw,
                    start_line=i + 1,
                )
            )
            i = j
        else:
            i += 1

    return day_blocks, info
