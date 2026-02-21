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
    function weekEndFromWeekStart(ws) {
      var d = new Date(ws + 'T12:00:00');
      d.setDate(d.getDate() + 6);
      return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    }
    container.innerHTML =
      '<div class="dashboard-title-row">' +
      '<div class="dashboard-title-block">' +
      '<h2 class="view-title">Dashboard</h2>' +
      '<p class="dashboard-subtitle">Datos de la semana seleccionada</p>' +
      '</div>' +
      '<div class="dashboard-week-bar">' +
      '<button type="button" id="dashboard-cargar" class="btn-primary dashboard-week-btn-actualizar">Actualizar</button>' +
      '<div class="dashboard-week-nav">' +
      '<button type="button" id="dashboard-prev" class="dashboard-week-arrow" title="Semana anterior">◀</button>' +
      '<span id="dashboard-week-range" class="dashboard-week-range dashboard-week-range--clickable" title="Seleccionar semana">' + formatWeekRange(weekStart) + '</span>' +
      '<button type="button" id="dashboard-next" class="dashboard-week-arrow" title="Semana siguiente">▶</button>' +
      '</div>' +
      '<span id="dashboard-semana-en-curso" class="dashboard-week-status hidden"></span>' +
      '<input type="date" id="dashboard-week-start" class="dashboard-week-input-hidden" value="' + weekStart + '" aria-hidden="true" tabindex="-1" />' +
      '</div>' +
      '</div>' +
      '<div id="dashboard-kpis" class="kpi-grid"></div>' +
      '<div id="dashboard-days-wrap" class="card"><h3>Días de la semana</h3><div id="dashboard-days-table-wrap"></div><div id="dashboard-days-cards-wrap" class="dashboard-days-cards-wrap"></div></div>' +
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
      '<div class="dashboard-import-item">' +
      '<label class="dashboard-import-label">PDF cuadrante (personal y horas programadas por turno)</label>' +
      '<input type="file" id="dashboard-pdf-file" accept=".pdf,application/pdf" class="dashboard-import-input" />' +
      '<button type="button" id="dashboard-import-pdf" class="btn-secondary btn-sm">Cargar PDF</button>' +
      '<span id="dashboard-pdf-status" class="dashboard-import-status"></span>' +
      '</div>' +
      '</div>' +
      '</div>' +
      '<div id="dashboard-bloque-ampliacion" class="card dashboard-bloque-ampliacion">' +
      '<h3 class="dashboard-bloque-ampliacion-title">Facturación últimos 30 días</h3>' +
      '<div id="dashboard-chart-30d" class="dashboard-chart-30d"><p class="loading">Cargando…</p></div>' +
      '</div>';
    var weekInput = document.getElementById('dashboard-week-start');
    var weekRangeEl = document.getElementById('dashboard-week-range');
    var badgeEl = document.getElementById('dashboard-semana-en-curso');
    var kpisEl = document.getElementById('dashboard-kpis');
    var resumenEl = document.getElementById('dashboard-resumen');
    var daysWrap = document.getElementById('dashboard-days-table-wrap');
    var daysCardsWrap = document.getElementById('dashboard-days-cards-wrap');

    // Asegurar 4 KPIs en una fila (evita depender de caché CSS).
    if (kpisEl) {
      kpisEl.style.gridTemplateColumns = 'repeat(4, minmax(0, 1fr))';
    }

    // Selector de semana: click sobre el rango abre el date picker, como en Registro/Preguntas.
    if (weekRangeEl && weekInput) {
      weekRangeEl.addEventListener('click', function () {
        try {
          if (typeof weekInput.showPicker === 'function') weekInput.showPicker();
          else weekInput.click();
        } catch (e) { try { weekInput.click(); } catch (e2) { } }
      });
    }
    function load() {
      var ws = (weekInput && weekInput.value) || weekStart;
      if (weekRangeEl) { weekRangeEl.textContent = 'Cargando…'; weekRangeEl.classList.add('dashboard-week-range--loading'); }
      if (badgeEl) {
        if (isCurrentWeek(ws)) { badgeEl.textContent = 'En curso'; badgeEl.classList.remove('hidden'); badgeEl.classList.add('dashboard-badge--current'); }
        else { badgeEl.textContent = ''; badgeEl.classList.add('hidden'); badgeEl.classList.remove('dashboard-badge--current'); }
      }
      kpisEl.innerHTML = '<p class="loading">Cargando…</p>';
      resumenEl.innerHTML = '';
      daysWrap.innerHTML = '';
      var todayYmd = (function () { var t = new Date(); return t.getFullYear() + '-' + String(t.getMonth() + 1).padStart(2, '0') + '-' + String(t.getDate()).padStart(2, '0'); })();
      var we = weekEndFromWeekStart(ws);
      Promise.all([
        auth.fetchWithAuth('/api/dashboard/week?weekStart=' + encodeURIComponent(ws) + '&asOf=' + encodeURIComponent(todayYmd)).then(function (r) {
        if (r.status === 401) {
          if (global.LUCAS_APP && global.LUCAS_APP.onUnauthorized) global.LUCAS_APP.onUnauthorized();
          return null;
        }
        if (!r.ok) throw new Error('Error al cargar');
        return r.json();
      }),
        auth.fetchWithAuth('/api/events?from=' + encodeURIComponent(ws) + '&to=' + encodeURIComponent(we)).then(function (r2) {
          if (r2.status === 401) return null;
          if (r2.status === 403) return [];
          if (!r2.ok) return [];
          return r2.json();
        }).catch(function () { return []; })
      ]).then(function (arr) {
        var data = arr[0];
        var events = arr[1] || [];
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
        // % vs objetivo (facturación)
        var objRaw = data.facturacionObjetivo != null ? data.facturacionObjetivo : data.FacturacionObjetivo;
        var objNum = objRaw != null && objRaw !== '' ? Number(objRaw) : NaN;
        if (objNum > 0 && data.totalRevenue != null) {
          var pctObj = ((data.totalRevenue - objNum) / objNum) * 100;
          var objetivoClass = pctObj > 0 ? 'kpi-card-sub--up' : (pctObj < 0 ? 'kpi-card-sub--down' : '');
          var pctObjStr = pctObj > 0 ? ('+' + pctObj.toFixed(1)) : pctObj.toFixed(1);
          pctVsPrev += '<div class="kpi-card-sub ' + objetivoClass + '">' + pctObjStr + '% vs fact. objetivo</div>';
        } else {
          pctVsPrev += '<div class="kpi-card-sub kpi-card-sub--muted">vs fact. objetivo: —</div>';
        }
        var prodValue = data.avgProductivity != null ? data.avgProductivity.toFixed(1) + ' €/h' : '—';
        var pctVsPrevProd = '';
        if (data.avgProductivity != null && data.prevWeekProductivity != null && data.prevWeekProductivity > 0) {
          var pctProd = ((data.avgProductivity - data.prevWeekProductivity) / data.prevWeekProductivity) * 100;
          if (pctProd > 0) pctVsPrevProd = '<div class="kpi-card-sub kpi-card-sub--up">+' + pctProd.toFixed(1) + '% vs sem. ant.</div>';
          else if (pctProd < 0) pctVsPrevProd = '<div class="kpi-card-sub kpi-card-sub--down">' + pctProd.toFixed(1) + '% vs sem. ant.</div>';
          else pctVsPrevProd = '<div class="kpi-card-sub">0% vs sem. ant.</div>';
        }
        // % vs objetivo (productividad)
        var prodObjRaw = data.productividadObjetivo != null ? data.productividadObjetivo : (data.ProductividadObjetivo != null ? data.ProductividadObjetivo : data.productividadIdealEurHora);
        var prodObjNum = prodObjRaw != null && prodObjRaw !== '' ? Number(prodObjRaw) : NaN;
        if (prodObjNum > 0 && data.avgProductivity != null) {
          var pctProdObj = ((data.avgProductivity - prodObjNum) / prodObjNum) * 100;
          var prodObjClass = pctProdObj > 0 ? 'kpi-card-sub--up' : (pctProdObj < 0 ? 'kpi-card-sub--down' : '');
          var pctProdObjStr = pctProdObj > 0 ? ('+' + pctProdObj.toFixed(1)) : pctProdObj.toFixed(1);
          pctVsPrevProd += '<div class="kpi-card-sub ' + prodObjClass + '">' + pctProdObjStr + '% vs prod. objetivo</div>';
        } else {
          pctVsPrevProd += '<div class="kpi-card-sub kpi-card-sub--muted">vs prod. objetivo: —</div>';
        }
        var costeValue = data.costePersonalEur != null ? data.costePersonalEur.toFixed(0) + ' €' : '—';
        var costeSub = '';
        if (data.costePersonalPctFacturacion != null) {
          var pctCoste = data.costePersonalPctFacturacion;
          var costeClass = pctCoste < 30 ? 'kpi-card-sub--coste-bueno' : (pctCoste <= 35 ? 'kpi-card-sub--coste-asumible' : 'kpi-card-sub--coste-alto');
          costeSub = '<div class="kpi-card-sub ' + costeClass + '">' + pctCoste.toFixed(1) + '% vs facturación</div>';
        }
        if (data.costePersonalPctVsHistoric != null) {
          costeSub += '<div class="kpi-card-sub">vs histórico: ' + Number(data.costePersonalPctVsHistoric).toFixed(1) + '%</div>';
        }
        if (data.costePersonalEurFromContrato != null) {
          costeSub += '<div class="kpi-card-sub">Contrato: ' + Number(data.costePersonalEurFromContrato).toFixed(0) + ' €</div>';
        }
        var hoursSub = '';
        if (data.avgHoursHistoric != null) hoursSub = '<div class="kpi-card-sub">Histórico: ' + Number(data.avgHoursHistoric).toFixed(1) + ' h</div>';

        var kpis = [
          { label: 'Facturación total', value: revValue, sub: pctVsPrev },
          { label: 'Productividad media', value: prodValue, sub: pctVsPrevProd },
          { label: 'Horas totales', value: data.totalHours != null ? data.totalHours.toFixed(1) : '—', sub: hoursSub },
          { label: 'Coste personal', value: costeValue, sub: costeSub }
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
        var eventsByDate = {};
        (events || []).forEach(function (e) {
          if (!e || !e.date) return;
          if (!eventsByDate[e.date]) eventsByDate[e.date] = [];
          eventsByDate[e.date].push(e);
        });

        var days = data.days || [];
        if (days.length === 0) {
          daysWrap.innerHTML = '<p class="dashboard-empty">Aún no hay días registrados para esta semana. Puedes añadirlos desde <a href="#registro" class="dashboard-link-registro">Registro de ejecución</a>.</p><p class="dashboard-empty-hint">Si acabas de cargar datos de muestra, asegúrate de que el selector de semana es la <strong>semana actual</strong> (lunes de esta semana) y pulsa <strong>Actualizar</strong>.</p>';
          if (daysCardsWrap) daysCardsWrap.innerHTML = '';
        } else {
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
          function weatherText(d) {
            if (!d) return '—';
            var hasAny = (d.weatherCode != null) || (d.weatherTempMax != null) || (d.weatherTempMin != null) || (d.weatherPrecipMm != null) || (d.weatherWindMaxKmh != null);
            if (!hasAny) return '—';
            var parts = [];
            var icon = weatherEmoji(d.weatherCode);
            parts.push(icon);
            if (d.weatherTempMax != null || d.weatherTempMin != null) {
              var tMax = d.weatherTempMax != null ? Number(d.weatherTempMax).toFixed(0) : '—';
              var tMin = d.weatherTempMin != null ? Number(d.weatherTempMin).toFixed(0) : '—';
              parts.push(tMax + '/' + tMin + '°C');
            }
            if (d.weatherPrecipMm != null) parts.push(Number(d.weatherPrecipMm).toFixed(0) + 'mm');
            if (d.weatherWindMaxKmh != null) parts.push(Number(d.weatherWindMaxKmh).toFixed(0) + 'km/h');
            return parts.join(' · ');
          }

          var headers = ['Día', 'Fecha', 'Clima', 'Facturación', 'Horas', 'Productividad (€/h)', 'Personal', 'Tendencia', 'Acciones'];
          var rows = days.map(function (d) {
            var dayName = d.dayName || dayNameFromDate(d.date);
            var dateShort = formatDateShort(d.date);
            var clima = '<span class="dashboard-weather">' + weatherText(d) + '</span>';
            var rev = d.revenue != null ? d.revenue.toFixed(0) + ' €' : '—';
            var hoursVal = d.effectiveHours != null ? d.effectiveHours : d.hoursWorked;
            var hours = hoursVal != null ? hoursVal.toFixed(1) : '—';
            var prodVal = d.effectiveProductivity != null ? d.effectiveProductivity : d.productivity;
            var prod = prodVal != null ? prodVal.toFixed(1) : '—';
            var staffParts = [];
            if (d.staffSummarySala != null && d.staffSummarySala !== '' && d.staffSummaryCocina != null && d.staffSummaryCocina !== '') {
              staffParts.push('Sala: ' + d.staffSummarySala + ' | Cocina: ' + d.staffSummaryCocina);
              if (d.plannedHoursFromPdf != null && d.plannedHoursFromPdf > 0) {
                if (d.plannedHoursBreakdown != null && d.plannedHoursBreakdown !== '') staffParts.push(d.plannedHoursBreakdown);
                staffParts.push('Horas (cuadrante): ' + Number(d.plannedHoursFromPdf).toFixed(1) + ' h');
              } else if (d.calculatedStaffHours != null) {
                staffParts.push('Horas calc.: ' + Number(d.calculatedStaffHours).toFixed(1) + ' h (Sala+Cocina × h/turno)');
              }
            }
            if (staffParts.length === 0 && d.staffTotal != null) staffParts.push(String(d.staffTotal));
            var staff = staffParts.length ? staffParts.join('<br/>') : '—';
            // Orden: 1) vs media (con hoy %), 2) tendencia 12 sem., 3) vs sem. ant.
            var trendParts = [];
            if (d.avgRevenueHistoric != null && d.avgRevenueHistoric !== '') {
              var mediaStr = 'vs media ' + Number(d.avgRevenueHistoric).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' €';
              if (d.pctVsAvgHistoric != null && typeof d.pctVsAvgHistoric === 'number') mediaStr += ' (hoy ' + (d.pctVsAvgHistoric >= 0 ? '+' : '') + d.pctVsAvgHistoric + '%)';
              trendParts.push(mediaStr);
            }
            if (d.trendLabel) trendParts.push(d.trendLabel);
            if (d.trendVsPrevWeek) trendParts.push(d.trendVsPrevWeek);
            var trend = trendParts.length ? trendParts.join('<br>') : '—';
            var dateStr = d.date || '';
            var evs = eventsByDate[dateStr] || [];
            if (evs.length > 0) {
              var evTxt = evs.slice(0, 2).map(function (e) { return (e.impact ? ('[' + e.impact + '] ') : '') + (e.name || ''); }).join('<br>');
              if (evs.length > 2) evTxt += '<br><span class="dashboard-events-more">+' + (evs.length - 2) + ' más</span>';
              trend += (trend ? '<br>' : '') + '<span class="dashboard-events-inline">' + evTxt + '</span>';
            }
            var dateStr = d.date || '';
            var acciones =
              '<a class="dashboard-action-link" href="#registro?date=' + encodeURIComponent(dateStr) + '">Registro</a>' +
              '<span class="dashboard-action-sep">·</span>' +
              '<a class="dashboard-action-link" href="#preguntas?date=' + encodeURIComponent(dateStr) + '">Feedback</a>';
            return [dayName, dateShort, clima, rev, hours, prod, staff, trend, acciones];
          });
          var personalTitle = 'Sala y cocina por turno (Mediodía-Tarde-Noche). Con PDF: se muestran las horas del cuadrante por turno (reales). Sin PDF (dato manual): Horas calc. = (Sala+Cocina) × h/turno (Config.).';
          var tendenciaTitle = 'vs media: media de facturación de ese día de la semana en las últimas 12 semanas. (hoy ±%): este día respecto a esa media. Tendencia ↑/↓: evolución del día de la semana en el tiempo (mitad reciente vs antigua de 12 sem.). vs sem. ant.: mismo día de la semana anterior.';
          var thead = '<thead><tr><th>Día</th><th>Fecha</th><th>Clima</th><th>Facturación</th><th>Horas</th><th>Productividad (€/h)</th><th title="' + personalTitle + '">Personal</th><th title="' + tendenciaTitle + '">Tendencia</th><th>Acciones</th></tr></thead>';
          var tbody = '<tbody>' + rows.map(function (row) { return '<tr>' + row.map(function (c) { return '<td>' + c + '</td>'; }).join('') + '</tr>'; }).join('') + '</tbody>';
          daysWrap.innerHTML = '<table class="dashboard-table">' + thead + tbody + '</table>';

          // Cards (móvil): más legible que una tabla de 7 columnas.
          if (daysCardsWrap) {
            daysCardsWrap.innerHTML = '<div class="dashboard-days-cards">' + days.map(function (d) {
              var dayName = d.dayName || dayNameFromDate(d.date);
              var dateShort = formatDateShort(d.date);
              var rev = d.revenue != null ? d.revenue.toFixed(0) + ' €' : '—';
              var hoursVal = d.effectiveHours != null ? d.effectiveHours : d.hoursWorked;
              var hours = hoursVal != null ? hoursVal.toFixed(1) + ' h' : '—';
              var prodVal = d.effectiveProductivity != null ? d.effectiveProductivity : d.productivity;
              var prod = prodVal != null ? prodVal.toFixed(1) + ' €/h' : '—';
              var staffLine = '—';
              if (d.staffSummarySala != null && d.staffSummarySala !== '' && d.staffSummaryCocina != null && d.staffSummaryCocina !== '')
                staffLine = 'Sala ' + d.staffSummarySala + ' | Cocina ' + d.staffSummaryCocina;
              else if (d.staffTotal != null) staffLine = 'Total: ' + d.staffTotal;
              var trendParts = [];
              if (d.avgRevenueHistoric != null && d.avgRevenueHistoric !== '') {
                var mediaStr = 'vs media ' + Number(d.avgRevenueHistoric).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' €';
                if (d.pctVsAvgHistoric != null && typeof d.pctVsAvgHistoric === 'number') mediaStr += ' (hoy ' + (d.pctVsAvgHistoric >= 0 ? '+' : '') + d.pctVsAvgHistoric + '%)';
                trendParts.push(mediaStr);
              }
              if (d.trendLabel) trendParts.push(d.trendLabel);
              if (d.trendVsPrevWeek) trendParts.push(d.trendVsPrevWeek);
              var trendLine = trendParts.length ? trendParts.join(' · ') : '—';
              var climaLine = weatherText(d);
              var hrefRegistro = '#registro?date=' + encodeURIComponent(d.date || '');
              var hrefPreguntas = '#preguntas?date=' + encodeURIComponent(d.date || '');
              var dateStr = d.date || '';
              var evs = eventsByDate[dateStr] || [];
              var eventsHtml = '';
              if (evs.length > 0) {
                eventsHtml = '<div class="dashboard-day-card-events"><div class="label">Eventos</div>' +
                  evs.slice(0, 2).map(function (e) {
                    return '<div class="dashboard-event-item">' +
                      '<span class="dashboard-event-name">' + (e.name || '') + '</span>' +
                      (e.impact ? '<span class="dashboard-event-impact">' + e.impact + '</span>' : '') +
                      '</div>';
                  }).join('') +
                  (evs.length > 2 ? '<div class="dashboard-events-more">+' + (evs.length - 2) + ' más</div>' : '') +
                  '</div>';
              }
              var addBtn = '<button type="button" class="btn-secondary btn-sm dashboard-event-add" data-date="' + dateStr + '">+ Evento</button>';
              return '' +
                '<div class="dashboard-day-card">' +
                '<div class="dashboard-day-card-head"><div class="dashboard-day-card-title">' + dayName + '</div><div class="dashboard-day-card-date">' + dateShort + '</div></div>' +
                '<div class="dashboard-day-card-kpis">' +
                '<div class="dashboard-day-card-kpi"><span class="label">Facturación</span><span class="value">' + rev + '</span></div>' +
                '<div class="dashboard-day-card-kpi"><span class="label">Horas</span><span class="value">' + hours + '</span></div>' +
                '<div class="dashboard-day-card-kpi"><span class="label">Prod.</span><span class="value">' + prod + '</span></div>' +
                '</div>' +
                '<div class="dashboard-day-card-meta">' +
                '<div><span class="label">Clima</span> <span class="dashboard-weather">' + climaLine + '</span></div>' +
                '<div><span class="label">Personal</span> ' + staffLine + '</div>' +
                (d.calculatedStaffHours != null ? '<div><span class="label">Horas equipo</span> ' + Number(d.calculatedStaffHours).toFixed(1) + ' h</div>' : '') +
                '<div><span class="label">Tendencia</span> ' + trendLine + '</div>' +
                '</div>' +
                '<div class="dashboard-day-card-actions">' +
                '<a class="btn-secondary btn-sm" href="' + hrefRegistro + '">Abrir registro</a>' +
                '<a class="btn-secondary btn-sm" href="' + hrefPreguntas + '">Abrir feedback</a>' +
                addBtn +
                '</div>' +
                eventsHtml +
                '</div>';
            }).join('') + '</div>';

            // Bind botones "+ Evento" (móvil)
            daysCardsWrap.querySelectorAll('.dashboard-event-add').forEach(function (btn) {
              btn.addEventListener('click', function () {
                var dateStr = btn.getAttribute('data-date') || '';
                var name = prompt('Nombre del evento', '');
                if (!name || !name.trim()) return;
                var impact = prompt('Impacto (Alto/Medio/Bajo o vacío)', '') || '';
                var desc = prompt('Descripción (opcional)', '') || '';
                auth.fetchWithAuth('/api/events', {
                  method: 'POST',
                  body: JSON.stringify({ date: dateStr, name: name, impact: impact, description: desc })
                }).then(function (r) {
                  if (!r.ok) return r.json().then(function (d) { throw new Error(d.message || 'Error'); });
                  return r.json();
                }).then(function () { load(); }).catch(function (e) { alert(e.message || 'Error al crear evento'); });
              });
            });
          }
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
            var scaleMaxReal = Math.max.apply(null, items.map(function (x) { return x.revenue || 0; }));
            // La escala máxima debe ser 25% superior al máximo real.
            var scaleMax = Math.ceil((scaleMaxReal > 0 ? scaleMaxReal : 1) * 1.25);
            var avg30 = items.reduce(function (s, it) { return s + (it.revenue || 0); }, 0) / (items.length || 1);
            var scaleLabel = 'Escala: 0 – ' + Number(scaleMax).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' € · Media 30d: ' + Number(avg30).toLocaleString('es-ES', { maximumFractionDigits: 0 }) + ' €';
            var barsArr = [];
            var lastWeekStartKey = null;
            var weekIndex = -1;
            items.forEach(function (x, idx) {
              var rev = x.revenue || 0;
              var pct = scaleMax > 0 ? Math.min(100, (rev / scaleMax) * 100) : 0;
              var dateShort = (x.date && x.date.length >= 10) ? (x.date.slice(8, 10) + '/' + x.date.slice(5, 7)) : '—';
              var dayName = (x.date && x.date.length >= 10) ? dayNameFromDate(x.date) : '—';
              var dt = x.date && x.date.length >= 10 ? new Date(x.date + 'T12:00:00') : null;

              // Fin de semana = viernes, sábado y domingo.
              var isWeekend = dt ? (dt.getDay() === 0 || dt.getDay() === 5 || dt.getDay() === 6) : false;

              // Resaltado por semanas: alternar fondo según semana (lunes como inicio).
              var weekStartKey = x.date && x.date.length >= 10 ? getWeekStart(x.date) : null;
              if (weekStartKey && weekStartKey !== lastWeekStartKey) {
                weekIndex++;
                lastWeekStartKey = weekStartKey;
              }
              var isAltWeek = weekIndex % 2 === 1;
              var isWeekBoundary = dt ? (dt.getDay() === 1 && idx !== 0) : false; // lunes

              var cls = 'dashboard-chart-bar-wrap' +
                (isWeekend ? ' is-weekend' : '') +
                (isAltWeek ? ' week-alt' : '') +
                (isWeekBoundary ? ' week-boundary' : '');

              barsArr.push(
                '<div class="' + cls + '" title="' + (x.date || '') + ': ' + (rev !== null && rev !== undefined ? Number(rev).toFixed(0) : 0) + ' €">' +
                '<div class="dashboard-chart-bar" style="height:' + pct + '%"></div>' +
                '<span class="dashboard-chart-label">' + dateShort + '</span>' +
                '<span class="dashboard-chart-day">' + dayName + '</span>' +
                '</div>'
              );
            });
            var bars = barsArr.join('');
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
    if (weekInput) weekInput.addEventListener('change', function () {
      // Normalizar a lunes aunque el usuario elija cualquier día.
      try {
        weekInput.value = getWeekStart(weekInput.value);
      } catch (e) { }
      load();
    });
    document.getElementById('dashboard-prev').addEventListener('click', function () {
      var ws = (weekInput && weekInput.value) || weekStart;
      weekInput.value = addDays(ws, -7);
      if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange(weekInput.value); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
      load();
    });
    document.getElementById('dashboard-next').addEventListener('click', function () {
      var ws = (weekInput && weekInput.value) || weekStart;
      weekInput.value = addDays(ws, 7);
      if (weekRangeEl) { weekRangeEl.textContent = formatWeekRange(weekInput.value); weekRangeEl.classList.remove('dashboard-week-range--loading'); }
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
      var fileName = this.files[0] && this.files[0].name ? this.files[0].name : '';
      var fd = new FormData();
      fd.append('file', this.files[0]);
      auth.fetchWithAuth('/api/import/excel?weekStart=' + encodeURIComponent(ws), { method: 'POST', body: fd }).then(function (r) {
        if (r.status === 401) return null;
        return r.json();
      }).then(function (data) {
        if (!data) return;
        var baseMsg = (data.message && data.message.length > 0)
          ? data.message
          : ((data.days_created || 0) + ' días creados, ' + (data.days_updated || 0) + ' actualizados, ' + (data.shifts_updated || 0) + ' turnos.');
        var msg = (fileName ? ('[' + fileName + '] ') : '') + baseMsg;
        if (data.errors && data.errors.length > 0) {
          var maxErr = 3;
          var shown = data.errors.slice(0, maxErr);
          msg += ' Errores: ' + shown.join(' ');
          if (data.errors.length > maxErr) msg += ' (+' + (data.errors.length - maxErr) + ' más)';
        }
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
    var pdfFile = document.getElementById('dashboard-pdf-file');
    var pdfStatus = document.getElementById('dashboard-pdf-status');
    document.getElementById('dashboard-import-pdf').addEventListener('click', function () { pdfFile.click(); });
    pdfFile.addEventListener('change', function () {
      if (!this.files || this.files.length === 0) return;
      var ws = (weekInput && weekInput.value) || weekStart;
      var pdfBtn = document.getElementById('dashboard-import-pdf');
      if (pdfBtn) pdfBtn.disabled = true;
      pdfStatus.textContent = 'Enviando…';
      pdfStatus.className = 'dashboard-import-status';
      var fileName = this.files[0] && this.files[0].name ? this.files[0].name : '';
      var fd = new FormData();
      fd.append('file', this.files[0]);
      auth.fetchWithAuth('/api/import/cuadrante-pdf?weekStart=' + encodeURIComponent(ws), { method: 'POST', body: fd }).then(function (r) {
        if (r.status === 401) return null;
        if (!r.ok) {
          return r.json().catch(function () { return {}; }).then(function (d) {
            throw new Error(d.message || ('Error del servidor (' + r.status + ')'));
          });
        }
        return r.json();
      }).then(function (data) {
        if (!data) return;
        var baseMsg = (data.message && data.message.length > 0)
          ? data.message
          : ((data.days_created || 0) + ' días creados, ' + (data.days_updated || 0) + ' actualizados, ' + (data.shifts_updated || 0) + ' turnos.');
        var msg = (fileName ? ('[' + fileName + '] ') : '') + baseMsg;
        if (data.errors && data.errors.length > 0) {
          var maxErr = 3;
          var shown = data.errors.slice(0, maxErr);
          msg += ' Errores: ' + shown.join(' ');
          if (data.errors.length > maxErr) msg += ' (+' + (data.errors.length - maxErr) + ' más)';
        }
        pdfStatus.textContent = msg;
        pdfStatus.className = data.errors && data.errors.length > 0 ? 'dashboard-import-status error' : 'dashboard-import-status success';
        load();
      }).catch(function (err) {
        var detail = (err && err.message) ? err.message : '';
        pdfStatus.textContent = detail ? ('Error al enviar: ' + detail) : 'Error al enviar.';
        pdfStatus.className = 'dashboard-import-status error';
      }).finally(function () {
        if (pdfBtn) pdfBtn.disabled = false;
      });
      this.value = '';
    });
    load();
  }
  global.LUCAS_DASHBOARD_VIEW = { render: render };
})(typeof window !== 'undefined' ? window : this);
