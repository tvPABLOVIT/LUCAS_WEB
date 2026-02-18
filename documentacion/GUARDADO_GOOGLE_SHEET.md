# Documentación: Cómo se guarda y actualiza el Google Sheet

**Fecha:** Febrero 2026  
**Alcance:** Cuándo se actualiza el Google Sheet al guardar desde Lucas (pestaña Preguntas o Registro de ejecución), qué se escribe, en qué celdas, cómo se obtiene la hoja del mes, credenciales y flujo completo.

---

## 1. Resumen

Cada vez que se **guarda un día** en Lucas (ya sea desde la **pestaña Preguntas** —feedback en la tablet— o desde **Registro de ejecución** en la app Windows), el sistema puede **sincronizar ese día con un Google Sheet** configurado. La sincronización es **opcional** (si no hay URL ni credenciales no se hace nada) y se ejecuta en **segundo plano** para no bloquear la respuesta al usuario.

| Origen del guardado | Dónde se dispara la sync | Cómo se invoca |
|---------------------|---------------------------|-----------------|
| **Registro de ejecución (app Windows)** | Tras `SaveDayAsync` en `RegistroService` | `GoogleSheetSyncService.SyncAsync(sheetId, credentialsPath, savedDay)` en `Task.Run` |
| **Preguntas / feedback (tablet)** | Tras `SaveChangesAsync` en `ExecutionController` (POST Create o PATCH Update) | `SyncToGoogleSheet(day)` → lee settings.json y llama `GoogleSheetSyncService.SyncAsync` en `Task.Run` |
| **Exportar todo (Configuración)** | Botón "Exportar todo al Google Sheet" | `RegistroService.ExportAllDaysToGoogleSheetAsync()` → `SyncAsync` por cada día guardado (síncrono, con errores visibles) |
| **Importar estimaciones (Excel)** | Tras importar días desde sN_AAAA.xlsx | `SyncAsync` por cada día creado/actualizado (síncrono, `throwOnError: true`) |

En todos los casos se usa el mismo servicio estático **`GoogleSheetSyncService.SyncAsync`**, que escribe **una fila por día** en la **hoja del mes** correspondiente (ej. "Febrero 2026").

---

## 2. Cuándo se actualiza el Google Sheet

### 2.1. Guardado desde Registro de ejecución (app Windows)

1. El usuario guarda el día en la pestaña **Registro de ejecución** (facturación, horas, personal, opcionalmente feedback por turno).
2. **RegistroService.SaveDayAsync** persiste en SQLite y llama a **GenerateDailyAnalysisAsync**.
3. Si existe **ConfiguracionService**:
   - Se obtiene el día recién guardado con **GetDayAsync(day.Date)** (incluyendo `ShiftFeedbacks`).
   - Se extrae **sheetId** de `GetGoogleSheetsUrl()` con **GoogleSheetSyncService.ExtractSheetIdFromUrl**.
   - Se obtiene **credentialsPath** con **GetGoogleCredentialsPath()** (si está vacío, en el servicio de sync se usará `google-credentials.json` en la carpeta de datos).
   - Si **sheetId** y **credentialsPath** están definidos, se lanza en segundo plano (`Task.Run`) **GoogleSheetSyncService.SyncAsync(sheetId, credentialsPath, savedDay)**.
4. Si `SyncAsync` lanza excepción, se registra en log y **no** afecta al guardado en BD.

### 2.2. Guardado desde Preguntas / feedback (tablet)

1. El usuario rellena las preguntas (V/R/M/D) por turno en la página de feedback y pulsa "Guardar Lucas".
2. El frontend llama a **POST /api/execution** (crear día) o **PATCH /api/execution/{date}** (actualizar día).
3. **ExecutionController** guarda o actualiza el día en SQLite y hace **SaveChangesAsync**.
4. Inmediatamente después se llama a **SyncToGoogleSheet(day)** (método estático del controlador).
5. **SyncToGoogleSheet**:
   - Lee **settings.json** desde `%LocalAppData%\ManagerOS\settings.json`.
   - Extrae **GoogleSheetsUrl** y **GoogleCredentialsPath**.
   - Si la ruta de credenciales está vacía, usa `%LocalAppData%\ManagerOS\google-credentials.json`.
   - Si **GoogleCredentialsPath** es relativa, la resuelve respecto a la carpeta ManagerOS.
   - Si **sheetId** o el archivo de credenciales no existen, **no hace nada** (return silencioso).
   - Si todo está bien, lanza en segundo plano (`Task.Run`) **GoogleSheetSyncService.SyncAsync(sheetId, credentialsPath, day)**.
6. Si hay excepción en la preparación o en SyncAsync, se registra en log; la respuesta HTTP ya se ha enviado (201 Created o 200 OK).

### 2.3. Exportar todo y importar estimaciones

- **Exportar todo:** el usuario pulsa "Exportar todo al Google Sheet" en Configuración. Se llama a **RegistroService.ExportAllDaysToGoogleSheetAsync()**, que recorre todos los días guardados y llama a **SyncAsync(sheetId, credentialsPath, day, throwOnError: true)** por cada uno. Es **síncrono** y si falla un día se devuelve error al usuario.
- **Importar estimaciones:** tras importar desde Excel sN_AAAA.xlsx, los días creados o actualizados se sincronizan con **SyncAsync(..., throwOnError: true)** por cada día; también es síncrono dentro del flujo de importación.

---

## 3. Configuración necesaria

| Dato | Dónde se guarda | Uso |
|------|------------------|-----|
| **GoogleSheetsUrl** | settings.json (app Windows y Backend leen la misma ruta si comparten máquina) | URL completa del spreadsheet (ej. https://docs.google.com/spreadsheets/d/ID/...). Se extrae el **ID** con `ExtractSheetIdFromUrl`. |
| **GoogleCredentialsPath** | settings.json | Ruta al JSON de la **cuenta de servicio** de Google. Si está vacía, se usa `google-credentials.json` en la carpeta de datos (`%LocalAppData%\ManagerOS\`). Puede ser solo el nombre del archivo (si está en esa carpeta) o ruta absoluta. |

- **Carpeta de datos:** `Environment.GetFolderPath(LocalApplicationData)\ManagerOS` (en Windows típicamente `%LocalAppData%\ManagerOS`).
- El **Backend** (ExecutionController) lee **settings.json** desde esa misma carpeta para SyncToGoogleSheet; por tanto, si la tablet y la app Windows usan el mismo Backend en el mismo PC, comparten la misma URL y credenciales.

---

## 4. Cómo se obtiene la hoja del mes

- Para cada día a sincronizar se calcula el **nombre de la hoja del mes** con la fecha del día:
  - Formato: **"Mes Año"** en español, primera letra en mayúscula (ej. "Enero 2026", "Febrero 2026", "Diciembre 2025").
  - Se usa `CultureInfo("es-ES")` y `DateTimeFormat.GetMonthName(date.Month)`; el año es `date.Year`.
- Si la hoja con ese nombre **ya existe** en el spreadsheet, se usa esa.
- Si **no existe**, se **crea** duplicando una hoja fuente:
  - Se busca una hoja llamada **"Plantilla"** (comparación sin distinguir mayúsculas).
  - Si no existe "Plantilla", se usa la **primera hoja** del documento.
  - Se ejecuta **DuplicateSheet** (Google Sheets API): la hoja duplicada se inserta en la posición 0 y se renombra al nombre del mes (ej. "Febrero 2026").
  - Tras duplicar, se **limpian** los rangos de datos para no arrastrar datos de la plantilla:
    - `'{monthSheetName}'!A2:I32` (filas 2 a 32)
    - `'{monthSheetName}'!A34:I1000` (filas 34 a 1000)
  - La **fila 1** se mantiene como cabecera y la **fila 33** se deja sin tocar (reservada para fórmulas/resumen en la plantilla).

---

## 5. Qué se escribe en el Google Sheet

### 5.1. Columnas (9 columnas: A a I)

El servicio escribe **exactamente 9 columnas** por fila. **No** se exportan personal de sala ni cocina; solo fecha, día de la semana, observaciones por turno, facturación por turno y total.

| Columna | Contenido | Origen |
|---------|-----------|--------|
| **A** | Fecha del día | `day.Date.ToString("yyyy-MM-dd", InvariantCulture)` |
| **B** | Día de la semana (nombre en español) | "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" según `day.Date.DayOfWeek` |
| **C** | Observaciones Mediodía | Texto resumen del turno Mediodía a partir de FeedbackQ1–Q4 (TurnoResumenBuilder.BuildResumenFromFeedback) |
| **D** | Observaciones Tarde | Igual para turno Tarde |
| **E** | Observaciones Noche | Igual para turno Noche |
| **F** | Facturación Mediodía (€) | `mediodia?.Revenue ?? 0` |
| **G** | Facturación Tarde (€) | `tarde?.Revenue ?? 0` |
| **H** | Facturación Noche (€) | `noche?.Revenue ?? 0` |
| **I** | Facturación total (€) | `day.TotalRevenue` |

Los turnos se identifican por **ShiftName** con comparación sin distinguir mayúsculas: "Mediodia", "Tarde", "Noche". Si falta un turno, su observación queda vacía y su facturación en 0.

### 5.2. Contenido de "Observaciones" (columnas C, D, E)

Para cada turno se llama a **TurnoResumenBuilder.BuildResumenFromFeedback(FeedbackQ1, FeedbackQ2, FeedbackQ3, FeedbackQ4)**:

- **Q1–Q4** son las respuestas de las 4 preguntas (Volumen, Ritmo, Margen, Dificultad); en BD se guardan como texto (ej. "Normal", "Alto").
- Se convierten a índices 1–5 (V, R, M, D). Si falta algún eje o está fuera de rango, se devuelve **cadena vacía** (no se escribe "Completa..." en el sheet).
- Se construye un párrafo tipo: *"Primera frase (volumen), segunda (ritmo), tercera (margen) y cuarta (dificultad). Cierre según tipo de turno."*
- Es el **mismo texto** que se muestra como "Resumen del turno" en la app; en el sheet aparece en la columna de observaciones de ese turno.

### 5.3. Fila en la que se escribe

- La fila se calcula como **día del mes + 1**:
  - Día 1 → fila **2** (fila 1 = cabecera).
  - Día 15 → fila **16**.
  - Día 31 → fila **32**.
- Rango escrito: **`'{monthSheetName}'!A{targetRow}:I{targetRow}`** (una sola fila, columnas A a I).
- **ValueInputOption:** `USER_ENTERED` (para que Google Sheets interprete números y fechas como en entrada de usuario).
- Si esa fila ya tenía datos, se **sobrescriben**. No se busca por fecha en la columna A para decidir la fila; la posición es fija según el día del mes, lo cual es coherente con tener **una hoja por mes** (cada fila 2–32 corresponde al día 1–31 de ese mes).

---

## 6. Flujo técnico de SyncAsync (GoogleSheetSyncService)

1. Validar **sheetId** y **credentialsPath** (y que el archivo de credenciales exista si `throwOnError`).
2. Validar que **day** tenga **ShiftFeedbacks** (no nulo).
3. Cargar credenciales desde el archivo JSON con **GoogleCredential.FromStream(...).CreateScoped(SheetsService.Scope.Spreadsheets)**.
4. Crear **SheetsService** con `ApplicationName = "Lucas"`.
5. Calcular **monthSheetName** = "Mes Año" (ej. "Febrero 2026").
6. **EnsureMonthSheetAsync:** si la hoja del mes no existe, duplicar Plantilla (o primera hoja), renombrar y limpiar rangos A2:I32 y A34:I1000.
7. Obtener los tres turnos (Mediodia, Tarde, Noche) de `day.ShiftFeedbacks`.
8. Construir la fila de 9 valores: Fecha, Día, Obs Mediodía, Obs Tarde, Obs Noche, Fact Mediodía, Fact Tarde, Fact Noche, Total.
9. **Update** del rango `'{monthSheetName}'!A{targetRow}:I{targetRow}` con `ValueRange` de una fila y `ValueInputOption = USER_ENTERED`.
10. Si ocurre excepción y no es `throwOnError`, se captura y no se relanza (sync opcional).

---

## 7. Utilidades del servicio (GoogleSheetSyncService)

| Método | Descripción |
|--------|-------------|
| **ExtractSheetIdFromUrl(url)** | Extrae el ID del spreadsheet desde una URL tipo `https://docs.google.com/spreadsheets/d/ID/...` (regex `/d/([a-zA-Z0-9_-]+)`). |
| **BuildSheetUrlWithGid(url, gid)** | Añade o reemplaza `#gid=XXX` en la URL para abrir el sheet en una pestaña concreta. |
| **GetLatestMonthSheetAsync(spreadsheetId, credentialsPath)** | Obtiene la última hoja cuyo título es "Mes Año" (para abrir el sheet en esa pestaña desde Configuración). |
| **FindRowForDateAsync** | (Uso interno/reserva.) Busca en la columna A de la hoja del mes la fila donde la fecha coincide con el día; no se usa actualmente en SyncAsync, que usa directamente `day.Date.Day + 1`. |

---

## 8. Dónde está el código

| Componente | Ubicación |
|------------|-----------|
| Sincronización (lógica de escritura) | `src/ManagerOS.Infrastructure/Integrations/GoogleSheets/GoogleSheetSyncService.cs` |
| Guardado Registro (app Windows) | `src/ManagerOS.Windows/Services/RegistroService.cs` (SaveDayAsync → SyncAsync en Task.Run) |
| Guardado Preguntas/feedback (Backend) | `src/ManagerOS.Backend/Controllers/ExecutionController.cs` (Create, Update → SyncToGoogleSheet) |
| Exportar todo / Importar Excel | `RegistroService.ExportAllDaysToGoogleSheetAsync`, `ImportFromEstimacionExcelAsync` |
| Resumen de turno (observaciones) | `src/ManagerOS.Core/Services/TurnoResumenBuilder.cs` (BuildResumenFromFeedback) |
| Configuración Google Sheet | `src/ManagerOS.Backend/GOOGLE_SHEET_SETUP.md` (creación de Sheet, cuenta de servicio, compartir) |

---

## 9. Resumen en una frase

Cada vez que se guarda un día desde **Preguntas** (tablet) o desde **Registro de ejecución** (Windows), si están configurados la URL del Google Sheet y el archivo de credenciales de la cuenta de servicio, se actualiza en segundo plano la **hoja del mes** correspondiente escribiendo **una fila** (fila = día del mes + 1) con **9 columnas**: Fecha, Día de la semana, Observaciones Mediodía/Tarde/Noche (resumen V/R/M/D), Facturación Mediodía/Tarde/Noche y Total; la hoja del mes se crea duplicando "Plantilla" (o la primera hoja) si no existía.
