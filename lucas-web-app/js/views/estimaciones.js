/**
 * Lucas Web — Vista Estimaciones
 * KPIs semanales, predicción semana siguiente, recomendaciones
 */
(function (global) {
  var auth = global.LUCAS_AUTH;
  var versionTimer = null, lastVersion = null;

  function getWeekStart(d) {
    var date = typeof d === 'string' ? new Date(d) : new Date(d);
    var day = date.getDay();
    var diff = date.getDate() - day + (day === 0 ? -6 : 1);
    var monday = new Date(date);
    monday.setDate(diff);
    return monday.getFullYear() + '-' + String(monday.getMonth() + 1).padStart(2, '0') + '-' + String(monday.getDate()).padStart(2, '0');
  }

  function getISOWeekNumber(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    var dayNum = d.getDay() || 7;
    d.setDate(d.getDate() + 4 - dayNum);
    var yearStart = new Date(d.getFullYear(), 0, 1);
    return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
  }

  function formatWeekRange(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    var end = new Date(d);
    end.setDate(end.getDate() + 6);
    var fmt = function (x) { return String(x.getDate()).padStart(2, '0') + ' ' + ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'][x.getMonth()]; };
    return fmt(d) + ' – ' + fmt(end) + ' ' + d.getFullYear();
  }

  function render(container) {
    if (versionTimer) { clearInterval(versionTimer); versionTimer = null; }
    var weekStart = getWeekStart(new Date());
    container.innerHTML =
      '<div class="dashboard-title-row">' +
      '<div class="dashboard-title-block">' +
      '<h2 class="view-title">Estimaciones</h2>' +
      '<p class="dashboard-subtitle">Cálculos, comparativas y predicción de la semana siguiente</p>' +
      '</div>' +
      '<div class="dashboard-week-bar">' +
      '<span id="estim-week-label" class="dashboard-week-label">Semana ' + getISOWeekNumber(weekStart) + '</span>' +
      '<button type="button" id="estim-prev" class="btn-week btn-secondary" title="Semana anterior">◀</button>' +
      '<input type="date" id="estim-week-start" class="dashboard-week-input" value="' + weekStart + '" aria-label="Lunes de la semana" />' +
      '<button type="button" id="estim-next" class="btn-week btn-secondary" title="Semana siguiente">▶</button>' +
      '<button type="button" id="estim-cargar" class="btn-week btn-primary">Actualizar</button>' +
      '<span id="estim-week-range" class="dashboard-week-range"></span>' +
      '</div>' +
      '</div>' +
      '<p class="loading" style="font-size:0.85rem;margin-top:-0.5rem;margin-bottom:1rem">Refresco automático cada 60 s si cambia la versión.</p>' +
      '<div id="estim-kpis" class="kpi-grid"></div>' +
      '<div id="estim-resumen" class="card"></div>' +
      '<div id="estim-prediction" class="card"></div>' +
      '<div id="estim-days-cards" class="estim-days-cards"></div>' +
      '<div id="estim-alertas" class="card"></div>' +
      '<div id="estim-recommendations" class="card"></div>';

    var weekInput = document.getElementById('estim-week-start');
    var weekLabelEl = document.getElementById('estim-week-label');
    var weekRangeEl = document.getElementById('estim-week-range');
    var kpisEl = document.getElementById('estim-kpis');
    var resumenEl = document.getElementById('estim-resumen');
    var predEl = document.getElementById('estim-prediction');
    var daysCardsEl = document.getElementById('estim-days-cards');
    var alertasEl = document.getElementById('estim-alertas');
    var recEl = document.getElementById('estim-recommendations');

    function totalToCocinaSala(n) {
      if (n <= 0) return { sala: 0, cocina: 0 };
      if (n === 1) return { sala: 1, cocina: 0 };
      if (n === 2) return { sala: 1, cocina: 1 };
      if (n === 3) return { sala: 2, cocina: 1 };
      if (n === 4) return { sala: 2, cocina: 2 };
      if (n === 5) return { sala: 3, cocina: 2 };
      return { sala: 3, cocina: 3 };
    }
    function getSalaCocinaScheme(med, tar, noc, prodEurHora, horasTurno, revenueDia) {
      if (!prodEurHora || !horasTurno) return { sala: '—', cocina: '—' };
      var div = prodEurHora * horasTurno;
      function pers(rev) { var n = Math.round(rev / div); return n < 1 ? 1 : Math.min(n, 6); }
      var m = pers(med), t = pers(tar), n = pers(noc);
      var req2 = revenueDia >= 2400, maxC = revenueDia > 3000 ? 3 : 2, maxS = revenueDia >= 3500 ? 3 : 2;
      function minS(rev) { return rev > 600 ? 2 : 1; }
      function aplic(tot, rev, out) {
        var r = totalToCocinaSala(tot);
        var s = Math.max(r.sala, req2 ? 2 : minS(rev));
        var c = Math.max(r.cocina, req2 ? 2 : 1);
        out.s = Math.min(s, maxS);
        out.c = Math.min(c, maxC);
      }
      var sm = {}, st = {}, sn = {};
      aplic(m, med, sm);
      aplic(t, tar, st);
      aplic(n, noc, sn);
      return { sala: sm.s + '-' + st.s + '-' + sn.s, cocina: sm.c + '-' + st.c + '-' + sn.c };
    }
    function confianzaLabel(revenue, min, max) {
      if (!revenue || revenue <= 0) return '—';
      var w = (max - min) / revenue;
      if (w < 0.30) return 'Alta';
      if (w < 0.50) return 'Media';
      return 'Baja';
    }
    function comfortLabel(eurPerWaiter, limit) {
      if (limit == null || limit <= 0) return '—';
      if (eurPerWaiter <= limit * 0.9) return 'cómodo';
      if (eurPerWaiter <= limit * 1.1) return 'límite';
      return 'alto';
    }
    /** Personal estimado por turno según límite cómodo: menor esquema que mantiene €/camarero y €/cocinero dentro del límite. Sin datos, límites bajos (350 €) y margen 5%. */
    function getSalaCocinaSchemeFromComfort(med, tar, noc, comfortBySchema, comfortByCocina) {
      var allowed = ['1-1', '1-2', '2-1', '2-2', '2-3', '3-2', '3-3'];
      var defaultLimitSala = 350, defaultLimitCocina = 350;
      var margin = 1.05;
      for (var i = 0; i < allowed.length; i++) {
        var schemaKey = allowed[i];
        var parts = schemaKey.split('-');
        var S = parseInt(parts[0], 10) || 1, C = parseInt(parts[1], 10) || 1;
        var limitSala = comfortBySchema[schemaKey] != null ? comfortBySchema[schemaKey] : defaultLimitSala;
        var limitCocina = comfortByCocina[parts[1]] != null ? comfortByCocina[parts[1]] : defaultLimitCocina;
        var eurCamMax = Math.max(med / S, tar / S, noc / S);
        var eurCocMax = Math.max(med / C, tar / C, noc / C);
        if (eurCamMax <= limitSala * margin && eurCocMax <= limitCocina * margin)
          return { sala: S + '-' + S + '-' + S, cocina: C + '-' + C + '-' + C };
      }
      return null;
    }

    function load() {
      var ws = (weekInput && weekInput.value) || weekStart;
      if (weekLabelEl) weekLabelEl.textContent = 'Semana ' + getISOWeekNumber(ws);
      if (weekRangeEl) weekRangeEl.textContent = formatWeekRange(ws);
      kpisEl.innerHTML = '<p class="loading">Cargando KPIs…</p>';
      resumenEl.innerHTML = '';
      predEl.innerHTML = '';
      if (daysCardsEl) daysCardsEl.innerHTML = '';
      if (alertasEl) alertasEl.innerHTML = '';
      recEl.innerHTML = '';

      Promise.all([
        auth.fetchWithAuth('/api/dashboard/week?weekStart=' + ws).then(function (r) { return r.ok ? r.json() : null; }),
        auth.fetchWithAuth('/api/estimaciones').then(function (r) { return r.ok ? r.json() : null; }),
        auth.fetchWithAuth('/api/predictions/next-week').then(function (r) { return r.ok ? r.json() : null; }),
        auth.fetchWithAuth('/api/recommendations?limit=10').then(function (r) { return r.ok ? r.json() : []; }),
        auth.fetchWithAuth('/api/settings').then(function (r) { return r.ok ? r.json() : null; }),
        auth.fetchWithAuth('/api/estimaciones/alertas').then(function (r) { return r.ok ? r.json() : { alertas: [] }; }),
        auth.fetchWithAuth('/api/analytics/staff-revenue-comfort?minShifts=1').then(function (r) { return r.ok ? r.json() : null; })
      ]).then(function (data) {
        var dash = data[0], pred = data[2], rec = data[3], settings = data[4], alertasResp = data[5], comfortResp = data[6];
        var comfortBySchema = {}, comfortByCocina = {};
        if (comfortResp && comfortResp.schemas && Array.isArray(comfortResp.schemas)) comfortResp.schemas.forEach(function (s) { if (s && s.schema != null) comfortBySchema[s.schema] = s.comfort_limit_approx; });
        if (comfortResp && comfortResp.cocina_schemas && Array.isArray(comfortResp.cocina_schemas)) comfortResp.cocina_schemas.forEach(function (s) { if (s && s.schema != null) comfortByCocina[s.schema] = s.comfort_limit_approx; });
        rec = Array.isArray(rec) ? rec : (rec && rec.items ? rec.items : []);
        var alertasList = (alertasResp && alertasResp.alertas) ? alertasResp.alertas : [];
        var prodObj = (settings && settings.ProductividadIdealEurHora != null && settings.ProductividadIdealEurHora !== '') ? parseFloat(settings.ProductividadIdealEurHora) : 50;
        var costeHora = (settings && settings.CostePersonalPorHora != null && settings.CostePersonalPorHora !== '') ? parseFloat(settings.CostePersonalPorHora) : null;
        if (dash) {
          kpisEl.innerHTML = '';
          // 4 KPIs según documento: promedios históricos (últimas 12 semanas) + coste personal vs histórico
          var costeVal = '—';
          if (dash.costePersonalPctVsHistoric != null && dash.costePersonalEurFromContrato != null)
            costeVal = dash.costePersonalPctVsHistoric.toFixed(1) + ' % (' + dash.costePersonalEurFromContrato.toFixed(0) + ' €)';
          else if (dash.costePersonalPctVsHistoric != null) costeVal = dash.costePersonalPctVsHistoric.toFixed(1) + ' %';
          else if (dash.costePersonalEurFromContrato != null) costeVal = dash.costePersonalEurFromContrato.toFixed(0) + ' €';
          var kpis = [
            { label: 'Facturación promedio semanal', value: (dash.avgRevenueHistoric != null ? dash.avgRevenueHistoric.toFixed(0) + ' €' : '—') },
            { label: 'Productividad promedio (€/h)', value: (dash.avgProductivityHistoric != null ? dash.avgProductivityHistoric.toFixed(1) : '—') },
            { label: 'Horas promedio semanal', value: (dash.avgHoursHistoric != null ? dash.avgHoursHistoric.toFixed(1) : '—') },
            { label: 'Coste personal (vs histórico)', value: costeVal }
          ];
          kpis.forEach(function (k) {
            var div = document.createElement('div');
            div.className = 'kpi-card';
            div.innerHTML = '<div class="label">' + k.label + '</div><div class="value">' + k.value + '</div>';
            kpisEl.appendChild(div);
          });
          if (resumenEl) resumenEl.innerHTML = '<h3>Resumen semana consultada</h3><p>' + (dash.resumenClasificacion ? dash.resumenClasificacion + ' ' : '') + (dash.resumenTexto || '—') + '</p>' + (dash.prevWeekRevenue != null ? '<p class="estim-semana-anterior">Semana anterior: ' + dash.prevWeekRevenue.toFixed(0) + ' € facturados' + (dash.prevWeekProductivity != null ? ', ' + dash.prevWeekProductivity.toFixed(1) + ' €/h productividad' : '') + '.</p>' : '');
        } else {
          kpisEl.innerHTML = '<p class="loading">No hay datos de dashboard para esta semana.</p>';
        }
        var predRevenue = pred && pred.totalRevenue != null ? pred.totalRevenue : null;
        var nextWeekRange = (pred && pred.weekStartMonday) ? (function () { var d = new Date(pred.weekStartMonday + 'T12:00:00'); var end = new Date(d); end.setDate(end.getDate() + 6); var fmt = function (x) { return String(x.getDate()).padStart(2, '0') + ' ' + ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'][x.getMonth()]; }; return fmt(d) + ' – ' + fmt(end) + ' ' + d.getFullYear(); })() : null;
        var horasNecesarias = (predRevenue != null && prodObj > 0) ? predRevenue / prodObj : null;
        var horasPorDia = horasNecesarias != null ? horasNecesarias / 7 : null;
        var costePersonalEur = (horasNecesarias != null && costeHora != null) ? horasNecesarias * costeHora : null;
        var costePctPred = (predRevenue != null && predRevenue > 0 && costePersonalEur != null) ? (costePersonalEur / predRevenue * 100).toFixed(1) : null;
        var parrafo = '';
        if (nextWeekRange != null && predRevenue != null) {
          parrafo = 'Para la semana del ' + nextWeekRange + ', se estima una facturación total de ' + predRevenue.toFixed(0) + ' €.';
          if (horasNecesarias != null) parrafo += ' La cantidad de horas necesarias para alcanzar la productividad objetivo (' + prodObj.toFixed(0) + ' €/h) con la facturación estimada es de ' + horasNecesarias.toFixed(0) + ' horas (unas ' + (horasPorDia != null ? horasPorDia.toFixed(1) : '—') + ' al día).';
          if (costePctPred != null) parrafo += ' El coste de personal se ubica en un ' + costePctPred + '% vs la facturación estimada.';
          var days = (pred && pred.dailyPredictionsJson) ? (function () { try { return JSON.parse(pred.dailyPredictionsJson); } catch (e) { return null; } })() : (pred && pred.days && pred.days.length) ? pred.days : null;
          if (days && days.length > 0) {
            var sorted = days.slice().filter(function (d) { return (d.revenue != null || d.predictedRevenue != null) && (d.revenue > 0 || (d.predictedRevenue && d.predictedRevenue > 0)); }).sort(function (a, b) { var ra = a.revenue != null ? a.revenue : a.predictedRevenue || 0; var rb = b.revenue != null ? b.revenue : b.predictedRevenue || 0; return rb - ra; });
            if (sorted.length > 0) {
              var topDays = sorted.slice(0, 3).map(function (d) { var nm = d.dayName || (d.date ? (new Date(d.date + 'T12:00:00')).toLocaleDateString('es-ES', { weekday: 'long' }) : ''); var rev = (d.revenue != null ? d.revenue : d.predictedRevenue || 0).toFixed(0); return nm + ' (' + rev + ' €)'; }).join(', ');
              parrafo += ' Los días con mayor estimación son: ' + topDays + '.';
            }
          }
        }
        if (pred && (pred.days || predRevenue != null)) predEl.innerHTML = '<h3>Predicción semana siguiente</h3>' + (parrafo ? '<p class="estim-parrafo">' + parrafo + '</p>' : '<p>Facturación estimada: ' + (predRevenue != null ? predRevenue.toFixed(0) + ' €' : '—') + '</p>'); else predEl.innerHTML = '<h3>Predicción semana siguiente</h3><p class="loading">Sin predicción guardada. Configura parámetros y registra días para que se genere una estimación.</p>';
        var days = (pred && pred.dailyPredictionsJson) ? (function () { try { return JSON.parse(pred.dailyPredictionsJson); } catch (e) { return null; } })() : (pred && pred.days && pred.days.length) ? pred.days : null;
        var horasPorTurno = (settings && settings.HorasPorTurno != null && settings.HorasPorTurno !== '') ? parseFloat(settings.HorasPorTurno) : 4;
        if (daysCardsEl && days && days.length > 0) {
          daysCardsEl.innerHTML = '<h3>Tarjetas por día (semana siguiente)</h3><div class="estim-days-grid"></div>';
          var grid = daysCardsEl.querySelector('.estim-days-grid');
          days.forEach(function (d) {
            var rev = d.revenue != null ? d.revenue : d.predictedRevenue || 0;
            var min = d.min != null ? d.min : rev * 0.85, max = d.max != null ? d.max : rev * 1.15;
            var conf = confianzaLabel(rev, min, max);
            var med = d.mediodia != null ? d.mediodia : rev / 3, tar = d.tarde != null ? d.tarde : rev / 3, noc = d.noche != null ? d.noche : rev / 3;
            var scheme = getSalaCocinaSchemeFromComfort(med, tar, noc, comfortBySchema, comfortByCocina) || getSalaCocinaScheme(med, tar, noc, prodObj, horasPorTurno, rev);
            var dateFmt = d.date ? (function () { var x = new Date(d.date + 'T12:00:00'); return String(x.getDate()).padStart(2, '0') + '/' + String(x.getMonth() + 1).padStart(2, '0'); })() : '—';
            var salaParts = (scheme.sala && scheme.sala !== '—') ? scheme.sala.split('-') : [], cocinaParts = (scheme.cocina && scheme.cocina !== '—') ? scheme.cocina.split('-') : [];
            var schemaKey = (salaParts.length >= 1 && cocinaParts.length >= 1) ? salaParts[0] + '-' + cocinaParts[0] : null;
            var limitSala = schemaKey != null ? comfortBySchema[schemaKey] : null, limitCocina = cocinaParts.length >= 1 ? comfortByCocina[cocinaParts[0]] : null;
            var salaMed = salaParts[0] ? parseInt(salaParts[0], 10) || 1 : 1, salaTar = salaParts[1] ? parseInt(salaParts[1], 10) || 1 : 1, salaNoc = salaParts[2] ? parseInt(salaParts[2], 10) || 1 : 1;
            var eurCamMed = salaMed > 0 ? med / salaMed : 0, eurCamTar = salaTar > 0 ? tar / salaTar : 0, eurCamNoc = salaNoc > 0 ? noc / salaNoc : 0;
            var labMed = limitSala != null ? comfortLabel(eurCamMed, limitSala) : '—', labTar = limitSala != null ? comfortLabel(eurCamTar, limitSala) : '—', labNoc = limitSala != null ? comfortLabel(eurCamNoc, limitSala) : '—';
            var comfortLine = '';
            if (limitSala != null || limitCocina != null) {
              var parts = [];
              if (limitSala != null) parts.push('Sala (' + (schemaKey || '—') + '): ~' + Math.round(limitSala) + ' €/camarero');
              if (limitCocina != null) parts.push('Cocina (' + (cocinaParts[0] || '') + '): ~' + Math.round(limitCocina) + ' €/cocinero');
              comfortLine = '<div class="estim-day-comfort" title="Según histórico de Límite cómodo">' + (parts.length ? 'Límite cómodo: ' + parts.join('; ') + '.' : '') + (limitSala != null ? ' Med/Tar/Noc €/cam: ' + Math.round(eurCamMed) + ' (' + labMed + '), ' + Math.round(eurCamTar) + ' (' + labTar + '), ' + Math.round(eurCamNoc) + ' (' + labNoc + ').' : '') + '</div>';
            }
            var clima = (d.weatherDescription != null && d.weatherDescription !== '') ? d.weatherDescription : '';
            var festivo = (d.isHoliday && d.holidayName) ? 'Festivo: ' + d.holidayName : '';
            var contexto = [clima, festivo].filter(Boolean).join(' · ') || '—';
            var card = '<div class="estim-day-card"><div class="estim-day-name">' + (d.dayName || '—') + ' <span class="estim-day-date">' + dateFmt + '</span></div>' +
              '<div class="estim-day-revenue">' + rev.toFixed(0) + ' €</div>' +
              '<div class="estim-day-range">Min–max: ' + min.toFixed(0) + ' – ' + max.toFixed(0) + ' €</div>' +
              '<div class="estim-day-conf">Confianza: ' + conf + '</div>' +
              '<div class="estim-day-shifts">Mediodía ' + med.toFixed(0) + ' € · Tarde ' + tar.toFixed(0) + ' € · Noche ' + noc.toFixed(0) + ' €</div>' +
              '<div class="estim-day-sala" title="Personal estimado por turno (Med/Tar/Noc) según facturación y límite cómodo">Sala ' + scheme.sala + ' · Cocina ' + scheme.cocina + '</div>' +
              (comfortLine || '') +
              (contexto !== '—' ? '<div class="estim-day-contexto">' + contexto + '</div>' : '') + '</div>';
            grid.insertAdjacentHTML('beforeend', card);
          });
        } else if (daysCardsEl) daysCardsEl.innerHTML = '';
        if (alertasEl) {
          if (alertasList.length > 0) alertasEl.innerHTML = '<h3>Qué puede afectar la semana siguiente</h3><ul class="estim-alertas-list">' + alertasList.map(function (a) { return '<li><strong>' + (a.titulo || a.tipo || '') + '</strong>: ' + (a.texto || '') + '</li>'; }).join('') + '</ul>';
          else alertasEl.innerHTML = '<h3>Qué puede afectar la semana siguiente</h3><p class="loading">Sin alertas disponibles.</p>';
        }
        if (rec.length) recEl.innerHTML = '<h3>Recomendaciones / Alertas</h3><ul>' + rec.map(function (r) { return '<li>' + (r.text || r.message || r.title || r.id || '—') + '</li>'; }).join('') + '</ul>'; else recEl.innerHTML = '<h3>Recomendaciones</h3><p class="loading">Ninguna.</p>';
        auth.fetchWithAuth('/api/recommendations/version').then(function (r) { if (r.ok) return r.json(); return null; }).then(function (v) { if (v && v.version != null) lastVersion = v.version; });
      }).catch(function () {
        kpisEl.innerHTML = '<p class="error-msg">Error al cargar. Comprueba que el Backend esté en marcha y la URL en js/config.js.</p>';
      });
    }

    function addDays(ymd, delta) { var d = new Date(ymd + 'T12:00:00'); d.setDate(d.getDate() + delta); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }
    document.getElementById('estim-cargar').addEventListener('click', load);
    if (weekInput) weekInput.addEventListener('change', load);
    document.getElementById('estim-prev').addEventListener('click', function () { var ws = (weekInput && weekInput.value) || weekStart; weekInput.value = addDays(ws, -7); load(); });
    document.getElementById('estim-next').addEventListener('click', function () { var ws = (weekInput && weekInput.value) || weekStart; weekInput.value = addDays(ws, 7); load(); });
    load();
    versionTimer = setInterval(function () {
      auth.fetchWithAuth('/api/recommendations/version').then(function (r) { if (!r.ok || r.status === 401) return null; return r.json(); }).then(function (v) {
        if (v && v.version != null && lastVersion != null && v.version !== lastVersion) { lastVersion = v.version; load(); }
      });
    }, 60000);
  }

  global.LUCAS_ESTIMACIONES_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
