(function (global) {
  var auth = global.LUCAS_AUTH;
  var DAY_NAMES = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
  function getWeekStart(d) {
    var date = typeof d === 'string' ? new Date(d + 'T12:00:00') : new Date(d);
    var day = date.getDay();
    var diff = date.getDate() - day + (day === 0 ? -6 : 1);
    var monday = new Date(date);
    monday.setDate(diff);
    return monday.getFullYear() + '-' + String(monday.getMonth() + 1).padStart(2, '0') + '-' + String(monday.getDate()).padStart(2, '0');
  }
  function addDays(ymd, delta) {
    var d = new Date(ymd + 'T12:00:00');
    d.setDate(d.getDate() + delta);
    return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  }
  function dayNameFromDate(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    return DAY_NAMES[d.getDay()];
  }
  function formatDateShort(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    return String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0');
  }
  function formatWeekRange(weekStartYmd) {
    var start = new Date(weekStartYmd + 'T12:00:00');
    var end = new Date(start);
    end.setDate(end.getDate() + 6);
    var fmt = function (d) { return String(d.getDate()).padStart(2, '0') + ' ' + ['ene','feb','mar','abr','may','jun','jul','ago','sep','oct','nov','dic'][d.getMonth()]; };
    return fmt(start) + ' – ' + fmt(end) + ' ' + start.getFullYear();
  }
  function isCurrentWeek(weekStartYmd) {
      var mon = new Date(weekStartYmd + 'T12:00:00');
      var today = new Date();
      var todayMon = getWeekStart(today);
      return weekStartYmd === todayMon;
    }
  function getISOWeekNumber(ymd) {
    var d = new Date(ymd + 'T12:00:00');
    var dayNum = d.getDay() || 7;
    d.setDate(d.getDate() + 4 - dayNum);
    var yearStart = new Date(d.getFullYear(), 0, 1);
    return Math.ceil((((d - yearStart) / 86400000) + 1) / 7);
  }
  function render(container) {
    var today = new Date();
    var weekStart = getWeekStart(today);
    container.innerHTML =
      '<div class="dashboard-title-row">' +
      '<div class="dashboard-title-block">' +
      '<h2 class="view-title">Dashboard</h2>' +
      '<p class="dashboard-subtitle">Datos de la semana seleccionada</p>' +
      '</div>' +
      '<div class="dashboard-week-bar">' +
      '<span id="dashboard-week-label" class="dashboard-week-label">Semana ' + getISOWeekNumber(weekStart) + '</span>' +
      '<button type="button" id="dashboard-prev" class="btn-week btn-secondary" title="Semana anterior">◀</button>' +
      '<input type="date" id="dashboard-week-start" class="dashboard-week-input" value="' + weekStart + '" aria-label="Lunes de la semana" />' +
      '<button type="button" id="dashboard-next" class="btn-week btn-secondary" title="Semana siguiente">▶</button>' +
      '<button type="button" id="dashboard-cargar" class="btn-week btn-primary">Actualizar</button>' +
      '<span id="dashboard-week-range" class="dashboard-week-range"></span>' +
      '<span id="dashboard-semana-en-curso" class="dashboard-badge hidden"></span>' +
      '</div>' +
      '</div>' +
      '<div id="dashboard-kpis" class="kpi-grid"></div>' +
      '<div id="dashboard-days-wrap" class="card"><h3>Días de la semana</h3><div id="dashboard-days-table-wrap"></div></div>' +
      '<div id="dashboard-resumen" class="card"></div>' +
      '<div class="card dashboard-import-card">' +
      '<h3>Importar datos</h3>' +
      '<div class="dashboard-import-row">' +
      '<div class="dashboard-import-item">' +
      '<label class="dashboard-import-label">Excel (facturación + horas reales por turno)</label>' +
      '<input type="file" id="dashboard-excel-file" accept=".xlsx,.xls" class="dashboard-import-input" />' +
      '<button type="button" id="dashboard-import-excel" class="btn-secondary btn-sm">Cargar Excel</button>' +
      '<span id="dashboard-excel-status" class="dashboard-import-status"></span>' +
      '</div>' +
      '</div>' +
      '</div>' +
      '<div id="dashboard-bloque-ampliacion" class="card dashboard-bloque-ampliacion">' +
      '<h3 class="dashboard-bloque-ampliacion-title">Facturación últimos 30 días</h3>' +
      '<div id="dashboard-chart-30d" class="dashboard-chart-30d"><p class="loading">Cargando…</p></div>' +
      '</div>';
    var weekInput = document.getElementById('dashboard-week-start');
    var weekLabelEl = document.getElementById('dashboard-week-label');
    var weekRangeEl = document.getElementById('dashboard-week-range');
    var badgeEl = document.getElementById('dashboard-semana-en-curso');
    var kpisEl = document.getElementById('dashboard-kpis');
    var resumenEl = document.getElementById('dashboard-resumen');
    var daysWrap = document.getElementById('dashboard-days-table-wrap');
    function load() {
      var ws = (weekInput && weekInput.value) || weekStart;
      if (weekLabelEl) weekLabelEl.textContent = 'Semana ' + getISOWeekNumber(ws);
      if (weekRangeEl) { weekRangeEl.textContent = 'Cargando…'; weekRangeEl.classList.add('dashboard-week-range--loading'); }
      if (badgeEl) {
        if (isCurrentWeek(ws)) { badgeEl.textContent = '✓ En curso'; badgeEl.classList.remove('hidden'); badgeEl.classList.add('dashboard-badge--current'); }
        else { badgeEl.textContent = ''; badgeEl.classList.add('hidden'); badgeEl.classList.remove('dashboard-badge--current'); }
      }
      kpisEl.innerHTML = '<p class="loading">Cargando…</p>';
      resumenEl.innerHTML = '';
      daysWrap.innerHTML = '';
      var todayYmd = (function () { var t = new Date(); return t.getFullYear() + '-' + String(t.getMonth() + 1).padStart(2, '0') + '-' + String(t.getDate()).padStart(2, '0'); })();
      auth.fetchWithAuth('/api/dashboard/week?weekStart=' + encodeURIComponent(ws) + '&asOf=' + encodeURIComponent(todayYmd)).then(function (r) {
        if (r.status === 401) {
          if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized();
          return null;
        }
        if (!r.ok) throw new Error('Error al cargar');
        return r.json();
      }).then(function (data) {
        if (!data) return;
        if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange((weekInput && weekInput.value) || weekStart); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
        kpisEl.innerHTML = '';
        var revValue = data.totalRevenue != null ? data.totalRevenue.toFixed(0) + ' €' : '—';
        var pctVsPrev = '';
        if (data.totalRevenue != null && data.prevWeekRevenue != null && data.prevWeekRevenue > 0) {
          var pct = ((data.totalRevenue - data.prevWeekRevenue) / data.prevWeekRevenue) * 100;
          if (pct > 0) pctVsPrev = '<div class="kpi-card-sub kpi-card-sub--up">+' + pct.toFixed(1) + '% vs sem. ant.</div>';
          else if (pct < 0) pctVsPrev = '<div class="kpi-card-sub kpi-card-sub--down">' + pct.toFixed(1) + '% vs sem. ant.</div>';
          else pctVsPrev = '<div class="kpi-card-sub">0% vs sem. ant.</div>';
        }
        if (data.avgRevenueHistoric != null) pctVsPrev += '<div class="kpi-card-sub">Media histórica: ' + data.avgRevenueHistoric.toFixed(0) + ' €</div>';
        var prodValue = data.avgProductivity != null ? data.avgProductivity.toFixed(1) + ' €/h' : '—';
        var pctVsPrevProd = '';
        if (data.avgProductivity != null && data.prevWeekProductivity != null && data.prevWeekProductivity > 0) {
          var pctProd = ((data.avgProductivity - data.prevWeekProductivity) / data.prevWeekProductivity) * 100;
          if (pctProd > 0) pctVsPrevProd = '<div class="kpi-card-sub kpi-card-sub--up">+' + pctProd.toFixed(1) + '% vs sem. ant.</div>';
          else if (pctProd < 0) pctVsPrevProd = '<div class="kpi-card-sub kpi-card-sub--down">' + pctProd.toFixed(1) + '% vs sem. ant.</div>';
          else pctVsPrevProd = '<div class="kpi-card-sub">0% vs sem. ant.</div>';
        }
        var costeValue = data.costePersonalEur != null ? data.costePersonalEur.toFixed(0) + ' €' : '—';
        var costeSub = '';
        if (data.costePersonalPctFacturacion != null) {
          var pctCoste = data.costePersonalPctFacturacion;
          var costeClass = pctCoste < 30 ? 'kpi-card-sub--coste-bueno' : (pctCoste <= 35 ? 'kpi-card-sub--coste-asumible' : 'kpi-card-sub--coste-alto');
          costeSub = '<div class="kpi-card-sub ' + costeClass + '">' + pctCoste.toFixed(1) + '% vs facturación</div>';
        }
        var objRaw = data.facturacionObjetivo != null ? data.facturacionObjetivo : data.FacturacionObjetivo;
        var objNum = objRaw != null && objRaw !== '' ? Number(objRaw) : NaN;
        var objetivoValue = !isNaN(objNum) ? objNum.toFixed(0) + ' €' : '—';
        var objetivoSub = '';
        if (objNum > 0 && data.totalRevenue != null) {
          var pctObj = (data.totalRevenue / objNum) * 100;
          var objetivoClass = pctObj >= 100 ? 'kpi-card-sub--up' : 'kpi-card-sub--down';
          objetivoSub = '<div class="kpi-card-sub ' + objetivoClass + '">' + pctObj.toFixed(1) + '% del objetivo</div>';
        }
        var kpis = [
          { label: 'Facturación total', value: revValue, sub: pctVsPrev },
          { label: 'Productividad media', value: prodValue, sub: pctVsPrevProd },
          { label: 'Horas totales', value: data.totalHours != null ? data.totalHours.toFixed(1) : '—', sub: '' },
          { label: 'Coste personal', value: costeValue, sub: costeSub },
          { label: 'Facturación objetivo', value: objetivoValue, sub: objetivoSub }
        ];
        kpis.forEach(function (k) {
          var div = document.createElement('div');
          div.className = 'kpi-card';
          div.innerHTML = '<div class="label">' + k.label + '</div><div class="value">' + k.value + '</div>' + (k.sub || '');
          kpisEl.appendChild(div);
        });
        if (resumenEl) {
          resumenEl.innerHTML = '<h3>Resumen</h3>' +
            (data.resumenClasificacion ? '<p class="dashboard-clasificacion">' + data.resumenClasificacion + '</p>' : '') +
            '<p>' + (data.resumenTexto || 'Sin datos para esta semana.') + '</p>';
        }
        var days = data.days || [];
        if (days.length === 0) {
          daysWrap.innerHTML = '<p class="dashboard-empty">Aún no hay días registrados para esta semana. Puedes añadirlos desde <a href="#registro" class="dashboard-link-registro">Registro de ejecución</a>.</p><p class="dashboard-empty-hint">Si acabas de cargar datos de muestra, asegúrate de que el selector de semana es la <strong>semana actual</strong> (lunes de esta semana) y pulsa <strong>Actualizar</strong>.</p>';
        } else {
          var headers = ['Día', 'Fecha', 'Facturación', 'Horas', 'Productividad (€/h)', 'Personal', 'Tendencia'];
          var rows = days.map(function (d) {
            var dayName = d.dayName || dayNameFromDate(d.date);
            var dateShort = formatDateShort(d.date);
            var rev = d.revenue != null ? d.revenue.toFixed(0) + ' €' : '—';
            var hours = d.hoursWorked != null ? d.hoursWorked.toFixed(1) : '—';
            var prod = d.productivity != null ? d.productivity.toFixed(1) : '—';
            var staffParts = [];
            if (d.staffSummarySala != null && d.staffSummarySala !== '' && d.staffSummaryCocina != null && d.staffSummaryCocina !== '') {
              staffParts.push('Sala: ' + d.staffSummarySala + ' | Cocina: ' + d.staffSummaryCocina);
              if (d.calculatedStaffHours != null) staffParts.push('Horas calc.: ' + Number(d.calculatedStaffHours).toFixed(1) + ' h (equipo del día)');
            }
            if (staffParts.length === 0 && d.staffTotal != null) staffParts.push(String(d.staffTotal));
            var staff = staffParts.length ? staffParts.join('<br/>') : '—';
            var trendParts = [d.trendLabel || '—'];
            if (d.avgRevenueHistoric != null && d.avgRevenueHistoric !== '') trendParts.push('vs media ' + Number(d.avgRevenueHistoric).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' €');
            if (d.trendVsPrevWeek) trendParts.push(d.trendVsPrevWeek);
            var trend = trendParts.join('<br>');
            return [dayName, dateShort, rev, hours, prod, staff, trend];
          });
          var personalTitle = 'Sala y cocina por turno (Mediodía-Tarde-Noche). Horas calc. = (Sala+Cocina) × horas por turno.';
          var thead = '<thead><tr><th>Día</th><th>Fecha</th><th>Facturación</th><th>Horas</th><th>Productividad (€/h)</th><th title="' + personalTitle + '">Personal</th><th>Tendencia</th></tr></thead>';
          var tbody = '<tbody>' + rows.map(function (row) { return '<tr>' + row.map(function (c) { return '<td>' + c + '</td>'; }).join('') + '</tr>'; }).join('') + '</tbody>';
          daysWrap.innerHTML = '<table class="dashboard-table">' + thead + tbody + '</table>';
        }
        var chartEl = document.getElementById('dashboard-chart-30d');
        if (chartEl) {
          var rawItems = data.last30Days || [];
          if (rawItems.length === 0) { chartEl.innerHTML = '<p class="dashboard-empty">Sin datos para los últimos 30 días.</p><p class="dashboard-empty-hint">Puedes cargar <a href="#configuracion">datos de muestra</a> en Configuración o importar desde Excel arriba.</p>'; }
          else {
            var today = new Date();
            var start = new Date(today);
            start.setDate(start.getDate() - 29);
            var byDate = {};
            rawItems.forEach(function (x) {
              if (x.date && x.date.length >= 10) byDate[x.date] = x.revenue != null ? x.revenue : 0;
            });
            var items = [];
            for (var i = 0; i < 30; i++) {
              var d = new Date(start);
              d.setDate(start.getDate() + i);
              var y = d.getFullYear();
              var m = String(d.getMonth() + 1).padStart(2, '0');
              var day = String(d.getDate()).padStart(2, '0');
              var dateStr = y + '-' + m + '-' + day;
              items.push({ date: dateStr, revenue: byDate[dateStr] != null ? byDate[dateStr] : 0 });
            }
            var scaleMax = Math.max.apply(null, items.map(function (x) { return x.revenue || 0; }));
            if (scaleMax <= 0) scaleMax = 1;
            var scaleLabel = 'Escala: 0 – ' + Number(scaleMax).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' €';
            var bars = items.map(function (x) {
              var rev = x.revenue || 0;
              var pct = scaleMax > 0 ? Math.min(100, (rev / scaleMax) * 100) : 0;
              var dateShort = (x.date && x.date.length >= 10) ? (x.date.slice(8, 10) + '/' + x.date.slice(5, 7)) : '—';
              var dayName = (x.date && x.date.length >= 10) ? dayNameFromDate(x.date) : '—';
              return '<div class="dashboard-chart-bar-wrap" title="' + (x.date || '') + ': ' + (rev !== null && rev !== undefined ? Number(rev).toFixed(0) : 0) + ' €"><div class="dashboard-chart-bar" style="height:' + pct + '%"></div><span class="dashboard-chart-label">' + dateShort + '</span><span class="dashboard-chart-day">' + dayName + '</span></div>';
            }).join('');
            chartEl.innerHTML = '<p class="dashboard-chart-scale">' + scaleLabel + '</p><div class="dashboard-chart-bars">' + bars + '</div>';
          }
        }
      }).catch(function (err) {
        if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange((weekInput && weekInput.value) || weekStart); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
        kpisEl.innerHTML = '<p class="error-msg">' + (err.message || 'Error al cargar.') + '</p>';
        var chartEl = document.getElementById('dashboard-chart-30d');
        if (chartEl) chartEl.innerHTML = '<p class="dashboard-empty">Sin datos para el gráfico.</p>';
      });
    }
    document.getElementById('dashboard-cargar').addEventListener('click', load);
    if (weekInput) weekInput.addEventListener('change', load);
    document.getElementById('dashboard-prev').addEventListener('click', function () {
      var ws = (weekInput && weekInput.value) || weekStart;
      weekInput.value = addDays(ws, -7);
      load();
    });
    document.getElementById('dashboard-next').addEventListener('click', function () {
      var ws = (weekInput && weekInput.value) || weekStart;
      weekInput.value = addDays(ws, 7);
      load();
    });
    var excelFile = document.getElementById('dashboard-excel-file');
    var excelStatus = document.getElementById('dashboard-excel-status');
    document.getElementById('dashboard-import-excel').addEventListener('click', function () { excelFile.click(); });
    excelFile.addEventListener('change', function () {
      if (!this.files || this.files.length === 0) return;
      var ws = (weekInput && weekInput.value) || weekStart;
      var excelBtn = document.getElementById('dashboard-import-excel');
      if (excelBtn) excelBtn.disabled = true;
      excelStatus.textContent = 'Enviando…';
      excelStatus.className = 'dashboard-import-status';
      var fd = new FormData();
      fd.append('file', this.files[0]);
      auth.fetchWithAuth('/api/import/excel?weekStart=' + encodeURIComponent(ws), { method: 'POST', body: fd }).then(function (r) {
        if (r.status === 401) return null;
        return r.json();
      }).then(function (data) {
        if (!data) return;
        var msg = (data.days_created || 0) + ' días creados, ' + (data.days_updated || 0) + ' actualizados, ' + (data.shifts_updated || 0) + ' turnos.';
        if (data.errors && data.errors.length > 0) msg += ' ' + data.errors.join(' ');
        excelStatus.textContent = msg;
        excelStatus.className = data.errors && data.errors.length > 0 ? 'dashboard-import-status error' : 'dashboard-import-status success';
        load();
      }).catch(function () {
        excelStatus.textContent = 'Error al enviar.';
        excelStatus.className = 'dashboard-import-status error';
      }).finally(function () {
        if (excelBtn) excelBtn.disabled = false;
      });
      this.value = '';
    });
    load();
  }
  global.LUCAS_DASHBOARD_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
