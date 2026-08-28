using System.Collections.Concurrent;
using G3MToolCLI.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using UndertaleModLib;
using GM3P.Patching;

namespace GM3P.G3MTool;

public class ExecuteService
{
    private static readonly ConcurrentDictionary<string, Script<object>> _scriptCache = new();

    private static ScriptOptions GetDefaultOptions(string? scriptPath = null)
    {
        var options = ScriptOptions.Default
        .AddImports(DefaultImports)
        .AddReferences(
            typeof(UndertaleData).Assembly,
            typeof(ImageMagick.MagickImage).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly
        )
        .WithOptimizationLevel(OptimizationLevel.Release);
        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            var scriptDirectory = Path.GetDirectoryName(Path.GetFullPath(scriptPath));
            options = options
                .WithFilePath(Path.GetFullPath(scriptPath))
                .WithSourceResolver(ScriptSourceResolver.Default.WithBaseDirectory(scriptDirectory!))
                .WithMetadataResolver(ScriptMetadataResolver.Default.WithBaseDirectory(scriptDirectory!));
        }
        return options;
    }

    private static readonly string[] DefaultImports =
    [
        "System",
        "System.IO",
        "System.Text",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.Linq",
        "System.Threading.Tasks",
        "System.Collections.Generic",
        "UndertaleModLib",
        "UndertaleModLib.Models",
        "UndertaleModLib.Util",
        "UndertaleModLib.Decompiler",
        "UndertaleModLib.Compiler",
        "UndertaleModLib.Scripting",
        "ImageMagick",
        "G3MToolCLI",
        "G3MToolCLI.Commands",
        "G3MToolCLI.Models",
        "G3MToolCLI.Services",
        "GM3P",
        "GM3P.Patching",
        "GM3P.Merging",
        "GM3P.Data",
        "GM3P.FileSystem",
        "GM3P.Core"
    ];

    public static async Task<ScriptResult> ExecuteScriptAsync(
        string scriptPath,
        string? dataPath,
        string outputPath,
        string[] args)
    {
        if (!File.Exists(scriptPath))
            return new ScriptResult { Success = false, Error = $"Script not found: {scriptPath}" };

        var hasData = !string.IsNullOrEmpty(dataPath) && File.Exists(dataPath);

        if (!string.IsNullOrEmpty(dataPath) && !hasData)
            return new ScriptResult { Success = false, Error = $"Data file not found: {dataPath}" };

        UndertaleData? data = null;
        try
        {
            if (hasData)
            {
                Console.WriteLine($"[ScriptExecutor] Loading data file: {dataPath}");

                using (var stream = new FileStream(dataPath!, FileMode.Open, FileAccess.Read))
                {
                    data = UndertaleIO.Read(stream);
                }

                Console.WriteLine($"[ScriptExecutor] Data loaded. Game: {data.GeneralInfo?.DisplayName?.Content ?? "Unknown"}");
            }
            else
            {
                Console.WriteLine("[ScriptExecutor] No data file specified, running script without game data");
            }

            Console.WriteLine($"[ScriptExecutor] Executing script: {scriptPath}");

            // Determine outputDir for script globals
            var outputDir = Directory.Exists(outputPath) ? outputPath : (Path.GetDirectoryName(outputPath) ?? outputPath);

            // Use first argument as inputDir for import scripts
            var inputDir = args.Length > 0 ? args[0] : null;

            var scriptContent = await File.ReadAllTextAsync(scriptPath);
            var result = await ExecuteScriptWithRoslynAsync(scriptContent, data!, scriptPath, outputDir, inputDir, dataPath);

            if (!result.Success)
                return result;

            if (hasData && !string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine($"[ScriptExecutor] Saving to: {outputPath}");

                var outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                UndertaleIO.Write(stream, data!);
                stream.Flush(true);
                stream.Close();
                try
                {
                    using var verify = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var verifiedData = UndertaleIO.Read(verify);
                }
                catch (Exception ex)
                {
                    try { File.Delete(outputPath); } catch { }
                    return new ScriptResult { Success = false, Error = $"Saved data validation failed: {ex.Message}" };
                }
            }

            Console.WriteLine("[ScriptExecutor] Script executed successfully");
            return new ScriptResult { Success = true, Output = result.Output };
        }
        catch (Exception ex)
        {
            return new ScriptResult { Success = false, Error = $"Script execution failed: {ex.Message}" };
        }
        finally
        {
            data?.Dispose();
        }
    }

    private static async Task<ScriptResult> ExecuteScriptWithRoslynAsync(
        string scriptContent,
        UndertaleData data,
        string? scriptPath = null,
        string? outputDir = null,
        string? inputDir = null,
        string? dataFilePath = null)
    {
        try
        {
            var globals = new ScriptGlobals
            {
                Data = data,
                FilePath = dataFilePath ?? scriptPath ?? "inline",
                ScriptPath = scriptPath,
                ScriptRootPath = scriptPath is null
                    ? Environment.CurrentDirectory
                    : Path.GetDirectoryName(Path.GetFullPath(scriptPath))
                        ?? Environment.CurrentDirectory,
                DataFilePath = dataFilePath ?? string.Empty,
                OutputDir = outputDir ?? string.Empty,
                InputDir = inputDir ?? string.Empty
            };
            globals.ScriptRunner = path => ExecuteNestedScriptAsync(path, globals).GetAwaiter().GetResult();

            // Use cached compiled script if available, otherwise compile and cache
            var cacheKey = scriptPath ?? scriptContent;
            var script = _scriptCache.GetOrAdd(cacheKey, _ =>
            {
                var s = CSharpScript.Create(scriptContent, GetDefaultOptions(scriptPath), typeof(ScriptGlobals));
                s.Compile();
                return s;
            });

            // Suppress script console output in non-verbose mode
            //TextWriter? originalOut = null;
            //if (!LogService.Verbose)
            {
                //originalOut = Console.Out;
                //Console.SetOut(TextWriter.Null);
            }
            try
            {
                await script.RunAsync(globals).ConfigureAwait(false);
            }
            finally
            {
                //if (originalOut != null)
                  //  Console.SetOut(originalOut);
            }

            return new ScriptResult { Success = true };
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join("\n", ex.Diagnostics.Select(d => d.ToString()));
            return new ScriptResult { Success = false, Error = $"Compilation error:\n{errors}" };
        }
        catch (ScriptException ex)
        {
            return new ScriptResult { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            return new ScriptResult { Success = false, Error = $"Runtime error: {ex.Message}" };
        }
    }

    private static async Task<bool> ExecuteNestedScriptAsync(string path, ScriptGlobals globals)
    {
        var scriptDirectory =
            Path.GetDirectoryName(globals.ScriptPath) ?? globals.ScriptRootPath;
        path = Path.GetFullPath(
            path,
            scriptDirectory
        );
        var relativePath = Path.GetRelativePath(globals.ScriptRootPath, path);
        if (
            Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        )
            throw new ScriptException("Nested script path escapes the mod directory.");
        if (!File.Exists(path))
            throw new ScriptException($"Script not found: {path}");
        var previousPath = globals.ScriptPath;
        try
        {
            globals.ScriptPath = path;
            var content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            await CSharpScript
                .EvaluateAsync(content, GetDefaultOptions(path), globals, typeof(ScriptGlobals))
                .ConfigureAwait(false);
            return true;
        }
        finally
        {
            globals.ScriptPath = previousPath;
        }
    }

}

public class ScriptResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Output { get; set; }
}
