Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading.Tasks

''' <summary>
''' Provides functions to execute REXX script files across Windows, Linux, and macOS platforms.
''' </summary>
Public Module RexxRunner

    ' Cache the interpreter path once at startup to avoid redundant OS checks
    Private ReadOnly _cachedInterpreter As String = GetRexxInterpreterExecutable()

    ''' <summary>
    ''' Resolves the cross-platform REXX interpreter executable name (e.g. rexx, regina, or rexx.exe).
    ''' </summary>
    Private Function GetRexxInterpreterExecutable() As String
        If RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
            ' Check for Regina REXX or Object REXX executables on Windows
            Return "rexx.exe"
        Else
            ' On Linux / macOS (Regina REXX or ooRexx)
            Return "rexx"
        End If
    End Function

    ''' <summary>
    ''' Executes a REXX script file (.rex, .rexx) with parameters across platforms, returning sanitized output lines.
    ''' </summary>
    ''' <param name="scriptPath">Absolute or relative path to the REXX script file.</param>
    ''' <param name="parameters">Optional list of string arguments/parameters passed to the REXX script.</param>
    ''' <param name="timeoutSeconds">Maximum execution time before hard termination (default 5 seconds).</param>
    ''' <param name="logHandler">Optional callback action to pipe diagnostic/security messages into the system log instead of the console.</param>
    Public Async Function ExecuteScriptAsync(scriptPath As String, Optional parameters As IEnumerable(Of String) = Nothing, Optional timeoutSeconds As Integer = 5, Optional logHandler As Action(Of String) = Nothing) As Task(Of List(Of String))
        ' 1. Security Code Validation
        Dim validation = RexxSecurityValidator.ValidateScript(scriptPath)
        If Not validation.IsValid Then
            Dim blockMsg = $"[Security Guardrail] REXX script execution denied: {validation.Reason}"
            If logHandler IsNot Nothing Then
                logHandler(blockMsg)
            End If
            Return New List(Of String) From {$"[Security Blocked]: {validation.Reason}"}
        End If

        Dim fullPath = Path.GetFullPath(scriptPath)
        Dim interpreter = _cachedInterpreter

        ' 2. Build argument string with double quotes escaping for parameters
        Dim sbArgs As New StringBuilder()
        sbArgs.Append($"""{fullPath}""")

        If parameters IsNot Nothing Then
            For Each param In parameters
                If param IsNot Nothing Then
                    Dim escapedParam = param.Replace("""", """""")
                    sbArgs.Append($" ""{escapedParam}""")
                End If
            Next
        End If

        Dim psi As New ProcessStartInfo() With {
            .FileName = interpreter,
            .Arguments = sbArgs.ToString(),
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .StandardOutputEncoding = Encoding.UTF8
        }

        Dim outputLines As New List(Of String)()

        Try
            Using proc As Process = Process.Start(psi)
                If proc Is Nothing Then
                    Throw New InvalidOperationException($"Failed to start REXX interpreter '{interpreter}'.")
                End If

                ' 3. Read output text with hard async Cancellation/Timeout
                Using cts As New System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
                    Try
                        Dim stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token)
                        Dim stderrTask = proc.StandardError.ReadToEndAsync(cts.Token)

                        Await Task.WhenAll(stdoutTask, stderrTask)
                        Await proc.WaitForExitAsync(cts.Token)

                        Dim stdoutText = stdoutTask.Result
                        If Not String.IsNullOrEmpty(stdoutText) Then
                            Dim splitLines = stdoutText.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
                            For Each line In splitLines
                                outputLines.Add(line)
                            Next
                        End If

                        Dim errText = stderrTask.Result
                        If proc.ExitCode <> 0 AndAlso Not String.IsNullOrWhiteSpace(errText) Then
                            outputLines.Add($"[REXX Error ExitCode={proc.ExitCode}]: {errText.Trim()}")
                        End If
                    Catch ex As OperationCanceledException
                        Try
                            If Not proc.HasExited Then proc.Kill(True)
                        Catch
                        End Try
                        outputLines.Add($"[Security Timeout]: REXX execution exceeded maximum allowed time ({timeoutSeconds}s) and was terminated.")
                    End Try
                End Using
            End Using
        Catch ex As Exception
            outputLines.Add($"[REXX Interpreter Error]: {ex.Message}")
        End Try

        ' 4. Sanitize and filter returned output lines
        Return RexxSecurityValidator.SanitizeCommands(outputLines)
    End Function

    ''' <summary>
    ''' Executes a REXX script file and returns the combined output as a single string.
    ''' </summary>
    Public Async Function ExecuteScriptSingleStringAsync(scriptPath As String, Optional parameters As IEnumerable(Of String) = Nothing, Optional logHandler As Action(Of String) = Nothing) As Task(Of String)
        Dim lines = Await ExecuteScriptAsync(scriptPath, parameters, 5, logHandler)
        Return String.Join(Environment.NewLine, lines)
    End Function

End Module