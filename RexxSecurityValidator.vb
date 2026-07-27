Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>
''' Provides security validation and output sanitization for REXX script execution across platforms.
''' </summary>
Public Class RexxSecurityValidator

    Private Shared Function GetDefaultWorkingDirectory() As String
        Dim envDir = Environment.GetEnvironmentVariable("HYPERION_SCRIPT_DIR")
        If Not String.IsNullOrWhiteSpace(envDir) Then
            Return System.IO.Path.GetFullPath(envDir.Trim())
        End If
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScriptData")
    End Function

    ''' <summary>
    ''' Designated directory where REXX scripts are permitted to execute and perform file operations.
    ''' Configurable via HYPERION_SCRIPT_DIR environment variable or --script-dir (-d) CLI flag.
    ''' </summary>
    Public Shared Property AllowedWorkingDirectory As String = GetDefaultWorkingDirectory()

    ''' <summary>
    ''' Dangerous REXX keywords or external system call routines that could compromise the host operating system.
    ''' </summary>
    Private Shared ReadOnly BlockedKeywords As String() = {
        "ADDRESS CMD",             ' Prevents Windows CMD execution
        "ADDRESS SYSTEM",          ' Prevents OS system command execution
        "ADDRESS BASH",            ' Prevents Unix Bash execution
        "ADDRESS SH",              ' Prevents Unix Shell execution
        "ADDRESS ENVIRONMENT",     ' Prevents modifying host environment vars
        "CALL RXSUBCOM",           ' Prevents external subcommand registration
        "VALUE('SYSTEM"            ' Prevents reading/writing system environment
    }

    ''' <summary>
    ''' Allowed Hercules command prefixes for output response sanitization.
    ''' </summary>
    Private Shared ReadOnly AllowedHerculesCommands As String() = {
        "DEVINIT", "MOUNT", "UNMOUNT", "REPLY", "IPL", "START", "STOP",
        "LOGOPT", "MESSAGE", "MSGLOG", "HERCULES", "ATTACH", "DETACH"
    }

    ''' <summary>
    ''' Validates a REXX script file (.rexx, .rex) before execution.
    ''' </summary>
    ''' <param name="scriptPath">Path to the REXX script file.</param>
    ''' <returns>Tuple indicating whether the script is safe and a reason if blocked.</returns>
    Public Shared Function ValidateScript(scriptPath As String) As (IsValid As Boolean, Reason As String)
        If String.IsNullOrWhiteSpace(scriptPath) Then
            Return (False, "Script path is null or empty.")
        End If

        Dim fullPath = Path.GetFullPath(scriptPath)
        If Not File.Exists(fullPath) Then
            Return (False, $"Script file not found: '{fullPath}'")
        End If

        Dim scriptContent As String
        Try
            scriptContent = File.ReadAllText(fullPath)
        Catch ex As Exception
            Return (False, $"Unable to read script file: {ex.Message}")
        End Try

        ' 1. Check for blocked keywords
        For Each keyword In BlockedKeywords
            If scriptContent.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 Then
                Return (False, $"Security Block: REXX script contains prohibited instruction '{keyword}'.")
            End If
        Next

        Return (True, "Valid")
    End Function

    ''' <summary>
    ''' Verifies whether targetPath resides strictly inside allowedDir.
    ''' </summary>
    Public Shared Function IsPathWithinAllowedDirectory(targetPath As String, allowedDir As String) As Boolean
        Try
            Dim fullTarget = Path.GetFullPath(targetPath)
            Dim fullAllowed = Path.GetFullPath(allowedDir).TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar
            Return fullTarget.StartsWith(fullAllowed, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Sanitizes output strings returned by a REXX script before sending them to Hercules.
    ''' Allows Hercules internal commands, device numbers, and SCP guest OS commands (prefixed by ., /, ", etc.).
    ''' </summary>
    ''' <param name="outputLines">Raw output lines from REXX SAY statements.</param>
    ''' <param name="logHandler">Optional callback action to receive security warning log messages.</param>
    ''' <returns>Sanitized list of safe Hercules command strings.</returns>
    Public Shared Function SanitizeCommands(outputLines As IEnumerable(Of String), Optional logHandler As Action(Of String) = Nothing) As List(Of String)
        Dim resultCommands As New List(Of String)()
        If outputLines Is Nothing Then Return resultCommands

        For Each line In outputLines
            If String.IsNullOrWhiteSpace(line) Then Continue For

            Dim trimmed = line.Trim()

            ' Disallow host shell injection characters (;&|><`$) for security safety
            If Regex.IsMatch(trimmed, "[;&|><`$]") Then
                Dim warnMsg = $"[Security Warning] Suppressed command containing illegal shell characters: '{trimmed}'"
                If logHandler IsNot Nothing Then
                    logHandler(warnMsg)
                End If
                Continue For
            End If

            ' Return all SAY output lines directly to the calling code unmolested
            resultCommands.Add(trimmed)
        Next

        Return resultCommands
    End Function

End Class
