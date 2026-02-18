/**
 * Lucas Web — Vista Configuración
 * GET /api/settings, PATCH /api/settings
 */
(function (global) {
  var auth = global.LUCAS_AUTH;
  var FIELDS = [
    { key: 'ProductividadIdealEurHora', label: 'Productividad ideal (€/h)', type: 'number', step: '0.01' },
    { key: 'HorasPorTurno', label: 'Horas por turno', type: 'number', step: '0.1' },
    { key: 'CostePersonalPorHora', label: 'Coste personal por hora (€)', type: 'number', step: '0.01' },
    { key: 'FacturacionObjetivoSemanal', label: 'Facturación objetivo (€/semana)', type: 'number', step: '1' },
    { key: 'NombreRestaurante', label: 'Nombre restaurante', type: 'text' },
    { key: 'DireccionRestaurante', label: 'Dirección', type: 'text' },
    { key: 'LatRestaurante', label: 'Latitud', type: 'text' },
    { key: 'LonRestaurante', label: 'Longitud', type: 'text' }
  ];
  var PARAM_KEYS = ['ProductividadIdealEurHora', 'HorasPorTurno', 'CostePersonalPorHora', 'FacturacionObjetivoSemanal'];
  var RESTAURANT_KEYS = ['NombreRestaurante', 'DireccionRestaurante', 'LatRestaurante', 'LonRestaurante'];
  function render(container) {
    container.innerHTML = '<h2 class="view-title">Configuración</h2><div class="card"><h3>Parámetros</h3><div id="config-params-wrap"><p class="loading">Cargando…</p></div><h3 style="margin-top:1.5rem">Restaurante</h3><div id="config-restaurant-wrap"></div><p id="config-message" class="hidden"></p><button type="button" id="config-guardar" class="btn-primary hidden">Guardar cambios</button></div><div class="card" style="margin-top:1.5rem"><h3>Datos de prueba</h3><p>Vuelve a cargar 2 meses de datos de muestra (facturación, horas, turnos). Se borrarán los días de ejecución actuales.</p><button type="button" id="config-cargar-muestra" class="btn-secondary">Cargar datos de muestra</button><p id="config-demo-message" class="hidden" style="margin-top:0.5rem"></p></div>';
    var paramsWrap = document.getElementById('config-params-wrap'), restaurantWrap = document.getElementById('config-restaurant-wrap'), msgEl = document.getElementById('config-message'), btn = document.getElementById('config-guardar'), demoMsgEl = document.getElementById('config-demo-message'), btnDemo = document.getElementById('config-cargar-muestra');
    function showMsg(t, isSuccess) { if (msgEl) { msgEl.textContent = t || ''; msgEl.classList.toggle('hidden', !t); msgEl.classList.toggle('success-msg', !!t && isSuccess); msgEl.classList.toggle('error-msg', !!t && !isSuccess); } }
    function fieldHtml(f, data) { var val = (data[f.key] != null ? data[f.key] : '') || ''; var input = f.type === 'number' ? '<input type="number" id="config-' + f.key + '" step="' + (f.step || '1') + '" value="' + val + '" />' : '<input type="text" id="config-' + f.key + '" value="' + val.replace(/"/g, '&quot;') + '" />'; return '<div class="form-group"><label>' + f.label + '</label>' + input + '</div>'; }
    auth.fetchWithAuth('/api/settings').then(function (r) {
      if (r.status === 401) return null;
      if (!r.ok) throw new Error('Error al cargar');
      return r.json();
    }).then(function (data) {
      if (!data) return;
      var paramFields = FIELDS.filter(function (f) { return PARAM_KEYS.indexOf(f.key) !== -1; });
      var restaurantFields = FIELDS.filter(function (f) { return RESTAURANT_KEYS.indexOf(f.key) !== -1; });
      paramsWrap.innerHTML = '<div class="form-row" style="flex-wrap:wrap">' + paramFields.map(function (f) { return fieldHtml(f, data); }).join('') + '</div>';
      restaurantWrap.innerHTML = '<div class="form-row" style="flex-wrap:wrap">' + restaurantFields.map(function (f) { return fieldHtml(f, data); }).join('') + '</div>';
      btn.classList.remove('hidden');
      btn.onclick = function () {
        var body = {}; FIELDS.forEach(function (f) { var el = document.getElementById('config-' + f.key); if (el) body[f.key] = el.value; });
        showMsg('');
        auth.fetchWithAuth('/api/settings', { method: 'PATCH', body: JSON.stringify(body) }).then(function (r) {
          if (r.status === 401) return;
          if (!r.ok) throw new Error('Error al guardar');
          showMsg('Guardado correctamente.', true);
        }).catch(function (e) { showMsg(e.message || 'Error al guardar', false); });
      };
      if (btnDemo) {
        btnDemo.onclick = function () {
          if (!demoMsgEl) return;
          demoMsgEl.textContent = 'Cargando…';
          demoMsgEl.classList.remove('hidden', 'success-msg', 'error-msg');
          btnDemo.disabled = true;
          auth.fetchWithAuth('/api/seed/demo', { method: 'POST' }).then(function (r) {
            if (r.status === 401) return null;
            if (!r.ok) throw new Error('Error al cargar datos de muestra');
            return r.json();
          }).then(function (res) {
            if (!res) return;
            var msg = 'Datos de muestra cargados';
            if (res.count != null) msg += ' (' + res.count + ' días, ' + (res.minDate || '') + ' a ' + (res.maxDate || '') + ')';
            msg += '. Redirigiendo al Dashboard…';
            demoMsgEl.textContent = msg;
            demoMsgEl.classList.add('success-msg');
            demoMsgEl.classList.remove('error-msg');
            window.location.hash = 'dashboard';
          }).catch(function (e) {
            demoMsgEl.textContent = e.message || 'Error al cargar datos de muestra';
            demoMsgEl.classList.add('error-msg');
            demoMsgEl.classList.remove('success-msg');
          }).finally(function () { btnDemo.disabled = false; });
        };
      }
    }).catch(function () { paramsWrap.innerHTML = '<p class="error-msg">Error al cargar configuración.</p>'; });
  }
  global.LUCAS_CONFIGURACION_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
