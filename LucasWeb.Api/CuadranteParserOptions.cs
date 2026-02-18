namespace LucasWeb.Api;

/// <summary>Opciones para invocar el parser Python de cuadrantes (LucasCuadranteParser).</summary>
public class CuadranteParserOptions
{
    public const string SectionName = "CuadranteParser";

    /// <summary>Comando para ejecutar Python (ej. "python" o "py").</summary>
    public string PythonPath { get; set; } = "python";

    /// <summary>Ruta absoluta a la carpeta LucasCuadranteParser (donde está main.py). Si está vacía se usa ..\LucasCuadranteParser respecto al ContentRootPath de la API.</summary>
    public string ParserProjectPath { get; set; } = "";
}
