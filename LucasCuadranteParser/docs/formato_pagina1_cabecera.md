# Formato PDF – Página 1 (completa)

Este documento describe la **primera página** del PDF de horarios (formato DayPlanning / cuadrante BETLEM) **línea a línea**. La especificación se basa en las explicaciones del usuario y es la **referencia para implementar** el parser cuando se suba un PDF.

El formato es tipo **hoja de cálculo**: filas y columnas. Cada día tiene una cabecera de columnas (horas del día) y filas por empleado con sus turnos.

---

## Resumen rápido

| Líneas   | Contenido                    | Acción |
|----------|------------------------------|--------|
| 1-2      | Imprimir fecha + rango PDF   | Ignorar "Imprimir fecha" y fecha; **usar** "Horarios de [ESTABLECIMIENTO] del [fecha] al [fecha]" |
| 3-7      | QR, Notas                    | **Ignorar** |
| 8        | Cabecera tabla: día + horas + Total + Firma | **Usar**: fecha del cuadrante, columnas de horas, columna Total (horas por persona) |
| 9-28     | Nombres del personal + horas del día | **Usar**: son los empleados y las horas que trabajaron ese día (cuidado: pueden haber trabajado en otro establecimiento → no nos interesa) |
| 29       | Total empleados por hora     | Cuidado: incluye quien trabaja en otro establecimiento; **considerar ignorar** si da problemas |
| 30-36    | Puestos (leyenda); Equipos; Empleados | **Usar** solo "Puestos:" (código de color; "Otros est." = otros establecimientos). **Ignorar** Equipos y lista Empleados |
| 37-53    | Rótulos + horarios por fila   | **Usar**: cada par rótulo → horario pertenece a una **fila** cuyo nombre está en la primera columna. "X - MOLINA" = trabajó en otro establecimiento |
| 55       | "-- 1 of 14 --"              | **Ignorar** |

---

## Especificación línea a línea (página 1)

### 1. Líneas 1–2
**Texto:** `Imprimir fecha :` y `Miércoles 04/03/2026  Horarios de BETLEM del 02/03/2026 al 15/03/2026`

- **"Imprimir fecha :"** → **Ignorar.**
- **"Miércoles 04/03/2026"** → **Ignorar** (fecha de impresión).
- **"Horarios de BETLEM del 02/03/2026 al 15/03/2026"** → **Usar:**
  - **BETLEM** = nombre del establecimiento.
  - **02/03/2026** y **15/03/2026** = rango de fechas del PDF; días de los que tenemos horarios en ese PDF (del 2 al 15 de marzo de 2026).

### 2. y 3. Líneas 3–7
**Texto:** "Descargue la aplicación", "escaneando este código QR", "para tener siempre a mano su", "horario.", "Notas"

- **Acción:** **Ignorar** todo (QR y Notas).

### 4. Línea 8
**Texto:** `Lunes 2 marzo  0h  1h  2h  3h  …  22h  23h  Total  Firma`

Formato tipo **hoja de cálculo** (filas y columnas).

- **"Lunes 2 marzo"** = **fecha a la que pertenece este cuadrante horario** (ese día).
- **0h, 1h, …, 23h** = cada **hora del día** es una **columna**.
- **"Total"** = columna donde van las **horas que hizo cada persona ese día**.
- **"Firma"** → **Ignorar.**

### 5. Líneas 9–28 (lista de personas y totales)
Pares de líneas: nombre → apellidos + total (ej. `Leonel Pablo Daniel` → `VITALE 00h00`, `Jose` → `GARCIA DE LA VEGA 08h00`, …, `Sin asignar  -`).

- Son los **nombres del personal** y las **horas que trabajó cada uno ese día**.
- **Cuidado:** Pueden haber trabajado en **otro establecimiento**; esas horas **no nos interesan** (solo contamos trabajo en el establecimiento del PDF).

### 6. Línea 29
**Texto:** `Total empleados  0 0 0 0 …`

- **Empleados que estuvieron en cada hora.** Cada columna de hora se divide en **4** (15 minutos cada uno). Ej.: `0 0 1 1` = alguien trabajó la segunda media hora; `2 2 2 2` = 2 personas toda la hora.
- **Cuidado:** Incluye a quien trabajó en **otro establecimiento**. **Valorar ignorar esta zona** si da problemas.

### 7. Líneas 30–36
**Texto:** `Puestos:  Camarero…  Chef Oper…  …  Ausencias  Otros est…`, luego `Equipos:`, `sala, cocina`, `Empleados:`, lista de nombres.

- **"Puestos:"** = todos los puestos del cuadrante; **código de color**. "Otros est." = **otros establecimientos**. Si se puede leer bien, facilita todo.
- **Ignorar:** "Equipos:", "sala, cocina", "Empleados:" y la lista de nombres.

### 8. Líneas 37–53 (rótulos y horarios)
Pares: `Descanso semanal` → `(8h)`, `Camarero/a` → `16:00 - 00:00`, `Chef Operativo - MOLINA` → `14:00 - 22:30`, etc.

- Esos **horarios y rótulos pertenecen a una fila** que tiene el **nombre del empleado en la primera columna** (izquierda).
- **"Chef Operativo - MOLINA"** = el chef trabajó en **MOLINA** (otro establecimiento); en la leyenda es "Otros est.". Esas horas **no cuentan** para nuestro establecimiento.
- **Importante:** "Chef Operativo" (sin " - NOMBRE") **sí trabaja con nosotros** y hay que contarlo. Solo se ignora cuando el rol indica **otro establecimiento** (ej. "Chef Operativo - MOLINA", "Camarero/a - CENTRIC").

### 9. Línea 55
**Texto:** `-- 1 of 14 --` → **Ignorar.**

---

## Cabecera (zona a interpretar) – detalle

La primera página tiene una cabecera con tres zonas. **Solo se usa la central.**

### Óvalo IZQUIERDO
- **Texto típico:** `Imprimir fecha: [fecha]` (ej. "Imprimir fecha: Miércoles 04/03/2026")
- **Acción:** **IGNORAR por completo.** Es la fecha en que se imprimió el archivo; no forma parte de los horarios y no debe usarse para nada.

### Óvalo CENTRAL (único que se usa)
- **Texto típico:** `Horarios de [ESTABLECIMIENTO] del [DD/MM/YYYY] al [DD/MM/YYYY]`
- **Ejemplo:** `Horarios de BETLEM del 02/03/2026 al 15/03/2026`
- **Extraer:**
  - **Establecimiento:** nombre entre "Horarios de " y " del " (ej. BETLEM).
  - **Fecha inicio PDF:** primera fecha (ej. 02/03/2026) = primer día con horarios en el documento.
  - **Fecha fin PDF:** segunda fecha (ej. 15/03/2026) = último día con horarios en el documento.
- **Uso en el parser:** validar que los días parseados caigan en este rango; opcionalmente usar el nombre del establecimiento en metadatos o salida.

### Óvalo DERECHO
- **Contenido:** código QR y texto tipo "Descargue la aplicación escaneando este código QR para tener siempre a mano su horario".
- **Acción:** **IGNORAR por completo.** No aporta datos para el parsing.

---

## Resumen para implementación futura

| Zona     | Acción   | Dato a extraer (solo central)                          |
|----------|----------|--------------------------------------------------------|
| Izquierda| Ignorar  | —                                                      |
| Central  | Parsear  | Establecimiento, fecha inicio PDF, fecha fin PDF      |
| Derecha  | Ignorar  | —                                                      |

Patrón sugerido para el texto central:  
`Horarios de (\w+) del (\d{1,2}/\d{1,2}/\d{4}) al (\d{1,2}/\d{1,2}/\d{4})`  
(capturar grupo 1 = establecimiento, grupo 2 = inicio, grupo 3 = fin).

---

## Dónde implementar cuando se suba un PDF

- **Flujo actual:** el usuario sube el PDF → `ImportController.ImportCuadrantePdf` → `CuadrantePdfService.ParsePdfAsync` → se guarda el PDF en temporal → se invoca el parser Python **LucasCuadranteParser** → se obtiene JSON con días/turnos → la API persiste en BD.
- **Lugar de la cabecera:** el texto de la página 1 se extrae en **LucasCuadranteParser** (ingest: `extract_text_from_pdf` devuelve todo el texto; segment o un nuevo paso puede parsear la cabecera de las primeras líneas).
- **Implementación recomendada:**
  1. En el **parser Python** (p. ej. en `pipeline/ingest.py` o `pipeline/segment.py`): al procesar el texto de la primera página, buscar la línea que coincida con el patrón del óvalo central; extraer establecimiento, fecha inicio y fecha fin; devolverlos en la info/metadatos del pipeline (por ejemplo en el `info` que ya devuelve `segment_by_days` con `week_start`).
  2. Opcional en la **API (C#):** si `CuadrantePdfService` recibe o calcula estos metadatos del parser, usarlos para validar que los días creados/actualizados caigan dentro del rango o para mostrar el establecimiento en mensajes/UI.

---

## Pasos para implementar (checklist)

Al codificar el tratamiento de la **página 1** al subir un PDF, aplicar esta especificación:

**Cabecera (líneas 1-7):**
- [ ] Ignorar "Imprimir fecha" y la fecha de impresión (ej. Miércoles 04/03/2026).
- [ ] Extraer de la línea central: establecimiento (BETLEM) y rango de fechas del PDF (02/03/2026 al 15/03/2026). Ignorar líneas 3-7 (QR, Notas).

**Tabla del día (línea 8 en adelante):**
- [ ] Línea 8: interpretar "Lunes 2 marzo" como fecha del cuadrante; columnas 0h-23h = horas del día; "Total" = horas por persona; ignorar "Firma".
- [ ] Líneas 9-28: lista de personal y horas trabajadas ese día; excluir horas trabajadas en otro establecimiento.
- [ ] Línea 29 ("Total empleados"): valorar ignorarla si incluye a otros establecimientos y da problemas.
- [ ] Líneas 30-36: usar solo "Puestos:" (leyenda/código de color; "Otros est." = otros establecimientos). Ignorar "Equipos:", "sala, cocina", "Empleados:" y lista de nombres.
- [ ] Líneas 37-53: cada par rótulo → horario va asociado a la fila del empleado (primera columna). Rótulos tipo "X - MOLINA" = otro establecimiento → no sumar esas horas.
- [ ] Ignorar "-- 1 of 14 --" y similares.

**General:**
- [ ] Incluir establecimiento y rango de fechas en el resultado del pipeline (info o JSON).
- [ ] (Opcional) En la API: validar días dentro del rango o mostrar establecimiento.
- [ ] Probar con DayPlanning.pdf y otros PDFs con el mismo formato de página 1.
