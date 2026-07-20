Imports System
Imports Terminal.Gui
Imports System.Threading.Tasks
Imports System.Collections.Generic
Imports System.Linq

Module Program
    Private _client As HyperionClient
    Private _top As Toplevel
    Private _mainWindow As Window
    Private _isConnected As Boolean = False
    
    ' UI Elements for Dashboard
    Private _lblMips As Label
    Private _lblSios As Label
    Private _lblPsw As Label
    Private _lblCpuInfo As Label
    
    Private _lstDevices As ListView
    Private _txtLogs As LogTextView
    Private _txtCommand As TextField
    
    Private _deviceList As New List(Of String)()
    Private _expandedGroups As New HashSet(Of String)()
    Private _lastDevices As DevicesResponse = Nothing
    Private _isRefreshing As Boolean = False
    Private _timerToken As Object = Nothing
    ' Maps devNum (upper-case) -> DeviceInfo; rebuilt on every device refresh
    Private _devInfoByNum As New Dictionary(Of String, DeviceInfo)()

    Sub Main()
        ' 1. Initialize the driver
        Application.Init()

        ' 2. Populate your variable AFTER Init()
        _top = Application.Top

        ' 3. Perform your UI construction
        ShowConnectionDialog()

        If _isConnected Then
            ShowDashboard(_client.Hostname, _client.Port)

            ' 4. Run using the existing _top variable
            Application.Run(_top)
        End If

        Application.Shutdown()
    End Sub

    Private Function GetConfigFilePath() As String
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim configDir = System.IO.Path.Combine(appData, "HyperionTUI")
        Try
            If Not System.IO.Directory.Exists(configDir) Then
                System.IO.Directory.CreateDirectory(configDir)
            End If
        Catch
        End Try
        Return System.IO.Path.Combine(configDir, "connection.txt")
    End Function

    Private Sub SaveLastConnection(host As String, port As Integer)
        Try
            Dim path = GetConfigFilePath()
            System.IO.File.WriteAllText(path, $"{host}:{port}")
        Catch
        End Try
    End Sub

    Private Sub LoadLastConnection(ByRef host As String, ByRef port As String)
        Try
            Dim path = GetConfigFilePath()
            If System.IO.File.Exists(path) Then
                Dim content = System.IO.File.ReadAllText(path).Trim()
                Dim parts = content.Split(":"c)
                If parts.Length = 2 Then
                    host = parts(0)
                    port = parts(1)
                End If
            End If
        Catch
        End Try
    End Sub

    Private Sub ShowConnectionDialog()
        Dim dialog As New Dialog("Connect to Hercules Hyperion", 60, 13)

        Dim defaultHost = "localhost"
        Dim defaultPort = "8038"
        LoadLastConnection(defaultHost, defaultPort)

        Dim lblHost As New Label("Hostname:") With {
            .X = 3,
            .Y = 2
        }
        Dim txtHost As New TextField(defaultHost) With {
            .X = 15,
            .Y = 2,
            .Width = 40
        }

        Dim lblPort As New Label("Port:") With {
            .X = 3,
            .Y = 4
        }
        Dim txtPort As New TextField(defaultPort) With {
            .X = 15,
            .Y = 4,
            .Width = 40
        }

        Dim lblStatus As New Label("") With {
            .X = 3,
            .Y = 6,
            .Width = 54,
            .ColorScheme = Colors.Error
        }

        Dim btnConnect As New Button("Connect") With {
            .X = 15,
            .Y = 8,
            .IsDefault = True
        }

        Dim btnExit As New Button("Exit") With {
            .X = 32,
            .Y = 8
        }

        AddHandler btnExit.Clicked, Sub()
                                        dialog.Running = False
                                    End Sub

        AddHandler btnConnect.Clicked, Async Sub()
                                           Dim host = txtHost.Text.ToString().Trim()
                                           Dim portStr = txtPort.Text.ToString().Trim()
                                           Dim port As Integer

                                           If String.IsNullOrEmpty(host) OrElse Not Integer.TryParse(portStr, port) Then
                                               lblStatus.Text = "Please enter a valid Hostname and Port."
                                               Return
                                           End If

                                           lblStatus.Text = "Connecting..."
                                           lblStatus.ColorScheme = Colors.Menu

                                           _client = New HyperionClient(host, port)
                                           Dim connected = Await _client.TestConnectionAsync()

                                           If connected Then
                                               _isConnected = True
                                               SaveLastConnection(host, port)
                                               dialog.Running = False
                                           Else
                                               lblStatus.Text = "Failed to connect to Hercules Hyperion server."
                                               lblStatus.ColorScheme = Colors.Error
                                           End If
                                       End Sub

        dialog.Add(lblHost, txtHost, lblPort, txtPort, lblStatus, btnConnect, btnExit)
        Application.Run(dialog)
    End Sub

    Private Sub ShowDashboard(host As String, port As Integer)
        _mainWindow = New Window($"Hercules Hyperion Dashboard - {host}:{port}") With {
            .X = 0,
            .Y = 0,
            .Width = Terminal.Gui.Dim.Fill(),
            .Height = Terminal.Gui.Dim.Fill()
        }

        ' --- TOP ROW: System Status ---
        Dim frameSysInfo As New FrameView("System Status") With {
            .X = 0,
            .Y = 0,
            .Width = Terminal.Gui.Dim.Fill(),
            .Height = 6
        }

        Dim lblMipsTitle As New Label("MIPS Rate:") With {.X = 2, .Y = 0}
        _lblMips = New Label("0") With {.X = 14, .Y = 0, .Width = 10}

        Dim lblSiosTitle As New Label("I/O Rate :") With {.X = 2, .Y = 1}
        _lblSios = New Label("0") With {.X = 14, .Y = 1, .Width = 10}

        Dim lblPswTitle As New Label("Active PSW:") With {.X = 30, .Y = 0}
        _lblPsw = New Label("--------------------------------") With {.X = 42, .Y = 0, .Width = 36}

        Dim lblCpuTitle As New Label("CPU Mode  :") With {.X = 30, .Y = 1}
        _lblCpuInfo = New Label("N/A") With {.X = 42, .Y = 1, .Width = 36}

        frameSysInfo.Add(lblMipsTitle, _lblMips, lblSiosTitle, _lblSios, lblPswTitle, _lblPsw, lblCpuTitle, _lblCpuInfo)

        ' --- LEFT SIDE: Device List ---
        Dim frameDevices As New FrameView("Devices") With {
            .X = 0,
            .Y = 6,
            .Width = Terminal.Gui.Dim.Percent(25),
            .Height = Terminal.Gui.Dim.Fill() - 3
        }

        _lstDevices = New ListView(_deviceList) With {
            .X = 0,
            .Y = 0,
            .Width = Terminal.Gui.Dim.Fill(),
            .Height = Terminal.Gui.Dim.Fill()
        }
        AddHandler _lstDevices.OpenSelectedItem, Async Sub(args)
                                                     Dim selectedText = args.Value.ToString()
                                                     Dim trimmed = selectedText.TrimStart()
                                                     If selectedText.StartsWith("▼ ") OrElse selectedText.StartsWith("► ") Then
                                                         ' Expand/collapse group header
                                                         Dim groupName = selectedText.Substring(2).Trim()
                                                         If _expandedGroups.Contains(groupName) Then
                                                             _expandedGroups.Remove(groupName)
                                                         Else
                                                             _expandedGroups.Add(groupName)
                                                         End If
                                                         RefreshDeviceListView()
                                                     ElseIf trimmed.StartsWith("[") OrElse
                                                            trimmed.StartsWith("○") OrElse
                                                            trimmed.StartsWith("●") OrElse
                                                            trimmed.StartsWith("◉") Then
                                                         ' A child device row (possibly with ○/●/◉ indicator)
                                                         Dim devClass = GetDeviceClassForItem(_lstDevices.SelectedItem)
                                                         If devClass = "RDR" OrElse devClass = "READER" Then
                                                             Dim devNum = System.Text.RegularExpressions.Regex.Match(selectedText, "\[([^\]]+)\]").Groups(1).Value
                                                             If Not String.IsNullOrEmpty(devNum) Then
                                                                 Await ShowAttachReaderDialog(devNum)
                                                             End If
                                                         ElseIf devClass = "TAPE" OrElse devClass = "TAP" Then
                                                             Dim devNum = System.Text.RegularExpressions.Regex.Match(selectedText, "\[([^\]]+)\]").Groups(1).Value
                                                             If Not String.IsNullOrEmpty(devNum) Then
                                                                 Await ShowAttachTapeDialog(devNum)
                                                             End If
                                                         End If
                                                     End If
                                                 End Sub
        frameDevices.Add(_lstDevices)

        ' --- RIGHT/CENTER: Syslog Terminal ---
        Dim logColorScheme As New ColorScheme()
        logColorScheme.Normal = Application.Driver.MakeAttribute(Color.Gray, Color.Black)
        logColorScheme.Focus = Application.Driver.MakeAttribute(Color.White, Color.Black)
        logColorScheme.HotNormal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
        logColorScheme.HotFocus = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)

        Dim frameLogs As New FrameView("System Log") With {
            .X = Pos.Percent(25),
            .Y = 6,
            .Width = Terminal.Gui.Dim.Fill(),
            .Height = Terminal.Gui.Dim.Fill() - 3,
            .ColorScheme = logColorScheme
        }

        _txtLogs = New LogTextView() With {
            .X = 0,
            .Y = 0,
            .Width = Terminal.Gui.Dim.Fill(),
            .Height = Terminal.Gui.Dim.Fill(),
            .ReadOnly = True,
            .ColorScheme = logColorScheme
        }
        frameLogs.Add(_txtLogs)

        ' --- BOTTOM ROW: Interactive Command Input ---
        Dim frameInput As New FrameView("Command Line") With {
            .X = 0,
            .Y = Pos.AnchorEnd(3),
            .Width = Terminal.Gui.Dim.Fill(),
            .Height = 3
        }

        Dim lblCmdPrompt As New Label("Command > ") With {
            .X = 1,
            .Y = 0
        }

        _txtCommand = New TextField("") With {
            .X = 11,
            .Y = 0,
            .Width = Terminal.Gui.Dim.Fill() - 2
        }

        AddHandler _txtCommand.KeyUp, Async Sub(args)
                                          If args.KeyEvent.Key = Key.Enter Then
                                              Dim cmd = _txtCommand.Text.ToString().Trim()
                                              If cmd.ToUpper = "*EXIT" Then
                                                  ' 1. Clear text
                                                  _txtCommand.Text = ""

                                                  ' 2. Simply request the stop. DO NOT call Shutdown() here.
                                                  Application.RequestStop()
                                                  Return
                                              End If

                                              If Not String.IsNullOrEmpty(cmd) Then
                                                  _txtCommand.Text = ""
                                                  Await ExecuteConsoleCommand(cmd)
                                              End If
                                          End If
                                      End Sub

        frameInput.Add(lblCmdPrompt, _txtCommand)

        ' Assemble window
        _mainWindow.Add(frameSysInfo, frameDevices, frameLogs, frameInput)
        
        ' Hotkey to exit: Ctrl+Q
        AddHandler _mainWindow.KeyUp, Sub(args)
                                          If args.KeyEvent.Key = (Key.CtrlMask Or CType(81, Key)) Then
                                              _top.Running = False
                                          End If
                                      End Sub

        ' Start periodic updates
        Dim timeoutSpan = TimeSpan.FromSeconds(1)
        _timerToken = Application.MainLoop.AddTimeout(timeoutSpan, AddressOf OnTimerTick)

        ' Add this to your ShowDashboard method, or ideally right before _top.Add(_mainWindow)
        Dim menu As New MenuBar(New MenuBarItem() {
            New MenuBarItem("_Hercules", New MenuItem() {
                New MenuItem("_Connect", "", AddressOf ShowConnectionDialog),
                New MenuItem("_Quit", "Ctrl+Q", Sub() Application.RequestStop())
            }),
            New MenuBarItem("_Tapes", New MenuItem() {
                New MenuItem("_Attach", "", Sub() ShowTapePickerForAttach()),
                New MenuItem("_Detach", "", Sub() ShowDetachTapeDialog())
            }),
            New MenuBarItem("_Printers", New MenuItem() {
                New MenuItem("_Attach", "", Nothing),
                New MenuItem("_Detach", "", Nothing)
            }),
            New MenuBarItem("_Readers", New MenuItem() {
                New MenuItem("_Attach", "", Sub() ShowReaderPickerForAttach()),
                New MenuItem("_Detach", "", Sub() ShowDetachReaderDialog())
            }),
            New MenuBarItem("_Punches", New MenuItem() {
                New MenuItem("_Attach", "", Nothing),
                New MenuItem("_Detach", "", Nothing)
            })
        })

        _top.Add(menu)
        _top.Add(_mainWindow)
        _txtCommand.SetFocus()
        ' Initial manual update trigger
        Task.Run(Sub() UpdateDashboardData(True))
    End Sub

    Private Function OnTimerTick(loopObj As MainLoop) As Boolean
        If Not _isRefreshing Then
            Task.Run(Sub() UpdateDashboardData(False))
        End If
        Return True
    End Function

    Private Async Sub UpdateDashboardData(isFirstTime As Boolean)
        _isRefreshing = True
        Try
            ' 1. Rates
            Dim rates = Await _client.GetRatesAsync()
            Application.MainLoop.Invoke(Sub()
                                            _lblMips.Text = If(rates.MipsRate.HasValue, (rates.MipsRate.Value / 1000000.0).ToString("N2"), "0.00")
                                            _lblSios.Text = If(rates.SiosRate.HasValue, rates.SiosRate.Value.ToString(), "0")
                                        End Sub)

            ' 2. CPU / PSW Info
            Dim cpus = Await _client.GetCpusAsync()
            If cpus.Cpus IsNot Nothing AndAlso cpus.Cpus.Length > 0 Then
                Dim firstCpu = cpus.Cpus(0)
                Application.MainLoop.Invoke(Sub()
                                                _lblPsw.Text = firstCpu.PSW
                                                _lblCpuInfo.Text = $"{firstCpu.CpuId} - Mode: {firstCpu.Mode} ({firstCpu.Percent}%)"
                                            End Sub)
            End If

            ' 3. Device list
            Dim devs = Await _client.GetDevicesAsync()
            If devs.Devices IsNot Nothing Then
                _lastDevices = devs
                RefreshDeviceListView()
            End If

            ' 4. Syslog update (only auto-refresh logs if user is not typing/actively reading)
            Dim logs = Await _client.GetSyslogAsync(40)
            If logs.Syslog IsNot Nothing Then
                Dim logContent = String.Join(Environment.NewLine, logs.Syslog)
                Application.MainLoop.Invoke(Sub()
                                                _txtLogs.Text = logContent
                                                ' Always scroll to bottom to tail logs
                                                _txtLogs.CursorPosition = New Point(0, logs.Syslog.Length)
                                            End Sub)
            End If
        Catch
            ' Silent catch for background refresh errors
        Finally
            _isRefreshing = False
        End Try
    End Sub

    Private Async Function ExecuteConsoleCommand(cmd As String) As Task
        ' Append a temporary indicator
        Application.MainLoop.Invoke(Sub()
                                        _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Executing: {cmd}...]"
                                    End Sub)

        Dim response = Await _client.SendCommandAsync(cmd)

        ' Let's refresh data immediately to show response
        UpdateDashboardData(False)
    End Function

    Private Sub RefreshDeviceListView()
        If _lastDevices Is Nothing OrElse _lastDevices.Devices Is Nothing Then Return

        ' Rebuild the devNum -> DeviceInfo lookup
        _devInfoByNum.Clear()
        Dim groups As New Dictionary(Of String, List(Of DeviceInfo))()
        For Each d In _lastDevices.Devices
            _devInfoByNum(d.DevNum.Trim().ToUpper()) = d
            Dim devClass = If(String.IsNullOrEmpty(d.DevClass), "OTHER", d.DevClass.Trim().ToUpper())
            If Not groups.ContainsKey(devClass) Then
                groups(devClass) = New List(Of DeviceInfo)()
            End If
            groups(devClass).Add(d)
        Next

        Dim newList As New List(Of String)()
        For Each groupName In groups.Keys
            Dim isExpanded = _expandedGroups.Contains(groupName)
            If isExpanded Then
                newList.Add($"▼ {groupName}")
                For Each d In groups(groupName)
                    Dim statusText = d.Status.Trim()
                    If String.IsNullOrEmpty(statusText) Then statusText = "online"
                    If groupName = "RDR" OrElse groupName = "READER" Then
                        ' Show reader-specific status indicators
                        Dim indicator As String
                        If d.IsSockDev Then
                            indicator = "◉"  ' sockdev reader
                        ElseIf d.HasFile Then
                            indicator = "●"  ' file attached
                        Else
                            indicator = "○"  ' idle / empty
                        End If
                        newList.Add($"   {indicator} [{d.DevNum}] {d.DevType} - {statusText}")
                    ElseIf groupName = "TAPE" OrElse groupName = "TAP" Then
                        ' Show tape-specific status indicators
                        Dim indicator = If(d.HasFile, "●", "○")
                        newList.Add($"   {indicator} [{d.DevNum}] {d.DevType} - {statusText}")
                    Else
                        newList.Add($"   [{d.DevNum}] {d.DevType} - {statusText}")
                    End If
                Next
            Else
                newList.Add($"► {groupName}")
            End If
        Next

        Application.MainLoop.Invoke(Sub()
                                        Dim selectedIndex = _lstDevices.SelectedItem
                                        _deviceList.Clear()
                                        _deviceList.AddRange(newList)
                                        _lstDevices.SetSource(_deviceList)
                                        If selectedIndex >= 0 AndAlso selectedIndex < _deviceList.Count Then
                                            _lstDevices.SelectedItem = selectedIndex
                                        End If
                                    End Sub)
    End Sub

    ''' <summary>
    ''' Walks the visible device list backward from <paramref name="itemIndex"/> to find the
    ''' nearest group header (▼ or ►) and returns its class name (e.g. "RDR", "TAPE").
    ''' </summary>
    Private Function GetDeviceClassForItem(itemIndex As Integer) As String
        For i = itemIndex - 1 To 0 Step -1
            Dim s = _deviceList(i)
            If s.StartsWith("▼ ") OrElse s.StartsWith("► ") Then
                Return s.Substring(2).Trim().ToUpper()
            End If
        Next
        Return String.Empty
    End Function

    ''' <summary>
    ''' Opens a file-picker dialog and, if confirmed, attaches the file to the
    ''' specified card reader device.  For sockdev readers the file is streamed
    ''' over TCP; for regular readers a DEVINIT command is issued.
    ''' </summary>
    ''' <summary>
    ''' Opens a file-picker dialog and, if confirmed, attaches the file to the
    ''' specified card reader device.  For sockdev readers the file is streamed
    ''' over TCP; for regular readers a DEVINIT command is issued.
    ''' </summary>
    Private Async Function ShowAttachReaderDialog(devNum As String) As Task
        Dim tcs As New TaskCompletionSource(Of String)()

        Application.MainLoop.Invoke(Sub()
                                        Try
                                            Dim dlg As New OpenDialog(
                                                $"Attach File to Reader {devNum}",
                                                "Select a JCL, card deck or data file to load into the reader:") With {
                                                .CanChooseFiles = True,
                                                .CanChooseDirectories = False,
                                                .AllowsMultipleSelection = False
                                            }
                                            Application.Run(dlg)
                                            If Not dlg.Canceled AndAlso dlg.FilePaths IsNot Nothing AndAlso dlg.FilePaths.Count > 0 Then
                                                tcs.SetResult(dlg.FilePaths(0))
                                            Else
                                                tcs.SetResult(Nothing)
                                            End If
                                        Catch ex As Exception
                                            tcs.SetResult(Nothing)
                                        End Try
                                    End Sub)

        Dim filePath = Await tcs.Task
        If String.IsNullOrEmpty(filePath) Then Return

        ' Log checking
        Application.MainLoop.Invoke(Sub()
                                        _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Resolving reader type for {devNum}...]"
                                    End Sub)

        ' Probe the device with devlist to see if it is a sockdev reader.
        ' We can't rely on the devices API assignment field because it doesn't
        ' contain the "sockdev" keyword — that only appears in devlist output.
        Dim rawDevList = Await _client.SendCommandAsync($"devlist {devNum}")
        Dim ep = Await _client.GetSockDevEndpointAsync(devNum)

        If ep.Host IsNot Nothing Then
            ' --- Debug message right before sending the file as a system log style message ---
            Application.MainLoop.Invoke(Sub()
                                            _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"Submitting {System.IO.Path.GetFileName(filePath)} to device {ep.Host} on port {ep.Port}"
                                        End Sub)

            Dim result = Await _client.SubmitToSockDevAsync(ep.Host, ep.Port, filePath)
            Application.MainLoop.Invoke(Sub()
                                            _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[{result}]"
                                        End Sub)
        Else
            ' Log devlist output for debugging purposes
            Application.MainLoop.Invoke(Sub()
                                            _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Debug devlist response: {rawDevList}]"
                                        End Sub)

            ' --- Regular reader: DEVINIT ---
            Dim escapedPath = filePath.Replace(" ", "' '")
            Dim cmd = $"devinit {devNum} {escapedPath}"

            Application.MainLoop.Invoke(Sub()
                                            _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Attaching {filePath} to reader {devNum}...]"
                                        End Sub)

            Dim response = Await _client.SendCommandAsync(cmd)
        End If

        UpdateDashboardData(False)
    End Function

    ''' <summary>
    ''' Menu-driven attach: shows a picker with all available reader devices,
    ''' then opens the file dialog for the selected one.  Same flow as pressing
    ''' ENTER on a reader row in the device pane.
    ''' </summary>
    Private Sub ShowReaderPickerForAttach()
        ' Gather reader devices from the lookup
        Dim readers = _devInfoByNum.Values.Where(
            Function(d) d.DevClass IsNot Nothing AndAlso (d.DevClass.Trim().ToUpper() = "RDR" OrElse d.DevClass.Trim().ToUpper() = "READER")
        ).ToList()

        If readers.Count = 0 Then
            MessageBox.Query("Readers", "No card reader devices found.", "OK")
            Return
        End If

        Dim dialog As New Dialog("Select Reader to Attach", 50, readers.Count + 6)

        Dim items = readers.Select(Function(r)
                                       Dim indicator As String
                                       If r.IsSockDev Then
                                           indicator = "◉"
                                       ElseIf r.HasFile Then
                                           indicator = "●"
                                       Else
                                           indicator = "○"
                                       End If
                                       Return $"{indicator} [{r.DevNum}] {r.DevType}"
                                   End Function).ToList()

        Dim lstReaders As New ListView(items) With {
            .X = 1,
            .Y = 1,
            .Width = Terminal.Gui.Dim.Fill() - 1,
            .Height = Terminal.Gui.Dim.Fill() - 2
        }

        Dim selectedDevNum As String = Nothing
        AddHandler lstReaders.OpenSelectedItem, Sub(args)
                                                    selectedDevNum = readers(lstReaders.SelectedItem).DevNum
                                                    dialog.Running = False
                                                End Sub

        Dim btnCancel As New Button("Cancel")
        AddHandler btnCancel.Clicked, Sub()
                                          dialog.Running = False
                                      End Sub

        dialog.Add(lstReaders)
        dialog.AddButton(btnCancel)
        Application.Run(dialog)

        If Not String.IsNullOrEmpty(selectedDevNum) Then
            Task.Run(Async Function()
                         Await ShowAttachReaderDialog(selectedDevNum)
                     End Function)
        End If
    End Sub

    ''' <summary>
    ''' Menu-driven detach: lists readers that have something attached and
    ''' issues "devinit {devNum} *" to clear the selected one.
    ''' </summary>
    Private Sub ShowDetachReaderDialog()
        Dim readers = _devInfoByNum.Values.Where(
            Function(d) d.DevClass IsNot Nothing AndAlso
                        (d.DevClass.Trim().ToUpper() = "RDR" OrElse d.DevClass.Trim().ToUpper() = "READER") AndAlso
                        (d.HasFile OrElse d.IsSockDev)
        ).ToList()

        If readers.Count = 0 Then
            MessageBox.Query("Detach Reader", "No readers currently have files attached.", "OK")
            Return
        End If

        Dim dialog As New Dialog("Select Reader to Detach", 50, readers.Count + 6)

        Dim items = readers.Select(Function(r)
                                       Dim indicator = If(r.IsSockDev, "◉", "●")
                                       Return $"{indicator} [{r.DevNum}] {r.DevType}"
                                   End Function).ToList()

        Dim lstReaders As New ListView(items) With {
            .X = 1,
            .Y = 1,
            .Width = Terminal.Gui.Dim.Fill() - 1,
            .Height = Terminal.Gui.Dim.Fill() - 2
        }

        Dim selectedDevNum As String = Nothing
        AddHandler lstReaders.OpenSelectedItem, Sub(args)
                                                    selectedDevNum = readers(lstReaders.SelectedItem).DevNum
                                                    dialog.Running = False
                                                End Sub

        Dim btnCancel As New Button("Cancel")
        AddHandler btnCancel.Clicked, Sub()
                                          dialog.Running = False
                                      End Sub

        dialog.Add(lstReaders)
        dialog.AddButton(btnCancel)
        Application.Run(dialog)

        If Not String.IsNullOrEmpty(selectedDevNum) Then
            Application.MainLoop.Invoke(Sub()
                                            _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Detaching reader {selectedDevNum}...]"
                                        End Sub)
            Task.Run(Async Function()
                         Await _client.SendCommandAsync($"devinit {selectedDevNum} *")
                         UpdateDashboardData(False)
                     End Function)
        End If
    End Sub

    ''' <summary>
    ''' Opens a file-picker dialog to select an AWS or HET tape file, and if confirmed,
    ''' issues a mount command to hercules.
    ''' </summary>
    Private Async Function ShowAttachTapeDialog(devNum As String) As Task
        Dim tcs As New TaskCompletionSource(Of String)()

        Application.MainLoop.Invoke(Sub()
                                        Try
                                            Dim dlg As New OpenDialog(
                                                $"Mount Tape onto Drive {devNum}",
                                                "Select a tape file (.aws, .het) to mount:") With {
                                                .CanChooseFiles = True,
                                                .CanChooseDirectories = False,
                                                .AllowsMultipleSelection = False
                                            }
                                            Application.Run(dlg)
                                            If Not dlg.Canceled AndAlso dlg.FilePaths IsNot Nothing AndAlso dlg.FilePaths.Count > 0 Then
                                                tcs.SetResult(dlg.FilePaths(0))
                                            Else
                                                tcs.SetResult(Nothing)
                                            End If
                                        Catch ex As Exception
                                            tcs.SetResult(Nothing)
                                        End Try
                                    End Sub)

        Dim filePath = Await tcs.Task
        If String.IsNullOrEmpty(filePath) Then Return

        ' Escape spaces in path for Hercules
        Dim escapedPath = filePath.Replace(" ", "' '")
        Dim cmd = $"mount {escapedPath} ON {devNum}"

        Application.MainLoop.Invoke(Sub()
                                        _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Mounting {System.IO.Path.GetFileName(filePath)} onto tape drive {devNum}...]"
                                    End Sub)

        Dim response = Await _client.SendCommandAsync(cmd)
        UpdateDashboardData(False)
    End Function

    ''' <summary>
    ''' Menu-driven tape mount: shows picker with all available tape drives,
    ''' then opens the file dialog for the selected drive.
    ''' </summary>
    Private Sub ShowTapePickerForAttach()
        Dim drives = _devInfoByNum.Values.Where(
            Function(d) d.DevClass IsNot Nothing AndAlso (d.DevClass.Trim().ToUpper() = "TAPE" OrElse d.DevClass.Trim().ToUpper() = "TAP")
        ).ToList()

        If drives.Count = 0 Then
            MessageBox.Query("Tapes", "No tape drive devices found.", "OK")
            Return
        End If

        Dim dialog As New Dialog("Select Tape Drive to Mount", 50, drives.Count + 6)

        Dim items = drives.Select(Function(t)
                                      Dim indicator = If(t.HasFile, "●", "○")
                                      Return $"{indicator} [{t.DevNum}] {t.DevType}"
                                  End Function).ToList()

        Dim lstDrives As New ListView(items) With {
            .X = 1,
            .Y = 1,
            .Width = Terminal.Gui.Dim.Fill() - 1,
            .Height = Terminal.Gui.Dim.Fill() - 2
        }

        Dim selectedDevNum As String = Nothing
        AddHandler lstDrives.OpenSelectedItem, Sub(args)
                                                   selectedDevNum = drives(lstDrives.SelectedItem).DevNum
                                                   dialog.Running = False
                                               End Sub

        Dim btnCancel As New Button("Cancel")
        AddHandler btnCancel.Clicked, Sub()
                                          dialog.Running = False
                                      End Sub

        dialog.Add(lstDrives)
        dialog.AddButton(btnCancel)
        Application.Run(dialog)

        If Not String.IsNullOrEmpty(selectedDevNum) Then
            Task.Run(Async Function()
                         Await ShowAttachTapeDialog(selectedDevNum)
                     End Function)
        End If
    End Sub

    ''' <summary>
    ''' Menu-driven tape unmount/detach: lists tape drives with files attached and
    ''' issues "unmount {devNum}" to clear/unload the tape drive.
    ''' </summary>
    Private Sub ShowDetachTapeDialog()
        Dim drives = _devInfoByNum.Values.Where(
            Function(d) d.DevClass IsNot Nothing AndAlso
                        (d.DevClass.Trim().ToUpper() = "TAPE" OrElse d.DevClass.Trim().ToUpper() = "TAP") AndAlso
                        d.HasFile
        ).ToList()

        If drives.Count = 0 Then
            MessageBox.Query("Detach Tape", "No tape drives currently have tapes mounted.", "OK")
            Return
        End If

        Dim dialog As New Dialog("Select Tape to Detach/Unmount", 50, drives.Count + 6)

        Dim items = drives.Select(Function(t) $"{t.DevNum} — {System.IO.Path.GetFileName(t.Assignment)}"        ).ToList()

        Dim lstDrives As New ListView(items) With {
            .X = 1,
            .Y = 1,
            .Width = Terminal.Gui.Dim.Fill() - 1,
            .Height = Terminal.Gui.Dim.Fill() - 2
        }

        Dim selectedDevNum As String = Nothing
        AddHandler lstDrives.OpenSelectedItem, Sub(args)
                                                   selectedDevNum = drives(lstDrives.SelectedItem).DevNum
                                                   dialog.Running = False
                                               End Sub

        Dim btnCancel As New Button("Cancel")
        AddHandler btnCancel.Clicked, Sub()
                                          dialog.Running = False
                                      End Sub

        dialog.Add(lstDrives)
        dialog.AddButton(btnCancel)
        Application.Run(dialog)

        If Not String.IsNullOrEmpty(selectedDevNum) Then
            Application.MainLoop.Invoke(Sub()
                                            _txtLogs.Text = _txtLogs.Text.ToString() & vbCrLf & $"[Unmounting tape from drive {selectedDevNum}...]"
                                        End Sub)
            Task.Run(Async Function()
                         Await _client.SendCommandAsync($"unmount {selectedDevNum}")
                         UpdateDashboardData(False)
                     End Function)
        End If
    End Sub

End Module

Public Class LogTextView
    Inherits TextView

    Private ReadOnly _yellowAttr As Terminal.Gui.Attribute = Terminal.Gui.Attribute.Make(Color.BrightYellow, Color.Black)
    Private ReadOnly _greenAttr As Terminal.Gui.Attribute = Terminal.Gui.Attribute.Make(Color.BrightGreen, Color.Black)
    
    Private _lastLine As List(Of System.Rune) = Nothing
    Private _lastMatchIndex As Integer = -1
    Private _lastMatchLength As Integer = -1
    Private _hasLastMatch As Boolean = False

    Protected Overrides Sub SetNormalColor(line As List(Of System.Rune), idx As Integer)
        ApplyCustomColor(line, idx)
    End Sub

    Protected Overrides Sub SetReadOnlyColor(line As List(Of System.Rune), idx As Integer)
        ApplyCustomColor(line, idx)
    End Sub

    Private Sub ApplyCustomColor(line As List(Of System.Rune), idx As Integer)
        If line Is Nothing OrElse line.Count = 0 Then
            MyBase.SetNormalColor(line, idx)
            Return
        End If

        If Not Object.ReferenceEquals(line, _lastLine) Then
            _lastLine = line
            Dim lineText = New String(line.Select(Function(r) Convert.ToChar(r.Value)).ToArray())
            Dim m = System.Text.RegularExpressions.Regex.Match(lineText, "\b(HHC[A-Z0-9]{5}[A-Z])\b")
            If m.Success Then
                _hasLastMatch = True
                _lastMatchIndex = m.Index
                _lastMatchLength = m.Length
            Else
                _hasLastMatch = False
            End If
        End If

        If _hasLastMatch AndAlso idx >= _lastMatchIndex AndAlso idx < _lastMatchIndex + _lastMatchLength Then
            Application.Driver.SetAttribute(_yellowAttr)
        Else
            Application.Driver.SetAttribute(_greenAttr)
        End If
    End Sub
End Class
