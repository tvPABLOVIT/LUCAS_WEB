(function (global) {
  'use strict';
  var auth = global.LUCAS_AUTH;
  var SHIFT_NAMES = ['Mediodia', 'Tarde', 'Noche'];
  var SHIFT_LABELS = ['Mediodía', 'Tarde', 'Noche'];
  var Q1_OPTIONS = ['Pocas mesas', 'Media sala', 'Sala completa', 'Sala y terraza completas', 'Sala y terraza completas y doblamos mesas'];
  var Q2_OPTIONS = ['Muy espaciadas, sin acumulación', 'Entradas tranquilas', 'Flujo constante', 'Muchas entradas juntas', 'Entradas continuas sin margen'];
  var Q3_OPTIONS = ['Siempre adelantado', 'Generalmente con margen', 'Justo', 'Poco margen', 'Ningún margen'];
  var Q4_OPTIONS = ['Muy fácil', 'Fácil', 'Normal', 'Difícil', 'Muy difícil'];

  function todayStr() {
    var d = new Date();
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  }
  function normalizeDateStr(str) {
    if (!str || typeof str !== 'string') return (str && String(str).trim()) || todayStr();
    var s = str.trim();
    var m = s.match(/^(\d{4})-(\d{1,2})-(\d{1,2})$/);
    if (m) return m[1] + '-' + m[2].padStart(2, '0') + '-' + m[3].padStart(2, '0');
    var d = new Date(s + (s.length === 10 ? 'T12:00:00' : ''));
    if (!isNaN(d.getTime())) return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    return todayStr();
  }
  function addDays(dateStr, delta) {
    var d = new Date(dateStr + 'T12:00:00');
    d.setDate(d.getDate() + delta);
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  }
  function weekNumber(dateStr) {
    var d = dateStr ? new Date(dateStr + 'T12:00:00') : new Date();
    d.setDate(d.getDate() + 4 - (d.getDay() === 0 ? 7 : d.getDay()));
    var yearStart = new Date(d.getFullYear(), 0, 1);
    return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
  }
  function formatDateDisplay(dateStr) {
    if (!dateStr) return '';
    var d = new Date(dateStr + 'T12:00:00');
    return String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0') + '/' + d.getFullYear();
  }
  function getDayName(dateStr) {
    if (!dateStr) return '';
    var d = new Date(dateStr + 'T12:00:00');
    var names = ['DOMINGO', 'LUNES', 'MARTES', 'MIÉRCOLES', 'JUEVES', 'VIERNES', 'SÁBADO'];
    return names[d.getDay()];
  }
  function radioGroup(name, options, selected) {
    var s = (selected || '').trim();
    return options.map(function (o) {
      var v = o.trim();
      var id = name + '-' + v.replace(/\s+/g, '_').replace(/,/g, '');
      return '<label class="registro-radio"><input type="radio" name="' + name + '" value="' + v.replace(/"/g, '&quot;') + '"' + (v === s ? ' checked' : '') + ' id="' + id + '" /> ' + o + '</label>';
    }).join('');
  }

  function defaultShift(name) {
    return {
      shift_name: name,
      revenue: 0,
      hours_worked: 0,
      staff_floor: 0,
      staff_kitchen: 0,
      feedback_q1: null,
      feedback_q2: null,
      feedback_q3: null,
      feedback_q4: null,
      feedback_q5: null,
      weather_code: null,
      weather_temp_avg: null,
      weather_precip_mm: null,
      weather_wind_max_kmh: null
    };
  }
  function defaultDayData(dateStr) {
    return {
      date: dateStr,
      total_revenue: 0,
      total_hours_worked: 0,
      staff_total: 0,
      notes: '',
      weather_code: null,
      weather_temp_max: null,
      weather_temp_min: null,
      weather_precip_mm: null,
      weather_wind_max_kmh: null,
      shifts: SHIFT_NAMES.map(defaultShift)
    };
  }
  function normalizeDayData(apiData) {
    var shifts = (apiData.shifts || []).slice();
    SHIFT_NAMES.forEach(function (name, i) {
      var found = shifts.find(function (s) { return (s.shift_name || s.ShiftName) === name; });
      if (found) {
        shifts[i] = {
          shift_name: name,
          revenue: found.revenue ?? found.Revenue ?? 0,
          hours_worked: found.hours_worked ?? found.HoursWorked ?? 0,
          staff_floor: found.staff_floor ?? found.StaffFloor ?? 0,
          staff_kitchen: found.staff_kitchen ?? found.StaffKitchen ?? 0,
          hours_sala_estimated: found.hours_sala_estimated ?? found.HoursSalaEstimated ?? null,
          hours_cocina_estimated: found.hours_cocina_estimated ?? found.HoursCocinaEstimated ?? null,
          revenue_per_waiter_sala: found.revenue_per_waiter_sala ?? found.RevenuePerWaiterSala ?? null,
          difficulty_score: found.difficulty_score ?? found.DifficultyScore ?? null,
          comfort_level: found.comfort_level ?? found.ComfortLevel ?? null,
          revenue_per_waiter_cocina: found.revenue_per_waiter_cocina ?? found.RevenuePerWaiterCocina ?? null,
          difficulty_score_kitchen: found.difficulty_score_kitchen ?? found.DifficultyScoreKitchen ?? null,
          comfort_level_kitchen: found.comfort_level_kitchen ?? found.ComfortLevelKitchen ?? null,
          feedback_q1: found.feedback_q1 ?? found.FeedbackQ1 ?? null,
          feedback_q2: found.feedback_q2 ?? found.FeedbackQ2 ?? null,
          feedback_q3: found.feedback_q3 ?? found.FeedbackQ3 ?? null,
          feedback_q4: found.feedback_q4 ?? found.FeedbackQ4 ?? null,
          feedback_q5: found.feedback_q5 ?? found.FeedbackQ5 ?? null,
          weather_code: found.weather_code ?? found.WeatherCode ?? null,
          weather_temp_avg: found.weather_temp_avg ?? found.WeatherTempAvg ?? null,
          weather_precip_mm: found.weather_precip_mm ?? found.WeatherPrecipMm ?? null,
          weather_wind_max_kmh: found.weather_wind_max_kmh ?? found.WeatherWindMaxKmh ?? null
        };
      } else shifts[i] = defaultShift(name);
    });
    return {
      id: apiData.id,
      date: apiData.date || apiData.Date,
      total_revenue: apiData.total_revenue ?? apiData.TotalRevenue ?? 0,
      total_hours_worked: apiData.total_hours_worked ?? apiData.TotalHoursWorked ?? 0,
      staff_total: apiData.staff_total ?? apiData.StaffTotal ?? 0,
      notes: apiData.notes ?? apiData.Notes ?? '',
      weather_code: apiData.weather_code ?? apiData.WeatherCode ?? null,
      weather_temp_max: apiData.weather_temp_max ?? apiData.WeatherTempMax ?? null,
      weather_temp_min: apiData.weather_temp_min ?? apiData.WeatherTempMin ?? null,
      weather_precip_mm: apiData.weather_precip_mm ?? apiData.WeatherPrecipMm ?? null,
      weather_wind_max_kmh: apiData.weather_wind_max_kmh ?? apiData.WeatherWindMaxKmh ?? null,
      shifts: shifts
    };
  }

  function weatherEmoji(code) {
    if (code == null) return '—';
    code = Number(code);
    if (code === 0) return '☀️';
    if (code >= 1 && code <= 3) return '⛅';
    if (code === 45 || code === 48) return '🌫️';
    if ((code >= 51 && code <= 67) || (code >= 80 && code <= 82)) return '🌧️';
    if (code >= 71 && code <= 77) return '❄️';
    if (code >= 95) return '⛈️';
    return '🌦️';
  }
  function weatherTextDay(day) {
    if (!day) return '—';
    var hasAny = (day.weather_code != null) || (day.weather_temp_max != null) || (day.weather_temp_min != null) || (day.weather_precip_mm != null) || (day.weather_wind_max_kmh != null);
    if (!hasAny) return '—';
    var parts = [];
    parts.push(weatherEmoji(day.weather_code));
    if (day.weather_temp_max != null || day.weather_temp_min != null) {
      var tMax = day.weather_temp_max != null ? Number(day.weather_temp_max).toFixed(0) : '—';
      var tMin = day.weather_temp_min != null ? Number(day.weather_temp_min).toFixed(0) : '—';
      parts.push(tMax + '/' + tMin + '°C');
    }
    if (day.weather_precip_mm != null) parts.push(Number(day.weather_precip_mm).toFixed(0) + 'mm');
    if (day.weather_wind_max_kmh != null) parts.push(Number(day.weather_wind_max_kmh).toFixed(0) + 'km/h');
    return parts.join(' · ');
  }
  function weatherTextShift(shift, fallbackDay) {
    if (!shift) return weatherTextDay(fallbackDay);
    var hasAny = (shift.weather_code != null) || (shift.weather_temp_avg != null) || (shift.weather_precip_mm != null) || (shift.weather_wind_max_kmh != null);
    if (!hasAny) return weatherTextDay(fallbackDay);
    var parts = [];
    parts.push(weatherEmoji(shift.weather_code));
    if (shift.weather_temp_avg != null) parts.push(Number(shift.weather_temp_avg).toFixed(0) + '°C');
    if (shift.weather_precip_mm != null) parts.push(Number(shift.weather_precip_mm).toFixed(0) + 'mm');
    if (shift.weather_wind_max_kmh != null) parts.push(Number(shift.weather_wind_max_kmh).toFixed(0) + 'km/h');
    return parts.join(' · ');
  }
  function updateWeatherUI() {
    var el = document.getElementById('registro-weather');
    if (!el) return;
    var day = state.dayData;
    var shift = day && day.shifts ? day.shifts[state.activeShiftIndex] : null;
    el.textContent = weatherTextShift(shift, day);
  }

  var state = { dayData: null, activeShiftIndex: 0, horasPorTurno: 4 };

  function sumShiftsRevenue() {
    if (!state.dayData) return 0;
    return state.dayData.shifts.reduce(function (s, sh) { return s + (parseFloat(sh.revenue) || 0); }, 0);
  }
  function sumShiftsHours() {
    if (!state.dayData) return 0;
    return state.dayData.shifts.reduce(function (s, sh) { return s + (parseFloat(sh.hours_worked) || 0); }, 0);
  }

  function getShiftScore(shift) {
    return (global.LUCAS_SCORING && global.LUCAS_SCORING.scoreFromShift(shift)) || { sgt: 0, estado: '—', resumenNivel3: '', completo: false };
  }
  function getDayResumen() {
    if (!state.dayData || !state.dayData.shifts.length) return { sgd: 0, estado: '—', texto: 'Sin datos de turnos. Día equilibrado.' };
    var scores = state.dayData.shifts.map(function (s) { return getShiftScore(s); });
    var withSgt = scores.filter(function (s) { return s.sgt >= 6; });
    var avg = withSgt.length ? withSgt.reduce(function (a, s) { return a + s.sgt; }, 0) / withSgt.length : 0;
    var estado = avg < 11 ? 'Tranquilo' : avg < 15 ? 'Equilibrado' : avg < 19 ? 'Productivo' : avg < 23 ? 'Exigente' : 'Crítico';
    var texto = withSgt.length ? 'Promedio SGT turnos: ' + avg.toFixed(1) + '. ' + estado + '.' : 'Sin datos de turnos. Día equilibrado.';
    return { sgd: avg.toFixed(1), estado: estado, texto: texto };
  }

  function collectFormFromShift(idx) {
    var s = state.dayData && state.dayData.shifts[idx];
    if (!s) return;
    var rev = document.getElementById('registro-revenue');
    var hrs = document.getElementById('registro-hours');
    var floor = document.getElementById('registro-staff_floor');
    var kitchen = document.getElementById('registro-staff_kitchen');
    var q1 = document.querySelector('input[name="registro_q1"]:checked');
    var q2 = document.querySelector('input[name="registro_q2"]:checked');
    var q3 = document.querySelector('input[name="registro_q3"]:checked');
    var q4 = document.querySelector('input[name="registro_q4"]:checked');
    var q5 = document.querySelector('input[name="registro_q5"]:checked');
    s.revenue = rev ? parseFloat(rev.value) || 0 : 0;
    s.hours_worked = hrs ? parseFloat(hrs.value) || 0 : 0;
    s.staff_floor = floor ? Math.min(99, Math.max(0, parseInt(floor.value, 10) || 0)) : 0;
    s.staff_kitchen = kitchen ? Math.min(99, Math.max(0, parseInt(kitchen.value, 10) || 0)) : 0;
    s.feedback_q1 = q1 ? q1.value : null;
    s.feedback_q2 = q2 ? q2.value : null;
    s.feedback_q3 = q3 ? q3.value : null;
    s.feedback_q4 = q4 ? q4.value : null;
    s.feedback_q5 = q5 ? q5.value : null;
  }
  function fillFormForShift(idx) {
    var s = state.dayData && state.dayData.shifts[idx];
    if (!s) return;
    var rev = document.getElementById('registro-revenue');
    var hrs = document.getElementById('registro-hours');
    var floor = document.getElementById('registro-staff_floor');
    var kitchen = document.getElementById('registro-staff_kitchen');
    if (rev) rev.value = s.revenue ?? 0;
    if (hrs) hrs.value = s.hours_worked ?? 0;
    if (floor) floor.value = s.staff_floor ?? 0;
    if (kitchen) kitchen.value = s.staff_kitchen ?? 0;
    ['q1', 'q2', 'q3', 'q4', 'q5'].forEach(function (q, i) {
      var val = s['feedback_q' + (i + 1)];
      var radio = document.querySelector('input[name="registro_' + q + '"][value="' + (val || '').replace(/"/g, '&quot;') + '"]');
      if (radio) radio.checked = true;
      else document.querySelectorAll('input[name="registro_' + q + '"]').forEach(function (r) { r.checked = false; });
    });
  }

  function updateSummaryBoxes() {
    var totalRev = document.getElementById('registro-total-revenue');
    var sumTurnos = document.getElementById('registro-sum-turnos');
    var totalHrs = document.getElementById('registro-total-hours');
    if (state.dayData) {
      // El usuario edita por turno, así que el total mostrado debe ser la suma de turnos (no un total "antiguo" del API).
      var sumRev = sumShiftsRevenue();
      var sumHrs = sumShiftsHours();
      if (totalRev) totalRev.textContent = sumRev.toFixed(0);
      if (sumTurnos) sumTurnos.textContent = 'Suma turnos: ' + sumRev.toFixed(2) + ' €';
      if (totalHrs) totalHrs.textContent = sumHrs.toFixed(1);
    } else {
      if (totalRev) totalRev.textContent = '0';
      if (sumTurnos) sumTurnos.textContent = 'Suma turnos: 0.00 €';
      if (totalHrs) totalHrs.textContent = '0.0';
    }
  }
  function updateTurnScoreAndResumen() {
    var s = state.dayData && state.dayData.shifts[state.activeShiftIndex];
    var score = getShiftScore(s);
    var sgtEl = document.getElementById('registro-sgt-estado');
    var resumenTurno = document.getElementById('registro-resumen-turno');
    if (sgtEl) sgtEl.textContent = 'SGT: ' + (score.sgt || 0) + '  Estado: ' + (score.estado || '—');
    if (resumenTurno) resumenTurno.textContent = score.completo ? score.resumenNivel3 : 'Completa Volumen, Ritmo, Margen y Dificultad para ver el resumen del turno.';
    var dayRes = getDayResumen();
    var resumenDia = document.getElementById('registro-resumen-dia');
    if (resumenDia) resumenDia.textContent = 'SGD: ' + dayRes.sgd + '  Estado: ' + dayRes.estado + ' — ' + dayRes.texto;
    updateWeatherUI();
  }
  function updateHorasCalculado() {
    var s = state.dayData && state.dayData.shifts[state.activeShiftIndex];
    var h = document.getElementById('registro-horas-calc');
    if (!h) return;
    var sum = s ? (s.staff_floor || 0) + (s.staff_kitchen || 0) : 0;
    h.value = (sum * state.horasPorTurno).toFixed(1);
  }

  function getFormHtml() {
    var s = state.dayData && state.dayData.shifts[state.activeShiftIndex];
    var score = getShiftScore(s);
    var horasCalc = s ? ((s.staff_floor || 0) + (s.staff_kitchen || 0)) * state.horasPorTurno : 0;
    return '<div class="registro-card">' +
      '<h3 class="registro-card-title">Datos del turno</h3>' +
      '<p id="registro-sgt-estado" class="registro-sgt-estado">SGT: ' + (score.sgt || 0) + '  Estado: ' + (score.estado || '—') + '</p>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="registro-revenue">Facturación (€)</label><input type="number" id="registro-revenue" step="0.01" min="0" value="' + (s ? s.revenue : 0) + '" title="Facturación del turno en euros" /></div>' +
      '<div class="form-group"><label for="registro-staff_floor" title="Nº de personas en sala en este turno">Personal sala</label><input type="number" id="registro-staff_floor" min="0" max="99" value="' + (s ? s.staff_floor : 0) + '" title="Nº de personas en sala" /></div>' +
      '<div class="form-group"><label for="registro-horas-calc" title="(Sala + Cocina) × horas por turno (Configuración)">Horas equipo (calculado)</label><input type="number" id="registro-horas-calc" step="0.1" readonly value="' + horasCalc.toFixed(1) + '" title="Horas de equipo del turno según personal" /></div>' +
      '<div class="form-group"><label for="registro-staff_kitchen" title="Nº de personas en cocina en este turno">Personal cocina</label><input type="number" id="registro-staff_kitchen" min="0" max="99" value="' + (s ? s.staff_kitchen : 0) + '" title="Nº de personas en cocina" /></div>' +
      '<div class="form-group"><label for="registro-hours" title="Horas reales trabajadas (del Excel o introducidas)">Horas reales</label><input type="number" id="registro-hours" step="0.1" min="0" value="' + (s ? s.hours_worked : 0) + '" title="Horas reales del turno" /></div>' +
      '</div>' +
      (s && (s.hours_sala_estimated != null || s.hours_cocina_estimated != null) && (s.hours_worked || 0) > 0 ? '<p class="registro-reparto-hint" title="Reparto de las horas reales según el personal indicado (sala/cocina)">Reparto estimado: sala ' + (s.hours_sala_estimated != null ? Number(s.hours_sala_estimated).toFixed(1) : '0') + ' h, cocina ' + (s.hours_cocina_estimated != null ? Number(s.hours_cocina_estimated).toFixed(1) : '0') + ' h</p>' : '') +
      (s && (s.revenue_per_waiter_sala != null || s.difficulty_score != null) ? '<p class="registro-comfort-hint" title="Facturación por persona en sala y dificultad del turno (1–5) para análisis de límite cómodo">' + (s.revenue_per_waiter_sala != null ? 'Facturación por personal de sala: ' + Number(s.revenue_per_waiter_sala).toFixed(0) + ' €' : '') + (s.revenue_per_waiter_sala != null && s.difficulty_score != null ? ' · ' : '') + (s.difficulty_score != null ? ' Dificultad: ' + Number(s.difficulty_score).toFixed(1) + '/5' : '') + (s.comfort_level ? ' · ' + s.comfort_level : '') + '</p>' : '') +
      (s && (s.revenue_per_waiter_cocina != null || s.difficulty_score_kitchen != null) ? '<p class="registro-comfort-hint" title="Facturación por persona en cocina y dificultad cocina (Q5) para análisis de límite cómodo">' + (s.revenue_per_waiter_cocina != null ? 'Facturación por personal de cocina: ' + Number(s.revenue_per_waiter_cocina).toFixed(0) + ' €' : '') + (s.revenue_per_waiter_cocina != null && s.difficulty_score_kitchen != null ? ' · ' : '') + (s.difficulty_score_kitchen != null ? ' Dificultad cocina: ' + Number(s.difficulty_score_kitchen).toFixed(1) + '/5' : '') + (s.comfort_level_kitchen ? ' · ' + s.comfort_level_kitchen : '') + '</p>' : '') +
      '</div></div>' +
      '<div class="registro-card">' +
      '<h3 class="registro-card-title">Feedback del turno (V, R, M, D)</h3>' +
      '<div class="registro-blocks-grid">' +
      '<div class="registro-block"><h4 class="registro-q-title">Volumen: ¿Cuánto trabajo hubo en este turno?</h4><div class="registro-radios">' + radioGroup('registro_q1', Q1_OPTIONS, s ? s.feedback_q1 : '') + '</div></div>' +
      '<div class="registro-block"><h4 class="registro-q-title">Ritmo: ¿Cómo fue el ritmo de entradas de clientes?</h4><div class="registro-radios">' + radioGroup('registro_q2', Q2_OPTIONS, s ? s.feedback_q2 : '') + '</div></div>' +
      '<div class="registro-block"><h4 class="registro-q-title">Margen: ¿Cuánto margen hubo para ir adelantado?</h4><div class="registro-radios">' + radioGroup('registro_q3', Q3_OPTIONS, s ? s.feedback_q3 : '') + '</div></div>' +
      '<div class="registro-block"><h4 class="registro-q-title">Dificultad: ¿Qué tan difícil fue el turno en general?</h4><div class="registro-radios">' + radioGroup('registro_q4', Q4_OPTIONS, s ? s.feedback_q4 : '') + '</div></div>' +
      '<div class="registro-block"><h4 class="registro-q-title">Q5 — Dificultad del turno en cocina</h4><div class="registro-radios">' + radioGroup('registro_q5', Q4_OPTIONS, s ? s.feedback_q5 : '') + '</div></div>' +
      '</div></div>' +
      '<div class="registro-resumen-turno" id="registro-resumen-turno">' + (score.completo ? score.resumenNivel3 : 'Completa Volumen, Ritmo, Margen y Dificultad para ver el resumen del turno.') + '</div>' +
      '<div class="registro-resumen-dia" id="registro-resumen-dia">' + getDayResumen().texto + '</div>';
  }

  function render(container) {
    var dateStr = state.dayData ? state.dayData.date : todayStr();
    var urlDate = new URLSearchParams(window.location.search).get('date');
    if (urlDate) dateStr = normalizeDateStr(urlDate);
    var weekNum = weekNumber(dateStr);
    var tabsHtml = '<div class="registro-shift-tabs">' + SHIFT_LABELS.map(function (label, i) {
      return '<button type="button" class="registro-shift-tab' + (i === state.activeShiftIndex ? ' active' : '') + '" data-shift="' + i + '">' + label + '</button>';
    }).join('') + '</div>';
    var navHtml = '<div class="registro-header-row">' +
      '<div class="registro-header-left">' +
      '<h2 class="view-title">Registro de Ejecución</h2>' +
      '<p class="registro-subtitle"><span class="registro-dia-label">Día laborable</span> · <span id="registro-weather" class="registro-weather">—</span></p>' +
      '<p id="registro-instruction" class="registro-instruction">Sin datos para este día. Rellena los turnos y guarda.</p>' +
      '</div>' +
      tabsHtml +
      '<div class="registro-header-right">' +
      '<div class="date-selector-bar">' +
      '<span id="registro-day-label" class="date-selector-day">' + getDayName(dateStr) + '</span>' +
      '<div class="date-selector-group">' +
      '<button type="button" id="registro-prev" class="date-selector-arrow" title="Día anterior">◀</button>' +
      '<div class="date-selector-input-wrap">' +
      '<input type="date" id="registro-fecha" value="' + dateStr + '" class="date-selector-input-hidden" />' +
      '<span id="registro-fecha-display" class="date-selector-display" title="Seleccionar fecha">' + formatDateDisplay(dateStr) + '</span>' +
      '</div>' +
      '<button type="button" id="registro-next" class="date-selector-arrow" title="Día siguiente">▶</button>' +
      '</div></div></div></div>';
    var summaryHtml = '<div class="registro-summary-boxes">' +
      '<div class="registro-summary-card registro-summary-revenue">' +
      '<div class="registro-summary-icon">€</div>' +
      '<div class="registro-summary-label">Facturación total (€)</div>' +
      '<div class="registro-summary-value" id="registro-total-revenue">0</div>' +
      '<div class="registro-summary-sub" id="registro-sum-turnos">Suma turnos: 0.00 €</div>' +
      '</div>' +
      '<div class="registro-summary-card registro-summary-hours">' +
      '<div class="registro-summary-icon">🕐</div>' +
      '<div class="registro-summary-label">Horas trabajadas</div>' +
      '<div class="registro-summary-value" id="registro-total-hours">0.0</div>' +
      '</div></div>';
    var formWrap = '<div id="registro-form-wrap">' + (state.dayData ? getFormHtml() : '<p class="loading">Cargando…</p>') + '</div>';
    var notesHtml = '<div class="registro-card">' +
      '<h3 class="registro-card-title">Notas del día (opcional)</h3>' +
      '<textarea id="registro-notes" rows="3" placeholder="Notas...">' + (state.dayData ? (state.dayData.notes || '') : '') + '</textarea>' +
      '</div>';
    var btnHtml = '<button type="button" id="registro-guardar" class="btn-primary btn-large">Guardar ejecución</button>';
    var msgHtml = '<p id="registro-message" class="registro-msg hidden"></p>';
    container.innerHTML = '<div class="card registro-card-outer">' + navHtml + summaryHtml + formWrap + notesHtml + btnHtml + msgHtml + '</div>';
    bind(container);
    updateSummaryBoxes();
    updateTurnScoreAndResumen();
    if (!state.dayData) loadDay(document.getElementById('registro-fecha').value);
    auth.fetchWithAuth('/api/settings').then(function (r) {
      if (r.ok) return r.text().then(function (t) { try { return t && t.trim() ? JSON.parse(t) : {}; } catch (e) { return {}; } });
      return {};
    }).then(function (data) {
      state.horasPorTurno = parseFloat(data.HorasPorTurno, 10) || 4;
      updateHorasCalculado();
    }).catch(function () {});
  }

  function bind(container) {
    var wrap = document.getElementById('registro-form-wrap');
    var fechaInput = document.getElementById('registro-fecha');
    var fechaDisplay = document.getElementById('registro-fecha-display');
    var dayLabel = document.getElementById('registro-day-label');
    function updateDateSelector() {
      if (fechaInput) {
        var v = fechaInput.value;
        if (fechaDisplay) fechaDisplay.textContent = formatDateDisplay(v);
        if (dayLabel) dayLabel.textContent = getDayName(v);
      }
    }
    if (fechaDisplay && fechaInput) fechaDisplay.addEventListener('click', function () { fechaInput.click(); });
    if (fechaInput) fechaInput.addEventListener('change', function () { updateDateSelector(); loadDay(fechaInput.value); });
    document.getElementById('registro-prev') && document.getElementById('registro-prev').addEventListener('click', function () {
      if (!state.dayData) return;
      collectFormFromShift(state.activeShiftIndex);
      var prev = addDays(state.dayData.date, -1);
      fechaInput.value = prev;
      updateDateSelector();
      loadDay(prev);
    });
    document.getElementById('registro-next') && document.getElementById('registro-next').addEventListener('click', function () {
      if (!state.dayData) return;
      collectFormFromShift(state.activeShiftIndex);
      var next = addDays(state.dayData.date, 1);
      fechaInput.value = next;
      updateDateSelector();
      loadDay(next);
    });
    container.querySelectorAll('.registro-shift-tab').forEach(function (tab) {
      tab.addEventListener('click', function () {
        var idx = parseInt(tab.getAttribute('data-shift'), 10);
        if (idx === state.activeShiftIndex) return;
        collectFormFromShift(state.activeShiftIndex);
        state.activeShiftIndex = idx;
        container.querySelectorAll('.registro-shift-tab').forEach(function (t) { t.classList.toggle('active', parseInt(t.getAttribute('data-shift'), 10) === idx); });
        wrap.innerHTML = getFormHtml();
        fillFormForShift(idx);
        updateTurnScoreAndResumen();
        updateHorasCalculado();
        bindRadioAndInputs(container);
      });
    });
    document.getElementById('registro-guardar') && document.getElementById('registro-guardar').addEventListener('click', save);
    bindRadioAndInputs(container);
  }

  function bindRadioAndInputs(container) {
    var wrap = document.getElementById('registro-form-wrap');
    if (!wrap) return;
    ['registro_q1', 'registro_q2', 'registro_q3', 'registro_q4', 'registro_q5'].forEach(function (name) {
      wrap.querySelectorAll('input[name="' + name + '"]').forEach(function (radio) {
        radio.addEventListener('change', function () { collectFormFromShift(state.activeShiftIndex); updateTurnScoreAndResumen(); });
      });
    });
    var floor = document.getElementById('registro-staff_floor');
    var kitchen = document.getElementById('registro-staff_kitchen');
    if (floor) floor.addEventListener('input', function () { collectFormFromShift(state.activeShiftIndex); updateHorasCalculado(); updateSummaryBoxes(); });
    if (kitchen) kitchen.addEventListener('input', function () { collectFormFromShift(state.activeShiftIndex); updateHorasCalculado(); updateSummaryBoxes(); });
    var rev = document.getElementById('registro-revenue');
    var hrs = document.getElementById('registro-hours');
    if (rev) rev.addEventListener('input', function () { collectFormFromShift(state.activeShiftIndex); updateSummaryBoxes(); });
    if (hrs) hrs.addEventListener('input', function () { collectFormFromShift(state.activeShiftIndex); updateSummaryBoxes(); });
  }

  function loadDay(dateStr) {
    dateStr = normalizeDateStr(dateStr);
    state.dayData = null;
    state.activeShiftIndex = 0;
    var wrap = document.getElementById('registro-form-wrap');
    var container = document.getElementById('dashboard-content');
    var instruction = document.getElementById('registro-instruction');
    var fi = document.getElementById('registro-fecha');
    if (fi) fi.value = dateStr;
    if (wrap) wrap.innerHTML = '<p class="loading">Cargando…</p>';
    if (instruction) instruction.textContent = 'Cargando…';
    auth.fetchWithAuth('/api/execution/' + dateStr).then(function (res) {
      if (res.status === 404) {
        state.dayData = defaultDayData(dateStr);
        if (instruction) instruction.textContent = 'Sin datos para este día. Rellena los turnos y guarda.';
        if (wrap) wrap.innerHTML = getFormHtml();
        var notesEl = document.getElementById('registro-notes');
        if (notesEl) notesEl.value = '';
        if (container) { bind(container); bindRadioAndInputs(container); }
        updateSummaryBoxes();
        updateTurnScoreAndResumen();
        updateHorasCalculado();
        updateWeatherUI();
        var fi = document.getElementById('registro-fecha');
        if (fi) fi.value = dateStr;
        var fd = document.getElementById('registro-fecha-display');
        if (fd) fd.textContent = formatDateDisplay(dateStr);
        var dl = document.getElementById('registro-day-label');
        if (dl) dl.textContent = getDayName(dateStr);
        return;
      }
      if (!res.ok) throw new Error('Error al cargar');
      return res.json();
    }).then(function (data) {
      if (!data) return;
      state.dayData = normalizeDayData(data);
      var fi = document.getElementById('registro-fecha');
      if (fi) fi.value = state.dayData.date;
      var fd = document.getElementById('registro-fecha-display');
      if (fd) fd.textContent = formatDateDisplay(state.dayData.date);
      var dl = document.getElementById('registro-day-label');
      if (dl) dl.textContent = getDayName(state.dayData.date);
      if (instruction) instruction.textContent = 'Datos cargados. Edita y guarda.';
      if (wrap) wrap.innerHTML = getFormHtml();
      fillFormForShift(state.activeShiftIndex);
      var notesEl = document.getElementById('registro-notes');
      if (notesEl) notesEl.value = state.dayData.notes || '';
      if (container) { bind(container); bindRadioAndInputs(container); }
      updateSummaryBoxes();
      updateTurnScoreAndResumen();
      updateHorasCalculado();
      updateWeatherUI();
    }).catch(function (err) {
      if (wrap) wrap.innerHTML = '<p class="error-msg">' + (err.message || 'Error al cargar el día') + '</p>';
      if (instruction) instruction.textContent = 'Error al cargar.';
    });
  }

  function save() {
    collectFormFromShift(state.activeShiftIndex);
    var notesEl = document.getElementById('registro-notes');
    if (state.dayData) state.dayData.notes = notesEl ? notesEl.value : '';
    var day = state.dayData;
    if (!day) return;
    // Registro controla los datos por turno: totales siempre coherentes con la suma.
    var totalRevenue = sumShiftsRevenue();
    var totalHours = sumShiftsHours();
    var staffTotal = day.shifts.reduce(function (sum, s) { return sum + (s.staff_floor || 0) + (s.staff_kitchen || 0); }, 0);
    var payload = {
      total_revenue: totalRevenue,
      total_hours_worked: totalHours,
      staff_total: staffTotal,
      notes: day.notes || '',
      shifts: day.shifts.map(function (s) {
        return {
          shift_name: s.shift_name,
          revenue: s.revenue ?? 0,
          hours_worked: s.hours_worked ?? 0,
          staff_floor: s.staff_floor ?? 0,
          staff_kitchen: s.staff_kitchen ?? 0,
          feedback_q1: s.feedback_q1 || null,
          feedback_q2: s.feedback_q2 || null,
          feedback_q3: s.feedback_q3 || null,
          feedback_q4: s.feedback_q4 || null,
          feedback_q5: s.feedback_q5 || null
        };
      })
    };
    var msgEl = document.getElementById('registro-message');
    function setMsg(t, ok) { if (msgEl) { msgEl.textContent = t || ''; msgEl.classList.toggle('hidden', !t); msgEl.className = 'registro-msg' + (t ? (ok ? ' success-msg' : ' error-msg') : '') + (t ? '' : ' hidden'); } }
    setMsg('Guardando…', true);
    if (day.id) {
      auth.fetchWithAuth('/api/execution/' + day.date, { method: 'PATCH', body: JSON.stringify(payload) }).then(function (r) {
        if (r.status === 401) return Promise.reject(new Error('Sesión expirada'));
        if (!r.ok) return r.text().then(function (t) { var d = null; try { d = t && t.trim() ? JSON.parse(t) : {}; } catch (e) {} throw new Error((d && d.message) || 'Error al guardar'); });
        return r.text().then(function (t) { try { return t && t.trim() ? JSON.parse(t) : null; } catch (e) { return null; } });
      }).then(function (data) {
        if (data) {
          state.dayData = normalizeDayData(data);
          var wrap = document.getElementById('registro-form-wrap');
          var container = document.getElementById('dashboard-content');
          if (wrap) wrap.innerHTML = getFormHtml();
          fillFormForShift(state.activeShiftIndex);
          if (container) { bind(container); bindRadioAndInputs(container); }
          updateTurnScoreAndResumen();
          updateHorasCalculado();
          updateWeatherUI();
        }
        setMsg('Guardado correctamente.', true);
        updateSummaryBoxes();
      }).catch(function (e) { setMsg(e.message || 'Error al guardar', false); });
    } else {
      auth.fetchWithAuth('/api/execution', { method: 'POST', body: JSON.stringify({ date: day.date, total_revenue: totalRevenue, total_hours_worked: totalHours, staff_total: staffTotal, notes: day.notes || '', shifts: payload.shifts }) }).then(function (r) {
        if (r.status === 401) return Promise.reject(new Error('Sesión expirada'));
        if (r.status === 409) return Promise.reject(new Error('El día ya existe'));
        if (!r.ok) return r.text().then(function (t) { var d = null; try { d = t && t.trim() ? JSON.parse(t) : {}; } catch (e) {} throw new Error((d && d.message) || 'Error al crear'); });
        return r.json();
      }).then(function (data) {
        if (data) state.dayData = normalizeDayData(data);
        setMsg('Día creado correctamente.', true);
        updateSummaryBoxes();
        loadDay(day.date);
      }).catch(function (e) { setMsg(e.message || 'Error al guardar', false); });
    }
  }

  global.LUCAS_REGISTRO_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
