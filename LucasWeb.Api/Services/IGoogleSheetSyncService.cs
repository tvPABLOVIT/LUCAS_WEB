using LucasWeb.Api.Models;

namespace LucasWeb.Api.Services;

/// <summary>Sincroniza días de ejecución con Google Sheets (URL y credenciales en Settings).</summary>
public interface IGoogleSheetSyncService
{
    /// <summary>Envía un día guardado a la hoja del mes (fila = día + 1, 9 columnas A–I). En segundo plano, no bloquea. Como doc GUARDADO_GOOGLE_SHEET.</summary>
    Task SyncDayAsync(ExecutionDay day, CancellationToken cancellationToken = default);

    /// <summary>Envía los días indicados al Google Sheet (hoja del mes por día, 9 columnas). Para Exportar todo / Import estimaciones.</summary>
    Task SyncAsync(IEnumerable<DateTime> dates, CancellationToken cancellationToken = default);
}
