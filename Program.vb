Imports System
Imports Terminal.Gui
Imports System.Threading.Tasks
Imports System.Collections.Generic

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
    Private _txtLogs As TextView
    Private _txtCommand As TextField
    
    Private _deviceList As New List(Of String)()
    Private _expandedGroups As New HashSet(Of String)()
    Private _lastDevices As DevicesResponse = Nothing
    Private _isRefreshing As Boolean = False
    Private _timerToken As Object = Nothing

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

    Private Sub ShowConnectionDialog()
        Dim dialog As New Dialog("Connect to Hercules Hyperion", 60, 13)

        Dim lblHost As New Label("Hostname:") With {
            .X = 3,
            .Y = 2
        }
        Dim txtHost As New TextField("localhost") With {
            .X = 15,
            .Y = 2,
            .Width = 40
        }

        Dim lblPort As New Label("Port:") With {
            .X = 3,
            .Y = 4
        }
        Dim txtPort As New TextField("8038") With {
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
        AddHandler _lstDevices.OpenSelectedItem, Sub(args)
                                                     Dim selectedText = args.Value.ToString()
                                                     If selectedText.StartsWith("▼ ") OrElse selectedText.StartsWith("► ") Then
                                                         Dim groupName = selectedText.Substring(2).Trim()
                                                         If _expandedGroups.Contains(groupName) Then
                                                             _expandedGroups.Remove(groupName)
                                                         Else
                                                             _expandedGroups.Add(groupName)
                                                         End If
                                                         RefreshDeviceListView()
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

        _txtLogs = New TextView() With {
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
                New MenuItem("_Attach", "", Nothing),
                New MenuItem("_Detach", "", Nothing)
            }),
            New MenuBarItem("_Printers", New MenuItem() {
                New MenuItem("_Attach", "", Nothing),
                New MenuItem("_Detach", "", Nothing)
            }),
            New MenuBarItem("_Readers", New MenuItem() {
                New MenuItem("_Attach", "", Nothing),
                New MenuItem("_Detach", "", Nothing)
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

        Dim groups As New Dictionary(Of String, List(Of DeviceInfo))()
        For Each d In _lastDevices.Devices
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
                    newList.Add($"   [{d.DevNum}] {d.DevType} - {statusText}")
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
End Module
