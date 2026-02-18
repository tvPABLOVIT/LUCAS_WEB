# -*- coding: utf-8 -*-
"""Pipeline: PDF cuadrante BETLEM -> JSON compatible con Lucas."""

from .ingest import extract_text_from_pdf
from .segment import segment_by_days
from .entities import extract_entities_from_day_blocks
from .relations import apply_shift_rules
from .normalize import to_lucas_week

__all__ = [
    "extract_text_from_pdf",
    "segment_by_days",
    "extract_entities_from_day_blocks",
    "apply_shift_rules",
    "to_lucas_week",
]
