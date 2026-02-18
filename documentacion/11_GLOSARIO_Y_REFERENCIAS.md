# 11 — Glosario y referencias

Términos, acrónimos y referencias a archivos del proyecto actual para localizar la lógica al implementar la app web.

---

## 1. Glosario

| Término | Significado |
|---------|-------------|
| **ExecutionDay** | Día de ejecución: un registro por fecha con facturación total, horas, personal, notas, clima, festivo. |
| **ShiftFeedback** | Turno dentro de un día: Mediodía, Tarde o Noche; revenue, hours, staff_floor, staff_kitchen, feedback Q1–Q4. |
| **Q1–Q4** | Las cuatro preguntas de feedback por turno: Volumen (Q1), Ritmo (Q2), Margen (Q3), Dificultad (Q4). |
| **V/R/M/D** | Volumen, Ritmo, Margen, Dificultad (ejes del scoring por turno). |
| **SGT** | Score Global Turno: fórmula (V×2)+R+(6−M)+D, rango 6–31. |
| **DOW** | Day of week (día de la semana: Lunes–Domingo). |
| **weekStart** | Lunes de la semana en formato yyyy-MM-dd. |
| **Semana completa** | Semana con 7 días con datos “financieramente completos” (revenue y horas en los 3 turnos). |
| **Productividad (€/h)** | TotalRevenue / TotalHoursWorked. |
| **Estimación semana siguiente** | Predicción de facturación (y opcionalmente horas/personal) para el lunes–domingo de la semana que viene. |
| **PIN** | Código numérico corto (4+ dígitos) para login rápido; se almacena como hash BCrypt (PinHash). |
| **Role** | user, admin, manager, master. Define qué pantallas puede ver el usuario. |

---

## 2. Acrónimos y nombres técnicos

| Sigla | Significado |
|-------|-------------|
| **BD** | Base de datos. |
| **API** | Interfaz de programación (REST en este proyecto). |
| **DTO** | Data Transfer Object (objeto de respuesta/request JSON). |
| **PK** | Primary Key. |
| **FK** | Foreign Key. |
| **BCrypt** | Algoritmo de hash para contraseñas/PINs. |
| **WMO** | Códigos de tiempo (World Meteorological Organization) usados por Open-Meteo. |
| **CKAN** | Estándar de datos abiertos (Open Data BCN usa API tipo CKAN). |

---

## 3. Referencias a archivos del proyecto actual

### Core (entidades y scoring)

- `ManagerOS.Core/Entities/ExecutionDay.cs` — Entidad día de ejecución.
- `ManagerOS.Core/Entities/ShiftFeedback.cs` — Entidad turno.
- `ManagerOS.Core/Entities/User.cs` — Usuario (PinHash, Role).
- `ManagerOS.Core/Entities/Recommendation.cs`, `WeeklyPrediction.cs`, `DailyAnalysis.cs`, `DetectedPattern.cs`, `DetectedTrend.cs`, `Event.cs`.
- `ManagerOS.Core/Services/TurnoResumenBuilder.cs` — (si se usa.)
- **ManagerOS.Windows/Services/TurnoScoringService.cs** — GetVolumenIndex, GetRitmoIndex, GetMargenIndex, GetDificultadIndex, GetSgt, GetEstadoTurno, GetTipoTurno, BuildResumenNivel3.

### Infraestructura (BD e integraciones)

- `ManagerOS.Infrastructure/Data/ApplicationDbContext.cs` — DbSets y OnModelCreating.
- `ManagerOS.Infrastructure/Migrations/*.cs` — Migraciones EF (nombres de columnas y tipos).
- `ManagerOS.Infrastructure/Integrations/GoogleSheets/GoogleSheetSyncService.cs` — Sincronización con Google Sheets.
- **ManagerOS.Windows/Services/ClimaService.cs** — Open-Meteo, geocoding, GetWeatherForDateAsync.
- **ManagerOS.Windows/Services/FestivosService.cs** — Nager, Nominatim, GetHolidayInfoAsync.
- **ManagerOS.Windows/Services/OpenDataBcnService.cs** — Eventos y obras, HaversineMeters, GetEventosYObrasCercaAsync.
- **ManagerOS.Windows/Services/GuiaBcnScraperService.cs** — Scraping GuiaBCN.

### Backend (API)

- `ManagerOS.Backend/Program.cs` — Configuración app, BD, CORS, sesión, reescritura /feedback/api, MapControllers.
- `ManagerOS.Backend/Controllers/AuthController.cs` — POST pin, GET me, POST logout.
- `ManagerOS.Backend/Controllers/ExecutionController.cs` — GET, GET {date}, POST, PATCH.
- `ManagerOS.Backend/Controllers/DashboardController.cs` — GET week.
- `ManagerOS.Backend/Controllers/RecommendationsController.cs` — GET, PATCH {id}, GET version.
- `ManagerOS.Backend/Controllers/EstimacionesController.cs` — GET, POST cache.
- PredictionsController (en RecommendationsController.cs) — GET next-week.

### Windows (lógica de negocio)

- **ManagerOS.Windows/Services/RegistroService.cs** — GetDayAsync, SaveDayAsync, GenerateDailyAnalysisAsync, export Google Sheets.
- **ManagerOS.Windows/Services/InteligenciaService.cs** — GetNextWeekDiagramacionAsync, GetAlertasDiagramacionAsync, GetFeedbackStrength, GetSalaCocinaScheme, TotalToCocinaSala, GetCompleteWeeks, IsFinanciallyComplete, BuildNextWeekPredictionAsync, RunFullBackgroundAnalysisAsync.
- **ManagerOS.Windows/Services/ConfiguracionService.cs** — HasAnyUserAsync, CreateMasterUserAsync, CreateUserWithPinAsync, GetUsersAsync, GetHorasPorTurno, GetProductividadIdealEurHora, GetLatLonRestaurante, GetPredictionBiasByDayOfWeek, settings.json.
- **ManagerOS.Windows/Services/DashboardService.cs** — (si existe; en el Backend la lógica está en DashboardController.)

### Vista tablet (feedback)

- `ManagerOS.Backend/wwwroot/feedback/index.html` — Estructura HTML: login, tabs, panel-preguntas, panel-registro, panel-estimaciones.
- `ManagerOS.Backend/wwwroot/feedback/app.js` — Lógica: auth, showAppWithRole, initFeedbackApp, loadDay, saveDay, initRegistroApp, loadRegistroDay, loadEstimaciones, getShiftByCurrentTime, updateTurnState.
- `ManagerOS.Backend/wwwroot/feedback/styles.css` — Estilos, versión noche, feedback-card, .hidden.

---

## 4. Documentos de la carpeta docs/lucas-web

La carpeta `docs/lucas-web` contiene guías anteriores (00_INDICE_Y_PLAN_GLOBAL, 01_ARQUITECTURA_Y_TECNOLOGIAS, etc.) orientadas a reutilizar el Backend existente. La carpeta **documentacion** (esta) está pensada para **construir la app web desde cero** con todo el detalle extraído del código actual; puede usarse junto con docs/lucas-web para cruzar referencias o migrar de una arquitectura a otra.
