using Mirror;
using System;
using UnityEngine;
using System.Collections.Generic;
using Sodium;

public enum OpCodes : byte
{
    ServerPublicKey = 0,
    ClientPublicKey = 1,
    Data = 2,
}

public class Monke : Transport
{

    [Header("Connection Variables")]
    public Transport CommunicationTransport;

    public bool showDebugLogs = false;

    private KeyPair _keyPair = default(KeyPair);
    private Dictionary<int, byte[]> _serverSessions;

    private byte[] _serverPublicKey;
    private byte[] _clientSendBuffer;
    private byte[] _readBuffer;
    private byte[] _encryptionBuffer;
    private byte[] _writeBuffer;

    private byte[] _nonce;

    public override string ServerGetClientAddress(int connectionId) => CommunicationTransport.ServerGetClientAddress(connectionId);
#if MIRROR_41_0_OR_NEWER
    public override void ServerDisconnect(int connectionId) => CommunicationTransport.ServerDisconnect(connectionId);
#else
    public override bool ServerDisconnect(int connectionId) => CommunicationTransport.ServerDisconnect(connectionId);
#endif
    public override int GetMaxPacketSize(int channelId = 0) => CommunicationTransport.GetMaxPacketSize(channelId);
    public override void ServerEarlyUpdate() => CommunicationTransport.ServerEarlyUpdate();
    public override void ClientEarlyUpdate() => CommunicationTransport.ClientEarlyUpdate();
    public override void ClientDisconnect() => CommunicationTransport.ClientDisconnect();
    public override void ClientLateUpdate() => CommunicationTransport.ClientLateUpdate();
    public override void ServerLateUpdate() => CommunicationTransport.ServerLateUpdate();
    public override bool ClientConnected() => CommunicationTransport.ClientConnected();
    public override bool ServerActive() => CommunicationTransport.ServerActive();
    public override void ServerStop() => CommunicationTransport.ServerStop();
    public override bool Available() => CommunicationTransport.Available();
    public override Uri ServerUri() => CommunicationTransport.ServerUri();
    public override void Shutdown() => CommunicationTransport.Shutdown();

    private void Start()
    {
        _clientSendBuffer = new byte[CommunicationTransport.GetMaxPacketSize()];
        SetupCallbacks();

        if (showDebugLogs)
            Debug.Log($"<color=green>MONKE | MONKE STARTED SUCCESSFULLY!</color>");

    }

    private void SetupCallbacks()
    {
        CommunicationTransport.OnServerConnected = OnServerConnect;
        CommunicationTransport.OnServerDisconnected = OnServerDisconnect;
        CommunicationTransport.OnServerDataReceived = OnServerDataReceive;
        CommunicationTransport.OnServerError = (i, e) => OnServerError?.Invoke(i, e);

        CommunicationTransport.OnClientDataReceived = OnClientDataReceive;
        CommunicationTransport.OnClientDisconnected = () => OnClientDisconnected?.Invoke();
        CommunicationTransport.OnClientError = (e) => OnClientError?.Invoke(e);
    }

    void GenerateInitialKeyPair()
    {
        _keyPair = PublicKeyBox.GenerateKeyPair();

        if (showDebugLogs)
            Debug.Log($"<color=green>MONKE | KEYPAIR GENERATED!</color>");
    }

    private void OnServerConnect(int conn)
    {
        int pos = 0;
        _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.ServerPublicKey);
        _clientSendBuffer.WriteBytes(ref pos, _keyPair.PublicKey);
    #if MIRROR_41_0_OR_NEWER
        CommunicationTransport.ServerSend(conn, new ArraySegment<byte>(_clientSendBuffer, 0, pos), 0);
    #else
        CommunicationTransport.ServerSend(conn, 0, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
    #endif
    }

    void OnServerDataReceive(int conn, ArraySegment<byte> data, int channel)
    {
        try
        {
            var rawData = data.Array;
            int pos = data.Offset;

            OpCodes opcode = (OpCodes)rawData.ReadByte(ref pos);

            switch (opcode)
            {
                case OpCodes.ClientPublicKey:
                    byte[] clientPublicKey = rawData.ReadBytes(ref pos);

                    _serverSessions.Add(conn, clientPublicKey);

                    if (showDebugLogs)
                        Debug.Log($"<color=green>MONKE | SERVER RECIEVED CLIENT PUBLIC KEY!</color>");

                    OnServerConnected?.Invoke(conn);
                    break;
                case OpCodes.Data:
                    _readBuffer = rawData.ReadBytes(ref pos);
                    _nonce = rawData.ReadBytes(ref pos);

                    if (_serverSessions.ContainsKey(conn))
                    {
                        _encryptionBuffer = PublicKeyBox.Open(_readBuffer, _nonce, _keyPair.PrivateKey, _serverSessions[conn]);
                        OnServerDataReceived?.Invoke(conn, new ArraySegment<byte>(_encryptionBuffer), channel);

                        if (showDebugLogs)
                            Debug.Log($"<color=green>MONKE | SERVER DATA | RAW DATA: " + _readBuffer.Length + " DATA DECRYPTED FROM CONN ID: " + conn + " SIZE: " + _encryptionBuffer.Length + "</color>" +
                            " <color=yellow>DELTA: " + (_readBuffer.Length - _encryptionBuffer.Length) + "</color>");
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error: " + e);
        }
    }

    void OnServerDisconnect(int conn)
    {
        if (_serverSessions.ContainsKey(conn))
            _serverSessions.Remove(conn);

        OnServerDisconnected?.Invoke(conn);
    }

    private void OnClientDataReceive(ArraySegment<byte> data, int channel)
    {
        try
        {
            var rawData = data.Array;
            int pos = data.Offset;

            OpCodes opcode = (OpCodes)rawData.ReadByte(ref pos);

            switch (opcode)
            {
                case OpCodes.ServerPublicKey:
                    _serverPublicKey = rawData.ReadBytes(ref pos);

                    pos = 0;
                    _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.ClientPublicKey);
                    _clientSendBuffer.WriteBytes(ref pos, _keyPair.PublicKey);
                #if MIRROR_41_0_OR_NEWER
                    CommunicationTransport.ClientSend(new ArraySegment<byte>(_clientSendBuffer, 0, pos), Channels.Reliable);
                #else
                    CommunicationTransport.ClientSend(Channels.Reliable, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
                #endif

                    if (showDebugLogs)
                        Debug.Log($"<color=green>MONKE | CLIENT RECIEVED SERVER PUBLIC KEY!</color>");

                    OnClientConnected?.Invoke();

                    break;
                case OpCodes.Data:
                    _readBuffer = rawData.ReadBytes(ref pos);
                    _nonce = rawData.ReadBytes(ref pos);

                    _encryptionBuffer = PublicKeyBox.Open(_readBuffer, _nonce, _keyPair.PrivateKey, _serverPublicKey);
                    OnClientDataReceived?.Invoke(new ArraySegment<byte>(_encryptionBuffer), channel);


                    if (showDebugLogs)
                        Debug.Log($"<color=green>MONKE | CLIENT DATA | RAW DATA: " + _readBuffer.Length + " DECRYPTED DATA LENGTH: " + _encryptionBuffer.Length + "</color>" +
                            " <color=yellow>DELTA: " + (_readBuffer.Length - _encryptionBuffer.Length) + "</color>");

                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error: " + e);
        }
    }

    public override void ClientConnect(string address)
    {
        GenerateInitialKeyPair();
        CommunicationTransport.ClientConnect(address);
    }

#if MIRROR_41_0_OR_NEWER
    public override void ClientSend(ArraySegment<byte> segment, int channelId)
#else
    public override void ClientSend(int channelId, ArraySegment<byte> segment)
#endif
    {
        if (_serverPublicKey != null)
        {
            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.Data);
            _writeBuffer = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, _writeBuffer, 0, segment.Count);
            _nonce = PublicKeyBox.GenerateNonce();

            _clientSendBuffer.WriteBytes(ref pos, PublicKeyBox.Create(_writeBuffer, _nonce, _keyPair.PrivateKey, _serverPublicKey));
            _clientSendBuffer.WriteBytes(ref pos, _nonce);
        #if MIRROR_41_0_OR_NEWER
            CommunicationTransport.ClientSend(new ArraySegment<byte>(_clientSendBuffer, 0, pos), channelId);
        #else
            CommunicationTransport.ClientSend(channelId, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        #endif
        }
    }

#if MIRROR_41_0_OR_NEWER
    public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
#else
    public override void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
#endif
    {
        if (_serverSessions.ContainsKey(connectionId))
        {
            int pos = 0;
            _clientSendBuffer.WriteByte(ref pos, (byte)OpCodes.Data);
            _writeBuffer = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, _writeBuffer, 0, segment.Count);
            _nonce = PublicKeyBox.GenerateNonce();

            _clientSendBuffer.WriteBytes(ref pos, PublicKeyBox.Create(_writeBuffer, _nonce, _keyPair.PrivateKey, _serverSessions[connectionId]));
            _clientSendBuffer.WriteBytes(ref pos, _nonce);
        #if MIRROR_41_0_OR_NEWER
               CommunicationTransport.ServerSend(connectionId, new ArraySegment<byte>(_clientSendBuffer, 0, pos), channelId);
        #else
               CommunicationTransport.ServerSend(connectionId, channelId, new ArraySegment<byte>(_clientSendBuffer, 0, pos));
        #endif
        }
    }

    public override void ServerStart()
    {
        GenerateInitialKeyPair();
        _serverSessions = new Dictionary<int, byte[]>();
        CommunicationTransport.ServerStart();
    }
}

public static class MonkeTools
{
    public static void WriteByte(this byte[] data, ref int position, byte value)
    {
        data[position] = value;
        position += 1;
    }

    public static byte ReadByte(this byte[] data, ref int position)
    {
        byte value = data[position];
        position += 1;
        return value;
    }

    public static void WriteBool(this byte[] data, ref int position, bool value)
    {
        unsafe
        {
            fixed (byte* dataPtr = &data[position])
            {
                bool* valuePtr = (bool*)dataPtr;
                *valuePtr = value;
                position += 1;
            }
        }
    }

    public static bool ReadBool(this byte[] data, ref int position)
    {
        bool value = BitConverter.ToBoolean(data, position);
        position += 1;
        return value;
    }

    public static void WriteString(this byte[] data, ref int position, string value)
    {
        data.WriteInt(ref position, value.Length);
        for (int i = 0; i < value.Length; i++)
            data.WriteChar(ref position, value[i]);
    }

    public static string ReadString(this byte[] data, ref int position)
    {
        string value = default;

        int stringSize = data.ReadInt(ref position);

        for (int i = 0; i < stringSize; i++)
            value += data.ReadChar(ref position);

        return value;
    }

    public static void WriteBytes(this byte[] data, ref int position, byte[] value)
    {
        data.WriteInt(ref position, value.Length);
        for (int i = 0; i < value.Length; i++)
            data.WriteByte(ref position, value[i]);
    }

    public static byte[] ReadBytes(this byte[] data, ref int position)
    {
        int byteSize = data.ReadInt(ref position);

        byte[] value = new byte[byteSize];

        for (int i = 0; i < byteSize; i++)
            value[i] = data.ReadByte(ref position);

        return value;
    }

    public static void WriteChar(this byte[] data, ref int position, char value)
    {
        unsafe
        {
            fixed (byte* dataPtr = &data[position])
            {
                char* valuePtr = (char*)dataPtr;
                *valuePtr = value;
                position += 2;
            }
        }
    }

    public static char ReadChar(this byte[] data, ref int position)
    {
        char value = BitConverter.ToChar(data, position);
        position += 2;
        return value;
    }

    public static void WriteInt(this byte[] data, ref int position, int value)
    {
        unsafe
        {
            fixed (byte* dataPtr = &data[position])
            {
                int* valuePtr = (int*)dataPtr;
                *valuePtr = value;
                position += 4;
            }
        }
    }

    public static int ReadInt(this byte[] data, ref int position)
    {
        int value = BitConverter.ToInt32(data, position);
        position += 4;
        return value;
    }
}

