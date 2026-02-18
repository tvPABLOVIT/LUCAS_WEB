# Flujo: subida y lectura del PDF de cuadrante

Resumen de cómo se sube el PDF del cuadrante BETLEM y cómo se lee hasta guardar días y turnos en la aplicación.

---

## 1. Frontend (Dashboard)

- **Dónde:** Dashboard → bloque “Importar” → **PDF cuadrante (personal y horas programadas por turno)**.
- **Elementos:** 
  - `<input type="file" id="dashboard-pdf-file" accept=".pdf" />`
  - Botón **Cargar PDF** (abre el selector de archivo).
  - `<span id="dashboard-pdf-status">` para mensaje de estado.
- **Flujo:**
  1. El usuario elige un archivo (o hace clic en “Cargar PDF” y luego elige).
  2. Se construye un `FormData`, se añade el archivo con la clave `file`: `fd.append('file', this.files[0])`.
  3. Se envía `POST /api/import/cuadrante-pdf?weekStart=...` con `body: fd`.  
     (El `weekStart` se envía por coherencia con el Excel; la API **no lo usa** para el PDF: las fechas salen del contenido del PDF.)
  4. No se fuerza `Content-Type` cuando el body es `FormData` (en `auth.js`), así el navegador envía `multipart/form-data` con boundary correcto.
  5. Se muestra “Enviando…”, luego el mensaje devuelto por la API (días creados/actualizados, turnos, errores) o “Error al enviar.” si falla la petición.
  6. Tras la respuesta se vuelve a cargar el dashboard (`load()`).

---

## 2. API (ImportController)

- **Endpoint:** `POST /api/import/cuadrante-pdf`
- **Límite:** `[RequestSizeLimit(10 * 1024 * 1024)]` → 10 MB.
- **Validaciones:**
  - Que llegue un archivo y no esté vacío.
  - Que la extensión sea `.pdf` (insensible a mayúsculas).
- **Flujo:**
  1. Abre el stream del archivo: `file.OpenReadStream()`.
  2. Llama a `_cuadrantePdf.ParsePdfAsync(stream)` → devuelve `List<CuadranteDayDto>`.
  3. Si la lista está vacía, responde 200 con mensaje “El PDF no contiene días reconocidos.”.
  4. Para cada día del DTO:
     - Parsea la fecha (`d.Date`).
     - Busca o crea `ExecutionDay` para esa fecha.
     - Asigna `PlannedHoursTotal = d.TotalHoursWorked` (horas totales del día según el PDF).
     - Por cada turno del día (`d.Shifts`): crea o actualiza `ShiftFeedback` con `PlannedHours`, `StaffFloor`, `StaffKitchen` (y `ShiftName`).
  5. Guarda cambios y responde con `ImportExcelResult`: `days_created`, `days_updated`, `shifts_updated`, `message`, `errors`.
- **Errores:** Si `ParsePdfAsync` lanza (por ejemplo `InvalidOperationException` por parser no encontrado o fallo de Python), se captura y se devuelve 200 con `result.Message` y `errors` para mostrarlos en la UI.

---

## 3. Servicio C# (CuadrantePdfService)

- **Ruta del parser:**  
  - Si está configurado `CuadranteParser:ParserProjectPath`, se usa esa ruta.  
  - Si no, se usa `ContentRootPath/../LucasCuadranteParser` (carpeta hermana de la API).
- **Comando Python:** `CuadranteParser:PythonPath` (por defecto `"python"`).
- **Flujo:**
  1. Comprueba que exista la carpeta del parser y `main.py` dentro.
  2. Escribe el stream del PDF en un **archivo temporal** (en `%TEMP%` o equivalente).
  3. Crea un directorio temporal de salida para el JSON.
  4. Inicia un proceso:
     - Ejecutable: `PythonPath` (ej. `python`).
     - Argumentos: `main.py`, `<ruta_temp_pdf>`, `--output-dir`, `<dir_temp_salida>`.
     - `WorkingDirectory`: carpeta del parser (donde está `main.py`).
     - Captura stdout y stderr.
  5. Espera a que el proceso termine. Si el código de salida no es 0, lanza `InvalidOperationException` con el mensaje de stderr (o stdout si stderr está vacío).
  6. Lee el archivo `cuadrante_lucas.json` del directorio de salida.
  7. Deserializa el JSON a `List<CuadranteDayDto>` (propiedades en snake_case gracias a `[JsonPropertyName(...)]`).
  8. En un `finally`, borra el archivo temporal del PDF y el directorio de salida (y su contenido).

---

## 4. Parser Python (LucasCuadranteParser)

- **Entrada:** ruta del PDF (archivo temporal que escribe la API).
- **Salida:** `cuadrante_lucas.json` en el directorio indicado con `--output-dir`.

### 4.1 Dependencia

- **pdfplumber:** extracción de texto del PDF.  
  Si no está instalado: `pip install pdfplumber`.

### 4.2 Pipeline

1. **ingest (`extract_text_from_pdf`)**  
   Abre el PDF con `pdfplumber.open(path)` y concatena el texto de todas las páginas (`page.extract_text()`). Devuelve un único string con saltos de línea.

2. **segment (`segment_by_days`)**  
   - Busca en el texto el rango de semana: patrón tipo `del 09/02/2026 al 15/02/2026` para obtener año (y opcionalmente inicio de semana).  
   - Busca cabeceras de día: `Lunes 9 febrero`, `Martes 10 febrero`, etc.  
   - Corta el texto en bloques por día (`DayBlock`: nombre día, número, mes, texto del bloque).

3. **entities (`extract_entities_from_day_blocks`)**  
   Para cada bloque de día:  
   - Construye la fecha (día + mes en español + año; el año puede venir del rango de la cabecera).  
   - Extrae empleados y sus horas totales (p. ej. “XXhYY” al final de línea).  
   - Extrae turnos: rol + rango horario (ej. `11:00 - 16:00`) o descansos/ausencias.  
   - Usa `KNOWN_ROLE_KEYWORDS` y `config.py` (mapeo puesto → sala/cocina) para clasificar personal.

4. **normalize (`to_lucas_week`)**  
   Convierte la lista de entidades a formato Lucas:  
   - Por cada día: `date` (ISO), `total_revenue` (0), `total_hours_worked`, `shifts`.  
   - Cada turno: `shift_name` (Mediodía/Tarde/Noche), `staff_floor`, `staff_kitchen`, `hours_worked`.  
   - Las horas se calculan por rangos (ej. 11–16 = 5 h) y se agrupan en Mediodía/Tarde/Noche según reglas en `relations.py`.

5. **main.py**  
   Escribe `week_data` en `output_dir/cuadrante_lucas.json` (UTF-8, indentado).

---

## 5. Formato JSON (Python → C#)

Cada día en `cuadrante_lucas.json` tiene esta forma (compatible con `CuadranteDayDto` / `CuadranteShiftDto`):

```json
{
  "date": "2026-02-09",
  "total_revenue": 0.0,
  "total_hours_worked": 12.5,
  "shifts": [
    {
      "shift_name": "Mediodia",
      "staff_floor": 2,
      "staff_kitchen": 1,
      "hours_worked": 5.0
    },
    ...
  ]
}
```

---

## 6. Consideraciones para hosting

- **Python y parser en el servidor:**  
  La API invoca `python main.py ...`. En el servidor (o contenedor) debe estar instalado Python, pdfplumber y la carpeta `LucasCuadranteParser` (con `main.py` y el pipeline).  
  Si el parser no está junto a la API, hay que configurar `CuadranteParser:ParserProjectPath` (ruta absoluta a la carpeta del parser).

- **Ruta de Python:**  
  En Linux/Docker suele ser `python3`. Se puede configurar con `CuadranteParser:PythonPath` (ej. `python3`).

- **Archivo temporal:**  
  El PDF se escribe en un archivo temporal; el parser lo recibe por argumento. No se requiere que tenga extensión `.pdf` para que pdfplumber funcione (lee por contenido), pero usar extensión `.pdf` puede ayudar a diagnósticos y a futuras extensiones.

---

## 7. Horas y distribución Mediodía / Tarde / Noche

- **Total del día:** El parser suma la duración de cada rango horario de cada empleado (ej. 11:00–16:00 = 5 h) y devuelve `total_hours_worked`. La API lo guarda en `PlannedHoursTotal` y lo muestra en la columna **Horas** del dashboard cuando no hay horas reales (Excel).
- **Por turno:** El parser usa **ventanas fijas** para repartir esas horas entre Mediodía, Tarde y Noche:
  - **Mediodía:** 10:00–16:00  
  - **Tarde:** 16:00–20:00  
  - **Noche:** 20:00–01:00 (día siguiente)  
  Cada rango del empleado se solapa con estas ventanas; las horas que caen en cada ventana son las que se asignan a ese turno. Si tu cuadrante usa otros horarios (ej. Mediodía 11:00–15:00), puedes copiar `LucasCuadranteParser/shift_windows.example.json` a `shift_windows.json` en esa carpeta y cambiar las horas para que coincidan con tu cuadrante.
- **En el dashboard:** Para cada día verás:
  - **Horas:** Total efectivo (Excel si existe, si no total del PDF, si no horas calculadas).
  - **Horas prog. (PDF):** Suma de horas programadas por turno que vienen del PDF (para comparar con el cuadrante).
  - **Mediodía: X h, Tarde: Y h, Noche: Z h:** Distribución por turno que leyó el PDF.
  - **Horas calc.:** (Sala+Cocina) × “Horas por turno” de Configuración; es una estimación teórica, no la lectura del PDF.

**Código de colores en el cuadrante:** Los bloques con color de categoría (Manager, Camarero/a, Cocinero/a, etc.) = trabajo en el restaurante (sí cuentan). **Gris sólido** = Ausencias (descanso, Abs, etc.) → no cuentan. **Gris con rayas** = Otros establecimientos (ej. "Camarero/a - CENTRIC", "Chef Operativo - MOLINA") → no cuentan para BETLEM. El parser no lee colores; detecta "otros establecimientos" por el texto **" - NOMBRE"** (CENTRIC, MOLINA, etc.) y ausencias por palabras clave.

Si al comparar con el cuadrante las horas o la distribución no coinciden, revisa las ventanas en `shift_windows.json` (o en `config.py` si no usas JSON) y que los rangos horarios del PDF se estén extrayendo bien en `entities.py`.

---

## 8. Resumen rápido

| Paso | Qué ocurre |
|------|------------|
| 1 | Usuario elige PDF en el Dashboard y se envía `POST /api/import/cuadrante-pdf` con el archivo en `file`. |
| 2 | La API valida extensión `.pdf` y tamaño ≤ 10 MB, abre stream y llama a `CuadrantePdfService.ParsePdfAsync`. |
| 3 | El servicio escribe el stream en un temp file, ejecuta `python main.py <temp_pdf> --output-dir <temp_dir>`, lee `cuadrante_lucas.json` y deserializa a DTOs. |
| 4 | El parser Python: pdfplumber → texto, segmentación por días, entidades (empleados, turnos, horas), normalización a JSON Lucas. |
| 5 | La API crea/actualiza `ExecutionDay` y `ShiftFeedback` con fechas, `PlannedHoursTotal`, `PlannedHours`, `StaffFloor`, `StaffKitchen` por turno. |
| 6 | Se responde con días creados/actualizados y turnos actualizados; el frontend muestra el mensaje y recarga el dashboard. |
| 7 | En el dashboard, las horas y la distribución Mediodía/Tarde/Noche se muestran para que puedas comparar con el cuadrante; si no coinciden, ajusta `shift_windows.json` en LucasCuadranteParser. |
