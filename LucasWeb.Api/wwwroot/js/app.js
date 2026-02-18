(function (global) {
  var auth = global.LUCAS_AUTH, ROLES_FULL_WEB = ['admin', 'manager', 'master'];
  /** Quita el # de la URL para evitar problemas al volver a iniciar sesión (otro usuario). */
  function clearUrlHash() {
    var path = (window.location.pathname || '/').replace(/\/$/, '') || '/';
    if (window.history && window.history.replaceState) {
      window.history.replaceState(null, '', path);
    } else {
      window.location.hash = '';
    }
  }
  function showScreen(id) { document.querySelectorAll('#app .screen').forEach(function (s) { s.classList.add('hidden'); }); var el = document.getElementById(id); if (el) el.classList.remove('hidden'); }
  function setActiveTab(route) { document.querySelectorAll('.nav-tabs .tab').forEach(function (t) { t.classList.toggle('active', t.getAttribute('data-route') === route); }); }
  function renderDashboardContent(route) {
    var he = document.getElementById('header-extra');
    if (he) he.innerHTML = '';
    var c = document.getElementById('dashboard-content');
    if (!c) return;
    setActiveTab(route);
    if (route === 'dashboard' && global.LUCAS_DASHBOARD_VIEW) global.LUCAS_DASHBOARD_VIEW.render(c);
    else if (route === 'estimaciones' && global.LUCAS_ESTIMACIONES_VIEW) global.LUCAS_ESTIMACIONES_VIEW.render(c);
    else if (route === 'preguntas' && global.LUCAS_PREGUNTAS_VIEW) global.LUCAS_PREGUNTAS_VIEW.render(c);
    else if (route === 'configuracion' && global.LUCAS_CONFIGURACION_VIEW) global.LUCAS_CONFIGURACION_VIEW.render(c);
    else if (route === 'registro' && global.LUCAS_REGISTRO_VIEW) global.LUCAS_REGISTRO_VIEW.render(c);
    else c.innerHTML = '<p class="loading">Cargando…</p>';
  }
  function handleHash() { var hash = (window.location.hash || '#dashboard').replace('#', ''); if (!hash) hash = 'dashboard'; if (['dashboard', 'estimaciones', 'preguntas', 'configuracion', 'registro'].indexOf(hash) === -1) hash = 'dashboard'; renderDashboardContent(hash); }
  function init() { var r = document.getElementById('user-role'); if (r) r.textContent = 'Rol: ' + (auth.getRole() || '—'); document.getElementById('btn-logout').addEventListener('click', function () { auth.logout(); showScreen('screen-login'); clearUrlHash(); if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.showError) global.LUCAS_LOGIN_VIEW.showError(''); if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.init) global.LUCAS_LOGIN_VIEW.init(); }); window.addEventListener('hashchange', handleHash); document.querySelectorAll('.nav-tabs .tab').forEach(function (t) { t.addEventListener('click', function (e) { e.preventDefault(); var r2 = t.getAttribute('data-route'); if (r2) window.location.hash = r2; }); }); }
  function onLoginSuccess() {
    showScreen('screen-dashboard');
    var role = auth.getRole();
    var r = document.getElementById('user-role');
    if (r) r.textContent = 'Rol: ' + (role || '—');
    if (ROLES_FULL_WEB.indexOf(role) !== -1) {
      init();
      handleHash();
    } else {
      initUserOnly();
    }
  }
  function bootstrap() {
    if (global.LUCAS_LOGIN_VIEW) global.LUCAS_LOGIN_VIEW.init();
    auth.checkSession().then(function (me) {
      if (!me) { showScreen('screen-login'); clearUrlHash(); return; }
      if (ROLES_FULL_WEB.indexOf(me.role) !== -1) onLoginSuccess();
      else { showScreen('screen-dashboard'); initUserOnly(); }
    }).catch(function () { showScreen('screen-login'); clearUrlHash(); });
  }
  function initUserOnly() {
    var r = document.getElementById('user-role');
    if (r) r.textContent = 'Rol: ' + (auth.getRole() || 'user');
    document.querySelector('.nav-tabs') && (document.querySelector('.nav-tabs').style.setProperty('display', 'none'));
    var c = document.getElementById('dashboard-content');
    if (c && global.LUCAS_PREGUNTAS_VIEW) global.LUCAS_PREGUNTAS_VIEW.renderAndLoad(c);
    document.getElementById('btn-logout').addEventListener('click', function () { auth.logout(); showScreen('screen-login'); clearUrlHash(); if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.showError) global.LUCAS_LOGIN_VIEW.showError(''); if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.init) global.LUCAS_LOGIN_VIEW.init(); });
  }
  function onUnauthorized() {
    showScreen('screen-login');
    clearUrlHash();
    if (global.LUCAS_LOGIN_VIEW) {
      if (global.LUCAS_LOGIN_VIEW.init) global.LUCAS_LOGIN_VIEW.init();
      if (global.LUCAS_LOGIN_VIEW.showError) global.LUCAS_LOGIN_VIEW.showError('Sesión expirada o no autorizada. Vuelve a iniciar sesión.');
    }
  }
  function run() {
    if (!global.LUCAS_AUTH) {
      document.body.innerHTML = '<div style="padding:2rem;font-family:sans-serif;text-align:center;max-width:480px;margin:2rem auto;">' +
        '<h2>No se pudo cargar la aplicación</h2>' +
        '<p>Faltan recursos (por ejemplo <code>js/config.js</code> o <code>js/auth.js</code>). Comprueba que el túnel envíe <strong>todo</strong> el tráfico (no solo /api) al backend en el mismo origen.</p>' +
        '<p><a href="">Recargar</a></p></div>';
      return;
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bootstrap); else bootstrap();
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run); else run();
  global.LUCAS_APP = { onLoginSuccess: onLoginSuccess, showScreen: showScreen, renderDashboardContent: renderDashboardContent, onUnauthorized: onUnauthorized, initUserOnly: initUserOnly };
})(typeof window !== 'undefined' ? window : this);
