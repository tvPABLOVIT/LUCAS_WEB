namespace LucasWeb.Api;

public class LucasOptions
{
    public const string SectionName = "Lucas";
    public int TokenExpirationMinutes { get; set; } = 480;
    public string DefaultPin { get; set; } = "1502";
    /// <summary>Si true, al arrancar y no haber días de ejecución se insertan 60 días de demo. En hosting/producción conviene false.</summary>
    public bool SeedDemoData { get; set; } = false;
    /// <summary>En producción, orígenes CORS permitidos (separados por coma). Vacío = AllowAnyOrigin. Ej: "https://app.tudominio.com,https://tudominio.com"</summary>
    public string AllowedOrigins { get; set; } = "";
}
