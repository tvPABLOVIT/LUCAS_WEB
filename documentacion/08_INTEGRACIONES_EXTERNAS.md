# 08 — Integraciones externas

Servicios externos usados por Lucas: clima (Open-Meteo), festivos (Nager + Nominatim), Open Data BCN (eventos y obras), GuiaBCN (scraping), Google Sheets. Detalle para replicar en la app web.

---

## 1. Clima — Open-Meteo

**Objetivo:** Previsión del tiempo por fecha para la ubicación del restaurante (lat/lon). Se usa en estimaciones (descripción del día, días de lluvia) y opcionalmente para ajustar estimación en días lluviosos.

**API:**  
- Geocoding: `https://geocoding-api.open-meteo.com/v1/search?name={dirección}&count=1` → devuelve latitude, longitude.  
- Forecast: `https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto` (o por fecha con `start_date`/`end_date`).

**Códigos WMO (weather_code):**  
0 Despejado, 1–3 nubes, 45/48 niebla, 51–55 llovizna, 61–65 lluvia, 71–77 nieve/granizo, 80–82 chubascos, 95–96 tormenta. Mapear a descripción en español (ej. “Lluvia moderada”, “Despejado”).

**Configuración:** Opcionalmente `ClimaApiKey` (Open-Meteo permite peticiones sin clave con límite; si se usa otra API, adaptar). Coordenadas del restaurante: `LatRestaurante`, `LonRestaurante`; si no hay, geocodificar desde `DireccionRestaurante` con el endpoint de búsqueda.

**Referencia:** ClimaService.cs — GetWeatherForDateAsync, GeocodeAsync, WmoDescriptions.

---

## 2. Festivos — Nager.Date + Nominatim

**Objetivo:** Saber si una fecha es festivo en el país del restaurante y el nombre del festivo (para mostrar en estimaciones y en “Contexto del día”).

**Flujo:**  
1. Dadas lat/lon del restaurante, obtener código de país: Nominatim reverse geocoding `https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json` → address.country_code (ej. ES, FR).  
2. Con el código de país (ej. ES), obtener festivos del año: `https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}` → array de { date (yyyy-MM-dd), localName, name }.  
3. Para una fecha concreta, buscar en ese array si existe; si sí, devolver (IsHoliday=true, HolidayName=localName o name).

**Caché:** Cachear por (lat, lon) el countryCode y por (countryCode, year) la lista de festivos para no abusar de las APIs.

**Configuración:** Lat/lon del restaurante (o dirección para geocodificar). No requiere API key (Nominatim pide User-Agent; Nager es gratuito).

**Referencia:** FestivosService.cs — GetHolidayInfoAsync, GetCountryCodeAsync, GetHolidaysForYearAsync.

---

## 3. Open Data BCN (Ayuntamiento de Barcelona)

**Objetivo:** Eventos y obras en espacio público cerca del restaurante (radio 300 m) o en un barrio/zona de interés, para la semana siguiente.

**API:** CKAN-style. Base: `https://opendata-ajuntament.barcelona.cat/data/api/3/action/`.  
- Listar recursos de un package: `package_show?id={packageId}` o buscar datasets.  
- Datastore: `datastore_search` con resource_id del recurso que tenga los datos (eventos con nombre, descripción, fecha, lat/lon o dirección/barrio).

**Packages usados:**  
- **agenda-diaria:** Eventos (nombre, descripción, fecha, ubicación).  
- **Obras en espacio público:** Package específico (nombre exacto en el código: FindObrasPackageIdAsync). Recursos con geometría o dirección; filtrar por distancia o barrio.

**Filtro por distancia:** Haversine entre (lat, lon) del restaurante y (lat, lon) del evento/obra. Incluir solo si distancia ≤ 300 m.  
**Filtro por barrio/zona:** Si no hay coordenadas en el evento, filtrar por campo de barrio o texto de ubicación (ej. “Supermanzana Consejo de Ciento”, “Eixample”).

**Configuración:** Lat/lon del restaurante, opcionalmente BarrioInteres, ZonaInteres (lista de cadenas para locationContains). Radio por defecto 300 m.

**Referencia:** OpenDataBcnService.cs — GetEventosYObrasCercaAsync, HaversineMeters, FetchDatastoreRecordsAsync, ParseRecord, FindObrasPackageIdAsync.

---

## 4. GuiaBCN (scraping)

**Objetivo:** Añadir más eventos/actividades cerca (ej. Eixample) que no estén en Open Data BCN. Se hace scraping de páginas de GuiaBCN (agenda, eventos).

**Implementación:** HTTP GET a URLs de GuiaBCN; parsear HTML para extraer título, fecha, enlace, ubicación. Filtrar por barrio/zona o por radio si se obtienen coordenadas. Combinar resultados con los de Open Data BCN.

**Riesgo:** Cambios en el HTML rompen el scraping. Mantener selectores actualizados o considerar solo Open Data BCN si se quiere minimizar mantenimiento.

**Referencia:** GuiaBcnScraperService.cs.

---

## 5. Google Sheets

**Objetivo:** Sincronizar días de ejecución guardados con una hoja de cálculo (exportar filas: fecha, facturación, horas, turnos, feedback). Opcional.

**Flujo:**  
1. Configuración: URL de la hoja (extraer sheetId) y ruta al archivo de credenciales JSON (cuenta de servicio).  
2. Tras guardar un ExecutionDay (POST o PATCH /api/execution), en segundo plano llamar al servicio de sincronización: leer/actualizar filas en la hoja según el día (fecha como clave).

**API:** Google Sheets API v4. Autenticación con cuenta de servicio (JWT). Scopes: spreadsheets.readonly o spreadsheets según lectura/escritura.

**Configuración:** GoogleSheetsUrl, GoogleCredentialsPath (ruta al JSON de la cuenta de servicio).

**Referencia:** GoogleSheetSyncService.cs (Infrastructure/Integrations/GoogleSheets); ExecutionController SyncToGoogleSheet; RegistroService SaveDayAsync (llamada a SyncAsync).

---

## 6. Resumen para la app web

| Integración | Uso en web | Necesidad |
|-------------|------------|-----------|
| Open-Meteo | Clima en estimaciones, días de lluvia, ajuste estimación | Recomendada |
| Nager + Nominatim | Festivos en estimaciones | Recomendada |
| Open Data BCN | Eventos y obras en radio 300 m / barrio | Recomendada si restaurante en Barcelona |
| GuiaBCN | Más eventos (scraping) | Opcional |
| Google Sheets | Exportar datos a hoja | Opcional |

Todas las llamadas externas deben hacerse en el backend (no exponer API keys en el front). Timeouts y reintentos recomendados; caché donde sea posible (festivos por año, clima por día).
