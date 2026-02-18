# -*- coding: utf-8 -*-
"""
Punto de entrada: convierte PDF de cuadrante BETLEM en JSON compatible con Lucas.
Uso: python main.py [ruta_al_PDF] [--output-dir DIR] [--csv]
"""

import argparse
import json
import sys
from pathlib import Path

# Añadir raíz del proyecto al path
sys.path.insert(0, str(Path(__file__).resolve().parent))

from pipeline.ingest import extract_text_from_pdf
from pipeline.segment import segment_by_days
from pipeline.entities import extract_entities_from_day_blocks
from pipeline.normalize import to_lucas_week


def main() -> int:
    parser = argparse.ArgumentParser(description="Convierte PDF cuadrante BETLEM a JSON Lucas")
    parser.add_argument(
        "pdf_path",
        nargs="?",
        default=None,
        help="Ruta al PDF (ej. documentacion/horas.pdf)",
    )
    parser.add_argument(
        "--output-dir",
        "-o",
        default="output",
        help="Carpeta de salida para JSON (y CSV si --csv)",
    )
    parser.add_argument(
        "--csv",
        action="store_true",
        help="Generar además un CSV resumen",
    )
    args = parser.parse_args()

    pdf_path = args.pdf_path
    if not pdf_path:
        # Por defecto: documentacion/horas.pdf respecto a la raíz del proyecto
        base = Path(__file__).resolve().parent
        pdf_path = base.parent / "documentacion" / "horas.pdf"
    pdf_path = Path(pdf_path)
    if not pdf_path.exists():
        print(f"Error: no se encuentra el PDF: {pdf_path}", file=sys.stderr)
        return 1

    try:
        text = extract_text_from_pdf(pdf_path)
    except Exception as e:
        print(f"Error extrayendo PDF: {e}", file=sys.stderr)
        return 1

    day_blocks, info = segment_by_days(text)
    if not day_blocks:
        print("No se detectaron días en el PDF.", file=sys.stderr)
        return 1

    year_from_week = None
    if info.get("week_start_dd_mm_yyyy"):
        _, _, y = info["week_start_dd_mm_yyyy"]
        year_from_week = y

    day_entities_list = extract_entities_from_day_blocks(day_blocks, year_from_week=year_from_week)
    week_data = to_lucas_week(day_entities_list)

    out_dir = Path(args.output_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    out_json = out_dir / "cuadrante_lucas.json"
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(week_data, f, ensure_ascii=False, indent=2)
    print(f"JSON guardado: {out_json}")

    if args.csv:
        import csv
        out_csv = out_dir / "cuadrante_lucas.csv"
        with open(out_csv, "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["date", "shift_name", "staff_floor", "staff_kitchen", "hours_worked"])
            for day in week_data:
                for s in day["shifts"]:
                    w.writerow([
                        day["date"],
                        s["shift_name"],
                        s["staff_floor"],
                        s["staff_kitchen"],
                        s["hours_worked"],
                    ])
        print(f"CSV guardado: {out_csv}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
