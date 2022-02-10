using System.Collections;
using System.Collections.Generic;


using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System;
using System.Xml.Serialization;
using System.Numerics;

using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;


using DataTanker;
using DataTanker.Settings;



using UnityEngine;

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

public struct messageAddrT
{
    public uint time;
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

public struct messageAddrs
{
    public messageAddrT[] addrs;
}

public struct messagePong
{
    public ulong nonce;
}

public struct messageGetHeaders
{
    public uint version;
    public List<byte[]> hashes;
    public byte[] hashStop;
}

public struct blockheader
{
    public uint version;
    public byte[] prev;
    public byte[] merkle_root;
    public uint time;
    public uint bits;
    public uint nonce;
    public uint nTx;
    public bool isPow;
}


public struct TransactionInput
{
    public byte[] txid;
    public uint utxo;
    public byte[] script;
    public uint sequence;
}
public struct TransactionOutput
{
    public ulong amount;
    public byte[] script;
}

public struct TxInfos
{
    public NodixApplication app;
    public int item;
    public appObj child;
    public appObj childOf;
    public appObj obj;
}
public struct Tx
{
    public byte[] hash;
    public uint version;
    public uint time;
    public TransactionInput[] inputs;
    public TransactionOutput[] outputs;
    public uint locktime;
}



public struct messageInventoryHash
{
    public uint type;
    public byte[] hash;

}

public struct messageInventory
{
    public messageInventory(long count)
    {
        hashList = new messageInventoryHash[count];
    }
    public messageInventoryHash[] hashList;
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


[System.Serializable]
public class ApplicationTypeEntry
{
    public string key;
    public uint type;
    public uint flags;

}

[System.Serializable]
public class ApplicationType
{
    public string name;
    public byte[] hash;
    public uint type;
    public ApplicationTypeEntry[] entries;

}


[System.Serializable]
public class NodixApplication
{
    public string name;
    public byte[] hash;
    public ApplicationType[] types;
}

[System.Serializable]
public class ApplicationsRoot
{
    public byte[] hash;
    public NodixApplication[] applications;

}


[System.Serializable]
public class Applications
{
    public Applications()
    {
        root = new ApplicationsRoot();
    }
    public ApplicationsRoot root;
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
    public bool hasVer = false;
    public bool recvHDR = false;
    
    public uint current_blk;

    public List<messageInventoryHash> inventory;

    public List<blockheader> RecvHeaders;
    public List<Tx> RecvTransactions;
    public object _blkLock = new object();
    public object _txLock = new object();
    public object _invLock = new object();

    private messageVersion ver;
    private ulong lastPingNonce;
    private IPAddress myip;
    private Socket client;
    private ulong nodeNonce = 1;
    private bool sentMP = false;
    
    

    long last_block;

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
        this.ver = new messageVersion();
        this.RecvHeaders = new List<blockheader>();
        this.RecvTransactions = new List<Tx>();
        this.inventory = new List<messageInventoryHash>();



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
    public uint getLastBlock()
    {
        if (!hasVer)
            return 0;

        return ver.last_blk;
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
    
    messageAddrT readADDRT(BinaryReader reader)
    {
        messageAddrT addr = new messageAddrT();
        byte[] ipc, port;

        addr.time = reader.ReadUInt32();
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


    private void SendPongMessage(ulong nonce)
    {
        messagePong pong = new messagePong();
        pong.nonce = nonce;

        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write(pong.nonce);

        byte[] messageBuffer = createMessageBuffer("pong", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }

    public void SendGetDataMessage(messageInventoryHash[] hashList)
    {

        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        Nodes.writeVINT((ulong)hashList.Length, writer);
        for (int n = 0; n < hashList.Length; n++)
        {
            writer.Write(hashList[n].type);
            writer.Write(hashList[n].hash);
        }

        byte[] messageBuffer = createMessageBuffer("getdata", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }

    public void SendGetHeadersMessage(List<byte[]> hdrs)
    {
        messageGetHeaders gethdr = new messageGetHeaders();
        gethdr.version = 10;
        gethdr.hashes = hdrs;
        gethdr.hashStop = new byte[32];


        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write(gethdr.version);
        Nodes.writeVINT((ulong)gethdr.hashes.Count, writer);

        for (int n = 0; n < gethdr.hashes.Count; n++)
        {
            writer.Write(gethdr.hashes[n]);
        }
        writer.Write(gethdr.hashStop);

        byte[] messageBuffer = createMessageBuffer("getheaders", m);

        recvHDR = true;

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

    public void SendGetAppsMessage()
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        byte[] messageBuffer = createMessageBuffer("getapps", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }

    public void SendGetAppTypeMessage(byte[] hash)
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write(hash);

        byte[] messageBuffer = createMessageBuffer("getapptypes", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }


    public void SendTmppoolMessage()
    {
        if (sentMP)
            return;

        sentMP = true;

        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        byte[] messageBuffer = createMessageBuffer("tmppool", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }
    public void SendMempoolMessage()
    {
        if (sentMP)
            return;

        sentMP = true;

        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        byte[] messageBuffer = createMessageBuffer("mempool", m);

        client.BeginSend(messageBuffer, 0, (int)(24 + m.Position), 0, new AsyncCallback(SendCallback), client);
    }

    public void SendTxMessage(Tx tx)
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        byte[] txbuffer = Nodes.TXtoBytes(tx);
        tx.hash = Nodes.Hash(Nodes.Hash(txbuffer));
        writer.Write(txbuffer);

        byte[] messageBuffer = createMessageBuffer("tx", m);

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
            ver.myAddr.services = 1;

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

    private void ReceiveVersionCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the state object and the client socket
            MessageState msg = (MessageState)ar.AsyncState;

            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceiveVersionCallback error ");
                return;
            }

            msg.writer.Write(msg.buffer, 0, bytesRead);

            if (msg.m.Position >= msg.hdr.size)
            {
                var reader = new BinaryReader(msg.m);
                msg.m.Position = 0;

                byte[] hash = Nodes.Hashd(msg.m.GetBuffer(), (int)msg.hdr.size);
                uint sum = (uint)(hash[0] | (hash[1] << 8) | (hash[2] << 16) | (hash[3] << 24));

                if (msg.hdr.sum != sum)
                {
                    Debug.Log("wrong version sum " + msg.m.GetBuffer().Length + " " + msg.hdr.sum.ToString("X8") + " " + sum.ToString("X8") + " " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(hash));
                }

                ver.proto_ver = reader.ReadUInt32();
                ver.services = reader.ReadUInt64();
                ver.timestamp = reader.ReadUInt64();
                ver.myAddr = readADDR(reader);
                ver.theirAddr = readADDR(reader);
                ver.nonce = reader.ReadUInt64();

                

                long ual = Nodes.readVINT(reader);

                StringBuilder sb = new StringBuilder();
                char[] user_agent = reader.ReadChars((int)ual);
                for (int n = 0; n < ual && user_agent[n] != 0; n++)
                {
                    sb.Append(user_agent[n]);
                }
                ver.user_agent = sb.ToString();
                ver.last_blk = reader.ReadUInt32();


                hasVer = true;

                Debug.Log("version message " + ver.proto_ver + " " + ver.user_agent + " " + ver.last_blk);

                ReceivePacketHDR();
            }
            else
                client.BeginReceive(msg.buffer, 0, (int)(msg.hdr.size- msg.m.Position), 0, new AsyncCallback(ReceiveVersionCallback), msg);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }


    }
    private void ReceiveVersion(messageHDR hdr)
    {
        // Create the state object.  
        MessageState version = new MessageState(hdr);

        // Begin receiving the data from the remote device.  
        client.BeginReceive(version.buffer, 0, version.BufferSize, 0, new AsyncCallback(ReceiveVersionCallback), version);
    }


    private void ReceiveHeadersCallBack(IAsyncResult ar)
    {
        
        try
        {
            recvHDR = false;

            // Retrieve the state object and the client socket
            MessageState msg = (MessageState)ar.AsyncState;
            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceiveHeadersCallBack error ");
                return;
            }

            msg.writer.Write(msg.buffer, 0, bytesRead);

            if (msg.m.Position >= msg.hdr.size)
            {
                var reader = new BinaryReader(msg.m);
                long count;
                msg.m.Position = 0;

                count = Nodes.readVINT(reader);

                for (int n = 0; n < count; n++)
                {
                    byte[] hdrBytes = reader.ReadBytes(80);
                    blockheader myhdr = Nodes.BytestoHDR(hdrBytes);
                    myhdr.nTx = (uint)Nodes.readVINT(reader);
                    myhdr.isPow = (reader.ReadByte() == 1) ? true : false;

                    lock (_blkLock)
                    {
                        RecvHeaders.Add(myhdr);
                    }
                }

                Debug.Log("get headers message ");

                ReceivePacketHDR();
            }
            else
                client.BeginReceive(msg.buffer, 0, (int)(msg.hdr.size - msg.m.Position), 0, new AsyncCallback(ReceiveHeadersCallBack), msg);

        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return;
        }
    }
    
    private void ReceiveHeaders(messageHDR hdr)
    {
        // Create the state object.  
        MessageState msg = new MessageState(hdr);
        // Begin receiving the data from the remote device.  
        client.BeginReceive(msg.buffer, 0, msg.BufferSize, 0, new AsyncCallback(ReceiveHeadersCallBack), msg);
    }
    private void ReceiveInventoryCallBack(IAsyncResult ar)
    {

        try
        {
           

            // Retrieve the state object and the client socket
            MessageState msg = (MessageState)ar.AsyncState;
            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceiveHeadersCallBack error ");
                return;
            }

            msg.writer.Write(msg.buffer, 0, bytesRead);

            if (msg.m.Position >= msg.hdr.size)
            {
                var reader = new BinaryReader(msg.m);
                long count;
                msg.m.Position = 0;
                string hashstr="";

                count = Nodes.readVINT(reader);

                //messageInventory inventory= new messageInventory(count);

                for (int n = 0; n < count; n++)
                {
                    messageInventoryHash h = new messageInventoryHash();
                    h.type = reader.ReadUInt32();
                    h.hash = reader.ReadBytes(32);

                    hashstr= hashstr + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(h.hash) + ",";

                    lock (_invLock)
                    {
                        inventory.Add(h);
                    }
                }

                Debug.Log("get inv message "+ hashstr);

                ReceivePacketHDR();
            }
            else
                client.BeginReceive(msg.buffer, 0, (int)(msg.hdr.size - msg.m.Position), 0, new AsyncCallback(ReceiveInventoryCallBack), msg);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return;
        }
    }

    private void ReceiveInventory(messageHDR hdr)
    {
        // Create the state object.  
        MessageState msg = new MessageState(hdr);
        // Begin receiving the data from the remote device.  
        client.BeginReceive(msg.buffer, 0, msg.BufferSize, 0, new AsyncCallback(ReceiveInventoryCallBack), msg);
    }

    private void ReceiveTxCallBack(IAsyncResult ar)
    {

        try
        {


            // Retrieve the state object and the client socket
            MessageState msg = (MessageState)ar.AsyncState;
            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceiveHeadersCallBack error ");
                return;
            }

            msg.writer.Write(msg.buffer, 0, bytesRead);

            if (msg.m.Position >= msg.hdr.size)
            {
                var reader = new BinaryReader(msg.m);
                msg.m.Position = 0;

                Tx transaction = Nodes.BytestoTX(msg.m.GetBuffer());
                transaction.hash = Nodes.Hash(Nodes.Hash(Nodes.TXtoBytes(transaction)));

                lock (_txLock)
                {
                    RecvTransactions.Add(transaction);
                }
                ReceivePacketHDR();

                Debug.Log("get tx message " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(transaction.hash));
            }
            else
                client.BeginReceive(msg.buffer, 0, (int)(msg.hdr.size - msg.m.Position), 0, new AsyncCallback(ReceiveTxCallBack), msg);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return;
        }
    }

    private void ReceiveTx(messageHDR hdr)
    {
        // Create the state object.  
        MessageState msg = new MessageState(hdr);
        // Begin receiving the data from the remote device.  
        client.BeginReceive(msg.buffer, 0, msg.BufferSize, 0, new AsyncCallback(ReceiveTxCallBack), msg);
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

                ReceivePacketHDR();
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

                ReceivePacketHDR();
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
    private void ReceiveAddrCallback(IAsyncResult ar)
    {
        messageAddrs addrs = new messageAddrs();

        try
        {
            // Retrieve the state object and the client socket
            MessageState msg = (MessageState)ar.AsyncState;
            int bytesRead = client.EndReceive(ar);
            if (bytesRead <= 0)
            {
                Debug.Log("ReceiveAddrCallback error ");
                return;
            }

            msg.writer.Write(msg.buffer, 0, bytesRead);

            if (msg.m.Position >= msg.hdr.size)
            {
                var reader = new BinaryReader(msg.m);
                msg.m.Position = 0;

                long cnt = Nodes.readVINT(reader);


                addrs.addrs = new messageAddrT[cnt];

                for(int n=0;n<cnt;n++)
                {
                    addrs.addrs[n] = readADDRT(reader);
                }

                Debug.Log("Addr message " + addrs.addrs[0].ip.ToString()+" "+ addrs.addrs[0].port);

                ReceivePacketHDR();
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            return;
        }

        
    }

    private void ReceiveAddr(messageHDR hdr)
    {
        try
        {
            // Create the state object.  
            MessageState msg = new MessageState(hdr);

            // Begin receiving the data from the remote device.  
            client.BeginReceive(msg.buffer, 0, msg.BufferSize, 0, new AsyncCallback(ReceiveAddrCallback), msg);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }
    private messageHDR createHDR(string cmd, MemoryStream m)
    {
        messageHDR hdr;

        byte[] hash = Nodes.Hashd(m.GetBuffer(), (int)m.Position);

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


    private void ReceiveUnknownCallback(IAsyncResult ar)
    {
        try
        {
            int bytesRead = client.EndReceive(ar);
            Debug.Log("recv unknown bytes ." + bytesRead);

            ReceivePacketHDR();
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

            if (bytesRead<=0)
            {
                Debug.Log("ReceiveCallback error "+address);
                return;
            }

            lastPingTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds() ;

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
                    case "mempool": ReceivePacketHDR(); break;
                    case "getaddr": ReceivePacketHDR(); break;
                    case "verack": ReceivePacketHDR(); SendPingMessage(); break;
                    case "ping": ReceivePing(hdr); break;
                    case "pong": ReceivePong(hdr); break;
                    case "headers": ReceiveHeaders(hdr); break;
                    case "inv": ReceiveInventory(hdr); break;
                    case "tx": ReceiveTx(hdr); break;
                    case "addr": ReceiveTx(hdr); break;
                    default: 
                        Debug.Log("unknown message : " + hdr.cmd);

                        MessageState unk = new MessageState(hdr);

                        client.BeginReceive(unk.buffer, 0, (int)unk.hdr.size, 0, new AsyncCallback(ReceiveVersionCallback), unk);

                        ReceivePacketHDR(); 
                    break;
                }
                
            }

        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }
}

public class appObj
{
    public uint type;
    public byte[] objData;
    public byte[] pubkey;
    public byte[] txh;

}


public class Nodes : MonoBehaviour
{
    public string seedNodeAdress = "nodix.eu";
    public ushort seedNodePort = 16819;

    private IPAddress myIP;
    static byte[] nullHash= new byte[32] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    public List<Node> NodesList;

    public static long nextGetHeaders = 0;

    uint block_height = 0;
    bool recvHDR = false;
    bool recvApps = false;
    bool sendingPos = false;

    vrRoom room;


    Applications apps;

    List<appObj> appObjs;

    IBPlusTreeKeyValueStorage<ComparableKeyOf<BigInteger>, ValueOf<blockheader>> blkstorage;
    IBPlusTreeKeyValueStorage<ComparableKeyOf<BigInteger>, ValueOf<Tx>> txstorage;
    IBPlusTreeKeyValueStorage<ComparableKeyOf<uint>, ValueOf<byte[]>> blksIdxtorage;

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


    public static byte[] Hashd(byte[] data, int l)
    {
        return Hash(HashL(data, l));
    }

    struct HMAC_SHA256_CTX
    {
        public Sha256Digest ictx;
        public Sha256Digest octx;
    }


    /* Initialize an HMAC-SHA256 operation with the given key. */
    static HMAC_SHA256_CTX HMAC_SHA256_Init(byte[] K, int Klen)
    {
        byte[] pad = new byte[64];
        byte[] khash = new byte[32];
        byte[] _k;


        HMAC_SHA256_CTX ctx=new HMAC_SHA256_CTX();

 
        /* If Klen > 64, the key is really SHA256(K). */
        if (Klen > 64) {
            _k = new byte[32];

            ctx.ictx = new Sha256Digest(); //SHA256_Init(&ctx->ictx);
            ctx.ictx.BlockUpdate(K, 0, Klen); //SHA256_Update(&ctx->ictx, K, Klen);
            ctx.ictx.DoFinal(khash, 0);//SHA256_Final(khash, &ctx->ictx);

            for(int n=0;n<32;n++)
            {
                _k[n] = khash[n];
            }

            Klen = 32;
        }
        else
        {
            _k = new byte[Klen];

            for (int n = 0; n < Klen; n++)
            {
                _k[n] = K[n];
            }

        }

        /* Inner SHA256 operation is SHA256(K xor [block of 0x36] || data). */
        ctx.ictx = new Sha256Digest(); //SHA256_Init(&ctx->ictx);
        for (int n = 0; n < 64; n++)
        {
            pad[n] = 0x36;
        }

        for (int i = 0; i < Klen; i++)
            pad[i] ^= _k[i];

        ctx.ictx.BlockUpdate(pad, 0, 64); //SHA256_Update(&ctx->ictx, pad, 64);

        /* Outer SHA256 operation is SHA256(K xor [block of 0x5c] || hash). */
        ctx.octx = new Sha256Digest(); //   SHA256_Init(&ctx->octx);
        // memset_c(pad, 0x5c, 64);
        for (int n = 0; n < 64; n++)
        {
            pad[n] = 0x5c;
        }

        for (int i = 0; i < Klen; i++)
        {
            pad[i] ^= _k[i];
        }

        ctx.octx.BlockUpdate(pad, 0, 64); //SHA256_Update(&ctx->octx, pad, 64);
        /* Clean the stack. */
        //memset_c(khash, 0, 32);
        for (int n = 0; n < 32; n++)
        {
            khash[n] = 0;
        }

        return ctx;
    }

    /* Add bytes to the HMAC-SHA256 operation. */
    static void HMAC_SHA256_Update(HMAC_SHA256_CTX ctx, byte[] bin, int len)
    {
        /* Feed data to the inner SHA256 operation. */
        ctx.ictx.BlockUpdate(bin, 0, len);
    }

    /* Finish an HMAC-SHA256 operation. */
    static void HMAC_SHA256_Final(byte[] digest, HMAC_SHA256_CTX ctx)
    {
        byte[] ihash = new byte[32];

        /* Finish the inner SHA256 operation. */
        ctx.ictx.DoFinal(ihash, 0); //SHA256_Final(ihash, &ctx->ictx);

        /* Feed the inner hash to the outer SHA256 operation. */
        ctx.octx.BlockUpdate(ihash, 0, 32); //SHA256_Update(&ctx->octx, ihash, 32);

        /* Finish the outer SHA256 operation. */
        ctx.octx.DoFinal(digest, 0);  //SHA256_Final(digest, &ctx->octx);

        /* Clean the stack. */
        // memset_c(ihash, 0, 32);
        for (int n = 0; n < 32; n++)
        {
            ihash[n] = 0;
        }

    }

    static byte[] be32enc(uint x)
    {
        byte[] p = new byte[4];
        p[3] = (byte)(x & 0xff);
        p[2] = (byte)((x >> 8) & 0xff);
        p[1] = (byte)((x >> 16) & 0xff);
        p[0] = (byte)((x >> 24) & 0xff);

        return p;
    }

    /**
    * PBKDF2_SHA256(passwd, passwdlen, salt, saltlen, c, buf, dkLen):
    * Compute PBKDF2(passwd, salt, c, dkLen) using HMAC-SHA256 as the PRF, and
    * write the output to buf.  The value dkLen must be at most 32 * (2^32 - 1).
    */
    static void PBKDF2_SHA256(byte[] passwd, int passwdlen, byte[] salt, int saltlen, ulong c, uint[] buf, int dkLen)
    {
        HMAC_SHA256_CTX PShctx, hctx;
        int i;
        byte[] ivec = new byte[4];
        byte[] U = new byte[32];
        byte[] T = new byte[32];
        ulong j;
        int k;
        int clen;

        PShctx = HMAC_SHA256_Init(passwd, passwdlen); 
        hctx = new HMAC_SHA256_CTX();

        /* Compute HMAC state after processing P and S. */
        
        HMAC_SHA256_Update(PShctx, salt, saltlen);

        /* Iterate through the blocks. */
        for (i = 0; i * 32 < dkLen; i++)
        {
            /* Generate INT(i + 1). */
            ivec = be32enc((uint)(i + 1));

            /* Compute U_1 = PRF(P, S || INT(i)). */
            //memcpy_c(&hctx, &PShctx, sizeof(HMAC_SHA256_CTX));
            hctx.ictx = new Sha256Digest(PShctx.ictx);
            hctx.octx = new Sha256Digest(PShctx.octx);


            HMAC_SHA256_Update(hctx, ivec, 4);
            HMAC_SHA256_Final(U, hctx);

            /* T_i = U_1 ... */

            for (int n = 0; n < 32; n++)
                T[n] = U[n];

            for (j = 2; j <= c; j++)
            {
                /* Compute U_j. */
                hctx=HMAC_SHA256_Init(passwd, passwdlen);
                HMAC_SHA256_Update(hctx, U, 32);
                HMAC_SHA256_Final(U, hctx);

                /* ... xor U_j ... */
                for (k = 0; k < 32; k++)
                    T[k] ^= U[k];
            }

            /* Copy as many bytes as necessary into buf. */
            clen = dkLen - i * 32;
            if (clen > 32)
                clen = 32;

            for (int n = 0; n < (clen/4); n++)
            {
                buf[i * 8 + n] = (uint)((T[n * 4 + 0]) | ( T[n * 4 + 1] <<8 ) | ( T[n * 4 + 2] << 16) | ( T[n * 4 +3] << 24));
            }
                

            //memcpy_c(&buf[i * 32], T, clen);
        }

        /* Clean PShctx, since we never called _Final on it. */
        //memset_c(&PShctx, 0, sizeof(HMAC_SHA256_CTX));
        PShctx.ictx = null;
        PShctx.octx = null;


    }

    /*
    #if defined (OPTIMIZED_SALSA) && ( defined (__x86_64__) || defined (__i386__) || defined(__arm__) )
    extern "C" void scrypt_core(unsigned int *X, unsigned int *V);
    #else
    */
    // Generic scrypt_core implementation
    //#define R(a, b) (((a) << (b)) | ((a) >> (32 - (b))))
    static uint R(uint a, byte b)
    {
        return (((a) << (b)) | ((a) >> (32 - (b))));
    }

    static void xor_salsa8(uint[] B, uint[] Bx)
    {
        uint x00, x01, x02, x03, x04, x05, x06, x07, x08, x09, x10, x11, x12, x13, x14, x15;
        int i;

        x00 = (B[0] ^= Bx[0]);
        x01 = (B[1] ^= Bx[1]);
        x02 = (B[2] ^= Bx[2]);
        x03 = (B[3] ^= Bx[3]);
        x04 = (B[4] ^= Bx[4]);
        x05 = (B[5] ^= Bx[5]);
        x06 = (B[6] ^= Bx[6]);
        x07 = (B[7] ^= Bx[7]);
        x08 = (B[8] ^= Bx[8]);
        x09 = (B[9] ^= Bx[9]);
        x10 = (B[10] ^= Bx[10]);
        x11 = (B[11] ^= Bx[11]);
        x12 = (B[12] ^= Bx[12]);
        x13 = (B[13] ^= Bx[13]);
        x14 = (B[14] ^= Bx[14]);
        x15 = (B[15] ^= Bx[15]);
        for (i = 0; i < 8; i += 2)
        {
            /* Operate on columns. */
            x04 ^= R(x00 + x12, 7); x09 ^= R(x05 + x01, 7);
            x14 ^= R(x10 + x06, 7); x03 ^= R(x15 + x11, 7);

            x08 ^= R(x04 + x00, 9); x13 ^= R(x09 + x05, 9);
            x02 ^= R(x14 + x10, 9); x07 ^= R(x03 + x15, 9);

            x12 ^= R(x08 + x04, 13); x01 ^= R(x13 + x09, 13);
            x06 ^= R(x02 + x14, 13); x11 ^= R(x07 + x03, 13);

            x00 ^= R(x12 + x08, 18); x05 ^= R(x01 + x13, 18);
            x10 ^= R(x06 + x02, 18); x15 ^= R(x11 + x07, 18);

            /* Operate on rows. */
            x01 ^= R(x00 + x03, 7); x06 ^= R(x05 + x04, 7);
            x11 ^= R(x10 + x09, 7); x12 ^= R(x15 + x14, 7);

            x02 ^= R(x01 + x00, 9); x07 ^= R(x06 + x05, 9);
            x08 ^= R(x11 + x10, 9); x13 ^= R(x12 + x15, 9);

            x03 ^= R(x02 + x01, 13); x04 ^= R(x07 + x06, 13);
            x09 ^= R(x08 + x11, 13); x14 ^= R(x13 + x12, 13);

            x00 ^= R(x03 + x02, 18); x05 ^= R(x04 + x07, 18);
            x10 ^= R(x09 + x08, 18); x15 ^= R(x14 + x13, 18);

        }
        B[0] += x00;
        B[1] += x01;
        B[2] += x02;
        B[3] += x03;
        B[4] += x04;
        B[5] += x05;
        B[6] += x06;
        B[7] += x07;
        B[8] += x08;
        B[9] += x09;
        B[10] += x10;
        B[11] += x11;
        B[12] += x12;
        B[13] += x13;
        B[14] += x14;
        B[15] += x15;
    }

    static void scrypt_core(uint[] X)
    {
        uint i, j, k;
        uint[] X1 = new uint[16], X2 = new uint[16];

        for (i = 0; i < 1024; i++)
        {

            //memcpy_c(&V[i * 32], X, 128);
            for (int n = 0; n < 32; n++)
                scratchpad[i * 32 + n] = X[n];

            for (int n = 0; n < 16; n++)
            {
                X1[n] = X[n];
                X2[n] = X[n + 16];
            }

            xor_salsa8(X1, X2);
            xor_salsa8(X2, X1);

            for (int n = 0; n < 16; n++)
            {
                X[n] = X1[n];
                X[n + 16] = X2[n];
            }
        }
        for (i = 0; i < 1024; i++)
        {
            j = 32 * (X[16] & 1023);
            for (k = 0; k < 32; k++)
                X[k] ^= scratchpad[j + k];

            for (int n = 0; n < 16; n++)
            {
                X1[n] = X[n];
                X2[n] = X[n + 16];
            }
            xor_salsa8(X1, X2);
            xor_salsa8(X2, X1);

            for (int n = 0; n < 16; n++)
            {
                X[n] = X1[n];
                X[n + 16] = X2[n];
            }
        }
    }

    /* cpu and memory intensive function to transform a 80 byte buffer into a 32 byte output
    scratchpad size needs to be at least 63 + (128 * r * p) + (256 * r + 64) + (128 * r * N) bytes
    r = 1, p = 1, N = 1024
    */

    static int SCRYPT_BUFFER_SIZE = (131072);
    static uint[] scratchpad= new uint[SCRYPT_BUFFER_SIZE];
    

    static byte[] scrypt_nosalt(byte[] input, int inputlen)
    {
        uint[] X = new uint[32];
        uint[] iresult = new uint[8];
        byte[] result = new byte[32];


        PBKDF2_SHA256(input, inputlen, input, inputlen, 1, X, 128);
        scrypt_core(X);

        byte[] bX = new byte[X.Length * sizeof(uint)];
        Buffer.BlockCopy(X, 0, bX, 0, bX.Length);

        PBKDF2_SHA256(input, inputlen, bX, 128, 1, iresult, 32);

        Buffer.BlockCopy(iresult, 0, result, 0, result.Length);

        return result;
    }


    static byte[] scrypt_blockhash(byte[] input)
    {

        for(int n=0;n< SCRYPT_BUFFER_SIZE;n++)
            scratchpad[n]= 0;

	    return scrypt_nosalt(input, 80);
    }


    public static byte[] blockPOW(byte[] data)
    {
        return scrypt_blockhash(data);
        //return SCrypt.Generate(data, data, 1024, 1, 1, 32);
        //return SCrypt.DeriveKey(data,data,1024,1,1,32);
    }


    public static long readVINT(BinaryReader r)
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

    public static void writeVINT(ulong n, BinaryWriter w)
    {
        if (n < 0xFD)
            w.Write((byte)n);
        else if (n < UInt16.MaxValue)
        {
            w.Write((byte)0xFD);
            w.Write((ushort)(n));
        }
        else if (n < UInt32.MaxValue)
        {
            w.Write((byte)0xFE);
            w.Write((uint)(n));
        }
        else
        {
            w.Write((byte)0xFF);
            w.Write(n);
        }

    }
    public static byte[] HDRtoBytes(blockheader hdr)
    {
        MemoryStream buffer = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(buffer);

        writer.Write(hdr.version);
        writer.Write(hdr.prev);
        writer.Write(hdr.merkle_root);
        writer.Write(hdr.time);
        writer.Write(hdr.bits);
        writer.Write(hdr.nonce);

        return buffer.ToArray();
    }



    public static blockheader BytestoHDR(byte[] data)
    {
        blockheader hdr = new blockheader();
        MemoryStream buffer = new MemoryStream(data);
        BinaryReader reader = new BinaryReader(buffer);

        hdr.version = reader.ReadUInt32();
        hdr.prev = reader.ReadBytes(32);
        hdr.merkle_root = reader.ReadBytes(32);

        hdr.time = reader.ReadUInt32();
        hdr.bits = reader.ReadUInt32();
        hdr.nonce = reader.ReadUInt32();

        return hdr;
    }

    public static Tx BytestoTX(byte[] data)
    {
        Tx tx = new Tx();
        MemoryStream buffer = new MemoryStream(data);
        BinaryReader reader = new BinaryReader(buffer);
        long cnt;

        tx.version = reader.ReadUInt32();
        tx.time = reader.ReadUInt32();

        cnt = Nodes.readVINT(reader);

        tx.inputs = new TransactionInput[cnt];

        for(int n=0;n<cnt;n++)
        {
            long len;

            tx.inputs[n].txid = reader.ReadBytes(32);
            tx.inputs[n].utxo = reader.ReadUInt32();

            len= Nodes.readVINT(reader);
            tx.inputs[n].script = reader.ReadBytes((int)len);
            tx.inputs[n].sequence = reader.ReadUInt32();
        }

        cnt = Nodes.readVINT(reader);

        tx.outputs = new TransactionOutput[cnt];

        for (int n = 0; n < cnt; n++)
        {
            long len;
            tx.outputs[n].amount = reader.ReadUInt64();
            len = Nodes.readVINT(reader);
            tx.outputs[n].script = reader.ReadBytes((int)len);
        }

        tx.locktime= reader.ReadUInt32();

        return tx;
    }

    public static byte[] TXtoBytes(Tx tx)
    {
        MemoryStream buffer = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(buffer);
        
        writer.Write(tx.version);
        writer.Write(tx.time);

        Nodes.writeVINT((ulong)tx.inputs.Length, writer);

        for (int n = 0; n < tx.inputs.Length; n++)
        {
            writer.Write(tx.inputs[n].txid);
            writer.Write(tx.inputs[n].utxo);
            Nodes.writeVINT((ulong)tx.inputs[n].script.Length, writer);
            writer.Write(tx.inputs[n].script);
            writer.Write(tx.inputs[n].sequence);
        }

        Nodes.writeVINT((ulong)tx.outputs.Length, writer);


        for (int n = 0; n < tx.outputs.Length; n++)
        {
            writer.Write(tx.outputs[n].amount);
            Nodes.writeVINT((ulong)tx.outputs[n].script.Length, writer);
            writer.Write(tx.outputs[n].script);
        }

        writer.Write(tx.locktime);

        byte[] ret = new byte[buffer.Position];

        Buffer.BlockCopy(buffer.ToArray(), 0, ret, 0, (int)buffer.Position);

        return ret;
    }


    public List<byte[]> block_locator_indexes()
    {
        byte[] hash;
        int index;
        int step = 1;
        List<byte[]> hash_list;
        uint cnt = 0;

        hash = new byte[32];
        hash_list = new List<byte[]>();

        for (index = (int)block_height; index > 1; index -= step)
        {
            hash = blksIdxtorage.Get((uint)index);

            hash_list.Add(hash);

            // Push top 10 indexes first, then back off exponentially.
            cnt++;
            if (cnt == 10)
            {
                step = step * 2;
                cnt = 0;
            }
        }
        //  Push the genesis block index.

        if(blksIdxtorage != null)
        {
            hash = blksIdxtorage.Get(0);
            hash_list.Add(hash);
        }

        return hash_list;
    }

    void testStore()
    {
        BigInteger key;
        blockheader hdr = new blockheader();
        blockheader hdr2 = new blockheader();
        var rand = new System.Random();
        byte[] myhash = new byte[32];


        rand.NextBytes(myhash);
        key = new BigInteger(myhash);

        hdr.version = 18;
        hdr.prev = new byte[32];
        hdr.merkle_root = new byte[32];
        hdr.time = 45;
        hdr.bits = 58;
        hdr.nonce = 72;

        blkstorage.Set(key, hdr);

        hdr2 = blkstorage.Get(key);
    }


    void makeGenesisBlock()
    {
        byte[] genesisHash= new byte[32] { 0x88, 0xfc, 0x41, 0xf6, 0x04, 0xa1, 0x95, 0x15, 0xcd, 0x30, 0xd3, 0x31, 0x75, 0xfd, 0x4d, 0xe7, 0xa8, 0xf0, 0xd1, 0x8d, 0xb7, 0x5d, 0x57, 0xa5, 0xd3, 0xc8, 0x6c, 0x16, 0xba, 0x0a, 0x00, 0x00 };
        blksIdxtorage.Set(0, genesisHash);

    }
    // Start is called before the first frame update
    void Start()
    {
        byte[] myip = new byte[4];
        myIP = new IPAddress(myip);


        appObjs = new List<appObj>();
        scratchpad = new uint[SCRYPT_BUFFER_SIZE];

        sendingPos = false;


        loadApps();

        NodesList = new List<Node>();
        NodesList.Add(new Node(seedNodeAdress, seedNodePort, true, myIP));

        if(!System.IO.Directory.Exists(Application.persistentDataPath + "/blks"))
            System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/blks");

        if (!System.IO.Directory.Exists(Application.persistentDataPath + "/blksidx"))
            System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/blksidx");
        
        if (!System.IO.Directory.Exists(Application.persistentDataPath + "/tx"))
            System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/tx");


        var settings = BPlusTreeStorageSettings.Default(32); // use default settings with 32-byte keys
        settings.AutoFlushTimeout = TimeSpan.FromMilliseconds(50);

        blkstorage = new StorageFactory().CreateBPlusTreeStorage<BigInteger, blockheader>(
                p => HDRtoBytes(p),      // value serialization
                p => BytestoHDR(p),     // value deserialization
                settings);


        blkstorage.OpenOrCreate(Application.persistentDataPath + "/blks");

        var settings2 = BPlusTreeStorageSettings.Default(4); // use default settings with 32-byte keys
        settings2.AutoFlushTimeout = TimeSpan.FromMilliseconds(50);

        blksIdxtorage = new StorageFactory().CreateBPlusTreeStorage<uint, byte[]>(
        p => p,      // value serialization
        p => p,     // value deserialization
        settings2);

        blksIdxtorage.OpenOrCreate(Application.persistentDataPath + "/blksidx");

        var settings3 = BPlusTreeStorageSettings.Default(32); // use default settings with 32-byte keys
        settings3.AutoFlushTimeout = TimeSpan.FromMilliseconds(50);

        txstorage = new StorageFactory().CreateBPlusTreeStorage<BigInteger, Tx>(
        p => TXtoBytes(p),      // value serialization
        p => BytestoTX(p),     // value deserialization
        settings3);

        txstorage.OpenOrCreate(Application.persistentDataPath + "/tx");

        if (block_height == 0)
            makeGenesisBlock();

        block_height = (uint)blksIdxtorage.Count() - 1;

        nextGetHeaders = System.DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1000;

        /*testStore();*/
    }

    public void setRoom(vrRoom room)
    {
        this.room = room;
    }

    public void addNode(string address, ushort port)
    {
        NodesList.Add(new Node(address, port, false, myIP));
    }

    public static bool isNullHash(byte[] h)
    {
        if (h == null)
            return true;

        if (compareHash(h, Nodes.nullHash) == 0)
            return true;

        return false;

    }
    public static int compareHash(byte [] h1, byte[] h2)
    {
        if (h1.Length != 32)
            return 0;
        if (h1.Length != h2.Length)
            return 0;

        for(int n=0;n<32;n++)
        {
            if (h1[n] < h2[n])
                return -1;
            else if (h1[n] > h2[n])
                return 1;

        }

        return 0;
    }

    string getScriptVar(byte[] b, int offset, out int nofs)
    {
        int len;

        if (b[offset] < 0xFD)
        {
            len = b[offset];
            offset++;
        }
        else if (b[offset] < 0xFE)
        {
            len = (int)((b[offset + 1]) | (b[offset + 2] << 8));
            offset += 3;
        }
        else if (b[offset] < 0xFF)
        {
            len = (int)((b[offset + 1]) | (b[offset + 2] << 8) | (b[offset + 3] << 16) | (b[offset + 3] << 24));
            offset += 5;
        }
        else
        {
            nofs = offset;
            return null;
        }

        if ((offset + len) > b.Length)
        {
            nofs = offset;
            return null;
        }

        nofs = offset+ len;

        return System.Text.Encoding.UTF8.GetString(b, offset, (int)len);
    }

    string getScriptData(byte[] b, int offset, out int nofs)
    {
        int len;

        if (b[offset] == 0x4C)
        {
            offset++;
            len = b[offset];
            offset++;
        }
        else if (b[offset] == 0x4D)
        {
            offset++;
            len = (int)((b[offset + 1]) | (b[offset + 2] << 8));
            offset += 2;
        }
        else if (b[offset] == 0x4E)
        {
            offset++;
            len = (int)((b[offset + 1]) | (b[offset + 2] << 8) | (b[offset + 3] << 16) | (b[offset + 3] << 24));
            offset += 5;
        }
        else
        {
            nofs = offset;
            return null;
        }

        if ((offset + len) > b.Length)
        {
            nofs = offset;
            return null;
        }

        nofs = offset + len;

        return System.Text.Encoding.UTF8.GetString(b, offset, (int)len);
    }


    bool get_type_infos(byte []script, out ApplicationTypeEntry tkey)
    {
        tkey = new ApplicationTypeEntry();
	    int offset = 0, next;


        tkey.key = getScriptVar(script, offset, out next);
	    if ((tkey.key.Length < 3) || (tkey.key.Length > 32))
        {
            return false;
        }

        offset = next;

        switch(script[offset])
         {
            case 4:
                tkey.type = (uint)((script[offset + 1]) | (script[offset + 2] << 8) | (script[offset + 2] << 16) | (script[offset + 2] << 24));
                offset += 5;
            break;
            case 2:
                tkey.type = (uint)((script[offset + 1]) | (script[offset + 2] << 8));
                offset += 3;
                break;
            case 1:
                tkey.type = (uint)(script[offset + 1]);
                offset += 2;
                break;
            default:
                return false;
        }

        if(script[offset]==0)
        {
            tkey.flags = 0;
        }
        else if (script[offset] == 1)
        {
            tkey.flags = script[offset+1];
        }
        else
        {
            return false;
        }

        return true;
    }

    public string getBlockSync()
    {
        uint max = 0;
        for (int n = 0; n < NodesList.Count; n++)
        {
            if (NodesList[n].getLastBlock() > max)
                max = NodesList[n].getLastBlock();
        }

        return "sync :" + block_height.ToString() + " / " + max;
    }


    public void saveApps()
    {
        string basePath = Application.persistentDataPath + "/";

        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);

        XmlSerializer serializer = new XmlSerializer(typeof(Applications));
        StreamWriter writer = new StreamWriter(basePath + "/apps.xml");
        serializer.Serialize(writer.BaseStream, apps);
        writer.Close();

    }

    public void loadApps()
    {
        string basePath = Application.persistentDataPath + "/";

        if (File.Exists(basePath + "/apps.xml"))
        {

            XmlSerializer serializer = new XmlSerializer(typeof(Applications));
            StreamReader reader = new StreamReader(basePath + "/apps.xml");
            apps = (Applications)serializer.Deserialize(reader.BaseStream);
            reader.Close();
        }
        else
        {
            apps = new Applications();
        }

    }
    bool setAppRoot(Tx tx)
    {
        apps.root.hash = tx.hash;
        saveApps();
        return true;
    }



    bool addApp(Tx tx , string appName)
    {

        if(apps.root.applications == null)
        {
            apps.root.applications = new NodixApplication[1];
            apps.root.applications[0] = new NodixApplication();
            apps.root.applications[0].hash = tx.hash;
            apps.root.applications[0].name = appName;
            return true;
        }

        for (int n = 0; n < apps.root.applications.Length; n++)
        {
            if (apps.root.applications[n].hash == tx.hash)
                return true;
        }

        NodixApplication[] napps = new NodixApplication[apps.root.applications.Length + 1];
        for (int n = 0; n < apps.root.applications.Length; n++)
        {
            napps[n] = apps.root.applications[n];
        }

        napps[apps.root.applications.Length] = new NodixApplication();
        napps[apps.root.applications.Length].hash = tx.hash;
        napps[apps.root.applications.Length].name = appName;
        apps.root.applications = napps;



        saveApps();

        return true;
    }

    NodixApplication findNodixApp(string name)
    {
        if (apps.root == null)
            return null;

        if (apps.root.applications == null)
            return null;

        for (int n=0;n<apps.root.applications.Length;n++)
        {
            if (apps.root.applications[n].name == name)
                return apps.root.applications[n];
        }

        return null;
    }

    NodixApplication isAppItem(TransactionInput tx)
    {
        if (apps.root == null)
            return null;

        if (apps.root.applications == null)
            return null;

        for (int n = 0; n < apps.root.applications.Length; n++)
        {
            if(compareHash(tx.txid, apps.root.applications[n].hash)==0)
            {
                return apps.root.applications[n];
            }
        }

        return null;
    }
    ApplicationType parseAppType(Tx tx)
    {
        ApplicationType newType = new ApplicationType();

        newType.entries = new ApplicationTypeEntry[tx.outputs.Length - 1];

        for (int ni = 0; ni < tx.outputs.Length; ni++)
        {
            ApplicationTypeEntry key;
            if (ni == 0)
            {
                int next;
                newType.name = getScriptVar(tx.outputs[ni].script, 0, out next);

                if (tx.outputs[ni].script[next] == 0)
                {
                    newType.type = 0;
                }
                else if (tx.outputs[ni].script[next] == 1)
                {
                    newType.type = (uint)(tx.outputs[ni].script[next + 1]);
                }
                else if (tx.outputs[ni].script[next] == 2)
                {
                    newType.type = (uint)((tx.outputs[ni].script[next + 1]) | (tx.outputs[ni].script[next + 2] << 8));
                }
                else if (tx.outputs[ni].script[next] == 4)
                {
                    newType.type = (uint)((tx.outputs[ni].script[next + 1]) | (tx.outputs[ni].script[next + 2] << 8) | (tx.outputs[ni].script[next + 3] << 16) | (tx.outputs[ni].script[next + 4] << 24));
                }
                continue;
            }

            if (tx.outputs[ni].amount != 0)
                continue;

            if (get_type_infos(tx.outputs[ni].script, out key))
            {
                newType.entries[ni - 1] = key;
            }
        }

        return newType;
    }

    ApplicationType findAppType(NodixApplication app, uint typeid)
    {
        if (apps.root == null)
            return null;

        if (apps.root.applications == null)
            return null;

        for (int n = 0; n < app.types.Length; n++)
        {
            if (app.types[n].type == typeid)
            {
                return app.types[n];
            }
        }

        return null;
    }

    appObj findAppObj(byte[] hash)
    {
        for(int n=0;n<appObjs.Count;n++)
        {
            if(compareHash(appObjs[n].txh,hash)==0)
                return appObjs[n];
        }

        BigInteger k = new BigInteger(hash);

        if (txstorage.Exists(k))
        {
            TxInfos infos;
            Tx tx = txstorage.Get(k);

            tx.hash = hash;

            if(ProcessTx(tx,out infos))
                return infos.obj;
        }


        return null;
    }

    bool ProcessTx(Tx tx,out TxInfos infos)
    {
        TxInfos txi= new TxInfos();

        txi.app = null;
        txi.obj = null;
        txi.childOf = null;
        txi.child = null;
        txi.item = 0;

        for (int ni = 0; ni < tx.inputs.Length; ni++)
        {
            if (compareHash(tx.inputs[ni].txid, Nodes.nullHash) ==0 )
            {
                if(tx.inputs[ni].utxo >= 0xFFFF)
                {
                    setAppRoot(tx);
                }
            }
            else if ((apps.root!=null)&&(apps.root.hash != null)&&(compareHash(tx.inputs[ni].txid, apps.root.hash) == 0))
            {
                int next;
                string appName = getScriptVar(tx.inputs[ni].script, 0, out next);
                addApp(tx, appName);

                if(appName == "UnityApp")
                    recvApps = false;
            }
            else if((txi.app = isAppItem(tx.inputs[ni])) != null)
            {
                switch(tx.inputs[ni].utxo)
                {
                    case 0:
                        txi.item = 1;
                    break;
                    case 1:
                        txi.item = 2;
                    break;
                    case 2:
                        txi.item = 3;
                    break;
                    case 3:
                        txi.item = 4;
                    break;
                    case 4:
                        txi.item = 5;
                    break;
                    case 5:
                        txi.item = 6;
                    break;
                    default:
                        infos = txi;
                        return false;

                }
                Debug.Log("app Item " + txi.app.name +" "+ txi.item);
            }
            else if((txi.childOf = findAppObj(tx.inputs[ni].txid)) != null)
            {
                Debug.Log("new child of " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(tx.inputs[ni].txid));
            }
        }

        if (txi.app != null)
        {
            switch(txi.item )
            {
                case 1:
                    ApplicationType newType = parseAppType(tx);

                    if (txi.app.types == null)
                    {
                        txi.app.types = new ApplicationType[1];
                        txi.app.types[0] = newType;
                    }
                    else
                    {
                        ApplicationType[] at = new ApplicationType[txi.app.types.Length + 1];

                        for (int n = 0; n < txi.app.types.Length; n++)
                        {
                            at[n] = txi.app.types[n];
                        }
                        at[txi.app.types.Length] = newType;
                        txi.app.types = at;
                    }
                    saveApps();
                break;
                case 2:
                    for (int ni = 0; ni < tx.outputs.Length; ni++)
                    {
                        if((tx.outputs[ni].amount & 0xFFFFFFFF00000000)== 0xFFFFFFFF00000000)
                        {
                            uint typeid = (uint)(tx.outputs[ni].amount & 0xFFFFFFFF);
                            byte opcode;
                            long tlen;
                            ApplicationType type = findAppType(txi.app, typeid);
                            if (type == null)
                            {
                                infos = txi;
                                return false;
                            }
                                

                            MemoryStream buffer = new MemoryStream(tx.outputs[ni].script);
                            BinaryReader reader = new BinaryReader(buffer);

                            int pkLen = reader.ReadByte();

                            if (pkLen != 33)
                            {
                                infos = txi;
                                return false;
                            }

                            txi.obj = new appObj();
                            txi.obj.pubkey = reader.ReadBytes(33);
                            txi.obj.type = typeid;
                            txi.obj.txh = tx.hash;
                            

                            //user.addr = WalletAddress.pub2addr(new ECPublicKeyParameters(domainParams.Curve.DecodePoint(user.pkey), domainParams));

                            opcode = reader.ReadByte();
                            if (opcode != 0xAC)
                            {
                                infos = txi;
                                return false;
                            }

                            opcode = reader.ReadByte();

                            if (opcode != 0x6A)
                            {
                                infos = txi;
                                return false;
                            }

                            opcode = reader.ReadByte();


                            if (opcode == 0x4c)
                                tlen = (long)reader.ReadByte();
                            else if (opcode == 0x4d)
                                tlen = (long)reader.ReadUInt16();
                            else if (opcode == 0x4e)
                                tlen = (long)reader.ReadUInt32();
                            else
                            {
                                infos = txi;
                                return false;
                            }


                            txi.obj.objData = new byte[tlen];

                            Buffer.BlockCopy(tx.outputs[ni].script, (int)buffer.Position, txi.obj.objData, 0, (int)tlen);

                            appObjs.Add(txi.obj);

                            Debug.Log("new app object " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(tx.hash));

                            //room.newObj(typeid, tx.outputs[ni].script);


                        }
                    }
                break;
            }
        }
        else if (txi.childOf != null)
        {
            MemoryStream buffer = new MemoryStream(tx.outputs[0].script);
            BinaryReader reader = new BinaryReader(buffer);

            int offset=0, next=0;

            string key = getScriptData(tx.outputs[0].script, 0, out next);
            offset = next ;

            if (tx.outputs[0].script[offset] != 0x4C)
            {
                infos = txi;
                return false;
            }

            if (tx.outputs[0].script[offset+1] != 32)
            {
                infos = txi;
                return false;
            }

            offset += 2;

            byte[] childHash = new byte[32];
            Buffer.BlockCopy(tx.outputs[0].script, offset, childHash, 0, 32);
            txi.child = findAppObj(childHash);

            if(txi.child != null)
                Debug.Log("new child " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(childHash));
            else
                Debug.Log("new child not found " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(childHash));

        }
        infos = txi;
        return true;
    }

    public void GetTx(byte[] hash)
    {
        BigInteger k = new BigInteger(hash);
        if (txstorage.Exists(k))
        {
            TxInfos infos;
            Tx tx = txstorage.Get(k);
            tx.hash = Hash(Hash(TXtoBytes(tx)));
            ProcessTx(tx,out infos);
            return;
        }

        byte[] rh = new byte[32];

        for (int n = 0; n < 32; n++)
        {
            rh[n] = room.roomHash[31 - n];
        }

        messageInventoryHash[] hashList = new messageInventoryHash[1];
        hashList[0].hash = rh;
        hashList[0].type = 1;

        NodesList[0].SendGetDataMessage(hashList);

    }

    bool isSync()
    {
        uint max = 0;
        if (NodesList.Count == 0)
            return false;

        for (int n = 0; n < NodesList.Count; n++)
        {
            if (NodesList[n].getLastBlock() > max)
                max = NodesList[n].getLastBlock();
        }

        if (block_height + 1 >= max)
            return true;
        else
            return false;
    }

    byte[] objScript(byte[] pubkey, byte[] obj)
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write((byte)33);
        writer.Write(pubkey);
        writer.Write((byte)0xAC);
        writer.Write((byte)0x6A);

        if (obj.Length < 256)
        {
            writer.Write((byte)0x4C);
            writer.Write((byte)obj.Length);
        }
        else if (obj.Length < 65536)
        {
            writer.Write((byte)0x4D);
            writer.Write((byte)obj.Length);
        }
        writer.Write(obj);

        byte[] script = new byte[m.Position];
        Buffer.BlockCopy(m.ToArray(), 0, script, 0, (int)m.Position);

        return script;
    }
    byte[] objchildScript(string key, byte[] hash)
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        writer.Write((byte)0x4C);
        writer.Write((byte)key.Length);
        writer.Write(key.ToCharArray());
        writer.Write((byte)0x4C);
        writer.Write((byte)32);
        writer.Write(hash);

        byte[] script = new byte[m.Position];
        Buffer.BlockCopy(m.ToArray(), 0, script, 0, (int)m.Position);

        return script;
    }



    byte[] computeTxSignHash(Tx tx, int i, byte[] script,uint hashtype)
    {
        MemoryStream buffer = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(buffer);

        writer.Write(tx.version);
        writer.Write(tx.time);

        Nodes.writeVINT((ulong)tx.inputs.Length, writer);

        for (int n = 0; n < tx.inputs.Length; n++)
        {
            writer.Write(tx.inputs[n].txid);
            writer.Write(tx.inputs[n].utxo);
            

            if (i == n)
            {
                Nodes.writeVINT((ulong)script.Length, writer);
                writer.Write(script);
            }
            else
            {
                writer.Write((byte)0);
            }
                

            writer.Write(tx.inputs[n].sequence);
        }
        Nodes.writeVINT((ulong)tx.outputs.Length, writer);

        for (int n = 0; n < tx.outputs.Length; n++)
        {
            writer.Write(tx.outputs[n].amount);
            Nodes.writeVINT((ulong)tx.outputs[n].script.Length, writer);
            writer.Write(tx.outputs[n].script);
        }

        writer.Write(tx.locktime);
        writer.Write((uint)hashtype);

        return Hashd(buffer.GetBuffer(), (int)buffer.Position);
    }
    
    public bool isSendingPos()
    {
        return sendingPos;
    }

    public void sendroomUserTx(byte[] roomHash,roomUser user, WalletAddress mykey, ECDomainParameters domainParams)
    {
        NodixApplication app = findNodixApp("UnityApp");

        if (app == null)
            return;

        sendingPos = true;

        ApplicationType UserType = findAppType(app, 0x1E00002B);
        Tx transaction = new Tx();
        Tx ctransaction = new Tx();

        transaction.version = 1;
        transaction.time = (uint)System.DateTimeOffset.Now.ToUnixTimeSeconds();

        transaction.inputs = new TransactionInput[1];
        transaction.outputs = new TransactionOutput[1];

        transaction.inputs[0].txid = app.hash;
        transaction.inputs[0].utxo = 1;
        transaction.inputs[0].sequence = 0xFFFFFFFF;

        transaction.outputs[0].amount = 0xFFFFFFFF00000000 | 0x1E00002B;
        transaction.outputs[0].script = objScript(user.pkey, room.userToBytes(user));

        transaction.locktime = 0xFFFFFFFF;


        byte[] sh = computeTxSignHash(transaction, 0, transaction.outputs[0].script, 1);
        byte[] sign = mykey.Sign(sh, domainParams);
        transaction.inputs[0].script = new byte[sign.Length + 1];
        transaction.inputs[0].script[0] = (byte)sign.Length;
        Buffer.BlockCopy(sign, 0, transaction.inputs[0].script, 1, sign.Length);

        transaction.hash = Hash(Hash(TXtoBytes(transaction)));

        ctransaction.version = 1;
        ctransaction.time = (uint)System.DateTimeOffset.Now.ToUnixTimeSeconds();

        ctransaction.inputs = new TransactionInput[1];
        ctransaction.outputs = new TransactionOutput[1];

        ctransaction.inputs[0].txid = roomHash;
        ctransaction.inputs[0].utxo = 0;
        ctransaction.inputs[0].sequence = 0xFFFFFFFF;

        ctransaction.outputs[0].amount = 0;
        ctransaction.outputs[0].script = objchildScript("users", transaction.hash);

        ctransaction.locktime = 0xFFFFFFFF;

        byte[] csh = computeTxSignHash(ctransaction, 0, transaction.outputs[0].script, 1);
        byte[] csign = mykey.Sign(csh, domainParams);

        ctransaction.inputs[0].script = new byte[csign.Length + 1 + 35];
        ctransaction.inputs[0].script[0] = (byte)(csign.Length+1);
        Buffer.BlockCopy(csign, 0, ctransaction.inputs[0].script, 1, csign.Length);
        ctransaction.inputs[0].script[csign.Length + 1] = 1;

        ctransaction.inputs[0].script[csign.Length + 2] = 33;
        Buffer.BlockCopy(user.pkey, 0, ctransaction.inputs[0].script, csign.Length+3, 33);

        ctransaction.hash = Hash(Hash(TXtoBytes(ctransaction)));

        for (int n = 0; n < NodesList.Count; n++)
        {
            NodesList[n].SendTxMessage(transaction);
            NodesList[n].SendTxMessage(ctransaction);
        }

        sendingPos = false;

        return;
    }


    // Update is called once per frame
    void Update()
    {

        //NodesList.Sort()

        for (int n=0;n<NodesList.Count;n++)
        {
            NodesList[n].current_blk = block_height;

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
                continue;
            }
             
            
            if ((System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - NodesList[n].lastPingTime) > 120000) 
            { 
                if (!NodesList[n].waitPong)
                    NodesList[n].SendPingMessage();
                else
                {
                    if(NodesList[n].recvHDR)
                        recvHDR = false;

                    NodesList[n].Disconnect();
                    NodesList.RemoveAt(n);
                    Debug.Log("ping timeout");
                    continue;
                }
            }


            if(isSync())
            {
                NodixApplication app;
                if ((!recvApps) && (NodesList[n].hasVer))
                {
                    if ((app = findNodixApp("UnityApp")) == null)
                        NodesList[n].SendGetAppsMessage();
                    else if ((app.types == null) || (app.types.Length == 0))
                        NodesList[n].SendGetAppTypeMessage(app.hash);

                    recvApps = true;
                }

                if (((app = findNodixApp("UnityApp")) != null) && (app.types != null) && (app.types.Length > 0))
                {
                    NodesList[n].SendTmppoolMessage();
                }
            }



            if (!recvHDR)
            {
                if (!NodesList[n].recvHDR)
                {
                    if (System.DateTimeOffset.Now.ToUnixTimeMilliseconds() > nextGetHeaders)
                    {
                        if (NodesList[n].getLastBlock() > block_height)
                        {
                            NodesList[n].SendGetHeadersMessage(block_locator_indexes());
                            recvHDR = true;
                        }
                    }
                }
            }

            lock (NodesList[n]._invLock)
            {
                messageInventoryHash[] hashList;
                int cnt = 0;

                for (int nn = 0; nn < NodesList[n].inventory.Count; nn++)
                {
                    if(NodesList[n].inventory[nn].type == 1)
                    {
                        if (!txstorage.Exists(new BigInteger(NodesList[n].inventory[nn].hash)))
                            cnt++;
                    }
                    else if (NodesList[n].inventory[nn].type == 2)
                    {
                        if(!blkstorage.Exists(new BigInteger(NodesList[n].inventory[nn].hash)))
                        {
                            cnt++;
                        }
                    }
                }

                if(cnt>0)
                {
                    hashList = new messageInventoryHash[cnt];

                    cnt = 0;

                    for (int nn = 0; nn < NodesList[n].inventory.Count; nn++)
                    {
                        if (NodesList[n].inventory[nn].type == 1)
                        {
                            if (!txstorage.Exists(new BigInteger(NodesList[n].inventory[nn].hash)))
                            {
                                hashList[cnt] = NodesList[n].inventory[nn];
                                cnt++;
                            }
                        }
                        else if (NodesList[n].inventory[nn].type == 2)
                        {
                            if (!blkstorage.Exists(new BigInteger(NodesList[n].inventory[nn].hash)))
                            {
                                hashList[cnt] = NodesList[n].inventory[nn];
                                cnt++;
                            }
                        }
                    }

                    NodesList[n].SendGetDataMessage(hashList);
                }

                NodesList[n].inventory.RemoveRange(0, NodesList[n].inventory.Count);
            }


            lock (NodesList[n]._txLock)
            {

                for (int nn = 0; nn < NodesList[n].RecvTransactions.Count; nn++)
                {
                    Tx tx = NodesList[n].RecvTransactions[nn];

                    if (!txstorage.Exists(new BigInteger(tx.hash)))
                    {
                        TxInfos infos;
                        if (ProcessTx(tx, out infos))
                        {
                            if ((infos.childOf != null) && (infos.child != null))
                                room.newObj(infos.childOf, infos.child);

                            if (tx.locktime != 0xFFFFFFFF)
                                txstorage.Set(new BigInteger(tx.hash), tx);
                        }

                    }
                }

                NodesList[n].RecvTransactions.RemoveRange(0, NodesList[n].RecvTransactions.Count);


            }

            lock (NodesList[n]._blkLock)
            {
                if (NodesList[n].RecvHeaders.Count > 0)
                {
                    for (int nn = 0; nn < NodesList[n].RecvHeaders.Count; nn++)
                    {
                        if (compareHash(blksIdxtorage.Get(block_height), NodesList[n].RecvHeaders[nn].prev) == 0)
                        {
                            blockheader blk = NodesList[n].RecvHeaders[nn];
                            byte[] b = HDRtoBytes(blk);
                            byte[] h = Nodes.Hashd(b, 80);

                            if (blk.isPow)
                            {
                                int nSize = (int)(blk.bits >> 24);
                                byte[] pow = blockPOW(b);
                                BigInteger Diff, diff;

                                diff = new BigInteger(pow);
                                Diff = new BigInteger(blk.bits & 0x00FFFFFF);
                                Diff = Diff << ((nSize - 3) * 8);

                                string d1 = Diff.ToString("X64");
                                string d2 = diff.ToString("X64");

                                if (diff < Diff)
                                    Debug.Log(" block ok \n" + d1 + "\n" + d2);
                                else
                                    Debug.Log(" block fail \n" + d1 + "\n" + d2);
                            }

                            block_height++;
                            blkstorage.Set(new BigInteger(h), NodesList[n].RecvHeaders[nn]);
                            blksIdxtorage.Set(block_height, h);

                        }
                    }

                    NodesList[n].RecvHeaders = new List<blockheader>();

                    Nodes.nextGetHeaders = System.DateTimeOffset.Now.ToUnixTimeMilliseconds() + 1000;
                    recvHDR = false;
                }
            }
            
        }
    }

    void OnDestroy()
    {
        blksIdxtorage.Close();
        blkstorage.Close();
        txstorage.Close();
    }
}
