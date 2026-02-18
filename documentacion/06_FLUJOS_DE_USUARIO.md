# 06 — Flujos de usuario

Descripción de los flujos principales: login, registro de ejecución, dashboard, estimaciones, configuración y vista tablet (feedback).

---

## 1. Login (app web)

1. Usuario abre la app web (o la URL de la tablet).
2. Si ya hay sesión/token válido: GET /api/auth/me → 200 con role → redirigir a la pantalla según rol (user → solo preguntas; admin/manager/master → pantalla principal con Registro, Estimaciones, Configuración).
3. Si no hay sesión: mostrar pantalla de login (campo PIN, botón Entrar).
4. Usuario introduce PIN → POST /api/auth/pin con { "pin": "1234" }.
5. Si 200: guardar token si se devuelve; opcionalmente recargar o redirigir; mostrar la app según role. Si 401: mostrar mensaje "PIN incorrecto".

---

## 2. Registro de ejecución (día)

**Actor:** Admin/manager/master.

1. Pantalla Registro: selector de fecha (yyyy-MM-dd), botón "Cargar día".
2. GET /api/execution/{date}. Si 404: formulario vacío (crear día nuevo). Si 200: rellenar campos con los datos (facturación total, horas, notas, y por cada turno: revenue, hours, staff_floor, staff_kitchen, feedback_q1–q4).
3. Usuario edita campos y guarda.
4. Si el día no existía: POST /api/execution con body completo (date, total_revenue, total_hours_worked, staff_total, notes, shifts). Si existía: PATCH /api/execution/{date} con los mismos campos.
5. Mostrar mensaje de éxito o error. Opcionalmente refrescar GET para ver estado final.

**Validaciones recomendadas:** total_revenue ≥ 0, total_hours_worked ≥ 0, staff_total ≥ 0; cada turno con revenue/hours ≥ 0; shift_name normalizado (Mediodia, Tarde, Noche).

---

## 3. Dashboard (resumen semanal)

**Actor:** Admin/manager/master.

1. Pantalla Dashboard: selector de semana (weekStart = lunes en yyyy-MM-dd) o “semana actual”.
2. GET /api/dashboard/week?weekStart=yyyy-MM-dd.
3. Mostrar: totalRevenue, avgProductivity, totalHours, avgStaff, avgRevenueHistoric, prevWeekRevenue, prevWeekProductivity, resumenClasificacion, resumenTexto, y la tabla/lista de days (día, revenue, productivity, trendLabel, context, etc.).

No hay escritura; solo lectura.

---

## 4. Estimaciones (semana siguiente)

**Actor:** Admin/manager/master.

1. Pantalla Estimaciones: título “Estimación semana siguiente”, rango de fechas (lunes–domingo de la semana siguiente).
2. **Opción A (Backend actual con caché):** GET /api/estimaciones. Si hay datos, mostrarlos (KPIs, días, alertas). Si no, fallback: GET /api/dashboard/week (semana anterior para promedios), GET /api/predictions/next-week, GET /api/recommendations, y construir en front la vista (o tener un endpoint en backend que lo construya).
3. **Opción B (App web pura):** Backend expone un endpoint único que calcula la misma lógica que InteligenciaService (GetNextWeekDiagramacionAsync, GetAlertasDiagramacionAsync) y devuelve JSON listo para mostrar.
4. Mostrar: KPIs históricos (facturación promedio semanal, productividad promedio, horas promedio, % coste personal), párrafo de estimación (facturación estimada, horas necesarias para productividad objetivo, % coste personal), tarjetas por día (nombre, fecha, revenue estimado, min–max, confianza, sala/cocina, clima, festivo), y bloques de alertas (Semana anterior, Tendencia, Clima, Festivos, Misma semana mes anterior, Eventos, Obras).
5. Refresco: cada X segundos (ej. 60) GET /api/recommendations/version; si cambia, recargar estimaciones.

---

## 5. Configuración

**Actor:** Admin/manager/master.

1. Pantalla Configuración: parámetros (HorasPorTurno, ProductividadIdealEurHora, CostoPersonalPorHora), nombre/dirección del restaurante, coordenadas (lat/lon), integraciones (clima API key, Google Sheets URL, credenciales), túnel (BackendUrl, UseTunnel, CloudflareTunnelToken), opcionalmente OpenAI/Gemini.
2. Lectura: en el proyecto actual los parámetros están en `settings.json` en disco. En app web, guardar en BD (tabla Settings o clave-valor) o en archivo en el servidor según stack.
3. Escritura: POST/PATCH a un endpoint de configuración (no existe en el Backend actual; hay que definirlo). Solo usuarios con rol admin/manager/master.
4. Usuarios: listar usuarios (GET), crear usuario con PIN y rol (POST). Ver 05.

---

## 6. Vista tablet (solo preguntas / feedback)

**Actor:** user (solo preguntas) o admin/manager/master (preguntas + Registro + Estimaciones).

1. Usuario abre la URL de la tablet (ej. https://túnel/feedback o /preguntas).
2. Si no hay sesión: pantalla de login por PIN. Tras login exitoso, GET /api/auth/me → role.
3. **Si role = user:** Mostrar solo la sección “Preguntas”: fecha, selector de turno (Mediodía, Tarde, Noche), personal sala/cocina, 4 preguntas (Q1–Q4) con opciones de radio, y “Facturación del día” solo en turno Noche. Botón Guardar. Sin pestañas Registro ni Estimaciones.
4. **Si role = admin/manager/master:** Mostrar pestañas: Preguntas, Registro, Estimaciones. Por defecto Preguntas activa. Misma UI de preguntas que para user; al cambiar de pestaña, cargar Registro o Estimaciones como en los flujos 2 y 4.
5. Al cargar Preguntas: fecha por defecto hoy; turno inicial según hora (ej. &lt; 16h → Mediodía, 16–20h → Tarde, ≥ 20h → Noche). GET /api/execution/{date} para cargar datos del día.
6. Al guardar: PATCH /api/execution/{date} con los shifts actualizados (feedback_q1–q4, staff_floor, staff_kitchen, y en Noche total_revenue si se rellenó “Facturación del día”). Mostrar “Turno guardado” / “Turno completo” según estado.

---

## 7. Orden de turnos y fecha

- Turnos siempre en orden: Mediodía, Tarde, Noche.
- Fecha: un solo día por formulario; navegación anterior/siguiente día actualizando la fecha y volviendo a GET /api/execution/{date}.

---

## 8. Referencia al código actual

- **wwwroot/feedback/index.html:** Estructura login + main (tabs + panel-preguntas, panel-registro, panel-estimaciones).
- **wwwroot/feedback/app.js:** showAppWithRole, initFeedbackApp, loadDay, saveDay, initRegistroApp, loadRegistroDay, loadEstimaciones, getShiftByCurrentTime, updateTurnState.
