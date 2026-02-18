/**
 * Lucas Web — Aplicación principal: rutas y pantallas
 */
(function (global) {
  var auth = global.LUCAS_AUTH;
  var ROLES_FULL_WEB = ['admin', 'manager', 'master'];

  /** Quita el # de la URL para evitar problemas al volver a iniciar sesión (otro usuario). */
  function clearUrlHash() {
    var path = (window.location.pathname || '/').replace(/\/$/, '') || '/';
    if (window.history && window.history.replaceState) {
      window.history.replaceState(null, '', path);
    } else {
      window.location.hash = '';
    }
  }

  function showScreen(id) {
    var screens = document.querySelectorAll('#app .screen');
    screens.forEach(function (s) { s.classList.add('hidden'); });
    var el = document.getElementById(id);
    if (el) el.classList.remove('hidden');
  }

  function setActiveTab(route) {
    document.querySelectorAll('.nav-tabs .tab').forEach(function (t) {
      t.classList.toggle('active', t.getAttribute('data-route') === route);
    });
  }

  function renderDashboardContent(route) {
    var container = document.getElementById('dashboard-content');
    if (!container) return;
    setActiveTab(route);
    if (route === 'dashboard' && global.LUCAS_DASHBOARD_VIEW) global.LUCAS_DASHBOARD_VIEW.render(container);
    else if (route === 'estimaciones' && global.LUCAS_ESTIMACIONES_VIEW) global.LUCAS_ESTIMACIONES_VIEW.render(container);
    else if (route === 'preguntas' && global.LUCAS_PREGUNTAS_VIEW) global.LUCAS_PREGUNTAS_VIEW.render(container);
    else if (route === 'configuracion' && global.LUCAS_CONFIGURACION_VIEW) global.LUCAS_CONFIGURACION_VIEW.render(container);
    else if (route === 'registro' && global.LUCAS_REGISTRO_VIEW) global.LUCAS_REGISTRO_VIEW.render(container);
    else container.innerHTML = '<p class="loading">Cargando…</p>';
  }

  function handleHash() {
    var hash = (window.location.hash || '#dashboard').replace('#', '');
    if (!hash) hash = 'dashboard';
    if (['dashboard', 'estimaciones', 'preguntas', 'configuracion', 'registro'].indexOf(hash) === -1) hash = 'dashboard';
    renderDashboardContent(hash);
  }

  function init() {
    var roleEl = document.getElementById('user-role');
    if (roleEl) roleEl.textContent = 'Rol: ' + (auth.getRole() || '—');

    document.getElementById('btn-logout').addEventListener('click', function () {
      auth.logout();
      showScreen('screen-login');
      clearUrlHash();
      if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.showError) global.LUCAS_LOGIN_VIEW.showError('');
      if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.init) global.LUCAS_LOGIN_VIEW.init();
    });

    window.addEventListener('hashchange', handleHash);

    document.querySelectorAll('.nav-tabs .tab').forEach(function (t) {
      t.addEventListener('click', function (e) {
        e.preventDefault();
        var r = t.getAttribute('data-route');
        if (r) window.location.hash = r;
      });
    });
  }

  function onLoginSuccess() {
    var role = auth.getRole();
    var roleEl = document.getElementById('user-role');
    if (roleEl) roleEl.textContent = 'Rol: ' + (role || '—');
    showScreen('screen-dashboard');
    init();
    handleHash();
  }

  function initUserOnly() {
    var r = document.getElementById('user-role');
    if (r) r.textContent = 'Rol: ' + (auth.getRole() || 'user');
    var nav = document.querySelector('.nav-tabs');
    if (nav) nav.style.display = 'none';
    var c = document.getElementById('dashboard-content');
    if (c && global.LUCAS_PREGUNTAS_VIEW) global.LUCAS_PREGUNTAS_VIEW.renderAndLoad(c);
    document.getElementById('btn-logout').addEventListener('click', function () {
      auth.logout();
      showScreen('screen-login');
      clearUrlHash();
      if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.showError) global.LUCAS_LOGIN_VIEW.showError('');
      if (global.LUCAS_LOGIN_VIEW && global.LUCAS_LOGIN_VIEW.init) global.LUCAS_LOGIN_VIEW.init();
    });
  }

  function bootstrap() {
    if (global.LUCAS_LOGIN_VIEW) global.LUCAS_LOGIN_VIEW.init();
    auth.checkSession().then(function (me) {
      if (!me) {
        showScreen('screen-login');
        clearUrlHash();
        return;
      }
      if (ROLES_FULL_WEB.indexOf(me.role) !== -1) {
        onLoginSuccess();
      } else {
        showScreen('screen-dashboard');
        initUserOnly();
      }
    }).catch(function () {
      showScreen('screen-login');
      clearUrlHash();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', bootstrap);
  } else {
    bootstrap();
  }

  function onUnauthorized() {
    showScreen('screen-login');
    clearUrlHash();
  }
  global.LUCAS_APP = {
    onLoginSuccess: onLoginSuccess,
    showScreen: showScreen,
    renderDashboardContent: renderDashboardContent,
    onUnauthorized: onUnauthorized,
  };
})(typeof window !== 'undefined' ? window : this);
