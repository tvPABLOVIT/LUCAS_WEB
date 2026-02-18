(function (global) {
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
  function getDayName(dateStr) {
    if (!dateStr) return '';
    var d = new Date(dateStr + 'T12:00:00');
    var names = ['DOMINGO', 'LUNES', 'MARTES', 'MIÉRCOLES', 'JUEVES', 'VIERNES', 'SÁBADO'];
    return names[d.getDay()];
  }
  function formatDateDisplay(dateStr) {
    if (!dateStr) return '';
    var d = new Date(dateStr + 'T12:00:00');
    return String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0') + '/' + d.getFullYear();
  }
  function getShiftByCurrentTime() {
    var h = new Date().getHours();
    if (h < 16) return 0;
    if (h < 20) return 1;
    return 2;
  }
  /** Obtiene la fecha del query string cuando la navegación es por hash (#preguntas?date=yyyy-MM-dd). */
  function getDateFromHash() {
    var hash = (window.location.hash || '');
    var q = hash.indexOf('?');
    var search = q >= 0 ? hash.slice(q) : '';
    var date = search ? new URLSearchParams(search).get('date') : null;
    if (date && /^\d{4}-\d{2}-\d{2}$/.test(date)) return date;
    return (new URLSearchParams(window.location.search).get('date')) || null;
  }
  function radioGroup(name, options, selected) {
    var s = (selected || '').trim();
    return options.map(function (o) {
      var v = o.trim();
      var id = name + '-' + v.replace(/\s+/g, '_').replace(/,/g, '');
      return '<label class="preguntas-radio"><input type="radio" name="' + name + '" value="' + v.replace(/"/g, '&quot;') + '"' + (v === s ? ' checked' : '') + ' id="' + id + '" /> ' + o + '</label>';
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
      recorded_by: null,
      edited_by: null,
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
  function trimFeedback(v) {
    if (v == null) return null;
    var s = (typeof v === 'string' ? v : String(v)).trim();
    return s.length ? s : null;
  }
  function hasFeedbackValue(v) {
    return !!(trimFeedback(v));
  }
  function getShiftDataByIndex(index) {
    if (!state.dayData || !state.dayData.shifts) return null;
    var arr = state.dayData.shifts;
    var name = SHIFT_NAMES[index];
    if (!name) return null;
    var byIndex = arr[index];
    var sn = (byIndex && (byIndex.shift_name ?? byIndex.ShiftName ?? '')).toString().trim();
    if (sn.toLowerCase() === name.toLowerCase()) return byIndex;
    var found = arr.find(function (s) {
      var n = (s.shift_name ?? s.ShiftName ?? '').toString().trim();
      return n.toLowerCase() === name.toLowerCase();
    });
    return found || byIndex || null;
  }

  function normalizeDayData(apiData) {
    var rawShifts = (apiData.shifts || []).slice();
    var names = SHIFT_NAMES.slice();
    var shifts = names.map(function (name, i) {
      var found = rawShifts.find(function (s) {
        var sn = (s.shift_name ?? s.ShiftName ?? '').toString().trim();
        return sn.toLowerCase() === name.toLowerCase();
      });
      if (found) {
        return {
          shift_name: name,
          revenue: found.revenue ?? found.Revenue ?? 0,
          hours_worked: found.hours_worked ?? found.HoursWorked ?? 0,
          staff_floor: found.staff_floor ?? found.StaffFloor ?? 0,
          staff_kitchen: found.staff_kitchen ?? found.StaffKitchen ?? 0,
          feedback_q1: trimFeedback(found.feedback_q1 ?? found.FeedbackQ1 ?? found.feedbackQ1),
          feedback_q2: trimFeedback(found.feedback_q2 ?? found.FeedbackQ2 ?? found.feedbackQ2),
          feedback_q3: trimFeedback(found.feedback_q3 ?? found.FeedbackQ3 ?? found.feedbackQ3),
          feedback_q4: trimFeedback(found.feedback_q4 ?? found.FeedbackQ4 ?? found.feedbackQ4),
          feedback_q5: trimFeedback(found.feedback_q5 ?? found.FeedbackQ5 ?? found.feedbackQ5),
          recorded_by: found.recorded_by ?? found.RecordedBy,
          edited_by: found.edited_by ?? found.EditedBy,
          weather_code: found.weather_code ?? found.WeatherCode ?? null,
          weather_temp_avg: found.weather_temp_avg ?? found.WeatherTempAvg ?? null,
          weather_precip_mm: found.weather_precip_mm ?? found.WeatherPrecipMm ?? null,
          weather_wind_max_kmh: found.weather_wind_max_kmh ?? found.WeatherWindMaxKmh ?? null
        };
      }
      return defaultShift(name);
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
  function isUserOnlyView() {
    var role = (auth && auth.getRole) ? auth.getRole() : '';
    var fullWebRoles = ['admin', 'manager', 'master'];
    return !(role && fullWebRoles.indexOf(String(role).toLowerCase()) !== -1);
  }
  function weatherTextOrUnavailable(shift, fallbackDay) {
    var t = weatherTextShift(shift, fallbackDay);
    if (t === '—' || !t) return isUserOnlyView() ? 'Clima no disponible' : 'No hay datos (Configuración → ubicación o prueba con hoy)';
    return t;
  }
  function dateOnly(str) {
    if (str == null) return '';
    var s = String(str);
    return s.length >= 10 ? s.substring(0, 10) : s;
  }
  function fetchWeatherForDate(dateStr, callback) {
    auth.fetchWithAuth('/api/weather/for-date?date=' + encodeURIComponent(dateStr))
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (data) {
        if (!data || !state.dayData) return;
        if (dateOnly(state.dayData.date) !== dateOnly(dateStr)) return;
        state.weatherUnavailableReason = data.reason || null;
        var shifts = state.dayData.shifts || [];
        var dayFromApi = data.day || data.Day;
        (data.shifts || data.Shifts || []).forEach(function (ws) {
          var name = (ws.shift_name || ws.ShiftName || '').toString().trim();
          var found = shifts.find(function (s) { return (s.shift_name || '').toString().trim().toLowerCase() === name.toLowerCase(); });
          if (found) {
            var wc = ws.weather_code ?? ws.WeatherCode; var wt = ws.weather_temp_avg ?? ws.WeatherTempAvg; var wp = ws.weather_precip_mm ?? ws.WeatherPrecipMm; var ww = ws.weather_wind_max_kmh ?? ws.WeatherWindMaxKmh;
            if (found.weather_code == null && wc != null) found.weather_code = wc;
            if (found.weather_temp_avg == null && wt != null) found.weather_temp_avg = wt;
            if (found.weather_precip_mm == null && wp != null) found.weather_precip_mm = wp;
            if (found.weather_wind_max_kmh == null && ww != null) found.weather_wind_max_kmh = ww;
          }
        });
        if (state.dayData.weather_code == null && dayFromApi) {
          state.dayData.weather_code = dayFromApi.weather_code ?? dayFromApi.WeatherCode ?? state.dayData.weather_code;
          state.dayData.weather_temp_max = dayFromApi.weather_temp_max ?? dayFromApi.WeatherTempMax ?? state.dayData.weather_temp_max;
          state.dayData.weather_temp_min = dayFromApi.weather_temp_min ?? dayFromApi.WeatherTempMin ?? state.dayData.weather_temp_min;
          state.dayData.weather_precip_mm = dayFromApi.weather_precip_mm ?? dayFromApi.WeatherPrecipMm ?? state.dayData.weather_precip_mm;
          state.dayData.weather_wind_max_kmh = dayFromApi.weather_wind_max_kmh ?? dayFromApi.WeatherWindMaxKmh ?? state.dayData.weather_wind_max_kmh;
          state.weatherUnavailableReason = null;
        }
        if (callback) callback();
      })
      .catch(function () { if (callback) callback(); });
  }
  function updateWeatherUI() {
    var el = document.getElementById('preguntas-weather');
    if (!el) return;
    if (state.weatherUnavailableReason === 'no_location') {
      el.textContent = isUserOnlyView() ? 'Clima no disponible' : 'Configura lat/lon en Configuración';
      return;
    }
    if (state.weatherUnavailableReason === 'no_data') {
      el.textContent = 'Sin datos para esta fecha';
      return;
    }
    var day = state.dayData;
    var shift = getShiftDataByIndex(state.activeShiftIndex);
    el.textContent = weatherTextOrUnavailable(shift, day);
  }

  function collectFormFromShift(shiftIndex) {
    var s = state.dayData && state.dayData.shifts[shiftIndex];
    var staffFloor = document.getElementById('preguntas-staff_floor');
    var staffKitchen = document.getElementById('preguntas-staff_kitchen');
    var q1 = document.querySelector('input[name="preguntas_q1"]:checked');
    var q2 = document.querySelector('input[name="preguntas_q2"]:checked');
    var q3 = document.querySelector('input[name="preguntas_q3"]:checked');
    var q4 = document.querySelector('input[name="preguntas_q4"]:checked');
    var q5 = document.querySelector('input[name="preguntas_q5"]:checked');
    if (s) {
      s.staff_floor = staffFloor ? Math.min(99, Math.max(0, parseInt(staffFloor.value, 10) || 0)) : 0;
      s.staff_kitchen = staffKitchen ? Math.min(99, Math.max(0, parseInt(staffKitchen.value, 10) || 0)) : 0;
      s.feedback_q1 = q1 ? q1.value : null;
      s.feedback_q2 = q2 ? q2.value : null;
      s.feedback_q3 = q3 ? q3.value : null;
      s.feedback_q4 = q4 ? q4.value : null;
      s.feedback_q5 = q5 ? q5.value : null;
    }
    if (shiftIndex === 2) {
      var rev = document.getElementById('preguntas-total_revenue');
      if (rev) {
        var val = parseEuro(rev.value);
        state.total_revenue = (val === val) ? val : 0;
        if (state.dayData) state.dayData.total_revenue = state.total_revenue;
      }
    }
  }
  var formatEuroFormatter = new Intl.NumberFormat('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  function formatEuro(value) {
    if (value == null || value === '' || (typeof value === 'number' && (value !== value || value < 0))) return '';
    var n = typeof value === 'number' ? value : parseEuro(value);
    if (n !== n) return '';
    return formatEuroFormatter.format(n);
  }
  function parseEuro(str) {
    if (str == null || str === '') return NaN;
    var s = String(str).trim().replace(/\s/g, '');
    if (!s) return NaN;
    s = s.replace(/\./g, '').replace(',', '.');
    return parseFloat(s);
  }
  function parseDecimal(str) {
    if (str == null || str === '') return NaN;
    var s = String(str).trim().replace(',', '.');
    return parseFloat(s);
  }
  function normVal(v) {
    return (v || '').toString().trim().replace(/\s+/g, ' ');
  }
  function fillFormForShift(shiftIndex, shiftDataOverride) {
    var s = shiftDataOverride || getShiftDataByIndex(shiftIndex);
    if (!s) return;
    var staffFloor = document.getElementById('preguntas-staff_floor');
    var staffKitchen = document.getElementById('preguntas-staff_kitchen');
    if (staffFloor) staffFloor.value = s.staff_floor ?? 0;
    if (staffKitchen) staffKitchen.value = s.staff_kitchen ?? 0;
    ['q1', 'q2', 'q3', 'q4', 'q5'].forEach(function (q, i) {
      var val = normVal(s['feedback_q' + (i + 1)]);
      var radios = document.querySelectorAll('input[name="preguntas_' + q + '"]');
      radios.forEach(function (r) {
        var rv = normVal(r.value);
        r.checked = rv === val || (rv && val && rv.toLowerCase() === val.toLowerCase());
      });
    });
    if (shiftIndex === 2) {
      var rev = document.getElementById('preguntas-total_revenue');
      var totalRev = (state.dayData && state.dayData.total_revenue != null) ? state.dayData.total_revenue : (state.total_revenue ?? '');
      if (rev) rev.value = formatEuro(totalRev);
    }
  }
  function isShiftComplete(s) {
    return !!(s && hasFeedbackValue(s.feedback_q1) && hasFeedbackValue(s.feedback_q2) && hasFeedbackValue(s.feedback_q3) && hasFeedbackValue(s.feedback_q4) && hasFeedbackValue(s.feedback_q5));
  }
  function bindRevenueFormat() {
    var rev = document.getElementById('preguntas-total_revenue');
    if (!rev) return;
    rev.addEventListener('blur', function () {
      var n = parseEuro(rev.value);
      if (n === n && n >= 0) rev.value = formatEuro(n);
    });
  }

  var state = { dayData: null, activeShiftIndex: 0, weatherUnavailableReason: null };

  function blockClass(hasAnswer) {
    return 'preguntas-block' + (hasAnswer ? ' preguntas-block--answered' : '');
  }
  function getFormInnerHtml() {
    var idx = state.activeShiftIndex;
    var s = getShiftDataByIndex(idx);
    var isNoche = idx === 2;
    var revenueCell = isNoche ? '<div class="form-group"><label for="preguntas-total_revenue">Facturación del día entero (€)</label><input type="text" id="preguntas-total_revenue" inputmode="decimal" placeholder="0,00" value="' + (formatEuro(state.dayData ? state.dayData.total_revenue : '') || '') + '" /></div>' : '';
    return '<div class="form-row">' +
      '<div class="form-group"><label for="preguntas-staff_floor" title="Nº de personas en sala en este turno">Personal sala</label><input type="number" id="preguntas-staff_floor" min="0" max="99" value="' + (s ? s.staff_floor : 0) + '" title="Nº de personas en sala" /></div>' +
      '<div class="form-group"><label for="preguntas-staff_kitchen" title="Nº de personas en cocina en este turno">Personal cocina</label><input type="number" id="preguntas-staff_kitchen" min="0" max="99" value="' + (s ? s.staff_kitchen : 0) + '" title="Nº de personas en cocina" /></div>' +
      revenueCell +
      '</div>' +
      '<div class="preguntas-blocks-grid">' +
      '<div class="' + blockClass(s && hasFeedbackValue(s.feedback_q1)) + '"><h3 class="preguntas-q-title">Volumen: ¿Cuánto trabajo hubo en este turno?</h3><div class="preguntas-radios">' + radioGroup('preguntas_q1', Q1_OPTIONS, s ? (s.feedback_q1 || '') : '') + '</div></div>' +
      '<div class="' + blockClass(s && hasFeedbackValue(s.feedback_q2)) + '"><h3 class="preguntas-q-title">Ritmo: ¿Cómo fue el ritmo de entradas de clientes?</h3><div class="preguntas-radios">' + radioGroup('preguntas_q2', Q2_OPTIONS, s ? (s.feedback_q2 || '') : '') + '</div></div>' +
      '<div class="' + blockClass(s && hasFeedbackValue(s.feedback_q3)) + '"><h3 class="preguntas-q-title">Margen: ¿Cuánto margen hubo para ir adelantado con el trabajo?</h3><div class="preguntas-radios">' + radioGroup('preguntas_q3', Q3_OPTIONS, s ? (s.feedback_q3 || '') : '') + '</div></div>' +
      '<div class="' + blockClass(s && hasFeedbackValue(s.feedback_q4)) + '"><h3 class="preguntas-q-title">Dificultad: ¿Qué tan duro fue el turno en general?</h3><div class="preguntas-radios">' + radioGroup('preguntas_q4', Q4_OPTIONS, s ? (s.feedback_q4 || '') : '') + '</div></div>' +
      '<div class="' + blockClass(s && hasFeedbackValue(s.feedback_q5)) + '"><h3 class="preguntas-q-title">Q5 — Dificultad del turno en cocina</h3><div class="preguntas-radios">' + radioGroup('preguntas_q5', Q4_OPTIONS, s ? (s.feedback_q5 || '') : '') + '</div></div>' +
      '</div>' +
      '<div id="preguntas-scoring" class="preguntas-scoring" aria-live="polite"></div>' +
      '<button type="button" id="preguntas-guardar" class="btn-primary btn-large">Guardar Lucas</button>' +
      '<p id="preguntas-status" class="preguntas-status" aria-live="polite"></p>';
  }
  function getFormHtml() {
    return '<div class="card preguntas-card" id="preguntas-card"><div id="preguntas-card-body">' + getFormInnerHtml() + '</div></div>';
  }

  function updateScoringBlock() {
    var el = document.getElementById('preguntas-scoring');
    if (!el || !global.LUCAS_SCORING) return;
    collectFormFromShift(state.activeShiftIndex);
    var s = getShiftDataByIndex(state.activeShiftIndex);
    var score = global.LUCAS_SCORING.scoreFromShift(s);
    if (!score.completo) {
      el.innerHTML = '';
      el.className = 'preguntas-scoring';
      return;
    }
    el.className = 'preguntas-scoring';
    el.innerHTML = '<h4 class="preguntas-scoring-title">Scoring del turno</h4>' +
      '<div class="preguntas-scoring-grid">' +
      '<div class="preguntas-scoring-item"><span class="preguntas-scoring-label">SGT</span><span class="preguntas-scoring-value">' + score.sgt + '</span> <small>(6–31)</small></div>' +
      '<div class="preguntas-scoring-item"><span class="preguntas-scoring-label">Estado</span><span class="preguntas-scoring-value">' + (score.estado || '—') + '</span></div>' +
      '<div class="preguntas-scoring-item"><span class="preguntas-scoring-label">Tipo</span><span class="preguntas-scoring-value">' + (score.tipo || '—') + ' — ' + (score.tipoLabel || '') + '</span></div>' +
      '</div>' +
      '<p class="preguntas-scoring-resumen">' + (score.resumenNivel3 || '').replace(/</g, '&lt;').replace(/>/g, '&gt;') + '</p>';
  }

  function updateShiftTabIndicators() {
    document.querySelectorAll('.preguntas-shift-tab').forEach(function (tab) {
      var idx = parseInt(tab.getAttribute('data-shift'), 10);
      var s = getShiftDataByIndex(idx);
      tab.classList.toggle('preguntas-shift-tab--complete', !!(s && isShiftComplete(s)));
    });
  }
  function updateStatus() {
    var el = document.getElementById('preguntas-status');
    if (!el) return;
    var s = getShiftDataByIndex(state.activeShiftIndex);
    if (isShiftComplete(s)) el.textContent = 'Turno completo';
    else el.textContent = 'Completa las 5 preguntas';
    el.className = 'preguntas-status ' + (isShiftComplete(s) ? 'success' : 'muted');
    updateScoringBlock();
    applyHighlightFromData(s);
    applyAnsweredStateFromDom();
    updateShiftTabIndicators();
    updateWeatherUI();
  }

  /** Aplica resaltado y aviso desde los datos del turno (no depende del DOM de los radios). */
  function applyHighlightFromData(shiftData) {
    var card = document.querySelector('.preguntas-card');
    if (!card) return;
    var blocks = card.querySelectorAll('.preguntas-block');
    if (blocks.length < 4) return;
    [1, 2, 3, 4].forEach(function (i) {
      var val = shiftData && shiftData['feedback_q' + i];
      var has = hasFeedbackValue(val);
      var block = blocks[i - 1];
      if (block) {
        if (has) block.classList.add('preguntas-block--answered');
        else block.classList.remove('preguntas-block--answered');
      }
    });
  }

  function applyAnsweredStateFromDom() {
    var card = document.querySelector('.preguntas-card');
    if (!card) return;
    var blocks = card.querySelectorAll('.preguntas-block');
    var shiftData = getShiftDataByIndex(state.activeShiftIndex);
    blocks.forEach(function (block, i) {
      var qKey = 'feedback_q' + (i + 1);
      var hasInData = shiftData && hasFeedbackValue(shiftData[qKey]);
      var checked = block.querySelector('.preguntas-radios input:checked');
      if (checked || hasInData) {
        block.classList.add('preguntas-block--answered');
      } else {
        block.classList.remove('preguntas-block--answered');
      }
    });
  }

  function bindRadioChanges() {
    ['preguntas_q1', 'preguntas_q2', 'preguntas_q3', 'preguntas_q4', 'preguntas_q5'].forEach(function (name) {
      document.querySelectorAll('input[name="' + name + '"]').forEach(function (radio) {
        radio.addEventListener('change', onRadioChange);
      });
    });
  }
  function onRadioChange() {
    updateStatus();
    applyAnsweredStateFromDom();
  }

  function render(container) {
    var dateStr = state.dayData ? state.dayData.date : todayStr();
    var urlDate = getDateFromHash();
    if (urlDate) dateStr = urlDate;
    state.activeShiftIndex = state.dayData ? state.activeShiftIndex : getShiftByCurrentTime();
    var role = (auth && auth.getRole) ? auth.getRole() : '';
    var fullWebRoles = ['admin', 'manager', 'master'];
    var isFullWeb = role && fullWebRoles.indexOf(String(role).toLowerCase()) !== -1;
    var isUserOnly = !isFullWeb;
    var shiftTabsHtml = '<div class="preguntas-shift-tabs">' + SHIFT_LABELS.map(function (label, i) {
      return '<button type="button" class="preguntas-shift-tab' + (i === state.activeShiftIndex ? ' active' : '') + '" data-shift="' + i + '">' + label + '</button>';
    }).join('') + '</div>';
    var dateSelectorBar = '<div class="date-selector-bar">' +
      '<span id="preguntas-week-label" class="preguntas-week-label"></span>' +
      '<span id="preguntas-day-label" class="date-selector-day">' + getDayName(dateStr) + '</span>' +
      '<div class="date-selector-group">' +
      '<button type="button" id="preguntas-prev" class="date-selector-arrow" title="Día anterior">◀</button>' +
      '<div class="date-selector-input-wrap">' +
      '<input type="date" id="preguntas-fecha" value="' + dateStr + '" class="date-selector-input-hidden" />' +
      '<span id="preguntas-fecha-display" class="date-selector-display" title="Seleccionar fecha">' + formatDateDisplay(dateStr) + '</span>' +
      '</div>' +
      '<button type="button" id="preguntas-next" class="date-selector-arrow" title="Día siguiente">▶</button>' +
      '</div>' +
      '<span class="preguntas-weather-in-row">Clima: <span id="preguntas-weather" class="preguntas-weather">—</span></span></div>';
    var headerExtra = document.getElementById('header-extra');
    if (headerExtra) headerExtra.innerHTML = '';
    if (isUserOnly && headerExtra) {
      headerExtra.innerHTML = '<div class="header-extra-inner">' + shiftTabsHtml + dateSelectorBar + '</div>';
      container.innerHTML = '<div id="preguntas-form-wrap">' + getFormHtml() + '</div>';
    } else {
      var titleRow = '<div class="preguntas-title-row">' +
        '<div class="preguntas-title-and-tabs">' +
        '<h2 class="view-title">Feedback diario</h2>' +
        shiftTabsHtml +
        '</div>' +
        dateSelectorBar + '</div>';
      container.innerHTML = '<div class="card preguntas-header-card">' + titleRow + '</div><div id="preguntas-form-wrap">' + getFormHtml() + '</div>';
    }
    bind(container);
    updateStatus();
    updateWeatherUI();
    // En vista user se carga por defecto el día actual (turno actual); el clima se pide para esa fecha (hoy si no hay ?date= en URL)
    var fechaInput = document.getElementById('preguntas-fecha');
    if (fechaInput) {
      var dateToLoad = (!state.dayData || urlDate) ? fechaInput.value : null;
      if (dateToLoad) loadDay(dateToLoad);
    }
  }

  function bind(container) {
    var wrap = document.getElementById('preguntas-form-wrap');
    var fechaInput = document.getElementById('preguntas-fecha');
    var fechaDisplay = document.getElementById('preguntas-fecha-display');
    var dayLabel = document.getElementById('preguntas-day-label');
    function updateDateSelector() {
      if (fechaInput) {
        var v = fechaInput.value;
        if (fechaDisplay) fechaDisplay.textContent = formatDateDisplay(v);
        if (dayLabel) dayLabel.textContent = getDayName(v);
      }
    }
    if (fechaDisplay && fechaInput) fechaDisplay.addEventListener('click', function () { fechaInput.click(); });
    if (fechaInput) fechaInput.addEventListener('change', function () { updateDateSelector(); loadDay(fechaInput.value); });
    document.getElementById('preguntas-prev') && document.getElementById('preguntas-prev').addEventListener('click', function () {
      if (!state.dayData) return;
      collectFormFromShift(state.activeShiftIndex);
      var prev = addDays(state.dayData.date, -1);
      fechaInput.value = prev;
      updateDateSelector();
      loadDay(prev);
    });
    document.getElementById('preguntas-next') && document.getElementById('preguntas-next').addEventListener('click', function () {
      if (!state.dayData) return;
      collectFormFromShift(state.activeShiftIndex);
      var next = addDays(state.dayData.date, 1);
      fechaInput.value = next;
      updateDateSelector();
      loadDay(next);
    });
    document.querySelectorAll('.preguntas-shift-tab').forEach(function (tab) {
      tab.addEventListener('click', function () {
        var idx = parseInt(tab.getAttribute('data-shift'), 10);
        if (idx === state.activeShiftIndex) return;
        collectFormFromShift(state.activeShiftIndex);
        state.activeShiftIndex = idx;
        document.querySelectorAll('.preguntas-shift-tab').forEach(function (t) { t.classList.toggle('active', parseInt(t.getAttribute('data-shift'), 10) === idx); });
        var cardBody = document.getElementById('preguntas-card-body');
        if (cardBody) {
          cardBody.innerHTML = getFormInnerHtml();
          fillFormForShift(idx);
          applyHighlightFromData(getShiftDataByIndex(idx));
          applyAnsweredStateFromDom();
          updateStatus();
          var btnSave = document.getElementById('preguntas-guardar');
          if (btnSave) btnSave.addEventListener('click', save);
          bindRadioChanges();
          bindRevenueFormat();
        } else {
          wrap.innerHTML = getFormHtml();
          fillFormForShift(idx);
          applyHighlightFromData(getShiftDataByIndex(idx));
          applyAnsweredStateFromDom();
          updateStatus();
          var btnSave = document.getElementById('preguntas-guardar');
          if (btnSave) btnSave.addEventListener('click', save);
          bindRadioChanges();
          bindRevenueFormat();
        }
      });
    });
    var btnSave = document.getElementById('preguntas-guardar');
    if (btnSave) btnSave.addEventListener('click', save);
    bindRadioChanges();
    bindRevenueFormat();
  }

  function loadDay(dateStr, options) {
    options = options || {};
    var preserveShift = options.preserveShift === true;
    var currentShift = state.activeShiftIndex;
    // No borrar state.dayData aquí: la respuesta de clima llega después y necesita hacer merge con el mismo día
    if (!preserveShift) state.activeShiftIndex = getShiftByCurrentTime();
    var wrap = document.getElementById('preguntas-form-wrap');
    var container = document.getElementById('dashboard-content');
    if (wrap) wrap.innerHTML = '<p class="loading">Cargando…</p>';
    auth.fetchWithAuth('/api/execution/' + dateStr).then(function (res) {
      if (res.status === 404) {
        state.dayData = defaultDayData(dateStr);
        if (!preserveShift) state.activeShiftIndex = getShiftByCurrentTime();
        var wl = document.getElementById('preguntas-week-label');
        if (wl) wl.textContent = 'Semana ' + weekNumber(dateStr);
        var wrap404 = document.getElementById('preguntas-form-wrap');
        if (wrap404) wrap404.innerHTML = getFormHtml();
        var cont404 = document.getElementById('dashboard-content');
        if (cont404) { bind(cont404); updateStatus(); }
        fetchWeatherForDate(dateStr, function () { updateWeatherUI(); updateShiftTabIndicators(); });
        return;
      }
      if (!res.ok) throw new Error('Error al cargar');
      return res.json();
    }).then(function (data) {
      if (!data) return;
      state.dayData = normalizeDayData(data);
      if (!preserveShift) {
        var byTime = getShiftByCurrentTime();
        var withFeedback = -1;
        for (var i = 0; i < (state.dayData.shifts || []).length; i++) {
          if (isShiftComplete(state.dayData.shifts[i])) { withFeedback = i; break; }
        }
        state.activeShiftIndex = withFeedback >= 0 ? withFeedback : byTime;
      } else {
        state.activeShiftIndex = Math.min(2, Math.max(0, currentShift));
      }
      var activeIdx = state.activeShiftIndex;
      var activeShift = getShiftDataByIndex(activeIdx);
      var shiftSnapshot = activeShift ? {
        shift_name: activeShift.shift_name,
        staff_floor: activeShift.staff_floor,
        staff_kitchen: activeShift.staff_kitchen,
        feedback_q1: activeShift.feedback_q1,
        feedback_q2: activeShift.feedback_q2,
        feedback_q3: activeShift.feedback_q3,
        feedback_q4: activeShift.feedback_q4,
        feedback_q5: activeShift.feedback_q5
      } : null;
      var fechaInput = document.getElementById('preguntas-fecha');
      if (fechaInput) fechaInput.value = state.dayData.date;
      var fd = document.getElementById('preguntas-fecha-display');
      if (fd) fd.textContent = formatDateDisplay(state.dayData.date);
      var dl = document.getElementById('preguntas-day-label');
      if (dl) dl.textContent = getDayName(state.dayData.date);
      var wl = document.getElementById('preguntas-week-label');
      if (wl) wl.textContent = 'Semana ' + weekNumber(state.dayData.date);
      var wrapEl = document.getElementById('preguntas-form-wrap');
      if (wrapEl) {
        wrapEl.innerHTML = getFormHtml();
        fillFormForShift(activeIdx, shiftSnapshot);
        applyHighlightFromData(shiftSnapshot);
        requestAnimationFrame(function () {
          var w = document.getElementById('preguntas-form-wrap');
          if (!w || !w.querySelector('.preguntas-card')) return;
          fillFormForShift(activeIdx, shiftSnapshot);
          applyHighlightFromData(shiftSnapshot);
        });
      }
      var cont = document.getElementById('dashboard-content');
      if (cont) {
        document.querySelectorAll('.preguntas-shift-tab').forEach(function (tab) {
          tab.classList.toggle('active', parseInt(tab.getAttribute('data-shift'), 10) === activeIdx);
        });
        bind(cont);
        updateStatus();
      }
      fetchWeatherForDate(state.dayData.date, function () { updateWeatherUI(); updateShiftTabIndicators(); });
    }).catch(function (err) {
      if (wrap) wrap.innerHTML = '<p class="error-msg">' + (err.message || 'Error al cargar el día') + '</p>';
    });
  }

  function save() {
    collectFormFromShift(state.activeShiftIndex);
    var day = state.dayData;
    if (!day) return;
    var dateStr = day.date;
    var staffTotal = day.shifts.reduce(function (sum, s) { return sum + (s.staff_floor || 0) + (s.staff_kitchen || 0); }, 0);
    var shiftsPayload = day.shifts.map(function (s) {
      return {
        shift_name: s.shift_name,
        revenue: (s.revenue != null ? s.revenue : 0),
        hours_worked: (s.hours_worked != null ? s.hours_worked : 0),
        staff_floor: s.staff_floor || 0,
        staff_kitchen: s.staff_kitchen || 0,
        feedback_q1: s.feedback_q1 || null,
        feedback_q2: s.feedback_q2 || null,
        feedback_q3: s.feedback_q3 || null,
        feedback_q4: s.feedback_q4 || null,
        feedback_q5: s.feedback_q5 || null,
        recorded_by: s.recorded_by || null,
        edited_by: null,
        weather_code: s.weather_code != null ? s.weather_code : null,
        weather_temp_avg: s.weather_temp_avg != null ? s.weather_temp_avg : null,
        weather_precip_mm: s.weather_precip_mm != null ? s.weather_precip_mm : null,
        weather_wind_max_kmh: s.weather_wind_max_kmh != null ? s.weather_wind_max_kmh : null
      };
    });
    var totalRevenue = day.total_revenue ?? 0;
    var totalHoursWorked = day.total_hours_worked ?? 0;
    var statusEl = document.getElementById('preguntas-status');
    var btn = document.getElementById('preguntas-guardar');
    if (btn) { btn.disabled = true; btn.setAttribute('aria-busy', 'true'); }
    if (statusEl) { statusEl.textContent = 'Guardando…'; statusEl.className = 'preguntas-status'; statusEl.removeAttribute('role'); }
    auth.fetchWithAuth('/api/execution/' + encodeURIComponent(dateStr))
      .then(function (getRes) {
        if (getRes.status === 401) return null;
        if (getRes.status === 404) {
          return auth.fetchWithAuth('/api/execution', {
            method: 'POST',
            body: JSON.stringify({
              date: dateStr,
              total_revenue: totalRevenue,
              total_hours_worked: totalHoursWorked,
              staff_total: staffTotal,
              notes: null,
              shifts: shiftsPayload
            })
          });
        }
        return auth.fetchWithAuth('/api/execution/' + encodeURIComponent(dateStr), {
          method: 'PATCH',
          body: JSON.stringify({
            shifts: shiftsPayload,
            total_revenue: totalRevenue,
            total_hours_worked: totalHoursWorked,
            staff_total: staffTotal
          })
        });
      })
      .then(function (res) {
        if (!res) return;
        if (res.status === 409) throw new Error('El día ya existe');
        if (!res.ok) return res.json().then(function (d) { throw new Error(d.message || 'Error al guardar'); }).catch(function (e) { throw e.message ? e : new Error('Error al guardar'); });
        return res.json();
      })
      .then(function (data) {
        if (!data) return;
        if (statusEl) { statusEl.textContent = 'Día guardado'; statusEl.className = 'preguntas-status success'; statusEl.removeAttribute('role'); statusEl.setAttribute('tabindex', '-1'); statusEl.focus(); }
        loadDay(dateStr, { preserveShift: true });
      })
      .catch(function (err) {
        if (statusEl) { statusEl.textContent = err.message || 'Error al guardar'; statusEl.className = 'preguntas-status error'; statusEl.setAttribute('role', 'alert'); statusEl.setAttribute('tabindex', '-1'); statusEl.focus(); }
      })
      .finally(function () {
        if (btn) { btn.disabled = false; btn.setAttribute('aria-busy', 'false'); }
      });
  }

  function renderAndLoad(container) {
    state.dayData = defaultDayData(todayStr());
    state.activeShiftIndex = getShiftByCurrentTime();
    render(container);
    loadDay(todayStr());
  }

  global.LUCAS_PREGUNTAS_VIEW = { render: render, renderAndLoad: renderAndLoad };
})(typeof window !== 'undefined' ? window : this);
