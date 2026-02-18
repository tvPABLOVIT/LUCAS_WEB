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
  function getShiftByCurrentTime() {
    var h = new Date().getHours();
    if (h < 16) return 0;
    if (h < 20) return 1;
    return 2;
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
      edited_by: null
    };
  }
  function defaultDayData(dateStr) {
    return {
      date: dateStr,
      total_revenue: 0,
      total_hours_worked: 0,
      staff_total: 0,
      notes: '',
      shifts: SHIFT_NAMES.map(defaultShift)
    };
  }
  function normalizeDayData(apiData) {
    var shifts = (apiData.shifts || []).slice();
    var names = SHIFT_NAMES.slice();
    names.forEach(function (name, i) {
      var found = shifts.find(function (s) { return (s.shift_name || s.ShiftName) === name; });
      if (found) {
        shifts[i] = {
          shift_name: name,
          revenue: found.revenue ?? found.Revenue ?? 0,
          hours_worked: found.hours_worked ?? found.HoursWorked ?? 0,
          staff_floor: found.staff_floor ?? found.StaffFloor ?? 0,
          staff_kitchen: found.staff_kitchen ?? found.StaffKitchen ?? 0,
          feedback_q1: found.feedback_q1 ?? found.FeedbackQ1 ?? null,
          feedback_q2: found.feedback_q2 ?? found.FeedbackQ2 ?? null,
          feedback_q3: found.feedback_q3 ?? found.FeedbackQ3 ?? null,
          feedback_q4: found.feedback_q4 ?? found.FeedbackQ4 ?? null,
          feedback_q5: found.feedback_q5 ?? found.FeedbackQ5 ?? null,
          recorded_by: found.recorded_by ?? found.RecordedBy,
          edited_by: found.edited_by ?? found.EditedBy
        };
      } else {
        shifts[i] = defaultShift(name);
      }
    });
    return {
      id: apiData.id,
      date: apiData.date || apiData.Date,
      total_revenue: apiData.total_revenue ?? apiData.TotalRevenue ?? 0,
      total_hours_worked: apiData.total_hours_worked ?? apiData.TotalHoursWorked ?? 0,
      staff_total: apiData.staff_total ?? apiData.StaffTotal ?? 0,
      notes: apiData.notes ?? apiData.Notes ?? '',
      shifts: shifts
    };
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
      s.staff_floor = staffFloor ? Math.min(3, Math.max(0, parseInt(staffFloor.value, 10) || 0)) : 0;
      s.staff_kitchen = staffKitchen ? Math.min(3, Math.max(0, parseInt(staffKitchen.value, 10) || 0)) : 0;
      s.feedback_q1 = q1 ? q1.value : null;
      s.feedback_q2 = q2 ? q2.value : null;
      s.feedback_q3 = q3 ? q3.value : null;
      s.feedback_q4 = q4 ? q4.value : null;
      s.feedback_q5 = q5 ? q5.value : null;
    }
    if (shiftIndex === 2) {
      var rev = document.getElementById('preguntas-total_revenue');
      if (rev) state.total_revenue = parseFloat(rev.value) || 0;
    }
  }
  function fillFormForShift(shiftIndex) {
    var s = state.dayData && state.dayData.shifts[shiftIndex];
    if (!s) return;
    var staffFloor = document.getElementById('preguntas-staff_floor');
    var staffKitchen = document.getElementById('preguntas-staff_kitchen');
    if (staffFloor) staffFloor.value = s.staff_floor ?? 0;
    if (staffKitchen) staffKitchen.value = s.staff_kitchen ?? 0;
    ['q1', 'q2', 'q3', 'q4', 'q5'].forEach(function (q, i) {
      var val = s['feedback_q' + (i + 1)];
      var radio = document.querySelector('input[name="preguntas_' + q + '"][value="' + (val || '').replace(/"/g, '&quot;') + '"]');
      if (radio) radio.checked = true;
      else document.querySelectorAll('input[name="preguntas_' + q + '"]').forEach(function (r) { r.checked = false; });
    });
    if (shiftIndex === 2) {
      var rev = document.getElementById('preguntas-total_revenue');
      if (rev) rev.value = state.total_revenue ?? '';
    }
  }
  function isShiftComplete(s) {
    return !!(s && s.feedback_q1 && s.feedback_q2 && s.feedback_q3 && s.feedback_q4 && s.feedback_q5);
  }

  var state = { dayData: null, activeShiftIndex: 0 };

  function getFormHtml() {
    var idx = state.activeShiftIndex;
    var s = state.dayData && state.dayData.shifts[idx];
    var isNoche = idx === 2;
    var revenueRow = isNoche ? '<div class="form-row"><div class="form-group"><label for="preguntas-total_revenue">Facturación del día entero (€)</label><input type="number" id="preguntas-total_revenue" step="0.01" min="0" value="' + (state.dayData ? state.dayData.total_revenue : '') + '" /></div></div>' : '';
    return '<div class="card preguntas-card">' +
      '<div class="form-row"><div class="form-group"><label for="preguntas-staff_floor" title="Nº de personas en sala en este turno">Personal sala</label><input type="number" id="preguntas-staff_floor" min="0" max="3" value="' + (s ? s.staff_floor : 0) + '" title="Nº de personas en sala" /></div>' +
      '<div class="form-group"><label for="preguntas-staff_kitchen" title="Nº de personas en cocina en este turno">Personal cocina</label><input type="number" id="preguntas-staff_kitchen" min="0" max="3" value="' + (s ? s.staff_kitchen : 0) + '" title="Nº de personas en cocina" /></div></div>' +
      revenueRow +
      '<div class="preguntas-block"><h3 class="preguntas-q-title">Q1 — Volumen (¿Cuánto trabajo hubo?)</h3><div class="preguntas-radios">' + radioGroup('preguntas_q1', Q1_OPTIONS, s ? s.feedback_q1 : '') + '</div></div>' +
      '<div class="preguntas-block"><h3 class="preguntas-q-title">Q2 — Ritmo (¿Cómo fue el ritmo de entradas?)</h3><div class="preguntas-radios">' + radioGroup('preguntas_q2', Q2_OPTIONS, s ? s.feedback_q2 : '') + '</div></div>' +
      '<div class="preguntas-block"><h3 class="preguntas-q-title">Q3 — Margen (¿Cuánto margen hubo?)</h3><div class="preguntas-radios">' + radioGroup('preguntas_q3', Q3_OPTIONS, s ? s.feedback_q3 : '') + '</div></div>' +
      '<div class="preguntas-block"><h3 class="preguntas-q-title">Q4 — Dificultad (¿Qué tan duro fue el turno?)</h3><div class="preguntas-radios">' + radioGroup('preguntas_q4', Q4_OPTIONS, s ? s.feedback_q4 : '') + '</div></div>' +
      '<div class="preguntas-block"><h3 class="preguntas-q-title">Q5 — Dificultad del turno en cocina</h3><div class="preguntas-radios">' + radioGroup('preguntas_q5', Q4_OPTIONS, s ? s.feedback_q5 : '') + '</div></div>' +
      '<button type="button" id="preguntas-guardar" class="btn-primary btn-large">Guardar Lucas</button>' +
      '<p id="preguntas-status" class="preguntas-status" aria-live="polite"></p></div>';
  }

  function updateStatus() {
    var el = document.getElementById('preguntas-status');
    if (!el) return;
    var s = state.dayData && state.dayData.shifts[state.activeShiftIndex];
    if (isShiftComplete(s)) el.textContent = 'Turno completo';
    else el.textContent = 'Completa las 5 preguntas';
    el.className = 'preguntas-status ' + (isShiftComplete(s) ? 'success' : 'muted');
  }

  function render(container) {
    var dateStr = state.dayData ? state.dayData.date : todayStr();
    state.activeShiftIndex = state.dayData ? state.activeShiftIndex : getShiftByCurrentTime();
    var nav = '<div class="form-row"><div class="form-group"><label>Fecha</label><input type="date" id="preguntas-fecha" value="' + dateStr + '" /></div>' +
      '<div class="form-group"><label>&nbsp;</label><div class="preguntas-nav-days"><button type="button" id="preguntas-prev" class="btn-secondary">◀ Anterior</button><button type="button" id="preguntas-next" class="btn-secondary">Siguiente ▶</button></div></div></div>';
    var shiftTabs = '<div class="preguntas-shift-tabs">' + SHIFT_LABELS.map(function (label, i) {
      return '<button type="button" class="preguntas-shift-tab' + (i === state.activeShiftIndex ? ' active' : '') + '" data-shift="' + i + '">' + label + '</button>';
    }).join('') + '</div>';
    container.innerHTML = '<h2 class="view-title">Preguntas</h2><div class="card">' + nav + shiftTabs + '</div><div id="preguntas-form-wrap">' + getFormHtml() + '</div>';
    bind(container);
    updateStatus();
    if (!state.dayData && document.getElementById('preguntas-fecha')) loadDay(document.getElementById('preguntas-fecha').value || todayStr());
  }

  function bind(container) {
    var wrap = document.getElementById('preguntas-form-wrap');
    var fechaInput = document.getElementById('preguntas-fecha');
    if (fechaInput) fechaInput.addEventListener('change', function () { loadDay(fechaInput.value); });
    document.getElementById('preguntas-prev') && document.getElementById('preguntas-prev').addEventListener('click', function () {
      if (!state.dayData) return;
      collectFormFromShift(state.activeShiftIndex);
      var prev = addDays(state.dayData.date, -1);
      fechaInput.value = prev;
      loadDay(prev);
    });
    document.getElementById('preguntas-next') && document.getElementById('preguntas-next').addEventListener('click', function () {
      if (!state.dayData) return;
      collectFormFromShift(state.activeShiftIndex);
      var next = addDays(state.dayData.date, 1);
      fechaInput.value = next;
      loadDay(next);
    });
    container.querySelectorAll('.preguntas-shift-tab').forEach(function (tab) {
      tab.addEventListener('click', function () {
        var idx = parseInt(tab.getAttribute('data-shift'), 10);
        if (idx === state.activeShiftIndex) return;
        collectFormFromShift(state.activeShiftIndex);
        state.activeShiftIndex = idx;
        container.querySelectorAll('.preguntas-shift-tab').forEach(function (t) { t.classList.toggle('active', parseInt(t.getAttribute('data-shift'), 10) === idx); });
        wrap.innerHTML = getFormHtml();
        fillFormForShift(idx);
        updateStatus();
        bind(container);
      });
    });
    var btnSave = document.getElementById('preguntas-guardar');
    if (btnSave) btnSave.addEventListener('click', save);
  }

  function loadDay(dateStr) {
    state.dayData = null;
    state.activeShiftIndex = getShiftByCurrentTime();
    var wrap = document.getElementById('preguntas-form-wrap');
    var container = document.getElementById('dashboard-content');
    if (wrap) wrap.innerHTML = '<p class="loading">Cargando…</p>';
    auth.fetchWithAuth('/api/execution/' + dateStr).then(function (res) {
      if (res.status === 404) {
        state.dayData = defaultDayData(dateStr);
        state.activeShiftIndex = getShiftByCurrentTime();
        if (wrap) wrap.innerHTML = getFormHtml();
        if (container) { bind(container); updateStatus(); }
        return;
      }
      if (!res.ok) throw new Error('Error al cargar');
      return res.json();
    }).then(function (data) {
      if (!data) return;
      state.dayData = normalizeDayData(data);
      state.activeShiftIndex = getShiftByCurrentTime();
      var fechaInput = document.getElementById('preguntas-fecha');
      if (fechaInput) fechaInput.value = state.dayData.date;
      if (wrap) wrap.innerHTML = getFormHtml();
      fillFormForShift(state.activeShiftIndex);
      if (container) { bind(container); updateStatus(); }
    }).catch(function (err) {
      if (wrap) wrap.innerHTML = '<p class="error-msg">' + (err.message || 'Error al cargar el día') + '</p>';
    });
  }

  function save() {
    collectFormFromShift(state.activeShiftIndex);
    var day = state.dayData;
    if (!day) return;
    var staffTotal = day.shifts.reduce(function (sum, s) { return sum + (s.staff_floor || 0) + (s.staff_kitchen || 0); }, 0);
    var totalHours = day.shifts.reduce(function (sum, s) { return sum + (parseFloat(s.hours_worked) || 0); }, 0);
    var payload = {
      total_revenue: day.total_revenue,
      total_hours_worked: totalHours,
      staff_total: staffTotal,
      notes: day.notes || '',
      shifts: day.shifts.map(function (s) {
        return {
          shift_name: s.shift_name,
          revenue: s.revenue || 0,
          hours_worked: s.hours_worked || 0,
          staff_floor: s.staff_floor || 0,
          staff_kitchen: s.staff_kitchen || 0,
          feedback_q1: s.feedback_q1 || null,
          feedback_q2: s.feedback_q2 || null,
          feedback_q3: s.feedback_q3 || null,
          feedback_q4: s.feedback_q4 || null,
          feedback_q5: s.feedback_q5 || null,
          recorded_by: s.recorded_by || null,
          edited_by: s.edited_by || null
        };
      })
    };
    var statusEl = document.getElementById('preguntas-status');
    var btn = document.getElementById('preguntas-guardar');
    if (btn) btn.disabled = true;
    var done = function (msg) {
      if (statusEl) { statusEl.textContent = msg; statusEl.className = 'preguntas-status success'; }
      if (btn) btn.disabled = false;
    };
    if (day.id) {
      auth.fetchWithAuth('/api/execution/' + day.date, { method: 'PATCH', body: JSON.stringify(payload) }).then(function (res) {
        if (res.status === 401) return;
        if (!res.ok) return res.json().then(function (d) { throw new Error(d.message || 'Error al guardar'); }).catch(function (e) { throw e.message ? e : new Error('Error al guardar'); });
        return res.json();
      }).then(function (data) {
        state.dayData = normalizeDayData(data);
        done(isShiftComplete(state.dayData.shifts[state.activeShiftIndex]) ? 'Turno completo' : 'Turno guardado');
      }).catch(function (err) {
        if (statusEl) { statusEl.textContent = err.message || 'Error'; statusEl.className = 'preguntas-status error'; }
        if (btn) btn.disabled = false;
      });
    } else {
      auth.fetchWithAuth('/api/execution', { method: 'POST', body: JSON.stringify({ date: day.date, total_revenue: day.total_revenue, total_hours_worked: totalHours, staff_total: staffTotal, notes: day.notes, shifts: payload.shifts }) }).then(function (res) {
        if (res.status === 401) return;
        if (res.status === 409) throw new Error('El día ya existe');
        if (!res.ok) return res.json().then(function (d) { throw new Error(d.message || 'Error al crear'); }).catch(function (e) { throw e.message ? e : new Error('Error al crear'); });
        return res.json();
      }).then(function (data) {
        state.dayData = normalizeDayData(data);
        done(isShiftComplete(state.dayData.shifts[state.activeShiftIndex]) ? 'Turno completo' : 'Turno guardado');
      }).catch(function (err) {
        if (statusEl) { statusEl.textContent = err.message || 'Error'; statusEl.className = 'preguntas-status error'; }
        if (btn) btn.disabled = false;
      });
    }
  }

  function renderAndLoad(container) {
    state.dayData = defaultDayData(todayStr());
    state.activeShiftIndex = getShiftByCurrentTime();
    render(container);
    loadDay(todayStr());
  }

  global.LUCAS_PREGUNTAS_VIEW = { render: render, renderAndLoad: renderAndLoad };
})(typeof window !== 'undefined' ? window : this);
