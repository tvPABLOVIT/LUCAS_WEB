/**
 * Vista "Límite cómodo por esquema": agregados por personal (sala-cocina) y banda de facturación por cada personal de sala / por cada personal de cocina.
 * Sirve para ver hasta qué facturación por persona suele estar bien el turno y cuándo conviene añadir personal de sala o de cocina.
 */
(function (global) {
  'use strict';
  var auth = global.LUCAS_AUTH;

  function render(container, options) {
    options = options || {};
    var embedded = options.embedded === true;
    var titleTag = embedded ? 'h3' : 'h2';
    var titleClass = embedded ? '' : 'view-title';
    var wrapStart = embedded ? '' : '<div class="card limite-comodo-card">';
    var wrapEnd = embedded ? '' : '</div>';
    if (embedded) {
      container.innerHTML =
        '<div class="card estim-card-compact limite-comodo-panel limite-comodo-panel-sala">' +
        '<h3>Límite cómodo – Sala</h3>' +
        '<p class="limite-comodo-desc">Facturación por persona de sala en un turno a partir de la cual el equipo suele valorar el turno como más difícil (escala 1–5). Por debajo de ese valor se considera cómodo.</p>' +
        '<p id="limite-comodo-status-sala" class="limite-comodo-status">Cargando…</p>' +
        '<div id="limite-comodo-content-sala"></div>' +
        '</div>' +
        '<div class="card estim-card-compact limite-comodo-panel limite-comodo-panel-cocina">' +
        '<h3>Límite cómodo – Cocina</h3>' +
        '<p class="limite-comodo-desc">Facturación por persona de cocina en un turno a partir de la cual el equipo suele valorar el turno como más difícil (escala 1–5). Por debajo de ese valor se considera cómodo.</p>' +
        '<p id="limite-comodo-status-cocina" class="limite-comodo-status">Cargando…</p>' +
        '<div id="limite-comodo-content-cocina"></div>' +
        '</div>';
    } else {
      container.innerHTML = wrapStart +
        '<' + titleTag + (titleClass ? ' class="' + titleClass + '"' : '') + '>Límite cómodo por esquema de personal</' + titleTag + '>' +
        '<p class="limite-comodo-desc">Límite cómodo calculado por separado para sala (facturación por cada personal de sala) y para cocina (facturación por cada personal de cocina). La dificultad media y el % de turnos difíciles indican a partir de qué facturación conviene añadir otro personal de sala o de cocina.</p>' +
        '<p id="limite-comodo-status" class="limite-comodo-status">Cargando…</p>' +
        '<div id="limite-comodo-content"></div>' + wrapEnd;
    }
    load(embedded);
  }

  function load(twoPanels) {
    var statusSalaEl = twoPanels ? document.getElementById('limite-comodo-status-sala') : document.getElementById('limite-comodo-status');
    var statusCocinaEl = twoPanels ? document.getElementById('limite-comodo-status-cocina') : null;
    var contentSalaEl = twoPanels ? document.getElementById('limite-comodo-content-sala') : null;
    var contentCocinaEl = twoPanels ? document.getElementById('limite-comodo-content-cocina') : null;
    var contentEl = twoPanels ? null : document.getElementById('limite-comodo-content');
    var statusEl = twoPanels ? null : document.getElementById('limite-comodo-status');
    var targetEl = contentEl || contentSalaEl;
    if (!targetEl && !contentCocinaEl) return;
    auth.fetchWithAuth('/api/analytics/staff-revenue-comfort?minShifts=1').then(function (r) {
      if (!r.ok) {
        var msg = 'Error al cargar los datos.';
        if (twoPanels && statusSalaEl) statusSalaEl.textContent = msg;
        if (twoPanels && statusCocinaEl) statusCocinaEl.textContent = msg;
        if (!twoPanels && statusEl) statusEl.textContent = msg;
        if (contentSalaEl) contentSalaEl.innerHTML = '';
        if (contentCocinaEl) contentCocinaEl.innerHTML = '';
        if (contentEl) contentEl.innerHTML = '';
        return;
      }
      return r.json();
    }).then(function (data) {
      if (!data) return;
      var hasSala = data.schemas && data.schemas.length > 0;
      var hasCocina = data.cocina_schemas && data.cocina_schemas.length > 0;
      var threshold = data.difficulty_threshold != null ? Number(data.difficulty_threshold) : 3.5;
      var bandsSource = data.bands_source || 'fixed';
      var totalSala = data.total_shifts_sala != null ? data.total_shifts_sala : 0;
      var totalCocina = data.total_shifts_cocina != null ? data.total_shifts_cocina : 0;
      var explainThreshold = 'Se considera por encima del límite cómodo cuando la <strong>dificultad media</strong> de los turnos en esa banda es ≥ ' + threshold + ' (escala 1–5). Por encima de ese rango de facturación por persona, el turno suele sentirse más difícil.';
      var explainBands = bandsSource === 'dynamic' ? 'Bandas calculadas a partir de tus datos (percentiles).' : 'Bandas por defecto (pocos datos para calcular bandas propias).';
      var emptySala = '<p class="limite-comodo-empty">Aún no hay suficientes turnos con feedback y personal de sala. Guarda turnos con facturación, personal sala y preguntas V, R, M, D en <a href="#registro">Registro de ejecución</a>.</p>';
      var emptyCocina = '<p class="limite-comodo-empty">Aún no hay suficientes turnos con feedback y personal de cocina. Guarda turnos con facturación, personal cocina y pregunta Q5 en <a href="#registro">Registro de ejecución</a>.</p>';
      if (statusSalaEl) statusSalaEl.textContent = '';
      if (statusCocinaEl) statusCocinaEl.textContent = '';
      if (!twoPanels && statusEl) statusEl.textContent = '';

      var colSala = '';
      var colCocina = '';
      if (hasSala) {
        var limitsSala = [];
        data.schemas.forEach(function (s) { if (s.comfort_limit_approx != null) limitsSala.push(Number(s.comfort_limit_approx)); });
        var summarySala = limitsSala.length ? 'Con tus datos: hasta <strong>~' + (limitsSala.length === 1 ? limitsSala[0].toFixed(0) : Math.min.apply(null, limitsSala).toFixed(0) + '–' + Math.max.apply(null, limitsSala).toFixed(0)) + ' €</strong> por persona de sala suele ser cómodo (según esquema).' : '';
        colSala += '<p class="limite-comodo-explain">' + explainThreshold + '</p>';
        colSala += '<p class="limite-comodo-bands-source">' + explainBands + '</p>';
        if (totalSala > 0) colSala += '<p class="limite-comodo-total">Basado en <strong>' + totalSala + '</strong> turnos con feedback.</p>';
        if (summarySala) colSala += '<p class="limite-comodo-summary">' + summarySala + '</p>';
        if (!twoPanels) colSala += '<h3 class="limite-comodo-section-title">Sala (facturación por cada personal de sala)</h3>';
        data.schemas.forEach(function (schema) {
          var bands = schema.bands || [];
          if (bands.length === 0) return;
          colSala += '<div class="limite-comodo-schema">';
          colSala += '<h4>Esquema ' + schema.schema + ' (sala-cocina)</h4>';
          if (schema.comfort_limit_approx != null) {
            colSala += '<p class="limite-comodo-approx" title="Primera banda donde la dificultad media ≥ ' + threshold + '">Límite cómodo aproximado: hasta ~' + Number(schema.comfort_limit_approx).toFixed(0) + ' € por personal de sala</p>';
          }
          colSala += '<table class="limite-comodo-table"><thead><tr><th>Facturación €/persona sala</th><th>Turnos</th><th>Dificultad media</th><th>% turnos difíciles (≥4)</th></tr></thead><tbody>';
          bands.forEach(function (b) {
            var range = b.max >= 9999 ? (b.min + '+') : (b.min + '–' + b.max);
            var pct = b.pct_difficult != null ? Number(b.pct_difficult).toFixed(0) + '%' : '—';
            var trTitle = b.pct_difficult != null && b.pct_difficult >= 30 ? ' title="Por encima de esta banda, más del 30% de los turnos fueron valorados como difíciles"' : '';
            colSala += '<tr' + trTitle + '><td>' + range + '</td><td>' + b.count + '</td><td>' + (b.avg_difficulty != null ? Number(b.avg_difficulty).toFixed(1) : '—') + '</td><td>' + pct + '</td></tr>';
          });
          colSala += '</tbody></table></div>';
        });
      }
      if (hasCocina) {
        var limitsCocina = [];
        data.cocina_schemas.forEach(function (s) { if (s.comfort_limit_approx != null) limitsCocina.push(Number(s.comfort_limit_approx)); });
        var summaryCocina = limitsCocina.length ? 'Con tus datos: hasta <strong>~' + (limitsCocina.length === 1 ? limitsCocina[0].toFixed(0) : Math.min.apply(null, limitsCocina).toFixed(0) + '–' + Math.max.apply(null, limitsCocina).toFixed(0)) + ' €</strong> por persona de cocina suele ser cómodo (según nº de cocina).' : '';
        colCocina += '<p class="limite-comodo-explain">' + explainThreshold + '</p>';
        colCocina += '<p class="limite-comodo-bands-source">' + explainBands + '</p>';
        if (totalCocina > 0) colCocina += '<p class="limite-comodo-total">Basado en <strong>' + totalCocina + '</strong> turnos con feedback.</p>';
        if (summaryCocina) colCocina += '<p class="limite-comodo-summary">' + summaryCocina + '</p>';
        if (!twoPanels) colCocina += '<h3 class="limite-comodo-section-title">Cocina (facturación por cada personal de cocina)</h3>';
        data.cocina_schemas.forEach(function (schema) {
          var bands = schema.bands || [];
          if (bands.length === 0) return;
          colCocina += '<div class="limite-comodo-schema">';
          colCocina += '<h4>Cocina: ' + schema.schema + ' personal</h4>';
          if (schema.comfort_limit_approx != null) {
            colCocina += '<p class="limite-comodo-approx" title="Primera banda donde la dificultad media cocina ≥ ' + threshold + '">Límite cómodo aproximado: hasta ~' + Number(schema.comfort_limit_approx).toFixed(0) + ' € por personal de cocina</p>';
          }
          colCocina += '<table class="limite-comodo-table"><thead><tr><th>Facturación €/persona cocina</th><th>Turnos</th><th>Dificultad media</th><th>% turnos difíciles (≥4)</th></tr></thead><tbody>';
          bands.forEach(function (b) {
            var range = b.max >= 9999 ? (b.min + '+') : (b.min + '–' + b.max);
            var pct = b.pct_difficult != null ? Number(b.pct_difficult).toFixed(0) + '%' : '—';
            var trTitle = b.pct_difficult != null && b.pct_difficult >= 30 ? ' title="Por encima de esta banda, más del 30% de los turnos fueron valorados como difíciles en cocina"' : '';
            colCocina += '<tr' + trTitle + '><td>' + range + '</td><td>' + b.count + '</td><td>' + (b.avg_difficulty != null ? Number(b.avg_difficulty).toFixed(1) : '—') + '</td><td>' + pct + '</td></tr>';
          });
          colCocina += '</tbody></table></div>';
        });
      }

      if (twoPanels) {
        if (contentSalaEl) contentSalaEl.innerHTML = colSala || emptySala;
        if (contentCocinaEl) contentCocinaEl.innerHTML = colCocina || emptyCocina;
      } else {
        if (!hasSala && !hasCocina) {
          contentEl.innerHTML = '<p class="limite-comodo-empty">Aún no hay suficientes turnos con feedback y personal para mostrar agregados. Guarda turnos con facturación, personal sala/cocina y las 5 preguntas (V, R, M, D, dificultad cocina) en <a href="#registro">Registro de ejecución</a>.</p>';
        } else {
          var html = '';
          if (colSala && colCocina) {
            html = '<div class="limite-comodo-content-grid"><div class="limite-comodo-col">' + colSala + '</div><div class="limite-comodo-col">' + colCocina + '</div></div>';
          } else if (colSala) {
            html = '<div class="limite-comodo-single">' + colSala + '</div>';
          } else if (colCocina) {
            html = '<div class="limite-comodo-single">' + colCocina + '</div>';
          }
          contentEl.innerHTML = html;
        }
      }
    }).catch(function () {
      var msg = 'Error de conexión.';
      if (twoPanels && statusSalaEl) statusSalaEl.textContent = msg;
      if (twoPanels && statusCocinaEl) statusCocinaEl.textContent = msg;
      if (!twoPanels && statusEl) statusEl.textContent = msg;
      if (contentSalaEl) contentSalaEl.innerHTML = '';
      if (contentCocinaEl) contentCocinaEl.innerHTML = '';
      if (contentEl) contentEl.innerHTML = '';
    });
  }

  global.LUCAS_LIMITE_COMODO_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
