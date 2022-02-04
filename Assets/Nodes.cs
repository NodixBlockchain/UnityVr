using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System;
using Org.BouncyCastle.Crypto.Digests;

public struct messageHDR
{
    public uint magic;
    public string cmd;
    public uint size;
    public uint sum;
    
}


public struct messageAddr
{
    public ulong services;
    public IPAddress ip;
    public ushort port;
}


public struct messageVersion
{
    public uint proto_ver;
    public ulong services;
    public ulong timestamp;
    public messageAddr myAddr;
    public messageAddr theirAddr;
    public ulong nonce;
    public String user_agent;
    public uint last_blk;
}

public struct messagePing
{
    public ulong nonce;
}

public struct messagePong
{
    public ulong nonce;
}

class MessageHeader
{
    // Size of receive buffer.  
    public const int BufferSize = 24;
    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];
    // Received data string.  
    //public StringBuilder sb = new StringBuilder();
    public BinaryWriter writer = null;
    public MemoryStream m = null;

    public MessageHeader()
    {
        m = new MemoryStream();
        writer = new BinaryWriter(m);
    }
}

class MessageState
{
    // Size of receive buffer.  
    public int BufferSize;
    // Receive buffer.  
    public byte[] buffer;
    // Received data string.  
    //public StringBuilder sb = new StringBuilder();
    public BinaryWriter writer = null;
    public MemoryStream m = null;
    public messageHDR hdr;

    public MessageState(messageHDR hdr)
    {
        this.hdr = hdr;
        BufferSize = (int)hdr.size;
        buffer = new byte[BufferSize];

        m = new MemoryStream();
        writer = new BinaryWriter(m);
    }
}


public class Node
{
    public bool isSeed;
    public IPAddress ip;
    public ushort P2PPort;
    public ushort HTTPPort;
    public long pingTime;
    public string address;

    public long connectTime;

    public long lastPingTime;
    public bool waitPong = false;
    public bool waitConnect = false;
    public bool connected = false;

    private ulong lastPingNonce;

    private IPAddress myip;
    private Socket client;
    private ulong nodeNonce = 1;
    


    public Node(string address, ushort port,bool isSeed, IPAddress myip)
    {
        this.address = address;
        this.P2PPort = port;
        this.isSeed = isSeed;
        this.lastPingTime = 0;
        this.pingTime = 0;
        this.waitConnect = true;
        this.connectTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        this.myip = myip;
        this.ip = null;

        try
        {
            // Establish the remote endpoint for the socket.  
            IPHostEntry ipHostInfo = Dns.GetHostEntry(address);
            for (int n = 0; n < ipHostInfo.AddressList.Length; n++)
            {
                if (ipHostInfo.AddressList[n].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    this.ip = ipHostInfo.AddressList[n];
            }

            IPEndPoint remoteEP = new IPEndPoint(this.ip, this.P2PPort);

            // Create a TCP/IP socket.  
            client = new Socket(this.ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Connect to the remote endpoint.  
            client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    public void Disconnect()
    {
        if (client != null)
        {
            client.Close();
            client = null;
        }
    }


    private void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            // Complete the connection.  
            client.EndConnect(ar);

            SendVersionMessage();
            ReceivePacketHDR();

            waitConnect = false;
            connected = true;
            Debug.Log("Connected to Node " + client.RemoteEndPoint.ToString());
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    public static byte[] Hash(byte[] data)
    {
        Sha256Digest sha256 = new Sha256Digest();
        sha256.BlockUpdate(data, 0, data.Length);
        byte[] hash = new byte[sha256.GetDigestSize()];
        sha256.DoFinal(hash, 0);
        return hash;
    }

    public static byte[] HashL(byte[] data, int l)
    {
        Sha256Digest sha256 = new Sha256Digest();
        sha256.BlockUpdate(data, 0, l);
        byte[] hash = new byte[sha256.GetDigestSize()];
        sha256.DoFinal(hash, 0);
        return hash;
    }

    long readVINT(BinaryReader r)
    {
        byte first;

        first = r.ReadByte();

        if (first == 0xFD)
            return r.ReadInt16();

        if (first == 0xFE)
            return r.ReadInt32();

        if (first == 0xFF)
            return r.ReadInt32();

        return first;
    }

    messageAddr readADDR(BinaryReader reader)
    {
        messageAddr addr = new messageAddr();
        byte[] ipc, port;

        addr.services = reader.ReadUInt64();

        ipc = reader.ReadBytes(16);
        if ((ipc[10] == 0xFF) && (ipc[11] == 0xFF))
        {
            byte[] ip = new byte[4];

            ip[0] = ipc[12];
            ip[1] = ipc[13];
            ip[2] = ipc[14];
            ip[3] = ipc[15];
            addr.ip = new IPAddress(ip);
        }

        port = reader.ReadBytes(2);
        addr.port = (ushort)((port[0] << 8) | port[1]);


        return addr;
    }

    void writeADDR(messageAddr addr, BinaryWriter writer)
    {
        byte[] ipc, ip, port;

        writer.Write(addr.services);

        ip = addr.ip.GetAddressBytes();

        ipc = new byte[16];

        if (addr.ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            ipc[10] = 0xFF;
            ipc[11] = 0xFF;

            ipc[12] = ip[0];
            ipc[13] = ip[1];
            ipc[14] = ip[2];
            ipc[15] = ip[3];
        }

        writer.Write(ipc);

        port = new byte[2];
        port[0] = (byte)(addr.port >> 8);
        port[1] = (byte)(addr.port & 0xFF);

        writer.Write(port);
    }

    private void ReceiveVersionCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the state object and the client socket
            MessageState version = (MessageState)ar.AsyncState;

            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceiveVersionCallback error ");
                return;
            }

            version.writer.Write(version.buffer, 0, bytesRead);

            if (version.m.Length >= version.hdr.size)
            {
                messageVersion ver = new messageVersion();
                var reader = new BinaryReader(version.m);
                version.m.Position = 0;

                byte[] hash = Hash(HashL(version.m.GetBuffer(), (int)version.hdr.size));
                uint sum = (uint)(hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24));

                if (version.hdr.sum != sum)
                {
                    Debug.Log("wrong version sum " + version.m.GetBuffer().Length + " " + version.hdr.sum.ToString("X8") + " " + sum.ToString("X8") + " " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(hash));
                }

                ver.proto_ver = reader.ReadUInt32();
                ver.services = reader.ReadUInt64();
                ver.timestamp = reader.ReadUInt64();
                ver.myAddr = readADDR(reader);
                ver.theirAddr = readADDR(reader);
                ver.nonce = reader.ReadUInt64();

                long ual = this.readVINT(reader);

                StringBuilder sb = new StringBuilder();
                char[] user_agent = reader.ReadChars((int)ual);
                for (int n = 0; n < ual && user_agent[n] != 0; n++)
                {
                    sb.Append(user_agent[n]);
                }
                ver.user_agent = sb.ToString();
                ver.last_blk = reader.ReadUInt32();

                Debug.Log("version message " + ver.proto_ver + " " + ver.user_agent + " " + ver.last_blk);

            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }


    }


    private void ReceivePongCallback(IAsyncResult ar)
    {
        messagePong Pong = new messagePong();

        try
        {
            // Retrieve the state object and the client socket
            MessageState pong = (MessageState)ar.AsyncState;
            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceivePingCallback error ");
                return;
            }




            pong.writer.Write(pong.buffer, 0, bytesRead);

            if (pong.m.Length >= pong.hdr.size)
            {
                var reader = new BinaryReader(pong.m);
                pong.m.Position = 0;
                Pong.nonce = reader.ReadUInt64();

                if(Pong.nonce != lastPingNonce)
                {
                    Debug.Log("pong nonce mismatch " + Pong.nonce + "!=" + lastPingNonce);
                }

                pingTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastPingTime;

                waitPong = false;

                Debug.Log("Pong message " + pingTime);
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return;
        }

        
    }
    private void ReceivePingCallback(IAsyncResult ar)
    {
        messagePing Ping = new messagePing();

        try
        {
            // Retrieve the state object and the client socket
            MessageState ping = (MessageState)ar.AsyncState;
            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceivePingCallback error ");
                return;
            }

            ping.writer.Write(ping.buffer, 0, bytesRead);

            if (ping.m.Length >= ping.hdr.size)
            {
                var reader = new BinaryReader(ping.m);
                ping.m.Position = 0;
                Ping.nonce = reader.ReadUInt64();
                Debug.Log("Ping message " + Ping.nonce);
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return;
        }

        SendPongMessage(Ping.nonce);
    }
    
    private void ReceivePing(messageHDR hdr)
    {
        // Create the state object.  
        MessageState msg = new MessageState(hdr);

        // Begin receiving the data from the remote device.  
        client.BeginReceive(msg.buffer, 0, msg.BufferSize, 0, new AsyncCallback(ReceivePingCallback), msg);
    }

    private void ReceivePong(messageHDR hdr)
    {
        // Create the state object.  
        MessageState msg = new MessageState(hdr);

        // Begin receiving the data from the remote device.  
        client.BeginReceive(msg.buffer, 0, msg.BufferSize, 0, new AsyncCallback(ReceivePongCallback), msg);
    }


    private messageHDR createHDR(string cmd, MemoryStream m)
    {
        messageHDR hdr;

        byte[] hash = Hash(HashL(m.GetBuffer(), (int)m.Position));

        hdr.magic = 0xD9BEFECA;
        hdr.cmd = cmd;
        hdr.size = (uint)m.Position;
        hdr.sum = (uint)(hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24));
        return hdr;
    }

    private byte[] createMessageBuffer(string cmd, MemoryStream payload)
    {
        messageHDR hdr = createHDR(cmd, payload);
        MemoryStream buffer = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(buffer);
        char[] bcmd = cmd.ToCharArray();
        char[] ccmd = new char[12];

        bcmd.CopyTo(ccmd, 0);

        writer.Write(hdr.magic);
        writer.Write(ccmd);
        writer.Write(hdr.size);
        writer.Write(hdr.sum);

        if (payload.Position > 0)
            writer.Write(payload.GetBuffer());

        return buffer.GetBuffer();

    }

    private void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Complete sending the data to the remote device.  
            int bytesSent = client.EndSend(ar);
            Debug.Log("Sent bytes to server." + bytesSent);

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }


    private void SendPongMessage( ulong nonce)
    {
        messagePong pong = new messagePong();
        pong.nonce = nonce;

        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write(pong.nonce);

        byte[] messageBuffer = createMessageBuffer("pong", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }

    public void SendPingMessage()
    {
        messagePong ping = new messagePong();
        ping.nonce = nodeNonce++;

        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write(ping.nonce);

        byte[] messageBuffer = createMessageBuffer("ping", m);

        lastPingTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        lastPingNonce = ping.nonce;
        waitPong = true;

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }

    private void SendVerackMessage()
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        byte[] messageBuffer = createMessageBuffer("verack", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);

    }

    private void SendVersionMessage()
    {
        try
        {
            messageVersion ver = new messageVersion();


            ver.proto_ver = 60018;
            ver.services = 0;
            ver.timestamp = (ulong)System.DateTimeOffset.Now.ToUnixTimeSeconds();

            ver.myAddr.ip = myip;
            ver.myAddr.port = 0;
            ver.myAddr.services = 0;

            ver.theirAddr.ip = IPAddress.Parse(((IPEndPoint)client.RemoteEndPoint).Address.ToString());
            ver.theirAddr.port = (ushort)((IPEndPoint)client.RemoteEndPoint).Port;
            ver.theirAddr.services = 0;

            ver.nonce = nodeNonce++;
            ver.user_agent = "NodixVR";
            ver.last_blk = 0;

            MemoryStream m = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(m);

            writer.Write(ver.proto_ver);
            writer.Write(ver.services);
            writer.Write(ver.timestamp);

            writeADDR(ver.myAddr, writer);
            writeADDR(ver.theirAddr, writer);

            writer.Write(ver.nonce);
            writer.Write((byte)ver.user_agent.Length);
            writer.Write(ver.user_agent.ToCharArray());
            writer.Write(ver.last_blk);

            byte[] messageBuffer = createMessageBuffer("version", m);
            client.BeginSend(messageBuffer, 0, (int)(m.Position + 24), 0, new AsyncCallback(SendCallback), client);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }


    private void ReceiveVersion( messageHDR hdr)
    {
        // Create the state object.  
        MessageState version = new MessageState(hdr);

        // Begin receiving the data from the remote device.  
        client.BeginReceive(version.buffer, 0, version.BufferSize, 0, new AsyncCallback(ReceiveVersionCallback), version);
    }

    private void ReceivePacketHDR()
    {
        try
        {
            // Create the state object.  
            MessageHeader state = new MessageHeader();

            // Begin receiving the data from the remote device.  
            client.BeginReceive(state.buffer, 0, MessageHeader.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }



    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the state object and the client socket
            MessageHeader state = (MessageHeader)ar.AsyncState;

            int bytesRead = client.EndReceive(ar);

            if(bytesRead<=0)
            {
                Debug.Log("ReceiveCallback error "+address);
                return;
            }

            state.writer.Write(state.buffer, 0, bytesRead);

            if (state.m.Length >= 24)
            {
                messageHDR hdr;

                var reader = new BinaryReader(state.m);

                state.m.Position = 0;

                hdr.magic = reader.ReadUInt32();

                StringBuilder sb = new StringBuilder();
                char[] cmd = reader.ReadChars(12);
                for (int n = 0; cmd[n] != 0 & n < 12; n++)
                {
                    sb.Append(cmd[n]);
                }
                hdr.cmd = sb.ToString();
                hdr.size = reader.ReadUInt32();
                hdr.sum = reader.ReadUInt32();

                Debug.Log("packet hdr : " + hdr.magic.ToString("X8") + ", " + hdr.cmd + ", " + hdr.size + ", " + hdr.sum.ToString("X8"));

                switch (hdr.cmd)
                {
                    case "version": ReceiveVersion(hdr); SendVerackMessage(); break;
                    case "mempool": break;
                    case "getaddr": break;
                    case "verack": SendPingMessage(); break;
                    case "ping": ReceivePing(hdr); break;
                    case "pong": ReceivePong(hdr); break;
                    default: Debug.Log("unknown message : " + hdr.cmd); break;
                }
                ReceivePacketHDR();
            }

        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }


}

public class Nodes : MonoBehaviour
{
    public string seedNodeAdress = "nodix.eu";
    public ushort seedNodePort = 16819;

    private IPAddress myIP;

    public List<Node> NodesList;

    

    // Start is called before the first frame update
    void Start()
    {
        byte[] myip = new byte[4];
        myIP = new IPAddress(myip);

        NodesList = new List<Node>();
        NodesList.Add(new Node(seedNodeAdress, seedNodePort, true, myIP));
    }

    public void addNode(string address, ushort port)
    {
        NodesList.Add(new Node(address, port, false, myIP));
    }


    // Update is called once per frame
    void Update()
    {
        for(int n=0;n<NodesList.Count;n++)
        {
            

            if (!NodesList[n].connected)
            {
                if(NodesList[n].waitConnect)
                {
                    if ((System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - NodesList[n].connectTime) > 5000)
                    {
                        NodesList.RemoveAt(n);
                        continue;
                    }
                }
            }
            else if ((System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - NodesList[n].lastPingTime) > 30000)
            {
                if (!NodesList[n].waitPong)
                    NodesList[n].SendPingMessage();
                else
                {
                    NodesList[n].Disconnect();
                    NodesList.RemoveAt(n);
                    Debug.Log("ping timeout");
                }
            }



        }
        
    }
}
