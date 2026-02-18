# -*- coding: utf-8 -*-
"""Configuración central: ventanas de turno, umbral y mapa sala/cocina."""

import json
from datetime import time
from pathlib import Path

# Ventanas de turno por defecto
# Mediodía: 10:00–16:00 (6 h)
# Tarde: 16:01–20:00 (desde las 16:01 hasta las 20)
# Noche: 20:00–01:00 día siguiente (hasta la 1 de la madrugada)
_DEFAULT_SHIFT_WINDOWS = {
    "Mediodia": (time(10, 0), time(16, 0)),   # mismo día
    "Tarde": (time(16, 1), time(20, 0)),      # 16:01 para no solapar con fin de Mediodía
    "Noche": (time(20, 0), time(1, 0)),       # 01:00 = día siguiente
}


def _parse_time(s: str) -> time:
    """Convierte 'HH:MM' o 'H:MM' en time."""
    s = s.strip()
    if ":" in s:
        parts = s.split(":", 1)
        h = int(parts[0].strip())
        m = int(parts[1].strip()) if len(parts) > 1 else 0
        return time(h, m)
    return time(0, 0)


def _load_shift_windows() -> dict:
    """Carga SHIFT_WINDOWS desde shift_windows.json si existe; si no, usa valores por defecto."""
    base = Path(__file__).resolve().parent
    json_path = base / "shift_windows.json"
    if not json_path.exists():
        return _DEFAULT_SHIFT_WINDOWS.copy()
    try:
        with open(json_path, encoding="utf-8") as f:
            data = json.load(f)
    except (json.JSONDecodeError, OSError):
        return _DEFAULT_SHIFT_WINDOWS.copy()
    result = {}
    for name in ("Mediodia", "Tarde", "Noche"):
        if name not in data or not isinstance(data[name], (list, tuple)) or len(data[name]) < 2:
            result[name] = _DEFAULT_SHIFT_WINDOWS[name]
        else:
            t0 = _parse_time(str(data[name][0]))
            t1 = _parse_time(str(data[name][1]))
            result[name] = (t0, t1)
    return result


SHIFT_WINDOWS = _load_shift_windows()

# Mínimo de horas dentro de la ventana para contar como "trabajó ese turno" (1h30)
MIN_HOURS_IN_SHIFT = 1.5

# Nombres de turnos en la salida (igual que Lucas)
SHIFT_NAMES = ["Mediodia", "Tarde", "Noche"]

# Mapa: puesto (como aparece en el PDF) -> "sala" o "cocina"
# Orden: claves más específicas primero (ej. "segundo de cocina" antes que "segundo")
# Sala: Manager, Camarero/a, Jefe de sala, Segundo de sala (y variantes)
# Cocina: Jefe de cocina, Segundo de cocina, Chef Operativo, Cocinero/a (y variantes)
ROLE_TO_AREA = {
    # Sala
    "jefe de sala": "sala",
    "segundo de sala": "sala",
    "camarero/a": "sala",
    "camarero": "sala",
    "camarera": "sala",
    "camarera/o - centric": "sala",
    "manager": "sala",
    "supervisor": "sala",
    # Cocina (segundo de cocina antes que "segundo" para no pillar "segundo de sala")
    "chef oper": "cocina",
    "chef operativo": "cocina",
    "chef operativo - molina": "cocina",
    "cocinero/a": "cocina",
    "cocinero": "cocina",
    "cocinera": "cocina",
    "jefe de cocina": "cocina",
    "jefe de co": "cocina",
    "segundo de cocina": "cocina",
    "segundo": "cocina",
    "soporte": "cocina",
    "soporte operaciones": "cocina",
}

# En el cuadrante: gris sólido = Ausencias; gris con rayas = Otros est. (no trabajan en BETLEM).
# El parser no lee colores; detecta ausencias por texto (NON_WORK_PATTERNS) y otros est. por " - NOMBRE" (CENTRIC, MOLINA, etc.).

# Patrones que no son turnos de trabajo (no suman horas en ventana)
NON_WORK_PATTERNS = [
    "descanso semanal",
    "descanso compensatorio",
    "recuperación festivo",
    "abs",
    "ausencia injustificada",
    "ausencia",
]
