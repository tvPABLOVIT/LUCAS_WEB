# Documentación: Lectura e importación de archivos Excel

**Fecha:** Febrero 2026  
**Alcance:** Cómo se leen los archivos Excel que se importan en ManagerOS (Lucas): formato genérico por columnas y formato de estimaciones (sN_AAAA). Incluye celdas, tipos de dato, reglas de conversión y flujo posterior.

---

## 1. Resumen

El sistema admite **dos tipos** de importación desde Excel:

| Tipo | Nombre archivo / detección | Origen en la UI | Método |
|------|----------------------------|-----------------|--------|
| **Estimaciones (sN_AAAA)** | Nombre `sN_AAAA.xlsx` (ej. s6_2026) o plantilla detectada por celdas | Configuración → "Importar archivo de estimaciones (Excel)" | `ImportFromEstimacionExcelAsync` |
| **Genérico por columnas** | Cualquier otro .xlsx; o mismo archivo si no coincide nombre ni plantilla | Dashboard → "Importar Excel" | `ImportFromExcelAsync` (flujo por filas/columnas) |

La librería usada para leer Excel es **ClosedXML** (`XLWorkbook`, `IXLWorksheet`, `IXLCell`). Siempre se usa la **primera hoja** del libro (`wb.Worksheet(1)`).

Tras importar con éxito (estimaciones o genérico), si está disponible `IInteligenciaService`, se lanza en segundo plano `RunFullBackgroundAnalysisAsync` (evaluación de predicciones, análisis semanal, recomendaciones).

---

## 2. Decisión de flujo al importar

### 2.1. Desde `ImportFromExcelAsync` (Dashboard → Importar Excel)

1. **Archivo no existe** → `ImportResult(0, 0, 0, "Archivo no encontrado.")`.
2. **Nombre del archivo** (sin extensión) coincide con el patrón **sN_AAAA**:
   - Regex: `@"s\s*(\d{1,2})\s*[_-]?\s*(\d{4})"` (ej. `s6_2026`, `s 6-2026`, `s6 2026`).
   - Si coincide → se delega en **`ImportFromEstimacionExcelAsync`** y no se usa el formato por columnas.
3. Se abre el libro y se toma la primera hoja.
4. **Detección de plantilla de estimaciones** (`LooksLikeEstimacionTemplate(ws)`):
   - Se cuentan las celdas en **fila 21, columnas C a I** (columnas 3 a 9) que se consideran "fecha" (`IsDateCell`).
   - Si **al menos 5** de esas 7 celdas son fechas válidas → se considera plantilla de estimaciones y se delega en **`ImportFromEstimacionExcelAsync`** (con el mismo archivo; el nombre ya no se valida en ese método para semana/año, ver apartado 3).
5. Si no es estimaciones → se usa el **formato genérico por columnas** (apartado 4).

**Nota:** Si se delega por plantilla (paso 4) pero el nombre del archivo **no** es sN_AAAA, al entrar en `ImportFromEstimacionExcelAsync` se validará de nuevo el nombre y se devolverá *"Nombre de archivo no válido. Use formato sN_AAAA (ej. s6_2026)."* Por tanto, para importar como estimaciones desde el Dashboard el archivo debe llamarse sN_AAAA; si no, aunque la hoja tenga la estructura de estimaciones, se exigirá el nombre correcto.

### 2.2. Desde Configuración → "Importar archivo de estimaciones (Excel)"

Se llama directamente a **`ImportFromEstimacionExcelAsync(filePath)`**. El nombre del archivo **debe** cumplir el patrón sN_AAAA; si no, se devuelve error sin abrir el contenido.

---

## 3. Formato de estimaciones (sN_AAAA.xlsx)

### 3.1. Nombre del archivo

- **Patrón:** `s` + opcional espacio + **número de semana (1–2 dígitos)** + opcional `_` o `-` + **año (4 dígitos)**.
- **Ejemplos válidos:** `s6_2026`, `s 6_2026`, `s6-2026`, `s12_2025`.
- **Regex:** `@"s\s*(\d{1,2})\s*[_-]?\s*(\d{4})"` (case insensitive).
- **Restricciones:** `weekNum` debe estar entre 1 y 53. Si no, se devuelve `ImportResult(0, 0, 0, "Número de semana no válido...")`.

El número de semana y el año se usan para:
- Resolver el año efectivo en fechas ambiguas (S1/S2 y diciembre del año anterior, ver más abajo).
- No se usan para filtrar qué filas leer: lo que manda es el **contenido de las celdas** (fila 21 y celdas de facturación/horas).

### 3.2. Estructura de celdas (primera hoja)

Todas las referencias son **fila, columna** en notación 1-based (como en el código: `ws.Cell(fila, col)`).

| Rango | Fila | Columnas | Contenido | Uso |
|-------|------|----------|-----------|-----|
| **Fechas de referencia (semana del Excel)** | 21 | C, D, E, F, G, H, I (3–9) | Una fecha por día (L–D) de la **semana que representa el archivo** | Se lee cada celda con `TryGetRefDateFromCell`; la **fecha de datos** es `refDate.AddDays(-14)` (2 semanas antes). |
| **Facturación Mediodía** | 22 | 3–9 | Importe en € por día | `GetDecimal(ws.Cell(22, col))` → `revMed`. |
| **Facturación Tarde** | 26 | 3–9 | Importe en € por día | `GetDecimal(ws.Cell(26, col))` → `revTar`. |
| **Facturación Noche** | 30 | 3–9 | Importe en € por día | `GetDecimal(ws.Cell(30, col))` → `revNoc`. |
| **Horas reales de la semana** | 39 | J (10) | Total de horas reales de la semana | `GetDecimal(ws.Cell(39, 10))` → se reparten por día según peso de facturación. |

**No se leen** (comentado en código): recursos/personal en filas 23, 27, 31. Solo se importan facturación y horas.

- **Columnas:** se iteran `col = 3` hasta `col = 9` (7 columnas). Si en una columna la fecha de la fila 21 no se puede leer, se salta esa columna y se incrementa el contador de errores.
- **Fecha “objetivo” (targetDate):** para cada columna, `refDate = TryGetRefDateFromCell(ws.Cell(21, col), ...)` y luego `targetDate = refDate.AddDays(-14)`. Es decir, el Excel representa una semana y el programa importa los datos como si fueran de **dos semanas antes**.
- **Excepción año:** si `weekNum <= 2` y `year == 2026` y `targetDate.Year == 2026`, se fuerza `targetDate = new DateTime(2025, 12, Min(targetDate.Day, 31))` para que los primeros días de 2026 no se asignen a enero 2026 sino a diciembre 2025 cuando corresponda.

### 3.3. Lectura de fechas (fila 21): `TryGetRefDateFromCell`

Se intenta, en este orden:

1. **Valor como `DateTime`**  
   `cell.GetValue<DateTime>()`. Si no es `default` y el año está entre 2000 y 2100, se usa mes y día y se pasa a `ResolveRefDate(month, day, fileYear, weekNum)`.

2. **Número serial de Excel (OADate)**  
   `cell.GetValue<double>()`. Si el valor está entre 36526 y 50000 (~año 2000–2036), se convierte con `DateTime.FromOADate(serial)` y se usa mes y día en `ResolveRefDate`.

3. **Texto**  
   `cell.GetString()`:
   - Parse con `CultureInfo("es-ES")` o `InvariantCulture`.
   - Si falla, se eliminan palabras de día de la semana (lunes, martes, …) y se vuelve a parsear.
   - Si falla, se busca un patrón `dd/mm` o `dd-mm` o `dd.mm` con regex `(\d{1,2})\s*[/\.-]\s*(\d{1,2})`: el primer grupo es día, el segundo mes; se valida 1–31 y 1–12 y se llama `ResolveRefDate(month, day, fileYear, weekNum)`.

Si nada funciona, se añade un error a `errList` ("fecha inválida en fila 21") y se devuelve `default` (se salta esa columna).

**ResolveRefDate(month, day, fileYear, weekNum):**  
Para semanas 1 y 2 del año, si el mes es 12, se usa año efectivo `fileYear - 1` (ej. S1 2026 con día 29/12 → 29/12/2025). En el resto de casos se usa `fileYear`. Se devuelve `new DateTime(effectiveYear, month, day)`.

### 3.4. Lectura de números: `GetDecimal` y `GetInt`

**GetDecimal(cell):**

1. `cell.GetValue<double>()`: si no es NaN, se convierte a `decimal`.
2. Si no, `cell.GetString()`:
   - Primero `decimal.TryParse(..., InvariantCulture)`.
   - Luego `CultureInfo("es-ES")`.
   - Por último `CurrentCulture`.
3. Si todo falla se devuelve `0`.

**GetInt(cell):** mismo orden pero con `int` y redondeo del double; si no hay número, `0`.

### 3.5. Detección de plantilla de estimaciones: `LooksLikeEstimacionTemplate`

- Para columnas 3–9 (C–I), se comprueba si la celda (fila 21, col) es “fecha” con **IsDateCell**.
- **IsDateCell(cell):**
  - `GetValue<DateTime>()` con año 2000–2100, o
  - `GetValue<double>()` entre 36526 y 50000, o
  - `GetString()` parseable como fecha con es-ES o InvariantCulture.
- Si **dateCount >= 5** → se considera plantilla de estimaciones.

### 3.6. Horas reales de la semana (J39) y reparto por día

- Se lee **una sola celda:** `ws.Cell(39, 10)` → J39, con `GetDecimal` → `realWeeklyHours`.
- Por cada día importado se tiene `revenuePerDay[i] = revMed + revTar + revNoc`.
- `totalRevenueWeek = Sum(revenuePerDay)`.
- Si `totalRevenueWeek > 0` y `realWeeklyHours > 0`:
  - `realHoursPerDay[i] = revenuePerDay[i] / totalRevenueWeek * realWeeklyHours`.
- Si no:
  - `realHoursPerDay[i] = realWeeklyHours / dayData.Count` (reparto a partes iguales).

Así cada día obtiene un total de horas en proporción a su facturación.

### 3.7. Horas por turno (Mediodía, Tarde, Noche) dentro de cada día

Para cada día:

- `total = revMed + revTar + revNoc`.
- `realHoursDay = realHoursPerDay[i]`.
- Si `total > 0` y `realHoursDay > 0`:
  - `hoursMed = revMed / total * realHoursDay`
  - `hoursTar = revTar / total * realHoursDay`
  - `hoursNoc = revNoc / total * realHoursDay`
- Si no, las tres quedan en 0.

### 3.8. Persistencia por día (estimaciones)

Para cada `(targetDate, revMed, revTar, revNoc)`:

- Se busca `ExecutionDay` por `Date == targetDate` (con `ShiftFeedbacks` incluidos).
- **Si no existe:** se crea `ExecutionDay` con `TotalRevenue = total`, `TotalHoursWorked = realHoursDay`, `StaffTotal = 0`, y tres `ShiftFeedback` (Mediodia, Tarde, Noche) con `Revenue` y `HoursWorked` según lo anterior; `StaffFloor` y `StaffKitchen` = 0.
- **Si existe:** se actualizan `TotalRevenue`, `TotalHoursWorked`, `StaffTotal = 0` y, por cada turno, `Revenue` y `HoursWorked` con `UpdateShiftRevenueAndHours` (si el turno no existe se añade).

Los días creados o actualizados se agregan a una lista para **sincronizar con Google Sheet** después de `SaveChangesAsync`: si hay URL de Google Sheets y credenciales configuradas, se llama a `GoogleSheetSyncService.SyncAsync` por cada uno de esos días.

### 3.9. Mensajes y resultado (estimaciones)

- Si no se pudo leer ninguna fecha en C21:I21: mensaje indicando que no se leyeron fechas y que la fila 21 debe contener fechas reales y el archivo ser sN_AAAA.
- Si se leyeron menos de 7 fechas: se añade a errores pero se sigue con las que haya.
- Mensaje final: `"Estimaciones: X importados, Y actualizados, Z errores. Google Sheet actualizado."` y, si hay errores, se añaden hasta 5 mensajes de `errList`.
- **ImportResult(Imported, Updated, Errors, Message).**

---

## 4. Formato genérico por columnas (ImportFromExcelAsync)

Se usa cuando el archivo **no** tiene nombre sN_AAAA y **no** se detecta plantilla de estimaciones en la primera hoja.

### 4.1. Estructura esperada

- **Primera fila:** se considera cabecera y **se omite** (`Rows().Skip(1)`).
- **Filas siguientes:** se usa `ws.RangeUsed()?.Rows()`; si no hay rango usado, no se procesa ninguna fila.

Columnas (índice 1-based):

| Columna | Contenido | Lectura | Uso |
|---------|-----------|---------|-----|
| 1 | Fecha | `row.Cell(1).GetString().Trim()` → `DateTime.TryParse` | `ExecutionDay.Date` |
| 2 | Facturación (€) | `row.Cell(2).GetString()` → `decimal.TryParse(..., InvariantCulture)`; si falla, 0 | `TotalRevenue` |
| 3 | Horas reales | `row.Cell(3).GetString()` → mismo parse; si falla, 0 | `TotalHoursWorked` |

No se importan más columnas (por ejemplo personal/recursos). No se crean turnos (Mediodía/Tarde/Noche); el día queda solo con totales.

### 4.2. Reglas por fila

- Si la celda de fecha está vacía (tras trim) → se salta la fila.
- Si la fecha no se puede parsear → se incrementa `errors` y se añade a `errList` ("Fila N: fecha inválida '...'") y se continúa.
- Si la fila es válida:
  - Si ya existe `ExecutionDay` con esa fecha → se actualizan `TotalRevenue`, `TotalHoursWorked` y `UpdatedAt`.
  - Si no existe → se inserta un nuevo `ExecutionDay` con `Date`, `TotalRevenue`, `TotalHoursWorked`, `StaffTotal = 0`, y timestamps.

No se sincroniza con Google Sheet en este flujo (solo en el de estimaciones). Sí se dispara `RunFullBackgroundAnalysisAsync` en segundo plano si hubo al menos un importado o actualizado.

### 4.3. Resultado

- Mensaje: `"Importados: X, Actualizados: Y, Errores: Z"` y, si hay errores, hasta 5 líneas de `errList` (o "Primeros 5 errores: ...").
- **ImportResult(Imported, Updated, Errors, Message).**

---

## 5. Resumen de celdas (referencia rápida)

### Formato estimaciones (sN_AAAA)

| Celda(s) | Contenido | Notas |
|----------|-----------|--------|
| C21:I21  | Fechas de la semana del Excel (L–D) | Fecha de datos = esta fecha − 14 días |
| C22:I22  | Facturación turno Mediodía (€) por día | 7 valores |
| C26:I26  | Facturación turno Tarde (€) por día | 7 valores |
| C30:I30  | Facturación turno Noche (€) por día | 7 valores |
| J39      | Horas reales totales de la semana | Repartidas por día por peso de facturación |

### Formato genérico

| Columna | Fila   | Contenido        |
|---------|--------|------------------|
| A (1)   | 2..N   | Fecha            |
| B (2)   | 2..N   | Facturación (€)  |
| C (3)   | 2..N   | Horas reales     |

(Fila 1 = cabeceras, no se importa.)

---

## 6. Dependencias y flujo posterior

- **ClosedXML:** apertura del .xlsx y acceso por fila/columna.
- **ApplicationDbContext:** lectura y escritura de `ExecutionDays` y `ShiftFeedbacks`.
- **ConfiguracionService:** solo en estimaciones, para URL de Google Sheets y ruta de credenciales.
- **GoogleSheetSyncService:** solo en estimaciones, para enviar los días tocados al sheet.
- **IInteligenciaService:** tras cualquier importación con éxito (imported + updated > 0), se ejecuta en segundo plano `RunFullBackgroundAnalysisAsync`.

---

## 7. Código de referencia

- **Servicio:** `src/ManagerOS.Windows/Services/RegistroService.cs`
  - `ImportFromExcelAsync` (líneas ~235–319)
  - `ImportFromEstimacionExcelAsync` (líneas ~323–491)
  - `LooksLikeEstimacionTemplate`, `IsDateCell`, `TryGetRefDateFromCell`, `ResolveRefDate`, `GetDecimal`, `GetInt`
  - `AddShift`, `UpdateShiftRevenueAndHours`
- **Interfaz:** `src/ManagerOS.Windows/Services/IRegistroService.cs`  
  - `ImportResult(Imported, Updated, Errors, Message)`
- **UI:** Configuración → `ImportarEstimacionExcelCommand` → `ImportFromEstimacionExcelAsync`; Dashboard → `ImportarExcelCommand` → `ImportFromExcelAsync`.

---

## 8. Dónde poner los datos (guía práctica)

### 8.1. Formato estimaciones (sN_AAAA.xlsx): mapa de celdas

**Hoja:** siempre la **primera hoja** del libro (Hoja1).

La lectura recorre **7 columnas** (C a I). Cada **columna** representa un día de la semana (Lunes a Domingo) de la **semana que representa el archivo**. Los datos se importan como si fueran de **2 semanas antes** (targetDate = fecha de la fila 21 − 14 días).

Mapa exacto (fila, columna en Excel):

```
        Col C    Col D    Col E    Col F    Col G    Col H    Col I
        (3)      (4)      (5)      (6)      (7)      (8)      (9)
        Lun      Mar      Mié      Jue      Vie      Sáb      Dom

Fila 21  Fecha   Fecha   Fecha   Fecha   Fecha   Fecha   Fecha   ← Fechas de la semana del Excel (ej. 10/02, 11/02, … 16/02)
Fila 22  € Med   € Med   € Med   € Med   € Med   € Med   € Med   ← Facturación turno MEDIODÍA (€) por día
...
Fila 26  € Tar   € Tar   € Tar   € Tar   € Tar   € Tar   € Tar   ← Facturación turno TARDE (€) por día
...
Fila 30  € Noc   € Noc   € Noc   € Noc   € Noc   € Noc   € Noc   ← Facturación turno NOCHE (€) por día
...
Fila 39  [no se usa por columna; solo J39]
```

**Celda J39 (fila 39, columna 10):** una sola celda con el **total de horas reales de la semana**. Esa cantidad se reparte entre los 7 días en proporción a la facturación de cada día (revMed+revTar+revNoc).

**Resumen: dónde poner cada dato**

| Dato | Dónde ponerlo | Ejemplo |
|------|----------------|--------|
| Fecha del Lunes de la semana del Excel | **C21** | 10/02/2026 o 10-feb-2026 |
| Fecha del Martes | **D21** | 11/02/2026 |
| … hasta Domingo | **E21, F21, G21, H21, I21** | 12/02 … 16/02 |
| Facturación Mediodía del Lunes | **C22** | 1200 |
| Facturación Mediodía del Martes | **D22** | 1150 |
| … (7 días) | **C22:I22** | Números en € |
| Facturación Tarde del Lunes | **C26** | 800 |
| … (7 días) | **C26:I26** | Números en € |
| Facturación Noche del Lunes | **C30** | 600 |
| … (7 días) | **C30:I30** | Números en € |
| Horas reales totales de la semana | **J39** | 240 |

**No se leen:** filas 23, 27, 31 (recursos/personal). Todo lo que esté fuera de las filas/columnas indicadas se ignora.

**Fechas en fila 21:** pueden ser tipo fecha de Excel, número serial (OADate) o texto. Formatos de texto aceptados: `dd/MM/yyyy`, `dd-MM-yyyy`, `dd.MM.yyyy`, o solo `dd/mm` (se usa el año del nombre del archivo). También se intenta quitar palabras como "Lunes", "Martes", etc. y volver a parsear. Si una celda de fecha no se puede leer, **esa columna entera se salta** (no se importa ese día).

**Números (facturación y horas):** pueden ser número en la celda o texto que se pueda parsear como número (InvariantCulture, es-ES o cultura actual). Si no se puede leer, se usa **0**.

### 8.2. Formato genérico: mapa de columnas

**Hoja:** primera hoja del libro.

- **Fila 1:** cabeceras. **No se importa** (se omite).
- **Desde fila 2:** una fila por día. Cada fila tiene exactamente 3 columnas usadas:

| Columna Excel | Letra | Dato | Dónde ponerlo | Ejemplo |
|---------------|-------|------|----------------|--------|
| 1 | A | Fecha del día | Primera columna de cada fila de datos | 01/02/2026, 2026-02-01, 1 feb 2026 |
| 2 | B | Facturación total del día (€) | Segunda columna | 3500 |
| 3 | C | Horas reales trabajadas ese día | Tercera columna | 42 |

**Ejemplo de contenido:**

```
Fila 1:  Fecha      | Facturación | Horas
Fila 2:  01/02/2026 | 3500        | 42
Fila 3:  02/02/2026 | 3200        | 40
...
```

Si la celda de fecha está vacía, la fila se salta. Si la fecha no se puede parsear, se cuenta como error y se sigue. Si facturación u horas no se parsean, se usa 0. **No se crean turnos** (Mediodía/Tarde/Noche); el día queda solo con TotalRevenue y TotalHoursWorked.

---

## 9. Cómo se decide qué formato usar

1. **Desde Configuración → "Importar archivo de estimaciones (Excel)"**  
   Siempre se usa **solo** el formato estimaciones. El **nombre del archivo debe** ser tipo sN_AAAA (ej. `s6_2026.xlsx`). Si no, error sin abrir el archivo.

2. **Desde Dashboard → "Importar Excel"**  
   - Si el **nombre** del archivo (sin extensión) coincide con `s` + número de semana + año (ej. `s6_2026`) → se usa formato estimaciones (y se exige nombre sN_AAAA).  
   - Si no, se **abre** el Excel y se mira la **primera hoja**.  
   - En la **fila 21**, columnas **C a I** (7 celdas): si **al menos 5** de esas celdas se consideran "fecha" (DateTime, serial Excel o texto parseable como fecha) → se considera plantilla de estimaciones y se intenta importar como estimaciones (pero si el nombre no es sN_AAAA, al entrar en ImportFromEstimacionExcelAsync fallará por nombre).  
   - Si no se detecta plantilla de estimaciones → se usa **formato genérico** (fila 1 = cabecera, filas siguientes = A=Fecha, B=Facturación, C=Horas).

Por tanto, para no tener sorpresas:
- Para **estimaciones:** nombre del archivo **sN_AAAA.xlsx** y poner fechas en C21:I21, facturación en 22/26/30 y horas en J39.
- Para **genérico:** no usar nombre sN_AAAA y poner cabecera en fila 1 y datos en columnas A, B, C desde fila 2.

---

## 10. Comportamiento con celdas vacías o incorrectas

### 10.1. Formato estimaciones

| Situación | Comportamiento |
|-----------|----------------|
| Celda C21 (o D21…) vacía o no fecha | Esa **columna** se salta; no se importa ese día. Se incrementa el contador de errores. |
| Celda C22 (facturación Mediodía) vacía o no número | GetDecimal devuelve **0**. El día se importa con 0 € en ese turno. |
| J39 vacía o no número | realWeeklyHours = **0**. Las horas por día se reparten a partes iguales (0 / 7 = 0) o según revenuePerDay si hay facturación. |
| Menos de 7 fechas válidas en C21:I21 | Se importan solo los días cuyas columnas tengan fecha válida. Se añade un error a errList ("Solo se leyeron X fechas..."). |
| Ninguna fecha válida en C21:I21 | No se importa ningún día; mensaje: "No se pudieron leer fechas en C21:I21...". |

### 10.2. Formato genérico

| Situación | Comportamiento |
|-----------|----------------|
| Fila con celda A vacía (tras trim) | Esa **fila se salta** (no se importa). |
| Fecha (A) no parseable | Error; se añade a errList "Fila N: fecha inválida '...'"; se continúa con la siguiente fila. |
| Facturación (B) u horas (C) vacías o no número | Se usa **0** para ese valor; el día se crea o actualiza con 0 en ese campo. |

---

## 11. Ejemplos de nombre de archivo y de fechas

**Nombres válidos para estimaciones:**  
`s6_2026.xlsx`, `s 6_2026.xlsx`, `s6-2026.xlsx`, `s12_2025.xlsx`, `S6_2026.xlsx` (case insensitive).  
Número de semana debe estar entre 1 y 53.

**Fechas en fila 21 (estimaciones):**  
- Excel como fecha: formato corto 10/02/2026.  
- Texto: `10/02/2026`, `10-02-2026`, `10.02.2026`, `29/12` (año del nombre del archivo).  
- Con día de la semana: `Lunes 10/02/2026` → se elimina "Lunes" y se parsea 10/02/2026.

**Fechas en formato genérico (columna A):**  
Se usa `DateTime.TryParse` con la cadena de la celda; suelen funcionar formatos como `01/02/2026`, `2026-02-01`, `1 feb 2026` según la cultura.

---

## 12. Flujo completo de lectura (paso a paso)

### Formato estimaciones

1. Comprobar que el archivo existe y que el nombre cumple sN_AAAA y weekNum 1–53.
2. Abrir el libro con ClosedXML y tomar `Worksheet(1)`.
3. Para **col = 3** hasta **9** (C a I):  
   a. Leer **fila 21, col** → refDate. Si no es fecha válida, saltar columna.  
   b. targetDate = refDate.AddDays(-14) (y excepción S1/S2 2026 → diciembre 2025).  
   c. Leer **fila 22, col** → revMed, **fila 26, col** → revTar, **fila 30, col** → revNoc (GetDecimal).  
   d. Añadir (targetDate, revMed, revTar, revNoc) a dayData.
4. Leer **J39** (fila 39, col 10) → realWeeklyHours.
5. Repartir realWeeklyHours por día en proporción a (revMed+revTar+revNoc) de cada día.
6. Para cada día en dayData: repartir las horas del día entre Mediodía/Tarde/Noche en proporción a revMed/revTar/revNoc.
7. Crear o actualizar ExecutionDay y tres ShiftFeedback por día; guardar en BD.
8. Si hay Google Sheet configurado, sincronizar cada día tocado.

### Formato genérico

1. Abrir el libro y tomar la primera hoja.
2. Obtener filas usadas: `RangeUsed()?.Rows()` y **omitir la primera** (cabecera).
3. Para cada fila: leer **Cell(1)** = fecha, **Cell(2)** = facturación, **Cell(3)** = horas.
4. Si fecha vacía → saltar fila. Si fecha no parseable → error y continuar.
5. Crear o actualizar ExecutionDay (solo totales; sin turnos); guardar en BD.
6. No se sincroniza con Google Sheet; sí se lanza RunFullBackgroundAnalysisAsync si hubo importados/actualizados.

---

Este documento describe con detalle cómo se leen los archivos Excel en ambos formatos, **dónde poner cada dato**, qué ocurre con celdas vacías o incorrectas, y qué se hace con los datos importados.
