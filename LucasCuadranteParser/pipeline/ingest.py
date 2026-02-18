# -*- coding: utf-8 -*-
"""Ingesta: leer PDF y extraer texto por página."""

from pathlib import Path
from typing import List

try:
    import pdfplumber
except ImportError:
    pdfplumber = None


def extract_text_from_pdf(pdf_path: str | Path) -> str:
    """
    Extrae todo el texto del PDF concatenando las páginas.
    Usa pdfplumber; si no está instalado, lanza ImportError con mensaje claro.
    """
    if pdfplumber is None:
        raise ImportError("Se necesita pdfplumber. Ejecuta: pip install pdfplumber")

    path = Path(pdf_path)
    if not path.exists():
        raise FileNotFoundError(f"PDF no encontrado: {path}")

    parts: List[str] = []
    with pdfplumber.open(path) as pdf:
        for page in pdf.pages:
            text = page.extract_text()
            if text:
                parts.append(text)
    return "\n".join(parts)
