Imports System.Net.Http
Imports System.Net.Sockets
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.IO

Public Class HyperionClient
    Private ReadOnly _httpClient As HttpClient
    Public Property Hostname As String
    Public Property Port As Integer
    Public Property Username As String
    Public Property Password As String

    Public Sub New(hostname As String, port As Integer, Optional username As String = Nothing, Optional password As String = Nothing)
        Me.Hostname = hostname
        Me.Port = port
        Me.Username = username
        Me.Password = password

        Dim handler As New HttpClientHandler()
        ' If auth is needed, setup credentials
        If Not String.IsNullOrEmpty(username) Then
            handler.Credentials = New System.Net.NetworkCredential(username, password)
        End If

        _httpClient = New HttpClient(handler)
        _httpClient.Timeout = TimeSpan.FromSeconds(5)
    End Sub

    Private ReadOnly Property BaseUrl As String
        Get
            Return $"http://{Hostname}:{Port}"
        End Get
    End Property

    Public Async Function TestConnectionAsync() As Task(Of Boolean)
        Try
            Dim response = Await _httpClient.GetAsync($"{BaseUrl}/cgi-bin/api/v1/version")
            Return response.IsSuccessStatusCode
        Catch
            Return False
        End Try
    End Function

    Public Async Function GetRatesAsync() As Task(Of RatesResponse)
        Try
            Dim responseString = Await _httpClient.GetStringAsync($"{BaseUrl}/cgi-bin/api/v1/rates")
            
            Dim mipsMatch = Regex.Match(responseString, """mipsrate""\s*:\s*(-?[0-9]+)")
            Dim siosMatch = Regex.Match(responseString, """siosrate""\s*:\s*(-?[0-9]+)")
            
            Dim res As New RatesResponse()
            If mipsMatch.Success Then
                res.MipsRate = Integer.Parse(mipsMatch.Groups(1).Value)
            End If
            If siosMatch.Success Then
                res.SiosRate = Integer.Parse(siosMatch.Groups(1).Value)
            End If
            Return res
        Catch ex As Exception
            Return New RatesResponse With {.MipsRate = 0, .SiosRate = 0, .ErrorMsg = ex.Message}
        End Try
    End Function

    Public Async Function GetSyslogAsync(Optional msgcount As Integer = 25, Optional command As String = "") As Task(Of SyslogResponse)
        Try
            Dim url = $"{BaseUrl}/cgi-bin/api/v1/syslog?msgcount={msgcount}"
            If Not String.IsNullOrEmpty(command) Then
                url &= $"&command={Uri.EscapeDataString(command)}"
            End If
            Dim responseString = Await _httpClient.GetStringAsync(url)
            
            Dim res As New SyslogResponse()
            res.MsgCount = msgcount
            
            ' Extract syslog array content
            Dim startToken = """syslog"":\s*\[\s*"""
            Dim startIdx = responseString.IndexOf("""syslog"":")
            If startIdx >= 0 Then
                Dim openBracketIdx = responseString.IndexOf("[", startIdx)
                If openBracketIdx >= 0 Then
                    Dim closeBracketIdx = responseString.LastIndexOf("]")
                    If closeBracketIdx > openBracketIdx Then
                        Dim arrayContent = responseString.Substring(openBracketIdx + 1, closeBracketIdx - openBracketIdx - 1).Trim()
                        ' Strip leading " and trailing "
                        If arrayContent.StartsWith(""""c) AndAlso arrayContent.EndsWith(""""c) Then
                            arrayContent = arrayContent.Substring(1, arrayContent.Length - 2)
                        End If
                        
                        ' Hercules joins lines using ","
                        Dim lines = arrayContent.Split(New String() {""","""}, StringSplitOptions.None)
                        Dim cleanedLines As New List(Of String)()
                        For Each line In lines
                            ' Unescape JSON backslashes and double quotes
                            Dim cleaned = line.Replace("\\", "\").Replace("\""", """")
                            If Not String.IsNullOrEmpty(cleaned) AndAlso cleaned.StartsWith("/") Then
                                cleaned = cleaned.Substring(1)
                            End If
                            cleanedLines.Add(cleaned)
                        Next
                        res.Syslog = cleanedLines.ToArray()
                    End If
                End If
            End If
            
            If res.Syslog Is Nothing Then
                res.Syslog = Array.Empty(Of String)()
            End If
            Return res
        Catch ex As Exception
            Return New SyslogResponse With {.Syslog = {$"Error fetching logs: {ex.Message}"}, .MsgCount = 0}
        End Try
    End Function

    Public Async Function GetDevicesAsync() As Task(Of DevicesResponse)
        Try
            Dim responseString = Await _httpClient.GetStringAsync($"{BaseUrl}/cgi-bin/api/v1/devices")
            
            ' Pattern to match each device object in the array
            Dim pattern = "\{""devnum"":""(?<devnum>[^""]*)"",""subchannel"":""(?<subchannel>[^""]*)"",""devclass"":\s*""(?<devclass>[^""]*)"",""devtype"":\s*""(?<devtype>[^""]*)"",""status"":\s*""(?<status>[^""]*)"",""assignment"":\s*""(?<assignment>[^""]*)""\}"
            Dim matches = Regex.Matches(responseString, pattern)
            
            Dim devicesList As New List(Of DeviceInfo)()
            For Each m As Match In matches
                Dim d As New DeviceInfo With {
                    .DevNum = m.Groups("devnum").Value,
                    .Subchannel = m.Groups("subchannel").Value,
                    .DevClass = m.Groups("devclass").Value,
                    .DevType = m.Groups("devtype").Value,
                    .Status = m.Groups("status").Value,
                    .Assignment = m.Groups("assignment").Value
                }
                devicesList.Add(d)
            Next
            
            Return New DevicesResponse With {.Devices = devicesList.ToArray()}
        Catch ex As Exception
            Return New DevicesResponse With {.Devices = Array.Empty(Of DeviceInfo)()}
        End Try
    End Function

    Public Async Function GetCpusAsync() As Task(Of CpusResponse)
        Try
            Dim responseString = Await _httpClient.GetStringAsync($"{BaseUrl}/cgi-bin/api/v1/cpus")
            
            ' Pattern to match each CPU object in the array
            Dim pattern = "\{""cpuid"":""(?<cpuid>[^""]*)"",""online"":\s*(?<online>true|false),""mode"":\s*""(?<mode>[^""]*)"",""percent"":\s*(?<percent>[0-9]+),""PSW"":\s*""(?<psw>[^""]*)"""
            Dim matches = Regex.Matches(responseString, pattern)
            
            Dim cpusList As New List(Of CpuInfo)()
            For Each m As Match In matches
                Dim c As New CpuInfo With {
                    .CpuId = m.Groups("cpuid").Value,
                    .Online = Boolean.Parse(m.Groups("online").Value),
                    .Mode = m.Groups("mode").Value,
                    .Percent = Integer.Parse(m.Groups("percent").Value),
                    .PSW = m.Groups("psw").Value
                }
                cpusList.Add(c)
            Next
            
            Return New CpusResponse With {.Cpus = cpusList.ToArray()}
        Catch ex As Exception
            Return New CpusResponse With {.Cpus = Array.Empty(Of CpuInfo)()}
        End Try
    End Function

    Public Async Function SendCommandAsync(command As String) As Task(Of String)
        Try
            Dim url = $"{BaseUrl}/cgi-bin/tasks/cmd?cmd={Uri.EscapeDataString(command)}"
            Dim responseString = Await _httpClient.GetStringAsync(url)
            Dim cleanText = responseString
            If cleanText.Contains("<PRE>") Then
                Dim startIdx = cleanText.IndexOf("<PRE>") + 5
                Dim endIdx = cleanText.IndexOf("</PRE>")
                If endIdx > startIdx Then
                    cleanText = cleanText.Substring(startIdx, endIdx - startIdx)
                End If
            End If
            cleanText = cleanText.Replace("<br>", vbCrLf).Replace("<BR>", vbCrLf)
            Return cleanText.Trim()
        Catch ex As Exception
            Return $"Error executing command: {ex.Message}"
        End Try
    End Function

    ''' <summary>
    ''' Issues "devlist devNum" and parses the sockdev host:port from the response.
    ''' Example response line:
    '''   HHC02279I 0:000C 3505 192.168.1.100:3505 sockdev ascii trunc eof IO[4]
    ''' Returns (host, port) or (Nothing, 0) if not a sockdev / parse failed.
    ''' </summary>
    Public Async Function GetSockDevEndpointAsync(devNum As String) As Task(Of (Host As String, Port As Integer))
        Try
            Dim raw = Await SendCommandAsync($"devlist {devNum}")
            ' Look for   ip:port   followed by   sockdev
            Dim m = Regex.Match(raw, "(\d{1,3}(?:\.\d{1,3}){3}):(\d+)\s+sockdev",
                                RegexOptions.IgnoreCase)
            If m.Success Then
                Dim host = m.Groups(1).Value
                Dim port = Integer.Parse(m.Groups(2).Value)
                Return (host, port)
            End If
        Catch
        End Try
        Return (Nothing, 0)
    End Function

    ''' <summary>
    ''' Streams the raw bytes of <paramref name="filePath"/> to a Hercules
    ''' socket-based card reader listening on <paramref name="host"/>:<paramref name="port"/>.
    ''' </summary>
    Public Async Function SubmitToSockDevAsync(host As String, port As Integer, filePath As String) As Task(Of String)
        Try
            Dim fileBytes = File.ReadAllBytes(filePath)
            Console.WriteLine($"Submitting {Path.GetFileName(filePath)} to device {host} on port {port}")
            Using client As New TcpClient()
                client.SendTimeout = 15000
                Await client.ConnectAsync(host, port)
                Using stream = client.GetStream()
                    Await stream.WriteAsync(fileBytes, 0, fileBytes.Length)
                    Await stream.FlushAsync()
                End Using
            End Using
            Return $"OK — sent {fileBytes.Length:N0} bytes ({Path.GetFileName(filePath)}) → {host}:{port}"
        Catch ex As Exception
            Return $"SockDev error: {ex.Message}"
        End Try
    End Function
End Class

Public Class RatesResponse
    Public Property MipsRate As Integer?
    Public Property SiosRate As Integer?
    Public Property ErrorMsg As String
End Class

Public Class SyslogResponse
    Public Property Command As String
    Public Property MsgCount As Integer
    Public Property Syslog As String()
End Class

Public Class DeviceInfo
    Public Property DevNum As String
    Public Property Subchannel As String
    Public Property DevClass As String
    Public Property DevType As String
    Public Property Status As String
    Public Property Assignment As String

    ''' <summary>
    ''' True when the device appears to be a socket reader.
    ''' The Hercules devices API may show "sockdev" or an IP:port pattern
    ''' (e.g. "0.0.0.0:3505", "*:3505") in the assignment or status fields.
    ''' </summary>
    Public ReadOnly Property IsSockDev As Boolean
        Get
            Dim a = If(Assignment, "").Trim().ToLower()
            Dim s = If(Status, "").Trim().ToLower()
            ' Check for the literal keyword
            If a.Contains("sockdev") OrElse s.Contains("sockdev") Then Return True
            ' Check for an IP:port or *:port pattern typical of socket listeners
            Dim sockPattern As New System.Text.RegularExpressions.Regex(
                "(\d{1,3}(\.\d{1,3}){3}|\*):\d+")
            If sockPattern.IsMatch(a) OrElse sockPattern.IsMatch(s) Then Return True
            Return False
        End Get
    End Property

    ''' <summary>True when the device has a regular file currently attached.</summary>
    Public ReadOnly Property HasFile As Boolean
        Get
            Return Not String.IsNullOrEmpty(Assignment) AndAlso Not IsSockDev
        End Get
    End Property
End Class

Public Class DevicesResponse
    Public Property Devices As DeviceInfo()
End Class

Public Class CpuInfo
    Public Property CpuId As String
    Public Property Online As Boolean
    Public Property Mode As String
    Public Property Percent As Integer
    Public Property PSW As String
End Class

Public Class CpusResponse
    Public Property Cpus As CpuInfo()
End Class
