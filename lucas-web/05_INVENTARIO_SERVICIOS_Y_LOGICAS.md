# Lucas Web — Inventario de servicios y lógicas

**Versión:** 1.0  
**Fecha:** Febrero 2026

---

## Servicios en la app Windows

| Servicio | Responsabilidad | ¿Exponer en web? |
|----------|------------------|------------------|
| **RegistroService** | GetDayAsync, SaveDayAsync, GenerateDailyAnalysisAsync, SyncToGoogleSheet | Ya expuesto vía ExecutionController |
| **DashboardService** | Resumen semanal (KPIs, días, tendencias) | Ya expuesto vía DashboardController |
| **InteligenciaService** | Patrones, tendencias, predicción, recomendaciones, RunFullBackgroundAnalysisAsync | Parcial: PredictionsController, RecommendationsController; lógica de predicción corre en app Windows |
| **ConfiguracionService** | settings.json, usuarios, parámetros, integraciones | No expuesto; opcional: API GET/PATCH settings |
| **AuthService** | Validación PIN/contraseña | Ya expuesto vía AuthController |
| **ClimaService** | Open-Meteo, geocodificación | Usado por app Windows; la web puede usar contexto ya cargado (ExecutionDay.WeatherCode) |
| **FestivosService** | Festivos por lat/lon | Usado por app Windows; la web ve IsHoliday en ExecutionDay |

---

## Lógicas que corren en app Windows

- **RunFullBackgroundAnalysisAsync:** Evaluar predicciones → RunWeeklyAnalysisAsync → Evaluar recomendaciones. Se ejecuta al abrir app, cada 5 min, tras guardar día o importar Excel.
- **Publicación de estimaciones:** RecomendacionesViewModel publica cache a `/api/estimaciones/cache` al cargar la pestaña Estimaciones.

---

## Dónde exponer en web

| Funcionalidad | Estado actual | Acción recomendada |
|---------------|---------------|--------------------|
| Registro día/turnos | ExecutionController | Mantener; la web usa POST/PATCH directamente |
| Dashboard/Estimaciones | DashboardController, EstimacionesController, PredictionsController | Mantener; la web usa GET |
| Configuración | No expuesta | Opcional: crear SettingsController con GET/PATCH de parámetros básicos (ProductividadIdealEurHora, HorasPorTurno, DireccionRestaurante, etc.) si la web debe editar configuración |
| Análisis en segundo plano | Solo app Windows | Mantener; la web no lanza análisis; los datos se actualizan cuando la app Windows está abierta o se puede implementar un job en Backend si se despliega en Modo B |
