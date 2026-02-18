using System.Diagnostics;
using System.Text.Json;
using LucasWeb.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace LucasWeb.Api.Services;

/// <summary>Invocación del parser Python LucasCuadranteParser para extraer datos del PDF de cuadrante.</summary>
public class CuadrantePdfService : ICuadrantePdfService
{
    private readonly CuadranteParserOptions _options;
    private readonly IWebHostEnvironment _env;

    public CuadrantePdfService(IOptions<CuadranteParserOptions> options, IWebHostEnvironment env)
    {
        _options = options?.Value ?? new CuadranteParserOptions();
        _env = env;
    }

    public async Task<List<CuadranteDayDto>> ParsePdfAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var parserDir = GetParserProjectPath();
        if (!Directory.Exists(parserDir))
            throw new InvalidOperationException($"No se encuentra la carpeta del parser: {parserDir}. Configure CuadranteParser:ParserProjectPath o coloque LucasCuadranteParser en la raíz del proyecto.");

        var mainPy = Path.Combine(parserDir, "main.py");
        if (!File.Exists(mainPy))
            throw new InvalidOperationException($"No se encuentra main.py en {parserDir}.");

        string tempPdfPath = Path.Combine(Path.GetTempPath(), "LucasCuadrante_" + Guid.NewGuid().ToString("N")[..8] + ".pdf");
        string tempOutDir = Path.Combine(Path.GetTempPath(), "LucasCuadrante_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempOutDir);
            await using (var fs = File.Create(tempPdfPath))
                await pdfStream.CopyToAsync(fs, cancellationToken);

            var psi = new ProcessStartInfo
            {
                FileName = _options.PythonPath,
                ArgumentList = { mainPy, tempPdfPath, "--output-dir", tempOutDir },
                WorkingDirectory = parserDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("No se pudo iniciar el proceso de Python.");

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"El parser devolvió código {process.ExitCode}. " + (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr));

            var jsonPath = Path.Combine(tempOutDir, "cuadrante_lucas.json");
            if (!File.Exists(jsonPath))
                throw new InvalidOperationException("El parser no generó cuadrante_lucas.json. " + stdout);

            var json = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<CuadranteDayDto>>(json);
            return list ?? new List<CuadranteDayDto>();
        }
        finally
        {
            try { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); } catch { }
            try { if (Directory.Exists(tempOutDir)) Directory.Delete(tempOutDir, true); } catch { }
        }
    }

    private string GetParserProjectPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.ParserProjectPath))
            return Path.GetFullPath(_options.ParserProjectPath);
        var contentRoot = _env.ContentRootPath ?? AppContext.BaseDirectory ?? ".";
        return Path.GetFullPath(Path.Combine(contentRoot, "..", "LucasCuadranteParser"));
    }
}
