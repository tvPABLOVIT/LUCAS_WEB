# Auditoría: parser vs formato página 1

Comparación del comportamiento actual del parser con la especificación en `formato_pagina1_cabecera.md`. Referencia: DayPlanning.pdf y salida en `output_test/cuadrante_lucas.json`.

---

## Qué hace bien el parser

### Cabecera (líneas 1-2)
- **Rango de fechas del PDF:** `segment.py` busca en todo el texto el patrón `del DD/MM/YYYY al DD/MM/YYYY` (`WEEK_RANGE`) y extrae la primera fecha como `week_start_dd_mm_yyyy` (día, mes, año). Eso coincide con "Horarios de BETLEM del **02/03/2026** al 15/03/2026", así que el **año (2026)** se usa correctamente para fechar los días.
- No usa "Imprimir fecha" ni "Miércoles 04/03/2026" para nada; al buscar solo el patrón "del ... al ...", la fecha de impresión se ignora de hecho.

### Líneas 3-7
- No hay lógica que dependa del texto del QR o de "Notas"; esa parte del texto no se usa. **Correcto.**

### Línea 8 (cabecera del día)
- **segment** detecta cabeceras de día con el patrón "Lunes|Martes|... + número + mes" (`DAY_HEADER` o variantes) y corta por bloques por día. La **fecha del cuadrante** (ej. "Lunes 2 marzo") se obtiene de la primera línea de cada bloque y se usa para construir la fecha del día (día, mes, año). **Correcto.**
- Las columnas 0h-23h y "Total"/"Firma" no se parsean como tabla; el parser extrae empleados y turnos del texto. Para la especificación actual no hace falta interpretar cada columna. **Aceptable.**

### Líneas 9-28 (empleados y totales)
- **entities.py** reconoce:
  - Pares **Nombre** → **Apellidos XXhYY** (ej. `Leonel Pablo Daniel` → `VITALE 00h00`).
  - Línea única **Nombre Rol XXhYY** (ej. `Guillermo Camarero/a 08h00`).
- Asocia cada línea de horarios (rótulo + rango) a la **persona** según la última línea de empleado vista (`employee_at_line`). Así se respeta que los rótulos/horarios pertenecen a una fila con el nombre en la primera columna. **Correcto en concepto.**

### Línea 29 (Total empleados)
- Si la línea contiene "Total empleados", el parser la **salta** (y la siguiente si es solo números). No usa esos números. **Correcto** y alineado con “considerar ignorar esta zona”.

### Líneas 30-36 (Puestos, Equipos, Empleados)
- No hay lógica que use "Equipos:", "sala, cocina" ni la lista de "Empleados:". Esas partes se ignoran. **Correcto.**
- **Puestos:** el parser no lee el código de color del PDF (solo texto). La distinción trabajo / descanso / otro establecimiento se hace por **texto** (Descanso semanal, Abs, " - MOLINA", etc.), no por color. La especificación dice “si podemos leer esto correctamente nos facilitaría todo”; actualmente no se usa la leyenda Puestos. **Aceptable** si el texto es suficiente.

### Líneas 37-53 (rótulos y horarios)
- Se detectan pares **rótulo** → **horario** (`Descanso semanal` → `(8h)`, `Camarero/a` → `16:00 - 00:00`, etc.).
- **Otro establecimiento:** `_is_other_establishment(role)` detecta roles con `" - NOMBRE"` (ej. "Chef Operativo - MOLINA"). Esos turnos se marcan con `is_other_establishment=True` y **no suman horas** (`duration_hours` devuelve 0). **Correcto.**  
  "Chef Operativo" a secas (sin " - MOLINA" etc.) **sí se cuenta**: solo se ignora cuando lleva el nombre de otro establecimiento.
- **Descanso/ausencia:** "Descanso semanal", "(8h)", "Abs", "Ausencia injustificada", etc. se tratan como no trabajo (`is_rest_or_absence`) y no suman. **Correcto.**

### Línea 55 ("-- 1 of 14 --")
- **segment** solo avanza cuando encuentra cabecera de día; no busca ni usa esa cadena. No afecta al resultado. **Correcto.**

### Salida
- Con DayPlanning.pdf el JSON tiene fechas **2026-03-02**, **2026-03-03**, etc., y por día: `total_hours_worked` y `shifts` (Mediodía, Tarde, Noche) con `staff_floor`, `staff_kitchen`, `hours_worked`. El formato es el esperado para Lucas.

---

## Qué falta o se puede mejorar (según la especificación)

| Especificación | Estado actual | Acción recomendada |
|----------------|---------------|--------------------|
| **Extraer nombre del establecimiento** (BETLEM) de "Horarios de BETLEM del ... al ..." | No se extrae; solo se usa el rango de fechas para el año. | Añadir en `segment` (o ingest) el parseo de la línea tipo "Horarios de X del ... al ..." y devolver `establishment` en `info` (y opcionalmente en la salida JSON). |
| **Devolver rango completo del PDF** (fecha inicio y fecha fin) | Solo se guarda el inicio en `week_start_dd_mm_yyyy`; la fecha fin no se expone. | Opcional: devolver también fecha fin en `info` para validar que los días parseados caigan dentro del rango. |
| **Leer código de color (Puestos)** | No se usa; solo texto. | Futuro: si se usa pdfplumber para leer propiedades de celdas/rectángulos por color, se podría clasificar por "Puestos" en lugar de solo por texto. No prioritario si el texto es fiable. |

---

## Resumen

- El parser **lee correctamente** la página 1 en lo esencial: ignora fecha de impresión y QR, usa el rango de fechas del PDF para el año, detecta días, empleados, turnos, descansos y otros establecimientos, y no cuenta horas de descanso ni de otros establecimientos.
- **Falta:** extraer y exponer el **nombre del establecimiento** (BETLEM) y, si se desea, el **rango completo** (fecha inicio y fin) en la salida o en `info` para validaciones o UI.
- El resto del comportamiento está alineado con la especificación línea a línea de la página 1; no es necesario cambiar el programa para “cumplir” la spec, salvo si se quieren añadir los puntos de la tabla anterior.
