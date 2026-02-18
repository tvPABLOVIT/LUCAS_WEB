# Clima: uso de coordenadas de Configuración

Todo el clima del programa usa **únicamente** las coordenadas configuradas en **Configuración → Parámetros → Ubicación del restaurante** (LatRestaurante y LonRestaurante). No hay coordenadas hardcodeadas ni fuentes alternativas.

## Dónde se leen las coordenadas (Settings)

- **WeatherController**
  - `GET /api/weather/for-date?date=...` — Feedback diario: clima del día y por turno.
  - `POST /api/weather/backfill` — Relleno manual de clima histórico.
- **EstimacionesController**
  - Alertas de la semana siguiente (lluvia, viento, temperatura) y predicción.
- **PredictionsController**
  - `GET /api/predictions/next-week` — Predicción de la semana siguiente y clima en cada tarjeta de día (Estimaciones).
  - `POST /api/predictions/next-week/save` — Guardado de la predicción con clima.
- **WeatherAutoBackfillHostedService**
  - Relleno automático de clima histórico (al arrancar y cada 24 h).

## Flujo de datos

1. **Configuración** guarda LatRestaurante y LonRestaurante (manual o con «Obtener coordenadas» desde la dirección).
2. Cualquier petición que necesite clima lee esas claves de Settings y llama a `IWeatherService.GetWeatherForRangeAsync(start, end, lat, lon)` (Open-Meteo).
3. **Tarjetas de cada día en Estimaciones**: el clima de cada día viene de la predicción enriquecida por `PredictionEnrichmentService`, que recibe lat/lon desde PredictionsController, y este los toma de Settings. Por tanto, el clima de cada tarjeta usa las coordenadas de Configuración.
4. **Feedback diario**: llama a `/api/weather/for-date`, que usa Lat/Lon de Settings.
5. **Impacto del clima (histórico)** y **backfill**: usan los mismos Settings.

Si no hay coordenadas en Configuración, no se obtiene clima externo: en Feedback diario se muestra «Configura lat/lon en Configuración», y en Estimaciones los días muestran «Sin datos de previsión» para el clima.
