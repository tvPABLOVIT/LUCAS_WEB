# 09 — Especificación pantallas web

Especificación pantalla a pantalla para la aplicación web de Lucas, enfocada en contenido, campos, validaciones y APIs a usar.

---

## 1. Pantalla de login

- **URL:** /login o raíz (si no hay sesión).
- **Elementos:** Logo/título “Lucas”, campo numérico/contraseña “PIN” (4+ dígitos), botón “Entrar”, mensaje de error (PIN incorrecto / error de red).
- **Comportamiento:** Al enviar, POST /api/auth/pin con { "pin": "..." }. Si 200: guardar token si viene en respuesta; redirigir a /app o /dashboard según rol. Si 401: mostrar error. No requiere sesión previa.
- **Roles:** No se distingue en esta pantalla; la redirección posterior depende del role devuelto.

---

## 2. Pantalla principal (después del login)

- **URL:** /app, /dashboard o / (con sesión).
- **Navegación:** Según rol:
  - **user:** Solo enlace/pestaña “Preguntas” (feedback).
  - **admin/manager/master:** Pestañas o menú: “Registro”, “Dashboard”, “Estimaciones”, “Configuración”, “Cerrar sesión”. Opcional: “Preguntas” (misma vista que para user).
- **Cabecera:** Título “Lucas Web”, rol mostrado (“Rol: admin”), botón Cerrar sesión (POST /api/auth/logout y redirigir a login).

---

## 3. Pantalla Registro (día de ejecución)

- **Solo para:** admin, manager, master.
- **Elementos:**
  - Selector de fecha (input type=date o equivalente), formato yyyy-MM-dd.
  - Botón “Cargar día”. Al pulsar: GET /api/execution/{date}. Si 404: formulario vacío para crear. Si 200: rellenar todos los campos.
  - Campos del día: Facturación total (€), Horas trabajadas, Notas (texto).
  - Por cada turno (Mediodía, Tarde, Noche): Revenue (€), Hours worked, Personal sala, Personal cocina, y las 4 preguntas (Q1–Q4) como listas de opciones (radio o select) con los textos exactos indicados en 03_REGLAS_DE_NEGOCIO_Y_FORMULAS.
  - Botón “Guardar”. Si el día no existía: POST /api/execution. Si existía: PATCH /api/execution/{date}. Body: date, total_revenue, total_hours_worked, staff_total, notes, shifts[] con shift_name, revenue, hours_worked, staff_floor, staff_kitchen, feedback_q1–q4, recorded_by, edited_by.
- **Validaciones:** Números ≥ 0; al menos un turno con datos. Mensaje de éxito/error tras guardar.

---

## 4. Pantalla Dashboard

- **Solo para:** admin, manager, master.
- **Elementos:** Selector de semana (weekStart = lunes en yyyy-MM-dd). Botón “Cargar” o carga automática. GET /api/dashboard/week?weekStart=...
- **Contenido a mostrar:** totalRevenue, avgProductivity, totalHours, avgStaff, avgRevenueHistoric, prevWeekRevenue, prevWeekProductivity, resumenClasificacion, resumenTexto, y tabla/lista de days con: dayName, date, revenue, productivity, avgRevenueHistoric, trendLabel, staffTotal, context.
- **Sin escritura.** Solo lectura.

---

## 5. Pantalla Estimaciones

- **Solo para:** admin, manager, master.
- **Título:** “Estimación semana siguiente” y rango de fechas (lunes–domingo de la semana siguiente).
- **KPIs (promedio histórico):** Facturación promedio semanal, Productividad promedio (€/h), Horas promedio semanales, % coste personal vs facturación (y opcionalmente coste en € en pequeño).
- **Párrafo de estimación:** “Para la semana del DD feb – DD feb AAAA, se estima una facturación total de X €. La cantidad de horas necesarias para alcanzar la productividad objetivo (50 €/h) con la facturación estimada es de Y horas (unas Z al día). El coste de personal se ubica en un W% vs la facturación estimada. Los días con mayor estimación son …”
- **Tarjetas por día (L–D):** dayName, date (dd/MM), predictedRevenue, rango min–max, confidenceLabel, esquema sala/cocina, clima (weatherDescription), festivo (isHoliday, holidayName). Opcional: desglose por turno.
- **Bloques “Qué puede afectar la semana siguiente”:** Solo mostrar si hay contenido. Orden sugerido: Semana anterior (datos reales), Tendencia (% más/menos), Clima (días de lluvia), Festivos (solo si hay), Misma semana mes anterior (facturación mes pasado y %), Eventos esta semana (solo si hay), Obras cerca (solo si hay).
- **Datos:** GET /api/estimaciones si existe; si no, GET /api/dashboard/week (para promedios), GET /api/predictions/next-week, GET /api/recommendations, y construir vista en front; o un endpoint único en backend que devuelva todo. Refresco: GET /api/recommendations/version cada 60 s; si cambia, recargar.
- **Estilo:** “Versión noche” (fondos oscuros, texto claro) como en la vista tablet actual si se desea consistencia.

---

## 6. Pantalla Configuración

- **Solo para:** admin, manager, master.
- **Secciones:**
  - **Parámetros:** Horas por turno (número), Productividad ideal (€/h), Coste personal por hora (€). Guardar en BD o settings.
  - **Restaurante:** Nombre, Dirección, Latitud, Longitud (opcional: geocodificar desde dirección). Barrio/Zona de interés (para eventos).
  - **Integraciones:** Clima API key (opcional), Google Sheets URL, ruta credenciales Google. Túnel: BackendUrl, UseTunnel, CloudflareTunnelToken (si aplica).
  - **Usuarios:** Listado (GET desde API de usuarios); botón “Crear usuario”: FullName, Email, PIN (4+), Rol (user/admin/manager). POST a API de usuarios. Solo usuarios con PIN pueden entrar por PIN.
- **APIs a definir:** GET/POST o GET/PATCH para configuración (parámetros y restaurante); GET/POST usuarios. En el proyecto actual no hay controlador de configuración; la app Windows lee/escribe settings.json. En la app web, implementar endpoints y persistencia en BD o archivo en el servidor.

---

## 7. Pantalla Preguntas (feedback) — vista tablet / user

- **URL:** /preguntas o /feedback. Accesible por user (solo esta pantalla) y por admin/manager/master (junto con Registro y Estimaciones).
- **Elementos:** Fecha (selector), Navegación día anterior/siguiente. Turno activo: Mediodía, Tarde, Noche (botones). Personal sala, Personal cocina (numéricos). Las 4 preguntas (Q1–Q4) con opciones de radio (textos exactos del documento 03). En turno “Noche”: campo “Facturación del día entero (€)”. Botón “Guardar Lucas”. Mensaje de estado (“Turno guardado”, “Turno completo”, “Completa las 4 preguntas”). Cerrar sesión.
- **Turno inicial según hora:** &lt; 16h → Mediodía; 16–20h → Tarde; ≥ 20h → Noche. Solo afecta a qué turno se muestra seleccionado al cargar.
- **Carga:** GET /api/execution/{date}. Si 404: formulario vacío (al guardar se hará POST). Si 200: rellenar fecha, total_revenue (en Noche como “Facturación del día”), y por cada turno los campos y las respuestas Q1–Q4.
- **Guardar:** PATCH /api/execution/{date} con shifts actualizados (y total_revenue si se editó en Noche). Mostrar “Turno guardado” o “Turno completo” si las 4 preguntas están respondidas.
- **Responsive:** Diseño apto para tablet (botones grandes, texto legible). Zoom opcional (slider 75–150%) como en la vista actual.

---

## 8. Accesibilidad y UX

- Etiquetas asociadas a campos (label/for o aria-label).
- Mensajes de error y éxito claros (aria-live si se desea).
- Contraste suficiente (especialmente en “versión noche”).
- En tablet: evitar dependencia de hover; áreas de toque suficientes.

---

## 9. Referencia al código actual

- **Vista tablet:** wwwroot/feedback/index.html (estructura), app.js (showAppWithRole, initFeedbackApp, loadDay, saveDay, getShiftByCurrentTime, updateTurnState), styles.css.
- **App Windows:** RegistroView.xaml, DashboardView.xaml, RecomendacionesView/Estimaciones, ConfiguracionView.xaml (contenido y bindings).
