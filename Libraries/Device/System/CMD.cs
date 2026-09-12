using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniversalDeviceToolkit.Lib.Resources;
using UniversalDeviceToolkit.Lib.Utils;

namespace UniversalDeviceToolkit.Lib.System;

/// <summary>
/// Provides secure command execution with comprehensive injection prevention.
/// </summary>
public static class CMD
{
    // Dangerous patterns for command injection detection
    private static readonly string[] DangerousPatterns = new[]
    {
        "&&",      // Command chaining
        "||",      // Command chaining
        "|",       // Pipe chaining
        ";",       // Command separator
        "`",       // PowerShell execution
        "$(",      // Command substitution
        "..",      // Directory traversal
        "../",     // Directory traversal
        "..\\",    // Directory traversal
        "%00",     // Null byte injection
        "${",      // Shell variable expansion
        "<(",      // Process substitution
    };

    // PowerShell specific dangerous patterns
    private static readonly Regex[] PowerShellDangerousPatterns = new[]
    {
        new Regex(@"-[eE][nN][cC]?\s+", RegexOptions.Compiled),
        new Regex(@"-[eE][nN][cC]?\s+[a-zA-Z0-9+/]{50,}={0,2}", RegexOptions.Compiled),
        new Regex(@"[iI][eE][xX]\s|[iI][eE][xX]\)|[iI][eE][xX]$", RegexOptions.Compiled),
        new Regex(@"[iI]nvoke-[eE]xpression", RegexOptions.Compiled),
    };

    /// <summary>
    /// Runs a command asynchronously with comprehensive security validation.
    /// </summary>
    public static async Task<(int, string)> RunAsync(string file, string? arguments, bool createNoWindow = true, bool waitForExit = true, Dictionary<string, string?>? environment = null, CancellationToken token = default)
    {
        // Input validation to prevent command injection
        if (!IsValidFileName(file))
            throw ExceptionHelper.InvalidFileName(nameof(file));
        
        if (arguments != null && ContainsDangerousInput(arguments))
            throw ExceptionHelper.DangerousArguments(nameof(arguments));

        if (waitForExit && string.IsNullOrWhiteSpace(arguments) && RequiresArgumentsForNonInteractiveShell(file))
            throw ExceptionHelper.InteractiveShellRequiresArgs(nameof(arguments));

        // Additional PowerShell-specific validation
        if (IsPowerShellExecutable(file) && arguments != null)
        {
            if (ContainsPowerShellDangerousPatterns(arguments))
                throw ExceptionHelper.PowerShellDangerousArgs(nameof(arguments));
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Running... [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]]");

        Process cmd = new Process();
        try
        {
            var shouldRedirectOutput = createNoWindow && waitForExit;

            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.CreateNoWindow = createNoWindow;
            cmd.StartInfo.RedirectStandardOutput = shouldRedirectOutput;
            cmd.StartInfo.RedirectStandardError = shouldRedirectOutput;
            cmd.StartInfo.WindowStyle = createNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
            cmd.StartInfo.FileName = file;
            if (!string.IsNullOrWhiteSpace(arguments))
                cmd.StartInfo.Arguments = arguments;

            var workingDirectory = GetWorkingDirectory(file);
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                cmd.StartInfo.WorkingDirectory = workingDirectory;

            if (environment is not null)
            {
                foreach (var (key, value) in environment)
                {
                    if (!IsValidEnvironmentVariable(key))
                        throw new ArgumentException(string.Format(Resource.Exception_InvalidEnvVariable, key));

                    if (value == null)
                    {
                        // If value is null, remove the environment variable
                        cmd.StartInfo.Environment.Remove(key);
                    }
                    else if (!ContainsDangerousInput(value))
                    {
                        cmd.StartInfo.Environment[key] = value;
                    }
                    else
                    {
                        throw new ArgumentException(string.Format(Resource.Exception_InvalidEnvVariableValue, key));
                    }
                }
            }

            cmd.Start();

            if (!waitForExit)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]]");

                // When waitForExit is false, the process runs asynchronously.
                // We must not dispose the Process object while the process is still running.
                cmd = null!; // Release reference, process continues running
                return (-1, string.Empty);
            }

            Task<string>? standardOutputTask = null;
            Task<string>? standardErrorTask = null;
            if (shouldRedirectOutput)
            {
                standardOutputTask = cmd.StandardOutput.ReadToEndAsync(token);
                standardErrorTask = cmd.StandardError.ReadToEndAsync(token);
            }

            try
            {
                await cmd.WaitForExitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { if (!cmd.HasExited) cmd.Kill(true); }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to kill process after cancellation", ex);
                }
                throw;
            }

            var exitCode = cmd.ExitCode;
            var output = string.Empty;
            if (shouldRedirectOutput)
            {
                await Task.WhenAll(standardOutputTask!, standardErrorTask!).ConfigureAwait(false);

                output = await standardOutputTask!.ConfigureAwait(false);
                var error = await standardErrorTask!.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(error))
                    output = string.IsNullOrWhiteSpace(output) ? error : $"{output}{Environment.NewLine}{error}";
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, exitCode={exitCode} output={output}]");

            return (exitCode, output);
        }
        finally
        {
            if (waitForExit && cmd is not null)
            {
                cmd.Dispose();
            }
        }
    }

    /// <summary>
    /// Validates a filename for security issues.
    /// </summary>
    private static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // Check for dangerous characters in the original input BEFORE normalization
        if (fileName.Contains("..") || fileName.Contains('|'))
            return false;

        try
        {
            // Check if it's a valid file path
            var path = Path.GetFullPath(fileName);
            
            // After normalization, check again for directory traversal
            if (path.Contains("..") || path.Contains('|'))
                return false;
            
            var fileInfo = new FileInfo(path);
            
            // Validate the filename portion using Windows invalid character list
            var fileNameOnly = fileInfo.Name;
            if (string.IsNullOrWhiteSpace(fileNameOnly))
                return false;
            
            var invalidChars = Path.GetInvalidFileNameChars();
            if (fileNameOnly.IndexOfAny(invalidChars) >= 0)
                return false;
            
            return true;
        }
        catch (Exception ex)
        {
            Log.Instance.TraceOnce(
                "cmd-validate-filename",
                $"CMD file path validation failed for '{fileName}'.",
                ex);
            return false;
        }
    }

    /// <summary>
    /// Validates environment variable name format.
    /// </summary>
    private static bool IsValidEnvironmentVariable(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
            return false;

        // Environment variable names should only contain alphanumeric characters and underscores
        foreach (char c in variableName)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if the executable requires arguments for safe non-interactive execution.
    /// </summary>
    private static bool RequiresArgumentsForNonInteractiveShell(string fileName)
    {
        var executableName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        return executableName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("powershell", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("pwsh", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the file is a PowerShell executable.
    /// </summary>
    private static bool IsPowerShellExecutable(string fileName)
    {
        var executableName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        return executableName.Equals("powershell", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("pwsh", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a safe working directory for shell executables when the current directory is a UNC path.
    /// </summary>
    private static string? GetWorkingDirectory(string fileName)
    {
        var currentDirectory = Environment.CurrentDirectory;
        if (string.IsNullOrWhiteSpace(currentDirectory))
            return null;

        if (!currentDirectory.StartsWith(@"\\", StringComparison.Ordinal))
            return currentDirectory;

        var executableName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(executableName))
            return currentDirectory;

        if (executableName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("powershell", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase)
            || executableName.Equals("pwsh", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.SystemDirectory;
        }

        return currentDirectory;
    }

    /// <summary>
    /// Checks for PowerShell-specific dangerous patterns.
    /// </summary>
    private static bool ContainsPowerShellDangerousPatterns(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        foreach (var regex in PowerShellDangerousPatterns)
        {
            if (regex.IsMatch(input))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if input contains dangerous patterns that could be used for command injection.
    /// </summary>
    public static bool ContainsDangerousInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        // Check for dangerous patterns
        foreach (string pattern in DangerousPatterns)
        {
            if (input.Contains(pattern, StringComparison.Ordinal))
                return true;
        }

        // Check for single ampersand command separator (but allow redirection patterns)
        if (ContainsUnescapedAmpersand(input))
            return true;

        return false;
    }

    /// <summary>
    /// Checks for command separator ampersands vs redirection patterns.
    /// </summary>
    private static bool ContainsUnescapedAmpersand(string input)
    {
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '&')
            {
                // Skip escaped ampersands (^&)
                if (i > 0 && input[i - 1] == '^')
                    continue;

                // Check if this is part of a redirection pattern
                bool isRedirection = false;

                // Check for >&N pattern
                if (i > 0 && input[i - 1] == '>')
                {
                    if (i + 1 < input.Length && (input[i + 1] == '1' || input[i + 1] == '2'))
                    {
                        if (i >= 2 && input[i - 2] is '1' or '2')
                        {
                            isRedirection = true;
                        }
                        else if (i < 2 || char.IsWhiteSpace(input[i - 2]))
                        {
                            isRedirection = true;
                        }
                    }
                }

                if (!isRedirection)
                    return true;
            }
        }

        return false;
    }
}
