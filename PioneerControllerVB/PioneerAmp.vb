Imports System.ComponentModel
Imports System.Net
Imports System.Runtime.CompilerServices
Imports System.Text
Imports NLog
Imports Telnet

Public Class PioneerAmp
    Implements INotifyPropertyChanged, IDisposable

#Region " Private Fields "

    Private Shared ReadOnly Logger As Logger = LogManager.GetCurrentClassLogger()

    Private ReadOnly mDevice As Device
    Private ReadOnly mSendLock As New Object

    Private mLastSendTime As DateTime = DateTime.MinValue
    Private mLastReceiveTime As DateTime = DateTime.MinValue

    Private mPowerState As PowerState
    Private mVolumedB As Single
    Private mMute As Boolean
    Private mInputSource As InputSource
    Private mListeningModeSet As ListeningMode
    Private mListeningMode As ListeningMode
    Private mBass As Integer
    Private mTreble As Integer
    Private ipAddress As IPAddress
    Private port As Integer?
    Private name As String

#End Region

#Region " Private Methods "

    Private Sub DeviceDataReceived(sender As Object, e As Device.ReceivedDataEventArgs)
        If e.Data.Length = 0 Then
            Logger.Debug("Device may be disconnected while reporting connected on tcp port")
            Return
        End If
        If e.Data.Length >= 2 AndAlso e.Data(e.Data.Length - 2) = &HD AndAlso e.Data(e.Data.Length - 1) = &HA Then

            Dim data(e.Data.Length - 3) As Byte
            Array.Copy(e.Data, data, data.Length)

            ProcessReceivedData(data)

        Else

            ProcessReceivedData(e.Data)

        End If
    End Sub

    Private Sub ProcessReceivedData(data As Byte())

        Dim stringDatas = Encoding.ASCII.GetString(data).Trim()
        If String.IsNullOrEmpty(stringDatas) Then Return

        Logger.Debug($"Received data from device {stringDatas}")

        Dim commandProcessed As Boolean = True
        For Each stringData As String In stringDatas.Split(vbCrLf).Select(Function(s) s.Trim)

            If stringData = "R" Then
                RaiseEvent ConfirmedReceived(Me, EventArgs.Empty)
            ElseIf stringData.StartsWith("PWR") Then
                If stringData(3) = "0"c Then
                    PowerState = PowerState.On
                ElseIf stringData(3) = "1"c Then
                    PowerState = PowerState.Off
                Else
                    PowerState = PowerState.Unknown
                End If
            ElseIf stringData.StartsWith("VOL") Then
                Dim pioneerVolume As Integer
                If Not Integer.TryParse(stringData.Substring(3), pioneerVolume) Then Throw New Exception($"Unparsable volume {stringData}")
                Dim dbVol As Single = (pioneerVolume - 161) / 2
                VolumedB = dbVol
            ElseIf stringData.StartsWith("MUT") Then
                Mute = (stringData(3) = "0")
            ElseIf stringData.StartsWith("FN") Then
                Dim source As Integer = -1
                If Integer.TryParse(stringData.Substring(2), source) AndAlso
                    source >= 0 Then InputSource = CType(source, InputSource)
            ElseIf stringData.StartsWith("SR") Then
                Dim listeningMdS As Integer = -1
                If Integer.TryParse(stringData.Substring(2), listeningMdS) AndAlso
                        listeningMdS >= 0 Then ListeningModeSet = CType(listeningMdS, ListeningMode)
            ElseIf stringData.StartsWith("LM") Then
                Dim listeningMd As Integer = -1
                If Integer.TryParse(stringData.Substring(2), listeningMd) AndAlso
                        listeningMd >= 0 Then ListeningMode = CType(listeningMd, ListeningMode)
                'ElseIf stringData.StartsWith("SPK") Then 'Speakers
                'ElseIf stringData.StartsWith("HO") Then  'HDMI output source
                'ElseIf stringData.StartsWith("EX") Then  'SBch Processing
                'ElseIf stringData.StartsWith("MC") Then  'MCACC Memory
                'ElseIf stringData.StartsWith("IS") Then  'Phase Control
            ElseIf stringData.StartsWith("TO") Then  'Tone
                Logger.Info(String.Format("Unimplemented Tone: {0}", stringData))
            ElseIf stringData.StartsWith("BA") Then  'Bass
                Dim bass As Integer = -1
                If Integer.TryParse(stringData.Substring(2), bass) AndAlso
                        bass >= 0 Then Me.Bass = bass - 6
            ElseIf stringData.StartsWith("TR") Then  'Treble
                Dim treble As Integer = -1
                If Integer.TryParse(stringData.Substring(2), treble) AndAlso
                        treble >= 0 Then Me.Treble = treble - 6
                'ElseIf stringData.StartsWith("HA") Then  'HDMI Audio
                'ElseIf stringData.StartsWith("PR") Then  'Tuner Preset
                'ElseIf stringData.StartsWith("FR") Then  'Tuner Frequency
                'ElseIf stringData.StartsWith("XM") Then  'XM CHANNEL
                'ElseIf stringData.StartsWith("SIR") Then 'SIRIUS CHANNEL
                'ElseIf stringData.StartsWith("APR") Then 'ZONE 2 POWER
                'ElseIf stringData.StartsWith("BPR") Then 'ZONE 3 POWER
                'ElseIf stringData.StartsWith("ZV") Then  'ZONE 2 VOLUME
                'ElseIf stringData.StartsWith("YV") Then  'ZONE 3 VOLUME
                'ElseIf stringData.StartsWith("Z2MUT") Then 'ZONE 2 Mute
                'ElseIf stringData.StartsWith("Z3MUT") Then 'ZONE 3 Mute
                'ElseIf stringData.StartsWith("Z2F") Then 'ZONE 2 INPUT
                'ElseIf stringData.StartsWith("Z3F") Then 'ZONE 3 INPUT
                'ElseIf stringData.StartsWith("PQ") Then  'PQLS
                'ElseIf stringData.StartsWith("CLV") Then 'CH level
                'ElseIf stringData.StartsWith("VSB") Then 'Virtual SB
                'ElseIf stringData.StartsWith("VHT") Then 'Virtual Height
                'ElseIf stringData.StartsWith("FL") Then  'FL display information
                'ElseIf stringData.StartsWith("RGB") Then 'Input Name information

            Else
                commandProcessed = False
                Logger.Info(String.Format("Unparsed data received: {0}", stringData))
            End If

            If commandProcessed Then mLastReceiveTime = DateTime.Now
        Next

    End Sub

    'Private Sub WriteLineToFile(format As String, ParamArray arg() As String)
    '    Logger.Info(String.Format(format, arg))
    '    WriteLineToFile(String.Format(format, arg))
    'End Sub

    Private Sub SendToDevice(text As String, Optional wait As Integer = 0)
        If mLastSendTime < DateTime.Now.AddMinutes(-5) AndAlso mLastReceiveTime < DateTime.Now.AddMinutes(-5) OrElse Not mDevice.Connected Then WakeUpCpu()

        Logger.Debug($"Sending Command to device {text}")
        Debug.WriteLine($"Sending Command to device {text}")

        SendToDevice(Encoding.ASCII.GetBytes(text), wait)
    End Sub

    Private Sub SendToDevice(data As Byte(), Optional wait As Integer = 0)
        If Not mDevice.Connected Then mDevice.Connect(True)

        Dim sendTime = DateTime.Now
        Try

            SyncLock mSendLock

                mDevice.SendData(data)

                If wait > 0 Then
                    Threading.Thread.Sleep(wait)
                End If

            End SyncLock

            mLastSendTime = sendTime

        Catch ex As Device.NotConnectedException
            If Not ex.IsReconnecting Then
                Logger.Error(ex, "Error in send, not reconnecting")
            Else
                Logger.Debug(ex, "Error in send, reconnecting")
            End If
        End Try

    End Sub

#End Region

#Region " Public Events "

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
    Private Event ConfirmedReceived As EventHandler

#End Region

#Region " Public Properties "

    Public Property PowerState As PowerState
        Get
            Return mPowerState
        End Get
        Private Set
            mPowerState = Value
            NotifyPropertyChanged()
        End Set
    End Property
    Public Property VolumedB As Single 'Volume in dB's
        Get
            Return mVolumedB
        End Get
        Private Set
            mVolumedB = Value
            NotifyPropertyChanged()
        End Set
    End Property
    Public Property Mute As Boolean
        Get
            Return mMute
        End Get
        Private Set
            mMute = Value
            NotifyPropertyChanged()
        End Set
    End Property
    Public Property InputSource As InputSource
        Get
            Return mInputSource
        End Get
        Private Set
            mInputSource = Value
            NotifyPropertyChanged()
        End Set
    End Property
    Public Property ListeningModeSet As ListeningMode
        Get
            Return mListeningModeSet
        End Get
        Private Set
            mListeningModeSet = Value
            NotifyPropertyChanged()
        End Set
    End Property
    Public Property ListeningMode As ListeningMode
        Get
            Return mListeningMode
        End Get
        Private Set
            mListeningMode = Value
            NotifyPropertyChanged()
        End Set
    End Property
    Public Property Bass As Integer
        Get
            Return mBass
        End Get
        Private Set
            mBass = Value
            NotifyPropertyChanged()
        End Set
    End Property    'Volume of bass in dB
    Public Property Treble As Integer
        Get
            Return mTreble
        End Get
        Private Set
            mTreble = Value
            NotifyPropertyChanged()
        End Set
    End Property

    Public ReadOnly Property LastSendTime As DateTime
        Get
            Return mLastSendTime
        End Get
    End Property
    Public ReadOnly Property LastReadTime As DateTime
        Get
            Return mLastReceiveTime
        End Get
    End Property


#End Region

#Region " Public Constructor "

    Public Sub New(ipAddress As String, port As Integer, Optional name As String = "VSX-828")
        Me.New(Net.IPAddress.Parse(ipAddress), port, name)
    End Sub
    Public Sub New(ipAddress As IPAddress, port As Integer, Optional name As String = "VSX-828")
        Me.mDevice = New Device(ipAddress, port, name)

        AddHandler mDevice.DataReceived, AddressOf DeviceDataReceived
        AddHandler ConfirmedReceived, Sub(s, e)
                                          Console.WriteLine("- Confirmed Received -")
                                      End Sub

        Logger.Info($"New PioneerAmp created")

    End Sub

#End Region

#Region " Public Methods "

    Public Sub Connect()
        Me.mDevice.Connect(True)
    End Sub

    Public Sub NotifyPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Public Sub WakeUpCpu()
        Logger.Debug($"Sending Wakeup to device")
        SendToDevice(New Byte() {}, 100)
    End Sub

    Public Sub RequestPowerState()
        SendToDevice("?P")
    End Sub
    Public Sub SetPowerState([on] As Boolean)
        If [on] Then
            SendToDevice("PO")
        Else
            SendToDevice("PF")
        End If
    End Sub

    Public Sub RequestVolumeLevel()
        SendToDevice("?V")
    End Sub
    Public Sub ChangeVolume(up As Boolean)
        If up Then
            SendToDevice("VU")
        Else
            SendToDevice("VD")
        End If
    End Sub
    Public Sub SetVolume(db As Single?)
        If db.HasValue AndAlso (db < -80 OrElse db > 12) Then Throw New ArgumentException("dbVolume must be >-80db and smaller than +12db")
        If Not db.HasValue Then SetVolume(0)
        Dim pioneerVolume As Integer = db * 2 + 161
        SetVolume(pioneerVolume)
    End Sub
    Public Sub SetVolume(pioneerVolume As Integer)
        If pioneerVolume < 0 OrElse pioneerVolume > 185 Then Throw New ArgumentException("Pioneer volume must be between 0 and 185")
        SendToDevice(pioneerVolume.ToString.PadLeft(3, "0"c) & "VL")
    End Sub

    Public Sub RequestMuteState()
        SendToDevice("?M")
    End Sub
    Public Sub SetMute([on] As Boolean)
        SendToDevice($"M{If([on], "O", "F")}")
    End Sub

    Public Sub RequestFunctionMode()
        SendToDevice("?F")
    End Sub

    Public Sub RequestListeningMode()
        SendToDevice("?S") 'UNVERIFIED
    End Sub
    Public Sub RequestPlayingListeningMode()
        SendToDevice("?L") 'UNVERIFIED
    End Sub

    Public Sub RequestToneStatus()
        SendToDevice("?TO") 'UNVERIFIED
    End Sub
    ''' <summary>
    ''' Sets tone On/Bypass
    ''' </summary>
    ''' <param name="[on]">Tone On or Tone Bypass</param>
    Public Sub SetTone([on] As Boolean) 'If this doesnt work try ToggleTone TO
        SendToDevice($"T{If([on], "O", "F")}")
    End Sub

    Public Sub SendCommandString(commandString As String)
        'mDevice.SendData(Encoding.ASCII.GetBytes(commandString))
        SendToDevice(commandString)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If mDevice IsNot Nothing Then mDevice.Dispose()
    End Sub


#End Region

End Class