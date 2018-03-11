Imports System.Net
Imports System.Net.Sockets
Imports System.Threading

Public Class Device
    Implements IDisposable

#Region " Private Members "

    Private ReadOnly logger As NLog.Logger = NLog.LogManager.GetCurrentClassLogger

    Private mServer As TcpClient
    Private mReadTask As Task
    Private mMayRead As Boolean
    Private _disposing As Boolean

#End Region

#Region " Public Members "

    Public ReadOnly Property IpAddress As IPAddress
    Public ReadOnly Property Port As Integer
    Public Property Name As String

    Public Property MessagePrefix As Byte() = New Byte() {&HD}
    Public Property MessageSuffix As Byte() = New Byte() {&HD, &HA}

    Public ReadOnly Property Connected As Boolean
        Get
            If mServer Is Nothing Then Return False
            Return mServer.Connected
        End Get
    End Property

#End Region

#Region " Public Events "

    Public Event DataReceived As EventHandler(Of ReceivedDataEventArgs)

#End Region

#Region " Constructor "

    Public Sub New(ipAddress As IPAddress, port As Integer, Optional name As String = Nothing)
        Me.IpAddress = ipAddress
        Me.Port = port
        Me.Name = name

        logger.Info($"Created new device (ip:{ipAddress} / port:{port} / name:{name})")
    End Sub

#End Region

#Region " Public Methods "

    Public Sub Connect(Optional startRead As Boolean = False)

        Dim startConnect = DateTime.Now

        If mServer IsNot Nothing Then
            mServer.Client.Close(1)
            mServer.Close()
            mServer = Nothing
        End If

        Do

            mServer = New TcpClient(IpAddress.ToString(), Port)

            If Not mServer.Connected Then
                logger.Debug("Not connected to client")
                Threading.Thread.Sleep(100)
            End If
        Loop Until mServer.Connected OrElse startConnect.AddSeconds(20) < DateTime.Now OrElse _disposing

        If Not mServer.Connected Then Throw New Exception($"Unable to connect to host at {IpAddress.ToString()}:{Port}")

        logger.Info("Connected to device")

        If startRead Then StartReading()
    End Sub

    Public Sub StartReading()
        StopReading()

        mMayRead = True

        mReadTask = New Task(Sub()
                                 Do
                                     Try
                                         Dim availableData = mServer.Client.Available

                                         If availableData = 0 Then
                                             Threading.Thread.Sleep(100)
                                         Else
                                             Dim buffer(availableData - 1) As Byte
                                             Dim receivedSize = mServer.Client.Receive(buffer, SocketFlags.None)

                                             Dim data(receivedSize - 1) As Byte
                                             Array.Copy(buffer, data, receivedSize)

                                             OnReceivedData(data)

                                         End If
                                     Catch ex As Exception

                                         logger.Error(ex, "Exception while reading, reconnect?")
                                         'Connect(mMayRead)
                                         'TODO disconnected handling
                                         'Continue!
                                     End Try

                                 Loop Until Not mMayRead OrElse _disposing

                                 mReadTask = Nothing
                             End Sub)

        mReadTask.Start()

    End Sub

    Public Sub SendData(data As Byte())
        If Not mServer.Connected Then
            Dim thr = New System.Threading.Thread(CType(Sub() Connect(mMayRead), ThreadStart))
            thr.Start()
            Throw New NotConnectedException(True)
        End If
        If data Is Nothing Then Return

        Dim message(data.Length - 1 + If(MessagePrefix Is Nothing, 0, MessagePrefix.Length) + If(MessageSuffix Is Nothing, 0, MessageSuffix.Length)) As Byte

        If MessagePrefix IsNot Nothing Then
            Array.Copy(MessagePrefix, message, MessagePrefix.Length)
        End If
        If MessageSuffix IsNot Nothing Then
            Array.Copy(MessageSuffix, 0, message, message.Length - MessageSuffix.Length, MessageSuffix.Length)
        End If

        Array.Copy(data, 0, message, If(MessagePrefix Is Nothing, 0, MessagePrefix.Length), data.Length)


        mServer.Client.Send(message)

    End Sub

    Public Sub StopReading()
        mMayRead = False

        If mReadTask IsNot Nothing Then mReadTask.Wait(150)
        If mReadTask IsNot Nothing Then
            mReadTask.Dispose()
            mReadTask = Nothing
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        'Dim abortRead = mReadTask IsNot Nothing
        _disposing = True
        mMayRead = False
        StopReading()
        mServer.Close()
    End Sub

#End Region

#Region " Private Methods "

    Private Sub OnReceivedData(data As Byte())
        RaiseEvent DataReceived(Me, New ReceivedDataEventArgs(data))
    End Sub

#End Region

#Region " Public EventArgs "

    Public Class ReceivedDataEventArgs
        Inherits EventArgs

        Public ReadOnly Property Data As Byte()

        Public Sub New(data As Byte())
            Me.Data = data
        End Sub
    End Class

    Public Class NotConnectedException
        Inherits Exception
        Property IsReconnecting() As Boolean

        Public Sub New(isReconnecting As Boolean)
            Me.IsReconnecting = isReconnecting
        End Sub

    End Class

#End Region

End Class
