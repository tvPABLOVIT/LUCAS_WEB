(function (global) {
  'use strict';
  var auth = global.LUCAS_AUTH;

  function safeParseResponse(r) {
    return r.text().then(function (text) {
      if (!text || !text.trim()) return null;
      try { return JSON.parse(text); } catch (e) { return null; }
    });
  }

  function weekNumber() {
    var d = new Date();
    d.setDate(d.getDate() + 4 - (d.getDay() || 7));
    var yearStart = new Date(d.getFullYear(), 0, 1);
    return 'Semana ' + Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
  }

  function render(container) {
    var week = weekNumber();
    var tabsHtml = '<div class="config-tabs">' +
      '<button type="button" class="config-tab active" data-tab="usuarios">Usuarios</button>' +
      '<button type="button" class="config-tab" data-tab="parametros">Parámetros</button>' +
      '<button type="button" class="config-tab" data-tab="integraciones">Integraciones</button>' +
      '</div>';
    var contentHtml = '<div id="config-content-usuarios" class="config-panel active"></div>' +
      '<div id="config-content-parametros" class="config-panel"></div>' +
      '<div id="config-content-integraciones" class="config-panel"></div>';
    container.innerHTML = '<h2 class="view-title">Configuración ' + week + '</h2>' +
      '<div class="card">' + tabsHtml + contentHtml + '</div>';
    bindTabs(container);
    renderUsuarios();
    renderParametros();
    renderIntegraciones();
  }

  function bindTabs(container) {
    container.querySelectorAll('.config-tab').forEach(function (tab) {
      tab.addEventListener('click', function () {
        var t = tab.getAttribute('data-tab');
        container.querySelectorAll('.config-tab').forEach(function (x) { x.classList.remove('active'); });
        container.querySelectorAll('.config-panel').forEach(function (x) { x.classList.remove('active'); });
        tab.classList.add('active');
        var panel = document.getElementById('config-content-' + t);
        if (panel) panel.classList.add('active');
      });
    });
  }

  function showMsg(el, text, isSuccess) {
    if (!el) return;
    el.textContent = text || '';
    el.className = 'config-msg' + (text ? (isSuccess ? ' success-msg' : ' error-msg') : '');
    el.classList.toggle('hidden', !text);
  }

  // ——— Usuarios ———
  function renderUsuarios() {
    var panel = document.getElementById('config-content-usuarios');
    if (!panel) return;
    panel.innerHTML = '<div class="config-section config-section-header">' +
      '<h3 class="config-section-title">Añadir usuario</h3>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-user-name">Nombre</label><input type="text" id="config-user-name" placeholder="Nombre" /></div>' +
      '<div class="form-group"><label for="config-user-email">Email</label><input type="email" id="config-user-email" placeholder="Email" required /></div>' +
      '<div class="form-group"><label for="config-user-pin">PIN (mín. 4, máx. 12 caracteres)</label><input type="password" id="config-user-pin" placeholder="PIN" minlength="4" maxlength="12" /></div>' +
      '<div class="form-group"><label for="config-user-role">Rol</label><select id="config-user-role"><option value="user">user</option><option value="manager">manager</option><option value="admin">admin</option><option value="master">Master</option></select></div>' +
      '</div>' +
      '<button type="button" id="config-user-add" class="btn-primary">Añadir usuario</button>' +
      '</div>' +
      '<div id="config-edit-user-panel" class="config-section config-section-header hidden">' +
      '<h3 class="config-section-title">Editar usuario</h3>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-edit-name">Nombre</label><input type="text" id="config-edit-name" /></div>' +
      '<div class="form-group"><label for="config-edit-email">Email</label><input type="email" id="config-edit-email" /></div>' +
      '<div class="form-group"><label for="config-edit-role">Rol</label><select id="config-edit-role"><option value="user">user</option><option value="manager">manager</option><option value="admin">admin</option><option value="master">Master</option></select></div>' +
      '<div class="form-group"><label for="config-edit-pin">Nuevo PIN (opcional, mín. 4, máx. 12)</label><input type="password" id="config-edit-pin" placeholder="Dejar vacío para no cambiar" maxlength="12" /></div>' +
      '<div class="form-group config-edit-active"><label class="config-checkbox"><input type="checkbox" id="config-edit-active" /> Activo</label></div>' +
      '</div>' +
      '<button type="button" id="config-edit-guardar" class="btn-primary">Guardar</button> <button type="button" id="config-edit-cancelar" class="btn-secondary">Cancelar</button>' +
      '</div>' +
      '<h3 class="config-section-title" style="margin-top:1.5rem">Usuarios del sistema</h3>' +
      '<div id="config-users-table-wrap"><p class="loading">Cargando…</p></div>' +
      '<p id="config-users-msg" class="config-msg hidden"></p>';
    loadUsers();
    document.getElementById('config-user-add') && document.getElementById('config-user-add').addEventListener('click', addUser);
    document.getElementById('config-edit-guardar') && document.getElementById('config-edit-guardar').addEventListener('click', saveEditUser);
    document.getElementById('config-edit-cancelar') && document.getElementById('config-edit-cancelar').addEventListener('click', cancelEditUser);
  }

  var editingUserId = null;

  function cancelEditUser() {
    editingUserId = null;
    var panel = document.getElementById('config-edit-user-panel');
    if (panel) panel.classList.add('hidden');
  }

  function saveEditUser() {
    if (!editingUserId) return;
    var msgEl = document.getElementById('config-users-msg');
    var name = document.getElementById('config-edit-name');
    var email = document.getElementById('config-edit-email');
    var role = document.getElementById('config-edit-role');
    var pin = document.getElementById('config-edit-pin');
    var active = document.getElementById('config-edit-active');
    var body = {};
    if (name && name.value.trim() !== '') body.fullName = name.value.trim();
    if (email) body.email = email.value.trim();
    if (role) body.role = role.value;
    if (active) body.isActive = active.checked;
    if (pin && pin.value.trim() !== '') {
      if (pin.value.length < 4) { showMsg(msgEl, 'El PIN debe tener al menos 4 caracteres.', false); return; }
      if (pin.value.length > 12) { showMsg(msgEl, 'El PIN no puede tener más de 12 caracteres.', false); return; }
      body.pin = pin.value;
    }
    if (Object.keys(body).length === 0) { cancelEditUser(); return; }
    showMsg(msgEl, '');
    auth.fetchWithAuth('/api/users/' + editingUserId, { method: 'PATCH', body: JSON.stringify(body) }).then(function (r) {
      return safeParseResponse(r).then(function (d) {
        if (!r.ok) throw new Error(d && d.message ? d.message : 'Error al actualizar');
        return d;
      });
    }).then(function () {
      showMsg(msgEl, 'Usuario actualizado.', true);
      cancelEditUser();
      loadUsers();
    }).catch(function (e) { showMsg(msgEl, e.message || 'Error', false); });
  }

  function loadUsers() {
    var wrap = document.getElementById('config-users-table-wrap');
    var msgEl = document.getElementById('config-users-msg');
    if (!wrap) return;
    auth.fetchWithAuth('/api/users').then(function (r) {
      if (r.status === 401) return null;
      if (r.status === 403) { wrap.innerHTML = '<p class="config-msg error-msg">Solo administradores o Master pueden gestionar usuarios.</p>'; return null; }
      if (!r.ok) return r.text().then(function (t) { try { var d = JSON.parse(t); throw new Error(d.message || 'Error al cargar usuarios'); } catch (e) { if (e instanceof SyntaxError) throw new Error('Error al cargar usuarios'); throw e; } });
      return safeParseResponse(r).then(function (d) { if (d == null) throw new Error('Respuesta del servidor no válida.'); return d; });
    }).then(function (list) {
      if (!list) return;
      if (list.length === 0) { wrap.innerHTML = '<p class="config-empty">No hay usuarios.</p>'; return; }
      var th = '<thead><tr><th>Nombre</th><th>Email</th><th>Rol</th><th>Activo</th><th>Acciones</th></tr></thead><tbody>';
      var rows = list.map(function (u) {
        var acciones = '<button type="button" class="btn-secondary btn-sm config-user-edit" data-id="' + (u.id || '') + '">Editar</button> ' +
          '<button type="button" class="btn-danger btn-sm config-user-delete" data-id="' + (u.id || '') + '">Eliminar</button>';
        return '<tr><td>' + escapeHtml(u.fullName || '') + '</td><td>' + escapeHtml(u.email || '') + '</td><td>' + escapeHtml(u.role || 'user') + '</td><td>' + (u.isActive ? 'True' : 'False') + '</td><td>' + acciones + '</td></tr>';
      }).join('');
      wrap.innerHTML = '<table class="config-table">' + th + rows + '</tbody></table>';
      wrap.querySelectorAll('.config-user-edit').forEach(function (btn) { btn.addEventListener('click', function () { editUser(btn.getAttribute('data-id')); }); });
      wrap.querySelectorAll('.config-user-delete').forEach(function (btn) { btn.addEventListener('click', function () { deleteUser(btn.getAttribute('data-id')); }); });
    }).catch(function (e) {
      wrap.innerHTML = '<p class="config-msg error-msg">' + (e.message || 'Error al cargar usuarios') + '</p>';
    });
  }

  function escapeHtml(s) {
    if (s == null) return '';
    var div = document.createElement('div');
    div.textContent = s;
    return div.innerHTML;
  }

  function addUser() {
    var name = document.getElementById('config-user-name');
    var email = document.getElementById('config-user-email');
    var pin = document.getElementById('config-user-pin');
    var role = document.getElementById('config-user-role');
    var msgEl = document.getElementById('config-users-msg');
    if (!name || !pin) return;
    var body = { fullName: name.value.trim(), email: email ? email.value.trim() : '', pin: pin.value, role: role ? role.value : 'user' };
    if (!body.fullName) { showMsg(msgEl, 'El nombre es obligatorio.', false); return; }
    if (!body.email) { showMsg(msgEl, 'El email es obligatorio.', false); return; }
    if (body.pin.length < 4) { showMsg(msgEl, 'El PIN debe tener al menos 4 caracteres.', false); return; }
    if (body.pin.length > 12) { showMsg(msgEl, 'El PIN no puede tener más de 12 caracteres.', false); return; }
    showMsg(msgEl, '');
    auth.fetchWithAuth('/api/users', { method: 'POST', body: JSON.stringify(body) }).then(function (r) {
      if (r.status === 401) return null;
      return safeParseResponse(r).then(function (d) {
        if (!r.ok) throw new Error(d && d.message ? d.message : 'Error al crear usuario');
        return d;
      });
    }).then(function () {
      showMsg(msgEl, 'Usuario añadido.', true);
      if (name) name.value = ''; if (email) email.value = ''; if (pin) pin.value = ''; if (role) role.value = 'user';
      loadUsers();
    }).catch(function (e) { showMsg(msgEl, e.message || 'Error al añadir usuario', false); });
  }

  function editUser(id) {
    if (!id) return;
    editingUserId = id;
    auth.fetchWithAuth('/api/users').then(function (r) {
      if (!r.ok) return null;
      return safeParseResponse(r);
    }).then(function (list) {
      if (!list) return;
      var u = list.find(function (x) { return x.id === id; });
      if (!u) return;
      var panel = document.getElementById('config-edit-user-panel');
      var nameEl = document.getElementById('config-edit-name');
      var emailEl = document.getElementById('config-edit-email');
      var roleEl = document.getElementById('config-edit-role');
      var pinEl = document.getElementById('config-edit-pin');
      var activeEl = document.getElementById('config-edit-active');
      if (panel) panel.classList.remove('hidden');
      if (nameEl) nameEl.value = u.fullName || '';
      if (emailEl) emailEl.value = u.email || '';
      if (roleEl) roleEl.value = u.role || 'user';
      if (pinEl) pinEl.value = '';
      if (activeEl) activeEl.checked = u.isActive !== false;
    });
  }

  function deleteUser(id) {
    if (!id || !confirm('¿Eliminar este usuario?')) return;
    var msgEl = document.getElementById('config-users-msg');
    showMsg(msgEl, '');
    auth.fetchWithAuth('/api/users/' + id, { method: 'DELETE' }).then(function (r) {
      if (r.status === 204 || r.ok) return;
      return safeParseResponse(r).then(function (d) { if (!r.ok) throw new Error(d && d.message ? d.message : 'Error al eliminar'); });
    }).then(function () { showMsg(msgEl, 'Usuario eliminado.', true); loadUsers(); cancelEditUser(); }).catch(function (e) { showMsg(msgEl, e.message || 'Error', false); });
  }

  // ——— Parámetros ———
  function renderParametros() {
    var panel = document.getElementById('config-content-parametros');
    if (!panel) return;
    panel.innerHTML = '<div class="config-section">' +
      '<h3 class="config-section-title">Parámetros del sistema</h3>' +
      '<p class="config-desc">Duración de cada turno en horas (p. ej. 4). Las horas de equipo se calculan como (Personal sala + Personal cocina) × este valor por turno. Sirve para el resumen Sala: X-Y-Z | Cocina: X-Y-Z y para repartir las horas reales entre sala y cocina.</p>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-HorasPorTurno">Horas por turno</label><input type="number" id="config-HorasPorTurno" step="0.1" min="1" max="24" value="4" /></div>' +
      '<div class="form-group"><label for="config-ProductividadIdealEurHora">Productividad ideal (€/h)</label><input type="number" id="config-ProductividadIdealEurHora" step="0.01" value="50" /></div>' +
      '<div class="form-group"><label for="config-CostePersonalPorHora">Coste por hora de personal (€/h)</label><input type="number" id="config-CostePersonalPorHora" step="0.01" value="15.73" /></div>' +
      '<div class="form-group"><label for="config-DescuentoFacturacionManualPorcentaje">Descuento facturación manual (%)</label><input type="number" id="config-DescuentoFacturacionManualPorcentaje" step="0.1" min="0" max="100" value="9.1" placeholder="9.1" /></div>' +
      '</div>' +
      '<p class="config-desc">Objetivo de facturación por hora trabajada; se usará en Estimaciones para comparar con la productividad real.</p>' +
      '<p class="config-desc">Coste medio por hora de personal; se usa en Estimaciones para el KPI Costo de personal (horas × €/h).</p>' +
      '<p class="config-desc">Porcentaje que se descuenta a la facturación cuando el usuario la introduce manualmente (ej. 9,1). No se aplica a la facturación importada del Excel.</p>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-FacturacionObjetivoSemanal">Facturación objetivo (€/semana)</label><input type="number" id="config-FacturacionObjetivoSemanal" step="1" min="0" placeholder="ej. 12000" /></div>' +
      '</div>' +
      '<p class="config-desc">Objetivo de facturación semanal; en el Dashboard se muestra la facturación real vs este objetivo (%).</p>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-PrediccionConservadoraFactor">Factor predicción conservadora (0,01–1)</label><input type="number" id="config-PrediccionConservadoraFactor" step="0.01" min="0.01" max="1" placeholder="1 = sin ajuste" /></div>' +
      '</div>' +
      '<p class="config-desc">Si lo defines (ej. 0,97), todas las predicciones se multiplican por este factor para bajar la estimación. Vacío o 1 = sin efecto.</p>' +
      '</div>' +
      '<div class="config-section">' +
      '<h3 class="config-section-title">Corregir formato de facturación</h3>' +
      '<p class="config-desc">Corrige días guardados con formato incorrecto (ej. 1,60635 en lugar de 1.606,35 €). Solo afecta a valores entre 0 y 100 con decimales que se interpretan como miles.</p>' +
      '<button type="button" id="config-fix-revenue-format" class="btn-secondary">Corregir facturación guardada</button>' +
      '<p id="config-fix-revenue-msg" class="config-msg hidden"></p></div>' +
      '<div class="config-section">' +
      '<h3 class="config-section-title">Empleados</h3>' +
      '<p class="config-desc">Lista de empleados con horas semanales de contrato (ej. 40 = 8h×5 días + 2 libres). Se usa para calcular el coste de personal total y el % sobre facturación estimada.</p>' +
      '<div class="form-row"><div class="form-group"><label for="config-empleado-name">Nombre</label><input type="text" id="config-empleado-name" placeholder="Nombre empleado" /></div>' +
      '<div class="form-group"><label for="config-empleado-hours">Horas semanales (1–80)</label><input type="number" id="config-empleado-hours" min="1" max="80" value="40" /></div>' +
      '<div class="form-group"><label>&nbsp;</label><button type="button" id="config-empleado-add" class="btn-primary">Añadir empleado</button></div></div>' +
      '<ul id="config-empleados-list" class="config-list"></ul>' +
      '</div>' +
      '<div class="config-section">' +
      '<h3 class="config-section-title">Ubicación del restaurante (para el clima)</h3>' +
      '<p class="config-desc">Escribe la dirección y pulsa «Obtener coordenadas»: se rellenan solas. Luego guarda parámetros. También puedes escribirlas a mano.</p>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-NombreRestaurante">Nombre del restaurante</label><input type="text" id="config-NombreRestaurante" /></div>' +
      '<div class="form-group"><label for="config-DireccionRestaurante">Dirección (ciudad o dirección completa)</label><input type="text" id="config-DireccionRestaurante" /></div>' +
      '</div>' +
      '<div class="form-row">' +
      '<div class="form-group"><label>&nbsp;</label><button type="button" id="config-geocode-btn" class="btn-secondary">Obtener coordenadas desde la dirección</button></div>' +
      '</div>' +
      '<div class="form-row">' +
      '<div class="form-group"><label for="config-LatRestaurante">Latitud</label><input type="text" id="config-LatRestaurante" placeholder="Se rellena al obtener coordenadas" readonly /></div>' +
      '<div class="form-group"><label for="config-LonRestaurante">Longitud</label><input type="text" id="config-LonRestaurante" placeholder="Se rellena al obtener coordenadas" readonly /></div>' +
      '</div></div>' +
      '<div class="config-section">' +
      '<h3 class="config-section-title">Ruta de la base de datos (solo lectura)</h3>' +
      '<div class="form-group"><input type="text" id="config-db-path" readonly value="—" /></div></div>' +
      '<div class="config-section">' +
      '<h3 class="config-section-title">Copias de seguridad</h3>' +
      '<p class="config-desc">Cree una copia antes de vaciar la BD. En la versión web la gestión de copias depende del servidor.</p>' +
      '<div class="form-row"><button type="button" id="config-backup-create" class="btn-secondary">Crear copia de seguridad</button>' +
      '<button type="button" id="config-backup-refresh" class="btn-secondary">Actualizar lista</button></div>' +
      '<div class="form-row"><div class="form-group"><label for="config-backup-restore">Restaurar desde copia:</label><select id="config-backup-restore"><option value="">—</option></select></div>' +
      '<button type="button" id="config-backup-restore-btn" class="btn-secondary">Restaurar desde copia</button></div></div>' +
      '<div class="config-section">' +
      '<h3 class="config-section-title">Limpiar base de datos</h3>' +
      '<p class="config-desc">Vacía toda la BD (días, análisis, patrones...). Los usuarios se mantienen. Se recomienda crear antes una copia de seguridad.</p>' +
      '<button type="button" id="config-database-clean" class="btn-danger">Limpiar base de datos</button></div>' +
      '<p id="config-param-msg" class="config-msg hidden"></p>' +
      '<button type="button" id="config-param-guardar" class="btn-primary">Guardar parámetros</button>';
    loadSettingsForParametros();
    document.getElementById('config-empleado-add') && document.getElementById('config-empleado-add').addEventListener('click', addEmpleado);
    document.getElementById('config-param-guardar') && document.getElementById('config-param-guardar').addEventListener('click', saveParametros);
    document.getElementById('config-geocode-btn') && document.getElementById('config-geocode-btn').addEventListener('click', obtenerCoordenadasDesdeDireccion);
    document.getElementById('config-backup-create') && document.getElementById('config-backup-create').addEventListener('click', createBackup);
    document.getElementById('config-backup-refresh') && document.getElementById('config-backup-refresh').addEventListener('click', refreshBackups);
    document.getElementById('config-backup-restore-btn') && document.getElementById('config-backup-restore-btn').addEventListener('click', restoreBackup);
    document.getElementById('config-database-clean') && document.getElementById('config-database-clean').addEventListener('click', cleanDatabase);
    document.getElementById('config-fix-revenue-format') && document.getElementById('config-fix-revenue-format').addEventListener('click', fixRevenueFormat);
  }

  function showFixRevenueMsg(text, isSuccess) {
    var el = document.getElementById('config-fix-revenue-msg');
    if (!el) return;
    el.textContent = text;
    el.classList.toggle('success', !!isSuccess);
    el.classList.remove('hidden');
  }

  function fixRevenueFormat() {
    var btn = document.getElementById('config-fix-revenue-format');
    if (btn) { btn.disabled = true; btn.setAttribute('aria-busy', 'true'); }
    auth.fetchWithAuth('/api/execution/fix-revenue-format', { method: 'POST' })
      .then(function (r) {
        if (r.status === 401) throw new Error('No autorizado');
        return safeParseResponse(r).then(function (data) {
          if (!r.ok) throw new Error(data && data.message ? data.message : 'Error');
          return data || { updated: 0, items: [] };
        });
      })
      .then(function (data) {
        if (!data || data.updated === 0) showFixRevenueMsg('No había datos que corregir.', true);
        else showFixRevenueMsg('Se corrigieron ' + data.updated + ' día(s): ' + (data.items || []).map(function (x) { return x.date + ' (' + x.before + ' → ' + x.after + ' €)'; }).join(', '), true);
      })
      .catch(function (e) { showFixRevenueMsg(e.message || 'Error al corregir.', false); })
      .then(function () {
        if (btn) { btn.disabled = false; btn.removeAttribute('aria-busy'); }
      });
  }

  var empleadosLocal = [];

  function loadSettingsForParametros() {
    auth.fetchWithAuth('/api/settings').then(function (r) {
      if (r.status === 401) return null;
      if (!r.ok) throw new Error('Error al cargar');
      return safeParseResponse(r);
    }).then(function (data) {
      if (!data) return;
      var el;
      if ((el = document.getElementById('config-HorasPorTurno'))) el.value = data.HorasPorTurno != null && data.HorasPorTurno !== '' ? data.HorasPorTurno : '4';
      if ((el = document.getElementById('config-ProductividadIdealEurHora'))) el.value = data.ProductividadIdealEurHora != null ? data.ProductividadIdealEurHora : '50';
      if ((el = document.getElementById('config-CostePersonalPorHora'))) el.value = data.CostePersonalPorHora != null ? data.CostePersonalPorHora : '15.73';
      if ((el = document.getElementById('config-DescuentoFacturacionManualPorcentaje'))) el.value = (data.DescuentoFacturacionManualPorcentaje != null && data.DescuentoFacturacionManualPorcentaje !== '') ? data.DescuentoFacturacionManualPorcentaje : '9.1';
      if ((el = document.getElementById('config-PrediccionConservadoraFactor'))) el.value = (data.PrediccionConservadoraFactor != null && data.PrediccionConservadoraFactor !== '') ? data.PrediccionConservadoraFactor : '';
      if ((el = document.getElementById('config-FacturacionObjetivoSemanal'))) el.value = (data.FacturacionObjetivoSemanal != null && data.FacturacionObjetivoSemanal !== '') ? data.FacturacionObjetivoSemanal : '';
      if ((el = document.getElementById('config-NombreRestaurante'))) el.value = data.NombreRestaurante != null ? data.NombreRestaurante : '';
      if ((el = document.getElementById('config-DireccionRestaurante'))) el.value = data.DireccionRestaurante != null ? data.DireccionRestaurante : '';
      var latEl = document.getElementById('config-LatRestaurante');
      var lonEl = document.getElementById('config-LonRestaurante');
      if (latEl) {
        latEl.value = data.LatRestaurante != null && data.LatRestaurante !== '' ? data.LatRestaurante : '';
        if (latEl.value) latEl.removeAttribute('readonly'); else latEl.setAttribute('readonly', 'readonly');
      }
      if (lonEl) {
        lonEl.value = data.LonRestaurante != null && data.LonRestaurante !== '' ? data.LonRestaurante : '';
        if (lonEl.value) lonEl.removeAttribute('readonly'); else lonEl.setAttribute('readonly', 'readonly');
      }
      try {
        var raw = (data.Empleados && data.Empleados.length) ? (typeof data.Empleados === 'string' ? JSON.parse(data.Empleados) : data.Empleados) : [];
        empleadosLocal = raw.map(function (e) { return { name: e.name || '', hours: e.hours != null ? e.hours : 40, position: e.position || '' }; });
      } catch (e) { empleadosLocal = []; }
      renderEmpleadosList();

      // Ruta de la base de datos (solo admin).
      var dbPathEl = document.getElementById('config-db-path');
      if (dbPathEl) {
        auth.fetchWithAuth('/api/database/info').then(function (r2) {
          if (r2.status === 403) { dbPathEl.value = 'Solo admin'; return null; }
          if (!r2.ok) return null;
          return safeParseResponse(r2);
        }).then(function (info) {
          if (!info) return;
          dbPathEl.value = info.db_path || '—';
        }).catch(function () { });
      }
    }).catch(function () {});
  }

  var POSICIONES = ['jefe de sala', 'manager', 'camarero', 'supervisor', 'jefe de cocina', 'segundo de cocina', 'cocinero', 'chef operativo'];

  function renderEmpleadosList() {
    var ul = document.getElementById('config-empleados-list');
    if (!ul) return;
    ul.innerHTML = empleadosLocal.map(function (emp, i) {
      var posOpts = POSICIONES.map(function (p) {
        var sel = (emp.position || '') === p ? ' selected' : '';
        return '<option value="' + escapeHtml(p) + '"' + sel + '>' + escapeHtml(p) + '</option>';
      }).join('');
      return '<li class="config-list-item">' +
        '<input type="text" class="config-empleado-name" data-i="' + i + '" value="' + escapeHtml(emp.name || '') + '" placeholder="Nombre" /> ' +
        '<input type="number" class="config-empleado-hours" data-i="' + i + '" min="0" value="' + (emp.hours != null ? emp.hours : 40) + '" /> ' +
        '<select class="config-empleado-position" data-i="' + i + '" title="Posición (Sala / Cocina)">' +
        '<option value="">—</option>' + posOpts + '</select> ' +
        '<button type="button" class="btn-secondary btn-sm config-empleado-quitar" data-i="' + i + '">Quitar</button></li>';
    }).join('');
    ul.querySelectorAll('.config-empleado-quitar').forEach(function (btn) {
      btn.addEventListener('click', function () { var i = parseInt(btn.getAttribute('data-i'), 10); empleadosLocal.splice(i, 1); renderEmpleadosList(); });
    });
    ul.querySelectorAll('.config-empleado-name, .config-empleado-hours, .config-empleado-position').forEach(function (inp) {
      inp.addEventListener('change', function () {
        var i = parseInt(inp.getAttribute('data-i'), 10);
        if (!empleadosLocal[i]) return;
        if (inp.classList.contains('config-empleado-name')) empleadosLocal[i].name = inp.value;
        else if (inp.classList.contains('config-empleado-hours')) empleadosLocal[i].hours = parseInt(inp.value, 10) || 0;
        else if (inp.classList.contains('config-empleado-position')) empleadosLocal[i].position = inp.value || '';
      });
    });
  }

  function addEmpleado() {
    var nameEl = document.getElementById('config-empleado-name');
    var hoursEl = document.getElementById('config-empleado-hours');
    var name = nameEl ? nameEl.value.trim() : '';
    var hours = hoursEl ? Math.min(80, Math.max(1, parseInt(hoursEl.value, 10) || 40)) : 40;
    if (!name) return;
    empleadosLocal.push({ name: name, hours: hours, position: '' });
    if (nameEl) nameEl.value = ''; if (hoursEl) hoursEl.value = '40';
    renderEmpleadosList();
  }

  function saveParametros() {
    var msgEl = document.getElementById('config-param-msg');
    var horas = parseFloat(document.getElementById('config-HorasPorTurno')?.value ?? '4', 10);
    var prod = parseFloat(document.getElementById('config-ProductividadIdealEurHora')?.value ?? '50', 10);
    var coste = parseFloat(document.getElementById('config-CostePersonalPorHora')?.value ?? '15.73', 10);
    var descuentoManual = document.getElementById('config-DescuentoFacturacionManualPorcentaje')?.value?.trim() ?? '';
    if (isNaN(horas) || horas < 1 || horas > 24) { showMsg(msgEl, 'Horas por turno debe estar entre 1 y 24.', false); return; }
    if (isNaN(prod) || prod < 0) { showMsg(msgEl, 'Productividad ideal debe ser ≥ 0.', false); return; }
    if (isNaN(coste) || coste < 0) { showMsg(msgEl, 'Coste por hora de personal debe ser ≥ 0.', false); return; }
    if (descuentoManual !== '' && (isNaN(parseFloat(descuentoManual.replace(',', '.'))) || parseFloat(descuentoManual.replace(',', '.')) < 0 || parseFloat(descuentoManual.replace(',', '.')) > 100)) { showMsg(msgEl, 'Descuento facturación manual debe estar entre 0 y 100.', false); return; }
    var factorConservador = document.getElementById('config-PrediccionConservadoraFactor')?.value?.trim() ?? '';
    var factObjSem = document.getElementById('config-FacturacionObjetivoSemanal')?.value?.trim() ?? '';
    var body = {
      HorasPorTurno: String(horas),
      ProductividadIdealEurHora: String(prod),
      CostePersonalPorHora: String(coste),
      DescuentoFacturacionManualPorcentaje: descuentoManual === '' ? '9.1' : String(descuentoManual.replace(',', '.')),
      FacturacionObjetivoSemanal: factObjSem === '' ? '' : String(parseFloat(factObjSem.replace(',', '.')) || 0),
      PrediccionConservadoraFactor: factorConservador === '' || factorConservador === '1' ? '' : factorConservador,
      NombreRestaurante: document.getElementById('config-NombreRestaurante')?.value ?? '',
      DireccionRestaurante: document.getElementById('config-DireccionRestaurante')?.value ?? '',
      LatRestaurante: (document.getElementById('config-LatRestaurante')?.value ?? '').trim(),
      LonRestaurante: (document.getElementById('config-LonRestaurante')?.value ?? '').trim(),
      Empleados: JSON.stringify(empleadosLocal)
    };
    auth.fetchWithAuth('/api/settings', { method: 'PATCH', body: JSON.stringify(body) }).then(function (r) {
      if (r.status === 401) return Promise.reject(new Error('Sesión expirada'));
      if (!r.ok) return safeParseResponse(r).then(function (d) { throw new Error((d && d.message) || 'Error al guardar'); });
      return safeParseResponse(r);
    }).then(function () { showMsg(msgEl, 'Parámetros guardados.', true); loadSettingsForParametros(); }).catch(function (e) { showMsg(msgEl, e.message || 'Error al guardar', false); });
  }

  function obtenerCoordenadasDesdeDireccion() {
    var msgEl = document.getElementById('config-param-msg');
    var address = (document.getElementById('config-DireccionRestaurante')?.value ?? '').trim();
    if (!address) { showMsg(msgEl, 'Escribe primero la dirección.', false); return; }
    showMsg(msgEl, 'Buscando coordenadas…', true);
    var q = 'address=' + encodeURIComponent(address);
    auth.fetchWithAuth('/api/weather/geocode?' + q).then(function (r) { return r.ok ? safeParseResponse(r) : Promise.resolve(null); }).then(function (data) {
      if (!data) { showMsg(msgEl, 'No se pudieron obtener coordenadas.', false); return; }
      if (data.lat != null && data.lon != null) {
        var latEl = document.getElementById('config-LatRestaurante');
        var lonEl = document.getElementById('config-LonRestaurante');
        if (latEl) latEl.value = data.lat;
        if (lonEl) lonEl.value = data.lon;
        latEl.removeAttribute('readonly');
        lonEl.removeAttribute('readonly');
        showMsg(msgEl, 'Coordenadas obtenidas. Guarda parámetros para aplicar.', true);
      } else {
        showMsg(msgEl, data.message || 'No se encontraron coordenadas para esta dirección.', false);
      }
    }).catch(function () { showMsg(msgEl, 'Error al obtener coordenadas.', false); });
  }

  function createBackup() {
    var msgEl = document.getElementById('config-param-msg');
    auth.fetchWithAuth('/api/database/backup', { method: 'POST' }).then(function (r) {
      if (!r.ok) throw new Error('Error');
      return r.text().then(function (t) { try { return t && t.trim() ? JSON.parse(t) : {}; } catch (e) { return {}; } });
    }).then(function (d) { showMsg(msgEl, (d && d.message) || 'Copia solicitada.', true); }).catch(function () { showMsg(msgEl, 'No disponible en esta versión.', false); });
  }

  function refreshBackups() {
    auth.fetchWithAuth('/api/database/backups').then(function (r) {
      if (!r.ok) return null;
      return safeParseResponse(r);
    }).then(function (list) {
      var sel = document.getElementById('config-backup-restore');
      if (!sel) return;
      sel.innerHTML = '<option value="">—</option>' + (list && list.length ? list.map(function (b) { return '<option value="' + escapeHtml(b.path || b.name || '') + '">' + escapeHtml(b.name || b.path || '') + '</option>'; }).join('') : '');
    }).catch(function () {});
  }

  function restoreBackup() {
    var msgEl = document.getElementById('config-param-msg');
    var sel = document.getElementById('config-backup-restore');
    var path = sel && sel.value ? sel.value : '';
    if (!path) { showMsg(msgEl, 'Seleccione una copia.', false); return; }
    auth.fetchWithAuth('/api/database/restore', { method: 'POST', body: JSON.stringify({ path: path }) }).then(function (r) {
      return safeParseResponse(r).then(function (d) {
        if (!r.ok) throw new Error(d && d.message ? d.message : 'Error');
        return d;
      });
    }).then(function (d) { showMsg(msgEl, d.message || 'Restauración solicitada.', true); }).catch(function (e) { showMsg(msgEl, e.message || 'No disponible.', false); });
  }

  function cleanDatabase() {
    var msgEl = document.getElementById('config-param-msg');
    var crearCopia = confirm('¿Crear copia de seguridad antes de vaciar?\n\nAceptar = crear copia y luego preguntar si vaciar.\nCancelar = no crear copia, ir a preguntar si vaciar.');
    if (crearCopia) {
      auth.fetchWithAuth('/api/database/backup', { method: 'POST' }).then(function (r) {
        if (r.ok) return safeParseResponse(r);
        return null;
      }).then(function (d) {
        if (d && d.message) showMsg(msgEl, d.message, true);
      }).catch(function () {});
    }
    if (!confirm('¿Vaciar la base de datos? Los usuarios se mantienen. Esta acción no se puede deshacer.')) return;
    auth.fetchWithAuth('/api/database/clean', { method: 'POST' }).then(function (r) {
      if (r.status === 401 || r.status === 403) throw new Error('No autorizado');
      if (!r.ok) throw new Error('Error');
      return safeParseResponse(r);
    }).then(function (d) { showMsg(msgEl, d.message || 'Base de datos limpiada.', true); }).catch(function (e) { showMsg(msgEl, e.message || 'Error', false); });
  }

  // ——— Integraciones ———
  function renderIntegraciones() {
    var panel = document.getElementById('config-content-integraciones');
    if (!panel) return;
    panel.innerHTML = '<div class="config-section config-section-integraciones">' +
      '<h3 class="config-section-title">Integraciones y backend</h3>' +
      '<p class="config-desc">Configuración del backend (tablet/feedback) y enlaces externos.</p>' +
      '<div class="config-integration-box config-integration-backend">' +
      '<h4>Backend (tablet / feedback)</h4>' +
      '<p class="config-desc">URL del backend al que se conecta la tablet (Modo B) o la URL pública del túnel (Modo A con cloudflared).</p>' +
      '<div class="form-group"><label for="config-BackendUrl">URL</label><input type="url" id="config-BackendUrl" placeholder="https://..." /></div>' +
      '<div class="form-group"><label class="config-checkbox"><input type="checkbox" id="config-UsarTunnelCloudflared" /> Usar túnel (cloudflared) en este equipo</label></div>' +
      '<p class="config-desc">URL del túnel (quick tunnel): aparece unos segundos después de iniciar o reiniciar el túnel. Cópiala y ábrela en la tablet.</p>' +
      '<div class="form-row"><div class="form-group"><label for="config-QuickTunnelUrl">Quick Tunnel URL</label><input type="text" id="config-QuickTunnelUrl" /></div><button type="button" class="btn-secondary">Copiar URL</button></div>' +
      '<p class="config-desc">Token del túnel (URL fija): si lo rellenas, la URL no cambiará al reiniciar.</p>' +
      '<div class="form-group"><label for="config-TunnelToken">Token del túnel</label><input type="text" id="config-TunnelToken" /></div>' +
      '<div class="form-row"><button type="button" class="btn-secondary">Reiniciar backend</button><button type="button" class="btn-secondary">Reiniciar túnel</button></div></div>' +
      '<div class="config-integration-box config-integration-clima">' +
      '<h4>Clima (API)</h4>' +
      '<p class="config-desc">Clave API para obtener tiempo (opcional; Open-Meteo no requiere clave).</p>' +
      '<div class="form-group"><label for="config-WeatherApiKey">Clave API</label><input type="text" id="config-WeatherApiKey" /></div></div>' +
      '<div class="config-integration-box config-integration-gemini">' +
      '<h4>Gemini (IA / resúmenes)</h4>' +
      '<p class="config-desc">Clave API de Google AI Studio (gratuita) para generar resúmenes de turno con IA en Registro.</p>' +
      '<div class="form-group"><label for="config-GeminiApiKey">Clave API</label><input type="text" id="config-GeminiApiKey" /></div></div>' +
      '<div class="config-integration-box config-integration-sheets">' +
      '<h4>Google Sheets</h4>' +
      '<p class="config-desc">URL de la hoja donde exportar resúmenes.</p>' +
      '<div class="form-group"><label for="config-GoogleSheetsUrl">URL de la hoja</label><input type="url" id="config-GoogleSheetsUrl" placeholder="https://docs.google.com/..." /></div>' +
      '<p class="config-desc">Archivo de credenciales (cuenta de servicio): nombre de archivo o ruta completa.</p>' +
      '<div class="form-group"><label for="config-GoogleCredentialsPath">Ruta credenciales</label><input type="text" id="config-GoogleCredentialsPath" /></div>' +
      '<div class="form-row"><button type="button" class="btn-secondary" id="config-sheets-open">Abrir Google Sheets</button><button type="button" class="btn-secondary" id="config-sheets-export">Exportar todo al Google Sheet</button><button type="button" class="btn-secondary" id="config-import-excel-btn">Importar archivo de estimaciones (Excel)</button></div>' +
      '<input type="file" id="config-import-excel-file" accept=".xlsx" style="display:none" />' +
      '<p class="config-desc">Exportar todo envía todos los días guardados al sheet. Importar: seleccione un Excel (ej. s6_2026.xlsx); extrae facturaciones de 2 sem antes y actualiza BD y Google Sheet.</p></div>' +
      '<p id="config-int-msg" class="config-msg hidden"></p>' +
      '<button type="button" id="config-int-guardar" class="btn-primary btn-large">Guardar integraciones</button></div>';
    loadSettingsForIntegraciones();
    document.getElementById('config-int-guardar') && document.getElementById('config-int-guardar').addEventListener('click', saveIntegraciones);
    var copyBtn = panel.querySelector('.config-integration-backend .form-row button');
    if (copyBtn) copyBtn.addEventListener('click', function () { var u = document.getElementById('config-QuickTunnelUrl'); if (u && u.value) { navigator.clipboard.writeText(u.value); showMsg(document.getElementById('config-int-msg'), 'URL copiada.', true); } });
    document.getElementById('config-sheets-open') && document.getElementById('config-sheets-open').addEventListener('click', function () { var u = document.getElementById('config-GoogleSheetsUrl'); if (u && u.value) window.open(u.value, '_blank'); });
    document.getElementById('config-sheets-export') && document.getElementById('config-sheets-export').addEventListener('click', function () {
      var msgEl = document.getElementById('config-int-msg');
      showMsg(msgEl, 'Exportando…');
      auth.fetchWithAuth('/api/import/sheets-export-all', { method: 'POST' }).then(function (r) {
        if (r.status === 401) return null;
        return safeParseResponse(r).then(function (d) {
          if (!r.ok) throw new Error(d && d.message ? d.message : 'Error al exportar');
          return d;
        });
      }).then(function (d) {
        if (!d) return;
        showMsg(msgEl, (d.message || 'Exportado.') + (d.count != null ? ' ' + d.count + ' días.' : ''), true);
      }).catch(function (e) { showMsg(msgEl, e.message || 'Error al exportar', false); });
    });
    var importBtn = document.getElementById('config-import-excel-btn');
    var importFile = document.getElementById('config-import-excel-file');
    if (importBtn && importFile) {
      importBtn.addEventListener('click', function () { importFile.click(); });
      importFile.addEventListener('change', function () {
        if (!importFile.files || !importFile.files[0]) return;
        var fd = new FormData();
        fd.append('file', importFile.files[0]);
        var msgEl = document.getElementById('config-int-msg');
        showMsg(msgEl, 'Importando…');
        auth.fetchWithAuth('/api/import/estimacion-excel', { method: 'POST', body: fd }).then(function (r) {
          if (r.status === 401) return null;
          return safeParseResponse(r).then(function (d) {
            if (!r.ok) throw new Error(d && d.message ? d.message : (d && d.errors && d.errors.length ? d.errors.join(' ') : 'Error al importar'));
            return d;
          });
        }).then(function (d) {
          if (!d) return;
          var msg = (d.message && d.message.length > 0) ? d.message : 'Importado.';
          if (d.days_created != null && !d.message) msg += ' Días creados: ' + d.days_created + '.';
          if (d.days_updated != null && !d.message) msg += ' Días actualizados: ' + d.days_updated + '.';
          if (d.shifts_updated != null && !d.message) msg += ' Turnos: ' + d.shifts_updated + '.';
          if (d.errors && d.errors.length) msg += (msg ? ' ' : '') + d.errors.join(' ');
          showMsg(msgEl, msg, true);
        }).catch(function (e) { showMsg(msgEl, e.message || 'Error al importar', false); });
        importFile.value = '';
      });
    }
  }

  function getMondayOfCurrentWeek() {
    var d = new Date();
    var day = d.getDay();
    var diff = d.getDate() - day + (day === 0 ? -6 : 1);
    var monday = new Date(d.setDate(diff));
    return monday.getFullYear() + '-' + String(monday.getMonth() + 1).padStart(2, '0') + '-' + String(monday.getDate()).padStart(2, '0');
  }

  function loadSettingsForIntegraciones() {
    auth.fetchWithAuth('/api/settings').then(function (r) {
      if (r.status === 401) return null;
      if (!r.ok) throw new Error('Error al cargar');
      return safeParseResponse(r);
    }).then(function (data) {
      if (!data) return;
      var el;
      if ((el = document.getElementById('config-BackendUrl'))) el.value = data.BackendUrl != null ? data.BackendUrl : '';
      if ((el = document.getElementById('config-UsarTunnelCloudflared'))) el.checked = data.UsarTunnelCloudflared === 'true' || data.UsarTunnelCloudflared === true;
      if ((el = document.getElementById('config-QuickTunnelUrl'))) el.value = data.QuickTunnelUrl != null ? data.QuickTunnelUrl : '';
      if ((el = document.getElementById('config-TunnelToken'))) el.value = data.TunnelToken != null ? data.TunnelToken : '';
      if ((el = document.getElementById('config-WeatherApiKey'))) el.value = data.WeatherApiKey != null ? data.WeatherApiKey : '';
      if ((el = document.getElementById('config-GeminiApiKey'))) el.value = data.GeminiApiKey != null ? data.GeminiApiKey : '';
      if ((el = document.getElementById('config-GoogleSheetsUrl'))) el.value = data.GoogleSheetsUrl != null ? data.GoogleSheetsUrl : '';
      if ((el = document.getElementById('config-GoogleCredentialsPath'))) el.value = data.GoogleCredentialsPath != null ? data.GoogleCredentialsPath : '';
    }).catch(function () {});
  }

  function safeParseJson(res) {
    return res.text().then(function (t) {
      if (!t || !t.trim()) return null;
      try { return JSON.parse(t); } catch (e) { return null; }
    });
  }

  function saveIntegraciones() {
    var msgEl = document.getElementById('config-int-msg');
    var body = {
      BackendUrl: document.getElementById('config-BackendUrl')?.value ?? '',
      UsarTunnelCloudflared: document.getElementById('config-UsarTunnelCloudflared')?.checked ? 'true' : 'false',
      QuickTunnelUrl: document.getElementById('config-QuickTunnelUrl')?.value ?? '',
      TunnelToken: document.getElementById('config-TunnelToken')?.value ?? '',
      WeatherApiKey: document.getElementById('config-WeatherApiKey')?.value ?? '',
      GeminiApiKey: document.getElementById('config-GeminiApiKey')?.value ?? '',
      GoogleSheetsUrl: document.getElementById('config-GoogleSheetsUrl')?.value ?? '',
      GoogleCredentialsPath: document.getElementById('config-GoogleCredentialsPath')?.value ?? ''
    };
    auth.fetchWithAuth('/api/settings', { method: 'PATCH', body: JSON.stringify(body) }).then(function (r) {
      if (r.status === 401) return Promise.reject(new Error('Sesión expirada'));
      if (!r.ok) return safeParseResponse(r).then(function (d) { throw new Error((d && d.message) || 'Error al guardar'); });
      return safeParseResponse(r);
    }).then(function () { showMsg(msgEl, 'Integraciones guardadas.', true); }).catch(function (e) { showMsg(msgEl, e.message || 'Error al guardar', false); });
  }

  global.LUCAS_CONFIGURACION_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
