/**
 * Lucas Web — Vista Registro
 * Ver/editar día de ejecución: GET/POST/PATCH /api/execution
 */
(function (global) {
  var auth = global.LUCAS_AUTH;

  function formatDateForInput(d) {
    if (!d) return '';
    var date = typeof d === 'string' ? new Date(d) : d;
    var y = date.getFullYear();
    var m = String(date.getMonth() + 1).padStart(2, '0');
    var day = String(date.getDate()).padStart(2, '0');
    return y + '-' + m + '-' + day;
  }

  function todayStr() {
    return formatDateForInput(new Date());
  }

  function render(container) {
    container.innerHTML = '<h2 class="view-title">Registro de ejecución</h2>' +
      '<div class="card"><h3>Día de ejecución</h3>' +
      '<div class="form-row">' +
      '<div class="form-group"><label>Fecha</label><input type="date" id="registro-fecha" value="' + todayStr() + '" /></div>' +
      '<div class="form-group"><label>&nbsp;</label><button type="button" id="registro-cargar" class="btn-primary">Cargar día</button></div>' +
      '</div>' +
      '<div id="registro-form-wrap"></div>' +
      '<p id="registro-message" class="error-msg hidden"></p></div>';

    var fechaInput = document.getElementById('registro-fecha');
    var wrap = document.getElementById('registro-form-wrap');
    var msgEl = document.getElementById('registro-message');

    function setMsg(text, isSuccess) {
      if (!msgEl) return;
      msgEl.textContent = text || '';
      msgEl.classList.toggle('hidden', !text);
      msgEl.classList.toggle('success-msg', !!text && isSuccess);
      msgEl.classList.toggle('error-msg', !!text && !isSuccess);
    }

    document.getElementById('registro-cargar').addEventListener('click', function () {
      var date = (fechaInput && fechaInput.value) || todayStr();
      if (!date) return;
      setMsg('');
      wrap.innerHTML = '<p class="loading">Cargando…</p>';
      auth.fetchWithAuth('/api/execution/' + date)
        .then(function (res) {
          if (res.status === 404) {
            wrap.innerHTML = '<p class="loading">No hay datos para esta fecha. Puedes crear el día más abajo.</p>' +
              getCreateFormHtml(date);
            bindCreateForm(date);
            return;
          }
          if (!res.ok) throw new Error('Error al cargar');
          return res.json();
        })
        .then(function (data) {
          if (!data) return;
          wrap.innerHTML = getEditFormHtml(data);
          bindEditForm(data);
        })
        .catch(function (err) {
          wrap.innerHTML = '<p class="error-msg">' + (err.message || 'Error al cargar el día') + '</p>';
        });
    });
  }

  var SHIFT_NAMES = ['Mediodia', 'Tarde', 'Noche'];
  function getCreateFormHtml(date) {
    var shiftsHtml = SHIFT_NAMES.map(function (name) {
      return '<div class="card"><h3>' + name + '</h3><div class="form-row">' +
        '<div class="form-group"><label>Facturación</label><input type="number" data-shift="' + name + '" data-field="revenue" step="0.01" min="0" value="0" /></div>' +
        '<div class="form-group"><label>Horas</label><input type="number" data-shift="' + name + '" data-field="hours_worked" step="0.1" min="0" value="0" /></div>' +
        '<div class="form-group"><label title="Nº de personas en sala en este turno">Sala (0-3)</label><input type="number" data-shift="' + name + '" data-field="staff_floor" min="0" max="3" value="0" title="Personal sala" /></div>' +
        '<div class="form-group"><label title="Nº de personas en cocina en este turno">Cocina (0-3)</label><input type="number" data-shift="' + name + '" data-field="staff_kitchen" min="0" max="3" value="0" title="Personal cocina" /></div>' +
        '</div></div>';
    }).join('');
    return '<div class="form-row">' +
      '<div class="form-group"><label>Facturación total</label><input type="number" id="re-total_revenue" step="0.01" min="0" placeholder="0" /></div>' +
      '<div class="form-group"><label>Horas trabajadas</label><input type="number" id="re-total_hours" step="0.1" min="0" placeholder="0" /></div>' +
      '</div><div class="form-group"><label>Notas</label><textarea id="re-notes" rows="2"></textarea></div>' +
      '<h3 style="margin-top:1rem">Turnos</h3>' + shiftsHtml +
      '<button type="button" id="registro-guardar-create" class="btn-primary">Crear día</button>';
  }

  var Q1_OPTIONS = ['', 'Pocas mesas', 'Media sala', 'Sala completa', 'Sala y terraza completas', 'Sala y terraza completas y doblamos mesas'];
  var Q2_OPTIONS = ['', 'Muy espaciadas, sin acumulación', 'Entradas tranquilas', 'Flujo constante', 'Muchas entradas juntas', 'Entradas continuas sin margen'];
  var Q3_OPTIONS = ['', 'Siempre adelantado', 'Generalmente con margen', 'Justo', 'Poco margen', 'Ningún margen'];
  var Q4_OPTIONS = ['', 'Muy fácil', 'Fácil', 'Normal', 'Difícil', 'Muy difícil'];
  function optionList(opts, selected) { var s = (selected || '').trim(); return (opts || []).map(function (o) { var v = (o || '').trim(); return '<option value="' + v.replace(/"/g, '&quot;') + '"' + (v === s ? ' selected' : '') + '>' + (o || '—') + '</option>'; }).join(''); }
  function getEditFormHtml(data) {
    var shifts = (data.shifts || []).map(function (s) {
      var sn = s.shift_name || '';
      var row1 = '<div class="form-row"><div class="form-group"><label>Facturación</label><input type="number" data-shift="' + sn + '" data-field="revenue" step="0.01" value="' + (s.revenue ?? '') + '" /></div><div class="form-group"><label title="Horas reales del turno">Horas</label><input type="number" data-shift="' + sn + '" data-field="hours_worked" step="0.1" value="' + (s.hours_worked ?? '') + '" /></div><div class="form-group"><label title="Nº de personas en sala">Sala</label><input type="number" data-shift="' + sn + '" data-field="staff_floor" min="0" max="3" value="' + (s.staff_floor ?? '') + '" /></div><div class="form-group"><label title="Nº de personas en cocina">Cocina</label><input type="number" data-shift="' + sn + '" data-field="staff_kitchen" min="0" max="3" value="' + (s.staff_kitchen ?? '') + '" /></div></div>';
      var row2 = '<div class="form-row"><div class="form-group"><label>Q1 Volumen</label><select data-shift="' + sn + '" data-field="feedback_q1">' + optionList(Q1_OPTIONS, s.feedback_q1) + '</select></div><div class="form-group"><label>Q2 Ritmo</label><select data-shift="' + sn + '" data-field="feedback_q2">' + optionList(Q2_OPTIONS, s.feedback_q2) + '</select></div><div class="form-group"><label>Q3 Margen</label><select data-shift="' + sn + '" data-field="feedback_q3">' + optionList(Q3_OPTIONS, s.feedback_q3) + '</select></div><div class="form-group"><label>Q4 Dificultad</label><select data-shift="' + sn + '" data-field="feedback_q4">' + optionList(Q4_OPTIONS, s.feedback_q4) + '</select></div></div>';
      var row3 = '<div class="form-row"><div class="form-group"><label>Q5 Dificultad cocina</label><select data-shift="' + sn + '" data-field="feedback_q5">' + optionList(Q4_OPTIONS, s.feedback_q5) + '</select></div></div>';
      return '<div class="card"><h3>' + (sn || 'Turno') + '</h3>' + row1 + row2 + row3 + '</div>';
    }).join('');
    return '<div class="form-row">' +
      '<div class="form-group"><label>Facturación total</label><input type="number" id="re-total_revenue" step="0.01" value="' + (data.total_revenue ?? '') + '" /></div>' +
      '<div class="form-group"><label>Horas trabajadas</label><input type="number" id="re-total_hours" step="0.1" value="' + (data.total_hours_worked ?? '') + '" /></div>' +
      '</div>' +
      '<div class="form-group"><label>Notas</label><textarea id="re-notes" rows="2">' + (data.notes || '') + '</textarea></div>' +
      shifts +
      '<button type="button" id="registro-guardar-patch" class="btn-primary">Guardar cambios</button>';
  }

  function bindCreateForm(date) {
    var btn = document.getElementById('registro-guardar-create');
    if (!btn) return;
    btn.onclick = function () {
      var totalRevenue = parseFloat(document.getElementById('re-total_revenue').value) || 0;
      var totalHours = parseFloat(document.getElementById('re-total_hours').value) || 0;
      var notes = (document.getElementById('re-notes') && document.getElementById('re-notes').value) || null;
      var shiftInputs = document.querySelectorAll('[data-shift][data-field]'), shiftMap = {};
      shiftInputs.forEach(function (inp) {
        var name = inp.getAttribute('data-shift');
        if (!shiftMap[name]) shiftMap[name] = {};
        shiftMap[name][inp.getAttribute('data-field')] = inp.value;
      });
      var shifts = SHIFT_NAMES.map(function (name) {
        var o = shiftMap[name] || {};
        return {
          shift_name: name,
          revenue: parseFloat(o.revenue) || 0,
          hours_worked: parseFloat(o.hours_worked) || 0,
          staff_floor: Math.min(3, Math.max(0, parseInt(o.staff_floor, 10) || 0)),
          staff_kitchen: Math.min(3, Math.max(0, parseInt(o.staff_kitchen, 10) || 0)),
          feedback_q1: null, feedback_q2: null, feedback_q3: null, feedback_q4: null, feedback_q5: null,
          recorded_by: null, edited_by: null,
        };
      });
      var staffTotal = shifts.reduce(function (sum, s) { return sum + s.staff_floor + s.staff_kitchen; }, 0);
      var msgEl = document.getElementById('registro-message');
      function setMsg(t, ok) { if (msgEl) { msgEl.textContent = t || ''; msgEl.classList.toggle('hidden', !t); msgEl.classList.toggle('success-msg', !!t && ok); msgEl.classList.toggle('error-msg', !!t && !ok); } }
      setMsg('');
      auth.fetchWithAuth('/api/execution', {
        method: 'POST',
        body: JSON.stringify({ date: date, total_revenue: totalRevenue, total_hours_worked: totalHours, staff_total: staffTotal, notes: notes, shifts: shifts }),
      }).then(function (res) {
        if (res.status === 401) return;
        if (res.status === 409) throw new Error('El día ya existe');
        if (!res.ok) return res.json().then(function (d) { throw new Error(d.message || 'Error al crear'); }).catch(function (e) { throw e.message ? e : new Error('Error al crear'); });
        setMsg('Día creado correctamente.', true);
        document.getElementById('registro-cargar').click();
      }).catch(function (e) {
        setMsg(e.message || 'Error al crear', false);
      });
    };
  }

  function bindEditForm(data) {
    var btn = document.getElementById('registro-guardar-patch');
    if (!btn) return;
    var date = data.date;
    btn.onclick = function () {
      var totalRevenue = parseFloat(document.getElementById('re-total_revenue').value) || 0;
      var totalHours = parseFloat(document.getElementById('re-total_hours').value) || 0;
      var notes = (document.getElementById('re-notes') && document.getElementById('re-notes').value) || null;
      var shiftInputs = document.querySelectorAll('[data-shift][data-field]');
      var shiftMap = {};
      shiftInputs.forEach(function (inp) {
        var name = inp.getAttribute('data-shift');
        if (!shiftMap[name]) shiftMap[name] = {};
        shiftMap[name][inp.getAttribute('data-field')] = inp.value;
      });
      var shifts = (data.shifts || []).map(function (s) {
        var o = shiftMap[s.shift_name] || {};
        var staffFloor = parseInt(o.staff_floor, 10) >= 0 ? Math.min(3, Math.max(0, parseInt(o.staff_floor, 10) || 0)) : (s.staff_floor ?? 0);
        var staffKitchen = parseInt(o.staff_kitchen, 10) >= 0 ? Math.min(3, Math.max(0, parseInt(o.staff_kitchen, 10) || 0)) : (s.staff_kitchen ?? 0);
        return {
          shift_name: s.shift_name,
          revenue: parseFloat(o.revenue) || s.revenue,
          hours_worked: parseFloat(o.hours_worked) || s.hours_worked,
          staff_floor: staffFloor,
          staff_kitchen: staffKitchen,
          feedback_q1: o.feedback_q1 || null,
          feedback_q2: o.feedback_q2 || null,
          feedback_q3: o.feedback_q3 || null,
          feedback_q4: o.feedback_q4 || null,
          feedback_q5: o.feedback_q5 || null,
          recorded_by: s.recorded_by,
          edited_by: s.edited_by,
        };
      });
      var staffTotal = shifts.reduce(function (sum, s) { return sum + (s.staff_floor || 0) + (s.staff_kitchen || 0); }, 0);
      var msgEl = document.getElementById('registro-message');
      function setMsg(t, ok) { if (msgEl) { msgEl.textContent = t || ''; msgEl.classList.toggle('hidden', !t); msgEl.classList.toggle('success-msg', !!t && ok); msgEl.classList.toggle('error-msg', !!t && !ok); } }
      setMsg('');
      auth.fetchWithAuth('/api/execution/' + date, {
        method: 'PATCH',
        body: JSON.stringify({
          total_revenue: totalRevenue,
          total_hours_worked: totalHours,
          staff_total: staffTotal,
          notes: notes,
          shifts: shifts,
        }),
      }).then(function (res) {
        if (res.status === 401) return;
        if (!res.ok) return res.json().then(function (d) { throw new Error(d.message || 'Error al guardar'); }).catch(function (e) { throw e.message ? e : new Error('Error al guardar'); });
        setMsg('Guardado correctamente.', true);
      }).catch(function (e) {
        setMsg(e.message || 'Error al guardar', false);
      });
    };
  }

  global.LUCAS_REGISTRO_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
