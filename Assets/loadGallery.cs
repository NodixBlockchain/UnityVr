using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Xml.Serialization;
using System.Text;
using System;

using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.IO.Pem;
using Org.BouncyCastle.Asn1.Sec;


using Org.BouncyCastle.Math;


using UnityEngine;
using Unity;

using GLTF;
using UnityGLTF.Extensions;

using UnityEngine.UI;
using UnityEngine.Networking;


/*using FASTER.core;*/

[System.Serializable]
public class GalleryScene
{
    public string objHash;
}

[System.Serializable]
public class GalleryItem
{
    public string name;
    public string desc;
    public GalleryScene scene;
   public string objHash;
}

[System.Serializable]
public class Gallery
{
    public string name;
    public string objAddr;
    public string objHash;
    public GalleryItem[] scenes;
}

[System.Serializable]
public class RoomSceneRoot
{
    public string objHash;
}

[System.Serializable]
public class RoomScene
{
    public string objHash;
    public RoomSceneRoot root;
}

[System.Serializable]
public class Room
{
    public string name;
    public RoomScene[] objects;
    public string objHash;
}

[System.Serializable]
public class NodePrivaKey
{
    public string privkey;
}


[System.Serializable]
public class WalletAddress
{
    

    public WalletAddress()
    {
        Name = null;
        PubAddr = null;
        PrivKey = null;
    }

    

    public byte[] Sign(byte[] data, ECDomainParameters domain)
    {
        byte[] key;
        ECPrivateKeyParameters privateKey;
        ECPublicKeyParameters publicParams;
        Org.BouncyCastle.Math.EC.ECPoint Q;

     

        key = WalletAddress.base58ToByteArray(PrivKey);

        privateKey = PrivateKeyFactory.CreateKey(key) as ECPrivateKeyParameters;
        Q = privateKey.Parameters.G.Multiply(privateKey.D);
        publicParams = new ECPublicKeyParameters(privateKey.AlgorithmName, Q, SecObjectIdentifiers.SecP256k1);

        ECDsaSigner signer = new Org.BouncyCastle.Crypto.Signers.ECDsaSigner();
        signer.Init(true, privateKey);
        BigInteger[] components = signer.GenerateSignature(data);

        MemoryStream stream = new MemoryStream();
        Org.BouncyCastle.Asn1.DerOutputStream der = new Org.BouncyCastle.Asn1.DerOutputStream(stream);
        Org.BouncyCastle.Asn1.Asn1EncodableVector v = new Org.BouncyCastle.Asn1.Asn1EncodableVector();
        v.Add(new Org.BouncyCastle.Asn1.DerInteger(components[0]));
        v.Add(new Org.BouncyCastle.Asn1.DerInteger(components[1]));
        der.WriteObject(new Org.BouncyCastle.Asn1.DerSequence(v));

        return stream.ToArray();
    }

    public string pub2addr(ECPublicKeyParameters key)
    {
        byte[] step1=new byte[25];
        ECPoint PubPoint = key.Q;

        byte[] total = PubPoint.GetEncoded(true);

        var digest1 = new Sha256Digest();
        byte[] result1 = new byte[digest1.GetDigestSize()];
        digest1.BlockUpdate(total, 0, total.Length);
        digest1.DoFinal(result1, 0);

        var digest2 = new RipeMD160Digest();
        var result2 = new byte[digest2.GetDigestSize()];
        digest2.BlockUpdate(result1, 0, result1.Length);
        digest2.DoFinal(result2, 0);

        step1[0] = 0x19;

        for(int n=0; n<20; n++)
        {
            step1[1 + n] = result2[n];
        }

        var digest3 = new Sha256Digest();
        var result3 = new byte[digest3.GetDigestSize()];
        digest3.BlockUpdate(step1, 0, 21);
        digest3.DoFinal(result3, 0);

        var digest4 = new Sha256Digest();
        var result4 = new byte[digest4.GetDigestSize()];
        digest4.BlockUpdate(result3, 0, result3.Length);
        digest4.DoFinal(result4, 0);

        for (int n = 0; n < 4; n++)
        {
            step1[21 + n] = result4[n];
        }

        return base58FromByteArray(step1);
    }
     

    public IEnumerator checkpub()
    {
        ECPublicKeyParameters publicParams = getPub();
        byte[] pk = publicParams.Q.GetEncoded(true);
        string addr = pub2addr(publicParams);
        Debug.Log("check public key len" + pk.Length);

        string xkey = Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(pk);
        //Org.BouncyCastle.Utilities.Encoders.Hex.Encode(pk);
        string URL = "http://127.0.0.1/jsonrpc/pubkeytoaddr/" + xkey;
        Debug.Log("check public key url " + URL + " " + addr);
        
        UnityWebRequest webRequest = UnityWebRequest.Get(URL);

        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("pubkeytoaddr  " + xkey + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("pubkeytoaddr  " + xkey + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                Debug.Log("pubkeytoaddr " + xkey + "\npubaddr " + PubAddr + "\naddr : "+ addr+ "\nReceived: " + webRequest.downloadHandler.text);
                break;
        }
    }

    public WalletAddress(string name, ECKeyPairGenerator generator)
    {
        AsymmetricCipherKeyPair keyPair = generator.GenerateKeyPair();

        Name = name;
        PubAddr = pub2addr((ECPublicKeyParameters)keyPair.Public);

        byte[] enc = Org.BouncyCastle.Pkcs.PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private).GetDerEncoded();
        PrivKey = base58FromByteArray(enc);
    }

    public WalletAddress(string name, ECPrivateKeyParameters privAddr)
    {
        byte[] enc = Org.BouncyCastle.Pkcs.PrivateKeyInfoFactory.CreatePrivateKeyInfo(privAddr).GetDerEncoded();

        Name = name;
        PrivKey = base58FromByteArray(enc);
        PubAddr = pub2addr(getPub());

        Debug.Log("new WalletAddress " + PubAddr + "\nPrivate :\n"+PrivKey);
    }

    public WalletAddress(string name, byte[] pk, ECDomainParameters dom )
    {
        ECPoint Q = dom.Curve.DecodePoint(pk);
        ECPublicKeyParameters pkey = new ECPublicKeyParameters(Q, dom);

        Name = name;
        PubAddr = pub2addr(pkey);
        PrivKey = null;
    }

    public WalletAddress(string name, string pubAddr)
    {
        Name    = name;
        PubAddr = pubAddr;
        PrivKey = null;
    }


    /// <summary>
    /// Converts a base-58 string to a byte array, returning null if it wasn't valid.
    /// </summary>
    public static byte[] base58ToByteArray(string base58)
    {
        Org.BouncyCastle.Math.BigInteger bi2 = new Org.BouncyCastle.Math.BigInteger("0");
        string b58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        foreach (char c in base58)
        {
            if (b58.IndexOf(c) != -1)
            {
                bi2 = bi2.Multiply(new Org.BouncyCastle.Math.BigInteger("58"));
                bi2 = bi2.Add(new Org.BouncyCastle.Math.BigInteger(b58.IndexOf(c).ToString()));
            }
            else
            {
                return null;
            }
        }

        byte[] bb = bi2.ToByteArrayUnsigned();

        // interpret leading '1's as leading zero bytes
        foreach (char c in base58)
        {
            if (c != '1') break;
            byte[] bbb = new byte[bb.Length + 1];
            Array.Copy(bb, 0, bbb, 1, bb.Length);
            bb = bbb;
        }

        return bb;
    }

    public ECPublicKeyParameters getPub()
    {
        byte[] key;
        ECPrivateKeyParameters privateKey;
        ECPublicKeyParameters publicParams;
        Org.BouncyCastle.Math.EC.ECPoint Q;

        key = WalletAddress.base58ToByteArray(PrivKey);

        privateKey = PrivateKeyFactory.CreateKey(key) as ECPrivateKeyParameters;

        if (privateKey == null)
        {
            Debug.Log("unable to decode private key " + PrivKey);
            return null;
        }

        Q = privateKey.Parameters.G.Multiply(privateKey.D);
        publicParams = new ECPublicKeyParameters(privateKey.AlgorithmName, Q, SecObjectIdentifiers.SecP256k1);


        
        return publicParams;
    }

    public static string base58FromByteArray(byte[] ba)
    {
        Org.BouncyCastle.Math.BigInteger addrremain = new Org.BouncyCastle.Math.BigInteger(1, ba);

        Org.BouncyCastle.Math.BigInteger big0 = new Org.BouncyCastle.Math.BigInteger("0");
        Org.BouncyCastle.Math.BigInteger big58 = new Org.BouncyCastle.Math.BigInteger("58");

        string b58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        string rv = "";

        while (addrremain.CompareTo(big0) > 0)
        {
            int d = Convert.ToInt32(addrremain.Mod(big58).ToString());
            addrremain = addrremain.Divide(big58);
            rv = b58.Substring(d, 1) + rv;
        }

        // handle leading zeroes
        foreach (byte b in ba)
        {
            if (b != 0) break;
            rv = "1" + rv;

        }
        return rv;
    }


    public string Name;
    public string PubAddr;
    public string PrivKey;
}

[System.Serializable]
public class Wallet
{
    private ECKeyPairGenerator generator;
    public ECDomainParameters domainParams;
    public WalletAddress mainKey;

    MonoBehaviour mono;

    public IEnumerator getnodepriv(string name, string pubaddr)
    {

        string URL = "http://127.0.0.1/jsonrpc/getprivaddr/" + name + "/" + pubaddr;
        Debug.Log("get private key url " + URL + " " + name + " " + pubaddr);

        UnityWebRequest webRequest = UnityWebRequest.Get(URL);

        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("getnodepriv  " + name + " " + pubaddr + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("getnodepriv " + name + " " + pubaddr + "  : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                NodePrivaKey myKey = JsonUtility.FromJson<NodePrivaKey>(webRequest.downloadHandler.text);

                byte[] key = Org.BouncyCastle.Utilities.Encoders.Hex.Decode(myKey.privkey.Substring(0,64));
                byte[] mkey = new byte[32];

                var rc4 = new Org.BouncyCastle.Crypto.Engines.RC4Engine();
                KeyParameter keyParam = new KeyParameter(Encoding.ASCII.GetBytes("1618Iadix"));
                rc4.Init(false, keyParam);
                rc4.ProcessBytes(key, 0, 32, mkey, 0);

                Debug.Log("getnodepriv " + name + " " + pubaddr + "\nReceived: " + webRequest.downloadHandler.text + "\ndec key ("+ mkey.Length+ ") " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(mkey));

                BigInteger ikey = new BigInteger(Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(mkey),16);

                ECPrivateKeyParameters privkey = new ECPrivateKeyParameters(ikey, domainParams );

                WalletAddress myaddr = new WalletAddress(name, privkey);

                //addresses.Add(myaddr);

                break;
        }
    }

    public Wallet(MonoBehaviour mainmono)
    {
        addresses = new List<WalletAddress> ();

        mono = mainmono;

        X9ECParameters curve = ECNamedCurveTable.GetByName("secp256k1");
        domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

        SecureRandom secureRandom = new SecureRandom();
        ECKeyGenerationParameters keyParams = new ECKeyGenerationParameters(domainParams, secureRandom);

        generator = new ECKeyPairGenerator("ECDSA");
        generator.Init(keyParams);
    }

    public void save()
    {
        string basePath = Application.persistentDataPath + "/Wallet";

        if (!Directory.Exists(basePath))
            Directory.CreateDirectory(basePath);

        XmlSerializer serializer = new XmlSerializer(typeof(List<WalletAddress>));
        StreamWriter writer = new StreamWriter(basePath  + "/wallet.xml");
        serializer.Serialize(writer.BaseStream, addresses);
        writer.Close();

    }

    public void load()
    {
        string basePath = Application.persistentDataPath + "/Wallet";
     
        if (File.Exists(basePath + "/wallet.xml"))
        {

            XmlSerializer serializer = new XmlSerializer(typeof(List<WalletAddress>));
            StreamReader reader = new StreamReader(basePath + "/wallet.xml");
            addresses = (List<WalletAddress>)serializer.Deserialize(reader.BaseStream);
            reader.Close();
        }
        else
        {
            addresses.Add(new WalletAddress("UnityApp", "BPgb5m5HGtNMXrUX9w1a8FfRE1GdGLM8P4"));
            save();
        }

        for(int n=0;n < addresses.Count;n++)
        {
            Debug.Log("private " + addresses[n]);

            if (addresses[n].PrivKey != null)
            {
                mono.StartCoroutine(addresses[n].checkpub());
                mainKey = addresses[n];
                return;
            }
        }

        var newAddress = new WalletAddress("Master", generator);

        addresses.Add(newAddress);

        mono.StartCoroutine(newAddress.checkpub());

        save();
    }

    public List<WalletAddress> addresses;
}


[System.Serializable]
public class Node
{

    public Node(string adr, int port, bool seed)
    {
        address = adr;

        try
        {
            IPHostEntry Entry = Dns.GetHostEntry(address);
            ip = Entry.AddressList[0];
            isSeed = seed;
            P2PPort = port;
            HTTPPort = 80;

        }
        catch
        {
            ip = null;
            isSeed = seed;
            P2PPort = 0;
            HTTPPort =  0;
        }
    }

    public string address;
    public IPAddress ip;
    public int P2PPort;
    public int HTTPPort;
    public bool isSeed;
    public int ping;
}



public class NodeTableRow
{
    public GameObject[] Columns;
}


public class itemTable
{
    public itemTable(string[] fields, float size)
    {
        Size = size;

        Fields = new string[fields.Length];

        for (int n = 0; n < fields.Length; n++)
        {
            Fields[n] = fields[n];
        }
    }

    public string[] Fields;
    public float Size;
    public NodeTableRow[] NodeRow;

}


[System.Serializable]
public class TxInput
{
    public string txid;
    public int index;
    public int value;
    public string srcaddr;
    public string script;
    public string signHash;
}

[System.Serializable]
public class TxOutput
{
    public int value;
    public string script;
    public string dstaddr;
}

[System.Serializable]
public class Transaction
{
    public string txid;
    public int version;
    public int time;
    public int locktime;
    public List<TxInput> txsin;
    public List<TxOutput> txsout;
    public Boolean issigned;
}


public class objNode
{
    public objNode(GameObject Obj)
    {
        nodeTx = null;
        obj = Obj;
    }
    public GameObject obj;
    public Transaction nodeTx;
    public Transaction nodePTx;
}

public class gltfRef
{
    public gltfRef(string Hash)
    {
        rootHash = Hash;
        objs = new List<objNode>();
    }

    public Transaction sceneTx;
    public List<objNode> objs;
    public string rootHash;
    
}

[System.Serializable]
public class RPCTx
{
    public Transaction transaction;
}
[System.Serializable]
public class RPCTransaction
{
    int id;
    string jsonrpc;
    public RPCTx result;
}

[System.Serializable]
public class RPCSignTx
{
    public string txid;
}
[System.Serializable]
public class RPCSign
{
    int id;
    string jsonrpc;
    public RPCSignTx result;
}


public struct SaveInfo
{
    public string appName;
    public int sceneTypeId;
    public int roomTypeId;
    public int nodeTypeId;
    public string server;
    public WalletAddress mainKey;
    public ECDomainParameters domainParams;
}

public class vrRoom
{
    public string name;
    public List<gltfRef> sceneObjects;
    public Transaction roomTx;
    MonoBehaviour mono;
    SaveInfo saveInfos;

    public vrRoom(MonoBehaviour Mono)
    {
        mono = Mono;
        sceneObjects = new List<gltfRef>();
    }

    public void addObj(GameObject obj, string hash)
    {
        obj.GetComponent<UnityGLTF.GLTFComponent>().transform.position = new Vector3(0.0f, 2.0f, 0.0f);
        obj.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.5f, 0.5f);

        objNode on = new objNode(obj);

        for (int n = 0; n < this.sceneObjects.Count; n++)
        {
            if (this.sceneObjects[n].rootHash == hash)
            {
                this.sceneObjects[n].objs.Add(on);
                return;
            }
        }

        gltfRef myref = new gltfRef(hash);
        myref.objs.Add(on);
        this.sceneObjects.Add(myref);
    }


    IEnumerator SubmitTx(string txid)
    {
        string URL = "http://" + this.saveInfos.server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitTx " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitTx " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                Debug.Log("SubmitTx " + submittx + " : \nReceived: " + webRequest.downloadHandler.text);
                break;
        }
    }

    IEnumerator signTxInputs(Transaction tx)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int n = 0; n < tx.txsin.Count; n++)
        {
            byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(tx.txsin[n].signHash), this.saveInfos.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + tx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signTx  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signTx  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("signTx signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);

                    tx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;

                    Debug.Log("signTx signtxinput " + tx.txid);
                    break;
            }
        }
        tx.issigned = true;
        mono.StartCoroutine(SubmitTx(tx.txid));
    }

    IEnumerator addnodes(gltfRef gltfref)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int on = 0; on < gltfref.objs.Count; on++)
        {
            string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + saveInfos.appName + "\",\"" + gltfref.sceneTx.txid + "\",\"nodes\",\"" + gltfref.objs[on].nodeTx.txid + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, addchild);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("addnode  " + addchild + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("addnode  " + addchild + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("addnode " + addchild + " : \nReceived: " + webRequest.downloadHandler.text);
                    gltfref.objs[on].nodePTx = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                    Debug.Log("addnode myTransaction " + gltfref.objs[on].nodePTx.txid + " " + gltfref.sceneTx.txid);
                    gltfref.objs[on].nodePTx.issigned = false;
                    mono.StartCoroutine(signTxInputs(gltfref.objs[on].nodePTx));

                    break;
            }
        }
    }

    IEnumerator SubmitNodesTx(gltfRef gltfref)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int n = 0; n < gltfref.objs.Count; n++)
        {
            string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + gltfref.objs[n].nodeTx.txid + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("SubmitNodeTx " + submittx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("SubmitNodeTx " + submittx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("SubmitNodeTx " + submittx + " : \nReceived: " + webRequest.downloadHandler.text);
                    break;
            }
        }

        mono.StartCoroutine(addnodes(gltfref));
    }

    IEnumerator signNodesTxInputs(gltfRef gltfref)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int on = 0; on < gltfref.objs.Count; on++)
        {
            for (int n = 0; n < gltfref.objs[on].nodeTx.txsin.Count; n++)
            {
                byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.objs[on].nodeTx.txsin[n].signHash), this.saveInfos.domainParams);
                string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + gltfref.objs[on].nodeTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
                UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
                webRequest.SetRequestHeader("Content-Type", "application/json");
                // Request and wait for the desired page.
                yield return webRequest.SendWebRequest();

                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.Log("signNodeTxInputs  " + signtx + " : Error: " + webRequest.error);
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.Log("signNodeTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                        break;
                    case UnityWebRequest.Result.Success:

                        Debug.Log("signNodeTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);

                        gltfref.objs[on].nodeTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;

                        break;
                }
            }
            gltfref.objs[on].nodeTx.issigned = true;
        }
        mono.StartCoroutine(SubmitNodesTx(gltfref));
    }

    IEnumerator makenodes(gltfRef gltfref)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int n = 0; n < gltfref.objs.Count; n++)
        {
            GameObject obj = gltfref.objs[n].obj;

            Quaternion TotalQ = obj.transform.localRotation * obj.GetComponentInChildren<MeshFilter>().transform.localRotation;
            GLTF.Math.Quaternion myQ = TotalQ.ToGltfQuaternionConvert();
            Vector3 mP = new Vector3(obj.transform.position.x * SchemaExtensions.CoordinateSpaceConversionScale.X, obj.transform.position.y * SchemaExtensions.CoordinateSpaceConversionScale.Y, obj.transform.position.z * SchemaExtensions.CoordinateSpaceConversionScale.Z);
            Vector3 Scale = obj.GetComponentInChildren<MeshFilter>().transform.localScale;

            var nodeJson = "{name: \"node " + n.ToString() + "\", ";
            nodeJson += "translation: [" + mP[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + mP[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + mP[2].ToString(System.Globalization.CultureInfo.InvariantCulture) + "], ";
            nodeJson += "scale: [" + Scale.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Scale.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Scale.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "], ";
            nodeJson += "rotation: [" + myQ.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + myQ.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + myQ.Z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + myQ.W.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]}";

            string makenodeobj = "{id:1 , jsonrpc: \"2.0\", method:\"makeappobjtx\", params : [\"" + this.saveInfos.appName + "\"," + this.saveInfos.nodeTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"," + nodeJson + "]}";

            UnityWebRequest webRequest = UnityWebRequest.Put(URL, makenodeobj);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("makenode  " + makenodeobj + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("makenode  " + makenodeobj + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("makenode " + makenodeobj + " : \nReceived: " + webRequest.downloadHandler.text);
                    gltfref.objs[n].nodeTx = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                    gltfref.objs[n].nodeTx.issigned = false;
                    Debug.Log("makenode myTransaction " + gltfref.objs[n].nodeTx.txid);
                    break;
            }
        }
        mono.StartCoroutine(signNodesTxInputs(gltfref));
    }

    IEnumerator addRoomSceneObj(gltfRef gltfref)
    {
        gltfref.sceneTx.issigned = true;

        for (int n = 0; n < this.sceneObjects.Count; n++)
        {
            if (this.sceneObjects[n].sceneTx == null)
                yield break;

            if (!this.sceneObjects[n].sceneTx.issigned)
                yield break;
        }

        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int n = 0; n < this.sceneObjects.Count; n++)
        {
            string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + saveInfos.appName + "\",\"" + this.roomTx.txid + "\",\"objects\",\"" + this.sceneObjects[n].sceneTx.txid + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, addchild);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("addRoomSceneObj  " + addchild + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("addRoomSceneObj  " + addchild + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("addRoomSceneObj " + addchild + " : \nReceived: " + webRequest.downloadHandler.text);
                    Transaction myTransaction = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                    Debug.Log("addRoomSceneObj myTransaction " + myTransaction.txid + " " + this.roomTx.txid);
                    myTransaction.issigned = false;
                    mono.StartCoroutine(signTxInputs(myTransaction));

                    break;
            }
        }
    }

    IEnumerator SubmitSceneTx(gltfRef gltfref)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + gltfref.sceneTx.txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitSceneTx  " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitSceneTx  " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                Debug.Log("SubmitSceneTx signtxinput" + submittx + " : \nReceived: " + webRequest.downloadHandler.text);

                mono.StartCoroutine(makenodes(gltfref));
                break;
        }
    }

    IEnumerator signSceneTxInputs(gltfRef gltfref)
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int n = 0; n < gltfref.sceneTx.txsin.Count; n++)
        {
            byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.sceneTx.txsin[n].signHash), this.saveInfos.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + gltfref.sceneTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signSceneTxInputs  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signSceneTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("signSceneTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);
                    gltfref.sceneTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;
                    Debug.Log("signSceneTxInputs signtxinput" + gltfref.sceneTx.txid);
                    break;
            }
        }
        mono.StartCoroutine(addRoomSceneObj(gltfref));

        mono.StartCoroutine(SubmitSceneTx(gltfref));
    }

    IEnumerator makeobjs()
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";

        for (int n = 0; n < this.sceneObjects.Count; n++)
        {
            gltfRef gltfref = this.sceneObjects[n];
            string sceneJSon = "{root : \"" + gltfref.rootHash + "\"}";
            string makeappobj = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + saveInfos.appName + "\"," + saveInfos.sceneTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"," + sceneJSon + "]}";
            Debug.Log("makeobj  " + URL + "  " + makeappobj);

            UnityWebRequest scenewebRequest = UnityWebRequest.Put(URL, makeappobj);
            scenewebRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return scenewebRequest.SendWebRequest();

            switch (scenewebRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("makeobj  " + makeappobj + " : Error: " + scenewebRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("makeobj  " + makeappobj + " : HTTP Error: " + scenewebRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("makeobj " + makeappobj + " : \nReceived: " + scenewebRequest.downloadHandler.text);
                    gltfref.sceneTx = JsonUtility.FromJson<RPCTransaction>(scenewebRequest.downloadHandler.text).result.transaction;
                    gltfref.sceneTx.issigned = false;
                    Debug.Log("makeobj myTransaction" + gltfref.sceneTx.txid + " " + gltfref.sceneTx.txsin.Count);
                    mono.StartCoroutine(signSceneTxInputs(gltfref));
                    break;
            }
        }
    }

    IEnumerator SubmitRoomTx()
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + this.roomTx.txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitRoomTx " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitRoomTx " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                Debug.Log("SubmitRoomTx " + submittx + " : \nReceived: " + webRequest.downloadHandler.text);

                mono.StartCoroutine(makeobjs());
                break;
        }
    }

    IEnumerator signRoomTxInputs()
    {
        string URL = "http://" + saveInfos.server + "/jsonrpc";


        for (int n = 0; n < this.roomTx.txsin.Count; n++)
        {
            byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(this.roomTx.txsin[n].signHash), this.saveInfos.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + this.roomTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signRoomTxInputs  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signRoomTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("signRoomTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);

                    this.roomTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;

                    Debug.Log("signRoomTxInputs signtxinput" + this.roomTx.txid);
                    break;
            }
        }

        this.roomTx.issigned = true;

        mono.StartCoroutine(SubmitRoomTx());
    }


    public IEnumerator makeroom(SaveInfo infos)
    {
        this.saveInfos = infos;
        string URL = "http://" + saveInfos.server + "/jsonrpc";
        string roomJSon = "{name : \"" + this.name + "\"}";
        string makeapproom = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + saveInfos.appName + "\"," + saveInfos.roomTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(this.saveInfos.mainKey.getPub().Q.GetEncoded(true)) + "\"," + roomJSon + "]}";
        Debug.Log("makeapproom  " + URL + "  " + makeapproom);

        UnityWebRequest scenewebRequest = UnityWebRequest.Put(URL, makeapproom);
        scenewebRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return scenewebRequest.SendWebRequest();

        switch (scenewebRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("makeroom  " + makeapproom + " : Error: " + scenewebRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("makeroom  " + makeapproom + " : HTTP Error: " + scenewebRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                Debug.Log("makeroom " + makeapproom + " : \nReceived: " + scenewebRequest.downloadHandler.text);
                this.roomTx = JsonUtility.FromJson<RPCTransaction>(scenewebRequest.downloadHandler.text).result.transaction;
                Debug.Log("makeroom myTransaction" + this.roomTx.txid + " " + this.roomTx.txsin.Count);
                this.roomTx.issigned = false;
                mono.StartCoroutine(signRoomTxInputs());

                break;
        }
    }

}


class WallSegment
{
    public GameObject defObj;
    public gltfRef gltfObjs;
    public cell startCell, endCell;

    public Vector3 getDirection()
    {
        return new Vector3(this.endCell.obj.transform.position.x - this.startCell.obj.transform.position.x, 0.0f, this.endCell.obj.transform.position.z - this.startCell.obj.transform.position.z);
    }


    public double getAngle()
    {
        double angle = 0.0;
        Vector3 dir = getDirection();
        dir.Normalize();

        if (dir.sqrMagnitude > 0.001f)
        {
            angle = Math.Atan2(dir.x, dir.z);
        }

        return angle;
    }


    public Vector3[] getArray(float step)
    {
        Vector3 dir = getDirection();
        float l = dir.magnitude;
        int nstep = (int)Math.Floor(l / step);
        Vector3[] ret = new Vector3[nstep];

        step = l / nstep;
        dir.Normalize();
        Vector3 start = new Vector3(startCell.obj.transform.position.x + (dir.x * step / 2.0f), 0.0f, startCell.obj.transform.position.z + (dir.z * step / 2.0f));

        for (int n = 0; n < nstep; n++)
        {
            ret[n] = new Vector3(start.x + n * dir.x * step, 0.0f, start.z + n * dir.z * step);
        }

        return ret;
    }

    public void recompute(float height, string URL)
    {

        this.defObj.SetActive(true);
        Vector3 delta = getDirection();

        this.defObj.transform.position = new Vector3(this.startCell.obj.transform.position.x + delta.x / 2.0f, this.startCell.obj.transform.position.y + height / 2.0f, this.startCell.obj.transform.position.z + delta.z / 2.0f);
        this.defObj.transform.localScale = new Vector3(4.0f, height, delta.magnitude);

        delta.Normalize();
        if (delta.sqrMagnitude > 0.001f)
        {
            double langle;

            langle = Math.Atan2(delta.x, delta.z);

            this.defObj.transform.rotation = Quaternion.Euler(0.0f, (float)(langle * 180.0f / Math.PI), 0.0f);
        }
        
    }



}


class cell
{
    public GameObject obj;
    
    public int X;
    public int Y;
}


class Grid
{
    Material selectedSphereMat;
    Material SphereEditMat;
    Material defaultSphereMat;

    public cell[] cells;
    public int w, h;
    public int x, y;

    public Grid(int X, int Y, int W, int H, Material selectedMat, Material EditMat, Material defaultMat)
    {
        x = X;
        y = Y;
        w = W;
        h = H;
        cells = new cell[w * h];

        selectedSphereMat = selectedMat;
        SphereEditMat = EditMat;
        defaultSphereMat = defaultMat;

        for (int ny = 0; ny < h; ny++)
        {
            for (int nx = 0; nx < w; nx++)
            {
                cells[(ny * w) + nx] = new cell();
                cells[(ny * w) + nx].obj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                cells[(ny * w) + nx].obj.name = "Plane " + nx.ToString() + " - " + ny.ToString();
                cells[(ny * w) + nx].obj.transform.position= new Vector3((nx + x) * 10.0f, -1.0f, (ny + y) * 10.0f);
                cells[(ny * w) + nx].obj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                cells[(ny * w) + nx].X = nx;
                cells[(ny * w) + nx].Y = ny;


                /*
                cells[(ny * w) + nx].lines = new GameObject[2];

                cells[(ny * w) + nx].lines[0] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cells[(ny * w) + nx].lines[0].name = "Line 1 " + nx.ToString() + " - " + ny.ToString();
                cells[(ny * w) + nx].lines[0].GetComponentInChildren<Renderer>().material.color = new Color(0, 0, 0);
                cells[(ny * w) + nx].lines[0].transform.position = new Vector3((nx + x) * 10.0f - 5.0f, 1.0f, (ny + y) * 10.0f );
                cells[(ny * w) + nx].lines[0].transform.localScale = new Vector3(0.75f, 0.75f, 10.0f);

                cells[(ny * w) + nx].lines[1] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cells[(ny * w) + nx].lines[1].name = "Line 2 " + nx.ToString() + " - " + ny.ToString();
                cells[(ny * w) + nx].lines[1].GetComponentInChildren<Renderer>().material.color = new Color(0, 0, 0);
                cells[(ny * w) + nx].lines[1].transform.position = new Vector3((nx + x) * 10.0f, 1.0f, (ny + y) * 10.0f - 5.0f);
                cells[(ny * w) + nx].lines[1].transform.localScale = new Vector3(10.0f, 0.75f, 0.75f);
                */
            }
        }
    }

    public cell FindCellByName(string Name)
    {
        for (int n = 0; n < w * h; n++)
        {
            if (cells[n].obj.name == Name)
                return cells[n];
        }

        return null;
    }
}


class Blinker : MonoBehaviour
{
    double cur;
    Color[] matCols;
    void Start()
    {
        MeshRenderer mrs = GetComponentInChildren<MeshRenderer>();
        cur = 0;

        matCols = new Color[mrs.materials.Length];

        for (int n = 0; n < mrs.materials.Length; n++)
        {
            matCols[n] = mrs.materials[n].color;
        }
    }
    void Update()
    {
        MeshRenderer mrs = GetComponentInChildren<MeshRenderer>();
        cur += Time.deltaTime;
        float alpha = (float)(Math.Sin(cur * 4.0f) + 1.0f) / 2.0f;

        for(int n=0;n< mrs.materials.Length;n++)
        {
            mrs.materials[n].SetColor("_Color", new Color(matCols[n].r * alpha, matCols[n].g * alpha, matCols[n].b * alpha, matCols[n].a * alpha));
        }

    }

    void OnDestroy()
    {
        MeshRenderer mrs = GetComponentInChildren<MeshRenderer>();

        for (int n = 0; n < mrs.materials.Length; n++)
        {
            mrs.materials[n].SetColor("_Color", new Color(matCols[n].r, matCols[n].g , matCols[n].b , matCols[n].a ));
        }
    }


}

public class loadGallery : MonoBehaviour
{
    [System.Serializable]
    private struct JsonArrayWrapper<T>
    {
        public T wrap_result;
    }

    public static T ParseJsonArray<T>(string json)
    {
        var temp = JsonUtility.FromJson<JsonArrayWrapper<T>>("{\"wrap_result\":" + json + "}");
        return temp.wrap_result;
    }

  
    public string appName = "UnityApp";
    public int sceneTypeId = 0x34;
    public int roomTypeId = 0x33;
    public int nodeTypeId = 0x08;
    public string server = "nodix.eu";


    public string seedNodeAdress = "nodix.eu";
    public int seedNodePort = 16819;
    public string address = "BPgb5m5HGtNMXrUX9w1a8FfRE1GdGLM8P4";

    public List<GameObject> MenuItems;

    public GameObject SaveButton;
    public Material selectedSphereMat;
    public Material SphereEditMat;
    public Material defaultSphereMat;

    public Material wallSegMat;

    public Vector2 StartPos = new Vector2(-50.0f, 0.0f);
    public Vector2 Spacing = new Vector2(0.0f, -40.0f);

    public Vector2 ItemsStartPos = new Vector2(0.0f, 0.0f);
    public Vector2 ItemsSpacing = new Vector2(35.0f, -30.0f);

    public GameObject contentPanel;
    public GameObject indexPanel;
    public Font textFont;

    public float wallHeight = 10.0f;
    public float sensitivity = 10.0f;
    public float speed = 500.0f;

    private Gallery myGallery;
    private const string baseURL = "/app/UnityApp";
    private Gallery[] galleries;
    private Room[] rooms;
    private GameObject[] ItemsButton = null;
    private GameObject[] GalleriesButton = null;
    private GameObject[] RoomsButton = null;
    private GameObject[] NodesTexts = null;
    private GameObject galleriesAddress;
    private vrRoom room;
    private Grid grid;

    private List<Node> Nodes;
    private itemTable nodesTable;
    private itemTable walletTable;
    private GameObject[] Headers;
    private GameObject emptyResult;
    private GameObject roomMenu;
    private Wallet wallet;
    private GameObject newAddr;

    private GameObject buttonPrefab;
    private Material lineMaterial;
    private GameObject EditWallPanel = null;

    private cell hoveredCell, selectedCell;
    private GameObject currentSeg, lastSeg;
    private List<WallSegment> wallSegments;
    
    private Boolean CreateWalls = false;
    private Boolean EditRoomWall = false;
    private Boolean outScope = false;

    private int SelectedWallSeg = -1;
    private int HoveredWallSeg = -1;

    private Vector3 lastCamPos;
    private GameObject roomFloor = null;

    private float maxYAngle = 80f;
    private Vector2 currentRotation;

    private GameObject currentWallObj;
    private string currentWallHash;

    private GameObject ObjPannel = null;
    private GameObject hoverRoomObject = null;

    private float lastSnapTime = 0.0f;
    private bool[] canSnap;
    private GameObject floorPlane;


    SaveInfo makeSaveInfos()
    {
        SaveInfo ret;

        ret.appName = appName;
        ret.domainParams = wallet.domainParams;
        ret.mainKey = wallet.mainKey;
        ret.nodeTypeId = nodeTypeId;
        ret.roomTypeId = roomTypeId;
        ret.sceneTypeId = sceneTypeId;
        ret.server = server;

        return ret;
    }


    // Start is called before the first frame update
    void Start()
    {
        wallet = new Wallet(this);
        wallet.load();

        canSnap = new bool[2];

        canSnap[0] = true;
        canSnap[1] = true;

        floorPlane = GameObject.Find("FloorPlane");

        room = new vrRoom(this);
        roomMenu = null;

        galleriesAddress = GameObject.Find("Address Value");
        galleriesAddress.GetComponentInChildren<InputField>().text = address;

        Node SeedNode = new Node(seedNodeAdress, seedNodePort, true);

        Nodes = new List<Node>();
        Nodes.Add(SeedNode);

        nodesTable = new itemTable(new string[] { "adress", "ip", "port", "ping" }, 50.0f);
        walletTable = new itemTable(new string[] { "label", "adress", "owner" }, 65.0f);

        if (selectedSphereMat != null)
        {
            MenuItems[0].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        }

        if (defaultSphereMat != null)
        {
            MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
            MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;
            MenuItems[3].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        }

        MenuItems[0].GetComponentInChildren<Button>().onClick.AddListener(() => RoomMenuClicked());
        MenuItems[1].GetComponentInChildren<Button>().onClick.AddListener(() => GalleryMenuClicked());
        MenuItems[2].GetComponentInChildren<Button>().onClick.AddListener(() => NodesMenuClicked());
        MenuItems[3].GetComponentInChildren<Button>().onClick.AddListener(() => WalletMenuClicked());

        StartCoroutine(loadRooms());

        /*
       
        */
        //var Teleport = new Locom
        /*StartCoroutine(wallet.getnodepriv("BitAdmin", "B8mPBEg2XbYSUwEh5a7yrfehvMNijpAm1P"));*/
    }

    void ClearTabs()
    {
        if(roomMenu != null)
        {
            Destroy(roomMenu);
            roomMenu = null;
        }

        if (emptyResult != null)
        {
            Destroy(emptyResult);
            emptyResult = null;
        }

        if (newAddr != null)
        {
            Destroy(newAddr);
            newAddr = null;
        }

        if (GalleriesButton != null)
        {
            for (int n = 0; n < GalleriesButton.Length; n++)
            {
                Destroy(GalleriesButton[n]);
            }
            GalleriesButton = null;
        }

        if (RoomsButton != null)
        {
            for (int n = 0; n < RoomsButton.Length; n++)
            {
                Destroy(RoomsButton[n]);
            }
            RoomsButton = null;
        }

        if (ItemsButton != null)
        {
            for (int n = 0; n < ItemsButton.Length; n++)
            {
                Destroy(ItemsButton[n]);
            }
            ItemsButton = null;
        }

        if (NodesTexts != null)
        {
            for (int n = 0; n < NodesTexts.Length; n++)
            {
                Destroy(NodesTexts[n]);
            }
            NodesTexts = null;
        }

        if (Headers != null)
        {
            for (int n = 0; n < Headers.Length; n++)
            {
                Destroy(Headers[n]);
            }
            Headers = null;
        }

        if (nodesTable.NodeRow != null)
        {
            for (int n = 0; n < nodesTable.NodeRow.Length; n++)
            {
                for (int nn = 0; nn < nodesTable.NodeRow[n].Columns.Length; nn++)
                {
                    Destroy(nodesTable.NodeRow[n].Columns[nn]);
                }
                nodesTable.NodeRow[n].Columns = null;
            }
            nodesTable.NodeRow = null;
        }
      
        if (walletTable.NodeRow != null)
        {
            for (int n = 0; n < walletTable.NodeRow.Length; n++)
            {
                for (int nn = 0; nn < walletTable.NodeRow[n].Columns.Length; nn++)
                {
                    Destroy(walletTable.NodeRow[n].Columns[nn]);
                }
                walletTable.NodeRow[n].Columns = null;
            }
            walletTable.NodeRow = null;
        }
    }
    
    
    void RoomMenuClicked()
    {
        MenuItems[0].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[3].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        roomMenu = Instantiate(Resources.Load("Room Menu")) as GameObject;

        var panel = GameObject.Find("Panel");
        var newButton = roomMenu.transform.Find("New Button");
        var saveButton = roomMenu.transform.Find("Save Button");

         newButton.GetComponent<Button>().onClick.AddListener(() => newRoom());
         saveButton.GetComponent<Button>().onClick.AddListener(() => saveAllScenes());

        roomMenu.transform.SetParent(panel.transform, false);

        StartCoroutine(loadRooms());
    }

    void GalleryMenuClicked() {
        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[3].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        StartCoroutine(loadGalleries());
    }


    void NodesMenuClicked() {
        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[3].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        contentPanel.GetComponent<GridLayoutGroup>().cellSize = new Vector2(nodesTable.Size, 30.0f);
        contentPanel.GetComponent<GridLayoutGroup>().padding.top = 20;

        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        Headers = new GameObject[nodesTable.Fields.Length];
        var panel = GameObject.Find("Panel");

        for (int n = 0; n < nodesTable.Fields.Length; n++)
        {
            Headers[n] = new GameObject();

            Headers[n].AddComponent<Text>().text = nodesTable.Fields[n];
            Headers[n].GetComponent<Text>().font = textFont;
            Headers[n].GetComponent<Text>().resizeTextForBestFit = true;
            Headers[n].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            Headers[n].GetComponent<RectTransform>().sizeDelta = new Vector2(50.0f,20.0f);
            Headers[n].transform.position = new Vector3(-34.0f , 8.0f, 0.0f);
            Headers[n].transform.localScale = new Vector3(0.4f,1.0f, 1.0f);
            Headers[n].transform.SetParent(contentPanel.transform, false);

        }

        nodesTable.NodeRow = new NodeTableRow[Nodes.Count];

        for (int n = 0; n < Nodes.Count; n++)
        {
            nodesTable.NodeRow[n] = new NodeTableRow();
            nodesTable.NodeRow[n].Columns = new GameObject[nodesTable.Fields.Length];

            nodesTable.NodeRow[n].Columns[0] = new GameObject();
            nodesTable.NodeRow[n].Columns[0].AddComponent<Text>().text = Nodes[n].address;
            nodesTable.NodeRow[n].Columns[0].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[0].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[0].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[0].transform.position = new Vector3(StartPos[0], StartPos[1] + (n + 1) * Spacing[1] , 0);
            nodesTable.NodeRow[n].Columns[0].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[0].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[1] = new GameObject();
            nodesTable.NodeRow[n].Columns[1].AddComponent<Text>().text = Nodes[n].ip.ToString();
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[1].transform.position = new Vector3(StartPos[0] , StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[1].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[1].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[2] = new GameObject();
            nodesTable.NodeRow[n].Columns[2].AddComponent<Text>().text = Nodes[n].P2PPort.ToString();
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[2].transform.position = new Vector3(StartPos[0] , StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[2].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[2].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[3] = new GameObject();
            nodesTable.NodeRow[n].Columns[3].AddComponent<Text>().text = Nodes[n].ping.ToString();
            nodesTable.NodeRow[n].Columns[3].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[3].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[3].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[3].transform.position = new Vector3(StartPos[0] , StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[3].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[3].transform.SetParent(contentPanel.transform, false);

        }
    }

    void selectAddress(int locn, bool priv)
    {
        Debug.Log("addr sel " + wallet.addresses[locn].PubAddr);

        galleriesAddress.GetComponentInChildren<InputField>().text = wallet.addresses[locn].PubAddr;

        for (int n = 0; n < walletTable.NodeRow.Length; n++)
        {
            Destroy(walletTable.NodeRow[n].Columns[0].GetComponent<Outline>());
            if (n == locn)
            {
                walletTable.NodeRow[n].Columns[0].AddComponent<Outline>();
                walletTable.NodeRow[n].Columns[0].GetComponent<Outline>().effectColor = new Color(255, 0, 0, 255);
                walletTable.NodeRow[n].Columns[0].GetComponent<Outline>().effectDistance = new Vector2(2, 2);
            }
        }

        if (newAddr != null)
        {
            Destroy(newAddr);
            newAddr = null;
        }


        /*
        walletTable.NodeRow[n].Columns[0].GetComponentInChildren<Text>();
        walletTable.NodeRow[n].Columns[0].GetComponent<Button>().Select();
        */
    }

    void addContactAddress()
    {
        string curAddr = galleriesAddress.GetComponentInChildren<InputField>().text;
        string Label = newAddr.GetComponentInChildren<InputField>().text;

        WalletAddress address = new WalletAddress(Label, curAddr);

        wallet.addresses.Add(address);
        wallet.save();
        WalletMenuClicked();

    }

    void WalletMenuClicked() {

        float startY = 10.0f;
        int nWallets, nContacts;
        string curAddr = galleriesAddress.GetComponentInChildren<InputField>().text;
        bool found;

        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[3].GetComponentInChildren<Renderer>().material = selectedSphereMat;

        ClearTabs();

        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        Headers = new GameObject[2];

        Headers[0] = new GameObject();

        Headers[0].AddComponent<Text>().text = "Wallet";
        Headers[0].GetComponent<Text>().font = textFont;
        Headers[0].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        Headers[0].transform.position = new Vector3(StartPos[0], startY + StartPos[1], 0.0f);
        Headers[0].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
        Headers[0].transform.SetParent(contentPanel.transform, false);

        Headers[1] = new GameObject();

        Headers[1].AddComponent<Text>().text = "Contacts";
        Headers[1].GetComponent<Text>().font = textFont;
        Headers[1].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        Headers[1].transform.position = new Vector3(StartPos[0] + 100.0f, startY + StartPos[1], 0.0f);
        Headers[1].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
        Headers[1].transform.SetParent(contentPanel.transform, false);

        walletTable.NodeRow = new NodeTableRow[wallet.addresses.Count];


        nWallets = 0;
        nContacts = 0;
        found = false;

        for (int n = 0; n < wallet.addresses.Count; n++)
        {
            walletTable.NodeRow[n] = new NodeTableRow();
            walletTable.NodeRow[n].Columns = new GameObject[1];

            if (wallet.addresses[n].PrivKey != null)
            {
                walletTable.NodeRow[n].Columns[0] = Instantiate(Resources.Load("ButtonKey")) as GameObject;
                if (walletTable.NodeRow[n].Columns[0] != null)
                {
                    int locn = n;
                    
                    walletTable.NodeRow[n].Columns[0].GetComponentInChildren<Text>().text = wallet.addresses[n].Name;
                    walletTable.NodeRow[n].Columns[0].GetComponent<Button>().onClick.AddListener(() => selectAddress( locn, true));

                    walletTable.NodeRow[n].Columns[0].transform.position = new Vector3(StartPos[0], startY + StartPos[1] + (nWallets + 1) * Spacing[1], 0);
                    walletTable.NodeRow[n].Columns[0].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
                    walletTable.NodeRow[n].Columns[0].transform.SetParent(contentPanel.transform, false);

                    nWallets++;
                }
            }
            else
            {
                walletTable.NodeRow[n].Columns[0] = Instantiate(Resources.Load("ButtonContact")) as GameObject;

                if(walletTable.NodeRow[n].Columns[0] != null )
                {
                    int locn = n;
                    
                    walletTable.NodeRow[n].Columns[0].GetComponentInChildren<Text>().text = wallet.addresses[n].Name;
                    walletTable.NodeRow[n].Columns[0].GetComponent<Button>().onClick.AddListener(() => selectAddress(locn, false));

                    walletTable.NodeRow[n].Columns[0].transform.position = new Vector3(StartPos[0] + 100.0f, startY + StartPos[1] + (nContacts + 1) * Spacing[1], 0);
                    walletTable.NodeRow[n].Columns[0].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
                    walletTable.NodeRow[n].Columns[0].transform.SetParent(contentPanel.transform, false);

                    nContacts++;
                }
            }

            if (curAddr == wallet.addresses[n].PubAddr)
            {
                walletTable.NodeRow[n].Columns[0].AddComponent<Outline>();
                walletTable.NodeRow[n].Columns[0].GetComponent<Outline>().effectColor = new Color(255, 0, 0, 255);
                walletTable.NodeRow[n].Columns[0].GetComponent<Outline>().effectDistance = new Vector2(2, 2);

                found = true;
            }
        }

        if(!found)
        {
            newAddr = Instantiate(Resources.Load("NewAddress")) as GameObject;
            newAddr.GetComponentInChildren<Button>().onClick.AddListener(() => addContactAddress());
            newAddr.transform.SetParent(contentPanel.transform, false);
        }
    }

    /*
    IEnumerator SubmitTx(string txid)
    {
        string URL = "http://" + server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitTx " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitTx " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                Debug.Log("SubmitTx " + submittx + " : \nReceived: " + webRequest.downloadHandler.text);
                break;
        }
    }

    IEnumerator signTxInputs(Transaction tx)
    {
        string URL = "http://" + server + "/jsonrpc";

        for (int n = 0; n < tx.txsin.Count; n++)
        {
            byte[] derSign = wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(tx.txsin[n].signHash), wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + tx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signTx  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signTx  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("signTx signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);

                    tx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;

                    Debug.Log("signTx signtxinput " + tx.txid);
                    break;
            }
        }
        tx.issigned = true;
        StartCoroutine(SubmitTx(tx.txid));
    }

    IEnumerator addnodes(gltfRef gltfref)
    {
        string URL = "http://" + server + "/jsonrpc";

        for (int on = 0; on < gltfref.objs.Count; on++)
        {
            string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + appName + "\",\"" +  gltfref.sceneTx.txid + "\",\"nodes\",\"" + gltfref.objs[on].nodeTx.txid + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, addchild);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("addnode  " + addchild + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("addnode  " + addchild + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("addnode " + addchild + " : \nReceived: " + webRequest.downloadHandler.text);
                    gltfref.objs[on].nodePTx = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                    Debug.Log("addnode myTransaction " + gltfref.objs[on].nodePTx.txid+ " " + gltfref.sceneTx.txid);
                    gltfref.objs[on].nodePTx.issigned = false;
                    StartCoroutine(signTxInputs(gltfref.objs[on].nodePTx));

                    break;
            }
        }
    }

    IEnumerator SubmitNodesTx(gltfRef gltfref)
    {
        string URL = "http://" + server + "/jsonrpc";

        for (int n = 0; n < gltfref.objs.Count; n++)
        {
            string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + gltfref.objs[n].nodeTx.txid + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("SubmitNodeTx " + submittx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("SubmitNodeTx " + submittx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("SubmitNodeTx " + submittx + " : \nReceived: " + webRequest.downloadHandler.text);
                    break;
            }
        }

        StartCoroutine(addnodes(gltfref));
    }

    IEnumerator signNodesTxInputs(gltfRef gltfref)
    {
        string URL = "http://" + server + "/jsonrpc";

        for (int on = 0; on < gltfref.objs.Count; on++)
        {
            for (int n = 0; n < gltfref.objs[on].nodeTx.txsin.Count; n++)
            {
                byte[] derSign = wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.objs[on].nodeTx.txsin[n].signHash), wallet.domainParams);
                string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + gltfref.objs[on].nodeTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
                UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
                webRequest.SetRequestHeader("Content-Type", "application/json");
                // Request and wait for the desired page.
                yield return webRequest.SendWebRequest();

                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.Log("signNodeTxInputs  " + signtx + " : Error: " + webRequest.error);
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.Log("signNodeTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                        break;
                    case UnityWebRequest.Result.Success:

                        Debug.Log("signNodeTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);

                        gltfref.objs[on].nodeTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;

                        break;
                }
            }
            gltfref.objs[on].nodeTx.issigned = true;
        }
        StartCoroutine(SubmitNodesTx(gltfref));
    }

 
    IEnumerator makenodes(gltfRef gltfref)
    {
        string URL = "http://" + server + "/jsonrpc";


        for (int n = 0; n < gltfref.objs.Count; n++)
        {
            GameObject obj = gltfref.objs[n].obj;

            var nodeJson = "{name: \"node " + n.ToString() + "\", translation: [" + obj.transform.position[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.position[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.position[2].ToString(System.Globalization.CultureInfo.InvariantCulture) + "] , scale: [" + obj.transform.localScale[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.localScale[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.localScale[2].ToString(System.Globalization.CultureInfo.InvariantCulture) + "], rotation: [" + obj.transform.rotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.rotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.rotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + obj.transform.rotation.w.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]}";

            string makenodeobj = "{id:1 , jsonrpc: \"2.0\", method:\"makeappobjtx\", params : [\"" + appName + "\"," + nodeTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + nodeJson + "]}";

            UnityWebRequest webRequest = UnityWebRequest.Put(URL, makenodeobj);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("makenode  " + makenodeobj + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("makenode  " + makenodeobj + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("makenode " + makenodeobj + " : \nReceived: " + webRequest.downloadHandler.text);
                    gltfref.objs[n].nodeTx = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                    gltfref.objs[n].nodeTx.issigned = false;
                    Debug.Log("makenode myTransaction " + gltfref.objs[n].nodeTx.txid);
                    break;
            }
        }

        StartCoroutine(signNodesTxInputs(gltfref));
    }


    IEnumerator addRoomSceneObj(gltfRef gltfref)
    {
        gltfref.sceneTx.issigned = true;

        for (int n = 0; n < room.sceneObjects.Count; n++)
        {
            if (room.sceneObjects[n].sceneTx == null)
                yield break;

            if (!room.sceneObjects[n].sceneTx.issigned)
                yield break;
        }

        string URL = "http://" + server + "/jsonrpc";

        for (int n = 0; n < room.sceneObjects.Count; n++)
        {
            string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + appName + "\",\"" + room.roomTx.txid + "\",\"objects\",\"" + room.sceneObjects[n].sceneTx.txid + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, addchild);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();
            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("addRoomSceneObj  " + addchild + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("addRoomSceneObj  " + addchild + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("addRoomSceneObj " + addchild + " : \nReceived: " + webRequest.downloadHandler.text);
                    Transaction myTransaction = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                    Debug.Log("addRoomSceneObj myTransaction " + myTransaction.txid + " " + room.roomTx.txid);
                    myTransaction.issigned = false;
                    StartCoroutine(signTxInputs(myTransaction));

                    break;
            }
        }
    }



    IEnumerator SubmitSceneTx(gltfRef gltfref)
    {
        string URL = "http://" + server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + gltfref.sceneTx.txid+"\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitSceneTx  " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitSceneTx  " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                Debug.Log("SubmitSceneTx signtxinput" + submittx + " : \nReceived: " + webRequest.downloadHandler.text);
                
                StartCoroutine(makenodes(gltfref));
                break;
        }
    }

    IEnumerator signSceneTxInputs(gltfRef gltfref)
    {
        string URL = "http://" + server + "/jsonrpc";


        for (int n = 0; n < gltfref.sceneTx.txsin.Count; n++)
        {
            byte[] derSign = wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.sceneTx.txsin[n].signHash), wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + gltfref.sceneTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signSceneTxInputs  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signSceneTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("signSceneTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);
                    gltfref.sceneTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;
                    Debug.Log("signSceneTxInputs signtxinput" + gltfref.sceneTx.txid);
                    break;
            }
        }
        StartCoroutine(addRoomSceneObj(gltfref));

        StartCoroutine(SubmitSceneTx(gltfref));
    }


    IEnumerator makeobjs(vrRoom Room)
    {
        string URL = "http://" + server + "/jsonrpc";


        for (int n = 0; n < Room.sceneObjects.Count; n++)
        {
            gltfRef gltfref = Room.sceneObjects[n];
            string sceneJSon = "{root : \"" + gltfref.rootHash + "\"}";
            string makeappobj = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + appName + "\"," + sceneTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + sceneJSon + "]}";
            Debug.Log("makeobj  " + URL + "  " + makeappobj);

            UnityWebRequest scenewebRequest = UnityWebRequest.Put(URL, makeappobj);
            scenewebRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return scenewebRequest.SendWebRequest();

            switch (scenewebRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("makeobj  " + makeappobj + " : Error: " + scenewebRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("makeobj  " + makeappobj + " : HTTP Error: " + scenewebRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("makeobj " + makeappobj + " : \nReceived: " + scenewebRequest.downloadHandler.text);
                    gltfref.sceneTx = JsonUtility.FromJson<RPCTransaction>(scenewebRequest.downloadHandler.text).result.transaction;
                    gltfref.sceneTx.issigned = false;
                    Debug.Log("makeobj myTransaction" + gltfref.sceneTx.txid + " " + gltfref.sceneTx.txsin.Count);
                    StartCoroutine(signSceneTxInputs(gltfref));

                    break;
            }
        }
    }


    IEnumerator SubmitRoomTx(vrRoom Room)
    {
        string URL = "http://" + server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + room.roomTx.txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitRoomTx " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitRoomTx " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                Debug.Log("SubmitRoomTx " + submittx + " : \nReceived: " + webRequest.downloadHandler.text);

                StartCoroutine(makeobjs(Room));
                break;
        }
    }
    IEnumerator signRoomTxInputs(vrRoom room)
    {
        string URL = "http://" + server + "/jsonrpc";


        for (int n = 0; n < room.roomTx.txsin.Count; n++)
        {
            byte[] derSign = wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(room.roomTx.txsin[n].signHash), wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + room.roomTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signRoomTxInputs  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signRoomTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("signRoomTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);

                    room.roomTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;

                    Debug.Log("signRoomTxInputs signtxinput" + room.roomTx.txid);
                    break;
            }
        }

        room.roomTx.issigned = true;

        StartCoroutine(SubmitRoomTx(room));
    }


    IEnumerator makeroom(vrRoom Room)
    {
        string URL = "http://" + server + "/jsonrpc";
        string roomJSon = "{name : \"" + Room.name + "\"}";
        string makeapproom = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + appName + "\"," + roomTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + roomJSon + "]}";
        Debug.Log("makeapproom  " + URL + "  " + makeapproom);

        UnityWebRequest scenewebRequest = UnityWebRequest.Put(URL, makeapproom);
        scenewebRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return scenewebRequest.SendWebRequest();

        switch (scenewebRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("makeroom  " + makeapproom + " : Error: " + scenewebRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("makeroom  " + makeapproom + " : HTTP Error: " + scenewebRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                Debug.Log("makeroom " + makeapproom + " : \nReceived: " + scenewebRequest.downloadHandler.text);
                Room.roomTx = JsonUtility.FromJson<RPCTransaction>(scenewebRequest.downloadHandler.text).result.transaction;
                Debug.Log("makeroom myTransaction" + Room.roomTx.txid + " " + Room.roomTx.txsin.Count);
                Room.roomTx.issigned = false;
                StartCoroutine(signRoomTxInputs(Room));

                break;
        }
    }
    */

    void saveAllScenes()
    {
        Debug.Log("save scenes " + room.sceneObjects.Count);
        StartCoroutine(room.makeroom(makeSaveInfos()));
    }


    void loadScene(string hash)
    {
        string URL = "http://" + server + baseURL + "/page/unity.site/scene/" + hash;
        

        Debug.Log("loading scene " + URL);

        GameObject obj = new GameObject("Scene " + hash);

        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;

        obj.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.5f, 0.5f);
        obj.AddComponent<Rigidbody>().isKinematic = true;
        obj.AddComponent<XRGrabInteractable>();

        obj.layer = 7;

        objNode on = new objNode(obj);

        for (int n = 0; n < room.sceneObjects.Count; n++)
        {
            if (room.sceneObjects[n].rootHash == hash)
            {
                room.sceneObjects[n].objs.Add(on);
                return;
            }
        }

        gltfRef myref = new gltfRef(hash);
        myref.objs.Add(on);
        room.sceneObjects.Add(myref);
    }


    void loadGLTF(string hash)
    {
        string URL = "http://" + server + baseURL + "/obj/" + hash;


        Debug.Log("loading scene " + URL);

        GameObject obj = new GameObject("Mesh " + hash);

        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;


        if (EditRoomWall)
        {
            if (currentWallObj != null)
                Destroy(currentWallObj);

            currentWallObj = obj;
            currentWallHash = hash;

            currentWallObj.transform.position = new Vector3(0.0f, 0.0f, -10.0f);
            currentWallObj.transform.rotation = Quaternion.Euler(-45.0f, 0.0f, 0.0f);
            currentWallObj.transform.localScale = new Vector3(20.0f, 20.0f, 20.0f);

            var wallObj = GameObject.Find("WallObj");
            currentWallObj.transform.SetParent(wallObj.transform, false);

        }
        else
        {
            room.addObj(obj, hash);

            obj.layer = 7;

            obj.AddComponent<Rigidbody>();
            obj.AddComponent<XRGrabInteractable>();
        }
    }

    IEnumerator loadGalleries()
    {
        string myAddr = galleriesAddress.GetComponentInChildren<InputField>().text;
        string URL = "http://" + server + baseURL;
        string ldgal = URL + "/objlst/47/" + myAddr;

        Debug.Log("url " + ldgal);


        UnityWebRequest webRequest = UnityWebRequest.Get(ldgal);
        
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("GALLERY LIST  " + myAddr + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("GALLERY LIST  " + myAddr + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:


                Debug.Log("GALLERY LIST " + myAddr + " : \nReceived: " + webRequest.downloadHandler.text);

                galleries = ParseJsonArray<Gallery[]>(webRequest.downloadHandler.text);

                if (GalleriesButton != null)
                {
                    for (int n = 0; n < GalleriesButton.Length; n++)
                    {
                        Destroy(GalleriesButton[n]);
                    }

                    GalleriesButton = null;
                }

                if (emptyResult != null)
                    Destroy(emptyResult);

                if (galleries.Length > 0)
                {
                    GalleriesButton = new GameObject[galleries.Length];

                    var panel = GameObject.Find("Panel");

                    for (int n = 0; n < galleries.Length; n++)
                    {
                        string galleryHash = galleries[n].objHash;

                        GalleriesButton[n] = Instantiate(Resources.Load("ButtonGallery")) as GameObject;
                        GalleriesButton[n].transform.position = new Vector3(StartPos[0] + n * Spacing[0], StartPos[1] + n * Spacing[1], 0);
                        GalleriesButton[n].transform.localScale= new Vector3(1.0f, 1.0f, 1.0f);

                        GalleriesButton[n].transform.SetParent(indexPanel.transform, false);

                        GalleriesButton[n].GetComponentInChildren<Text>().text = galleries[n].name;
                        GalleriesButton[n].name = "gallery " + galleries[n].name;

                        GalleriesButton[n].GetComponent<Button>().onClick.AddListener(() => GalleryClicked(galleryHash));
                    }
                }
                else
                {
                    if(emptyResult != null)
                        Destroy(emptyResult);

                    emptyResult = new GameObject();
                    emptyResult.AddComponent<Text>().text = "no gallaries for this address";
                    emptyResult.GetComponent<Text>().font = textFont;
                    emptyResult.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                    emptyResult.transform.position = new Vector3(ItemsStartPos[0], StartPos[1], 0.0f);
                    emptyResult.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                    emptyResult.transform.SetParent(contentPanel.transform, false);
                }


                break;
        }

    }

    IEnumerator loadRooms()
    {
        string myAddr = galleriesAddress.GetComponentInChildren<InputField>().text;
        string URL = "http://" + server + baseURL + "/objlst/" + roomTypeId.ToString() + "/" + myAddr;

        Debug.Log("load Rooms " + URL);


        UnityWebRequest webRequest = UnityWebRequest.Get(URL);

        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("ROOM LIST  " + myAddr + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("ROOM LIST  " + myAddr + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                float itemX, itemY;

               
                Destroy(emptyResult);
                if (ItemsButton != null)
                {
                    for (int n = 0; n < ItemsButton.Length; n++)
                    {
                        Destroy(ItemsButton[n]);
                    }
                }

                rooms = ParseJsonArray<Room []>(webRequest.downloadHandler.text);

                if (rooms != null) 
                {
                    ItemsButton = new GameObject[rooms.Length];

                    itemX = ItemsStartPos[0];
                    itemY = ItemsStartPos[1];

                    for (int n = 0; n < rooms.Length; n++)
                    {
                        string itemHash = rooms[n].objHash;
                        ItemsButton[n] = Instantiate(Resources.Load("ButtonRoom")) as GameObject;
                        ItemsButton[n].transform.position = new Vector3(itemX, itemY, 0.0f);
                        ItemsButton[n].transform.SetParent(contentPanel.transform, false);

                        ItemsButton[n].GetComponentInChildren<Text>().text = rooms[n].name;
                        ItemsButton[n].name = "room " + rooms[n].name;

                        ItemsButton[n].GetComponent<Button>().onClick.AddListener(() => RoomClicked(itemHash));

                        itemX += ItemsSpacing[0];

                        if (itemX >= 3.0f * ItemsSpacing[0])
                        {
                            itemY += ItemsSpacing[1];
                            itemX = ItemsStartPos[0];
                        }
                    }
                }
                else
                {
                    if (emptyResult != null)
                        Destroy(emptyResult);

                    emptyResult = new GameObject();
                    emptyResult.AddComponent<Text>().text = "no rooms";
                    emptyResult.GetComponent<Text>().font = textFont;
                    emptyResult.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                    emptyResult.transform.position = new Vector3(ItemsStartPos[0], StartPos[1], 0.0f);
                    emptyResult.transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);

                    emptyResult.transform.SetParent(contentPanel.transform, false);
                }


                break;
        }

    }

   
    IEnumerator loadWebGallery(string hash)
    {
        string URL = "http://" + server + baseURL;
        string gu = URL + "/obj/" + hash+"/2";
        Debug.Log("loading gallery " + gu);
        
        UnityWebRequest webRequest = UnityWebRequest.Get(gu);

        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.LogError("GALLERY : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.LogError("GALLERY : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                float itemX, itemY;

                Debug.Log("gallery " + hash + " data " + webRequest.downloadHandler.text);
                Destroy(emptyResult);
                if (ItemsButton != null)
                {
                    for (int n = 0; n < ItemsButton.Length; n++)
                    {
                        Destroy(ItemsButton[n]);
                    }
                }

                myGallery = JsonUtility.FromJson<Gallery>(webRequest.downloadHandler.text);

                if ((myGallery != null) && (myGallery.scenes != null))
                {
                    ItemsButton = new GameObject[myGallery.scenes.Length];

                    itemX = ItemsStartPos[0];
                    itemY = ItemsStartPos[1];

                    for (int n = 0; n < myGallery.scenes.Length; n++)
                    {
                        string itemHash = myGallery.scenes[n].scene.objHash;
                        ItemsButton[n] = Instantiate(Resources.Load("ButtonItem")) as GameObject;
                        ItemsButton[n].transform.position = new Vector3(itemX, itemY, 0.0f);
                        ItemsButton[n].transform.SetParent(contentPanel.transform, false);

                        ItemsButton[n].GetComponentInChildren<Text>().text = myGallery.scenes[n].name;
                        ItemsButton[n].name = "item " + myGallery.scenes[n].name;

                        ItemsButton[n].GetComponent<Button>().onClick.AddListener(() => loadGLTF(itemHash));

                        itemX += ItemsSpacing[0];

                        if (itemX >= 2.0f * ItemsSpacing[0])
                        {
                            itemY += ItemsSpacing[1];
                            itemX = ItemsStartPos[0];
                        }
                    }
                }
                else
                {
                    if(emptyResult!=null)
                        Destroy(emptyResult);

                    emptyResult = new GameObject();
                    emptyResult.AddComponent<Text>().text = "no items";
                    emptyResult.GetComponent<Text>().font = textFont;
                    emptyResult.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                    emptyResult.transform.position = new Vector3(ItemsStartPos[0], StartPos[1], 0.0f);
                    emptyResult.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                    emptyResult.transform.SetParent(contentPanel.transform, false);
                }

               
            break;
        }
    }

    IEnumerator loadRoom(string hash)
    {
        string URL = "http://" + server + baseURL + "/obj/" + hash + "/15";
        Debug.Log("loading room " + URL);

        UnityWebRequest webRequest = UnityWebRequest.Get(URL);

        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.LogError("Room : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.LogError("Room : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                Debug.Log("ROOM  " + hash + " : \nReceived: " + webRequest.downloadHandler.text);

                var mroom = JsonUtility.FromJson<Room>(webRequest.downloadHandler.text);
                if(mroom.objects == null)
                {
                    Debug.Log("ROOM  no objects");
                }
                else
                {
                    for (int n = 0; n < mroom.objects.Length; n++)
                    {
                        loadScene(mroom.objects[n].objHash);
                    }
                }
             break;
        }
    }

    void RoomClicked(string hash)
    {
        Debug.Log("room " + hash + " selected");
        StartCoroutine(loadRoom(hash));
    }

    void GalleryClicked(string hash)
    {
        Debug.Log("gallery " + hash + " selected");
        StartCoroutine(loadWebGallery(hash));
    }

    public void ResetGrid()
    {
        for (int n = 0; n < grid.cells.Length; n++)
        {
            Destroy(grid.cells[n].obj);
        }
    }

    void roomEditor()
    {
        grid = new Grid(-10, -10, 20, 20, selectedSphereMat, SphereEditMat, defaultSphereMat);

        EditWallPanel = Instantiate(Resources.Load("EditWalls")) as GameObject;
        GameObject.Find("ToggleWall").GetComponent<Toggle>().onValueChanged.AddListener(ToggleEditWalls);
        GameObject.Find("ToggleRoom").GetComponent<Toggle>().onValueChanged.AddListener(ToggleEditRoom);
        GameObject.Find("ToggleSet").GetComponent<Toggle>().onValueChanged.AddListener(ToggleEditWall);

        CreateWalls = true;
        EditRoomWall = true;

        var CameraOffset = GameObject.Find("Camera Offset");
        CameraOffset.transform.position = new Vector3(-0, 100.0f, -80.0f);
        CameraOffset.transform.rotation = Quaternion.Euler(new Vector3(45, 0, 0));

        floorPlane.SetActive(false);
    }

    void newRoom()
    {
        for (int n = 0; n < room.sceneObjects.Count; n++)
        {
            for (int i = 0; i < room.sceneObjects[n].objs.Count; i++)
            {
                Destroy(room.sceneObjects[n].objs[i].obj);
            }
            room.sceneObjects[n].objs = new List<objNode>();
        }
        room.sceneObjects = new List<gltfRef>();

        if (wallSegments != null)
        {
            for (int n = 0; n < wallSegments.Count; n++)
            {
                Destroy(wallSegments[n].defObj);
            }
        }

        wallSegments = new List<WallSegment>();

        if (grid != null)
            ResetGrid();

        if (EditWallPanel != null)
            Destroy(EditWallPanel);

        roomEditor();
    }

    public int FindWallSegment(int x, int y)
    {
        for (int n = 0; n < wallSegments.Count; n++)
        {
            WallSegment mySeg = wallSegments[n];

            if ((mySeg.startCell.X == x)&& (mySeg.startCell.Y == y))
            {
                return n;
            }
        }
        return -1;
    }

    void computeNewSeg()
    {
        if (currentSeg == null)
        {
            currentSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            currentSeg.GetComponentInChildren<Renderer>().material = wallSegMat;
            
            currentSeg.layer = 6;
        }

        if (wallSegments.Count >= 2)
        {
            if (lastSeg == null)
            {
                lastSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lastSeg.GetComponentInChildren<Renderer>().material = wallSegMat;
                lastSeg.layer = 6;
            }

            Vector3 ldelta = new Vector3(wallSegments[0].startCell.obj.transform.position.x - hoveredCell.obj.transform.position.x, 0.0f, wallSegments[0].startCell.obj.transform.position.z - hoveredCell.obj.transform.position.z);

            lastSeg.transform.position = new Vector3(hoveredCell.obj.transform.position.x + ldelta.x / 2.0f, -1.0f + wallHeight / 2.0f, hoveredCell.obj.transform.position.z + ldelta.z / 2.0f);
            lastSeg.transform.localScale = new Vector3(4.0f, wallHeight, ldelta.magnitude);

            ldelta.Normalize();
            if (ldelta.sqrMagnitude > 0.001f)
            {
                double langle;

                langle = Math.Atan2(ldelta.x, ldelta.z);
                lastSeg.transform.rotation = Quaternion.Euler(0.0f, (float)(langle * 180.0f / Math.PI), 0.0f);
            }
        }

        var delta = new Vector3(hoveredCell.obj.transform.position.x - selectedCell.obj.transform.position.x, 0.0f, hoveredCell.obj.transform.position.z - selectedCell.obj.transform.position.z);

        currentSeg.transform.position = new Vector3(selectedCell.obj.transform.position.x + delta.x / 2.0f, -1.0f + wallHeight / 2.0f, selectedCell.obj.transform.position.z + delta.z / 2.0f);
        currentSeg.transform.localScale = new Vector3(4.0f, wallHeight, delta.magnitude);

        delta.Normalize();
        if (delta.sqrMagnitude > 0.001f)
        {
            double angle;

            angle = Math.Atan2(delta.x, delta.z);
            currentSeg.transform.rotation = Quaternion.Euler(0.0f, (float)(angle * 180.0f / Math.PI), 0.0f);
        }
    }


    void updateWallSeg(int n)
    {
        int seg1, seg2;

        seg1 = n;

        if ( n == 0)
        {
            seg2 = wallSegments.Count - 1;
        }
        else
        {
            seg2 = n - 1;
        }

        wallSegments[seg1].startCell = hoveredCell;
        wallSegments[seg2].endCell = hoveredCell;


        string URL = null;
        
        if (wallSegments[seg1].gltfObjs != null)
        {
            URL = "http://" + server + baseURL + "/obj/" + wallSegments[seg1].gltfObjs.rootHash;
           for (int i = 0; i < wallSegments[seg1].gltfObjs.objs.Count; i++)
           {
               Destroy(wallSegments[seg1].gltfObjs.objs[i].obj);
           }
        }
   
        wallSegments[seg1].recompute(wallHeight, URL);

        if (wallSegments[seg2].gltfObjs != null)
        {
            URL = "http://" + server + baseURL + "/obj/" + wallSegments[seg2].gltfObjs.rootHash;
            for (int i = 0; i < wallSegments[seg2].gltfObjs.objs.Count; i++)
            {
                Destroy(wallSegments[seg2].gltfObjs.objs[i].obj);
            }
        }

        wallSegments[seg2].recompute(wallHeight, URL);
       

    }

    void MakeLastSeg()
    {
        if (wallSegments.Count >= 2)
        {
            var newSegObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newSegObj.GetComponentInChildren<Renderer>().material = wallSegMat;
            newSegObj.layer = 6;

            Vector3 ldelta = new Vector3(wallSegments[0].startCell.obj.transform.position.x - wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.x, 0.0f, wallSegments[0].startCell.obj.transform.position.z - wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.z);

            newSegObj.transform.position = new Vector3(wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.x + ldelta.x / 2.0f, -1.0f + wallHeight / 2.0f, wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.z + ldelta.z / 2.0f);
            newSegObj.transform.localScale = new Vector3(4.0f, wallHeight, ldelta.magnitude);

            ldelta.Normalize();
            if (ldelta.sqrMagnitude > 0.001f)
            {
                double langle;
                langle = Math.Atan2(ldelta.x, ldelta.z);
                newSegObj.transform.rotation = Quaternion.Euler(0.0f, (float)(langle * 180.0f / Math.PI), 0.0f);
            }

           WallSegment seg = new WallSegment();

           seg.defObj = newSegObj;
           seg.startCell = wallSegments[wallSegments.Count - 1].endCell;
           seg.endCell = wallSegments[0].startCell;

           seg.defObj.name = "Wall " + wallSegments.Count;

           wallSegments.Add(seg);
        }
        if (lastSeg != null)
        {
            Destroy(lastSeg);
            lastSeg = null;
        }
        if (currentSeg != null)
        {
            Destroy(currentSeg);
            currentSeg = null;
        }
    }

    void removeLastWall()
    {
        if (wallSegments.Count >= 2)
        {
            Destroy(wallSegments[wallSegments.Count - 1].defObj);

            if (wallSegments[wallSegments.Count - 1].gltfObjs != null)
            {
                for (int n = 0; n < wallSegments[wallSegments.Count - 1].gltfObjs.objs.Count; n++)
                {
                    Destroy(wallSegments[wallSegments.Count - 1].gltfObjs.objs[n].obj);
                }
            }

            wallSegments.RemoveAt(wallSegments.Count - 1);
        }
    }

    void EnableCreateWalls()
    {
        removeLastWall();

        if (wallSegments.Count > 0)
            selectedCell = wallSegments[wallSegments.Count - 1].endCell;

        CreateWalls = true;
        outScope = false;
    }

    void DisableCreateWalls()
    {
        if (hoveredCell != null)
            hoveredCell.obj.GetComponentInChildren<Renderer>().material = defaultSphereMat;

        if (selectedCell != null)
            selectedCell.obj.GetComponentInChildren<Renderer>().material = defaultSphereMat;

        hoveredCell = null;
        selectedCell = null;

        CreateWalls = false;
    }

    Vector3 findWallCenter()
    {
        Vector3 center;
        Vector3 min, max;

        if (wallSegments.Count < 2)
            return new Vector3(0.0f, 0.0f, 0.0f);
        
        min = wallSegments[0].startCell.obj.transform.position;
        max = wallSegments[0].startCell.obj.transform.position;
        for (int n=1;n< wallSegments.Count;n++)
        {
            if (wallSegments[n].startCell.obj.transform.position.x < min.x)
                min.x = wallSegments[n].startCell.obj.transform.position.x;

            if (wallSegments[n].startCell.obj.transform.position.y < min.y)
                min.y = wallSegments[n].startCell.obj.transform.position.y;

            if (wallSegments[n].startCell.obj.transform.position.z < min.z)
                min.z = wallSegments[n].startCell.obj.transform.position.z;

            if (wallSegments[n].startCell.obj.transform.position.x > max.x)
                max.x = wallSegments[n].startCell.obj.transform.position.x;

            if (wallSegments[n].startCell.obj.transform.position.y > max.y)
                max.y = wallSegments[n].startCell.obj.transform.position.y;

            if (wallSegments[n].startCell.obj.transform.position.z > max.z)
                max.z = wallSegments[n].startCell.obj.transform.position.z;
        }

        center = min + (max - min) / 2.0f;
        return center;
    }

    Vector2[] computeUvs(Vector3[] vertices)
    {
      
        Vector3 min, max;
        Vector2 size;

        min = vertices[0];
        max = vertices[0];
        for (int n = 0; n < vertices.Length; n++)
        {
            if (vertices[n].x < min.x)
                min.x = vertices[n].x;

            if (vertices[n].y < min.y)
                min.y = vertices[n].y;

            if (vertices[n].z < min.z)
                min.z = vertices[n].z;

            if (vertices[n].x > max.x)
                max.x = vertices[n].x;

            if (vertices[n].y > max.y)
                max.y = vertices[n].y;

            if (vertices[n].z > max.z)
                max.z = vertices[n].z;
        }

        size.x = max.x - min.x;
        size.y = max.z - min.z;

        Vector2[] uvs = new Vector2[vertices.Length];

        for (int n = 0; n < vertices.Length; n++)
        {
            uvs[n].x = (vertices[n].x - min.x)/ size.x;
            uvs[n].y = (vertices[n].z - min.z)/ size.y;
        }


        return uvs;


    }
 

    Vector3 createFloor()
    {
        if (roomFloor != null)
            Destroy(roomFloor);

        roomFloor = new GameObject();
        roomFloor.AddComponent<MeshFilter>();
        roomFloor.AddComponent<MeshRenderer>().material = Instantiate(Resources.Load("FloorMat")) as Material;
        
        var mesh = new Mesh();
        
        var vertices = new Vector3[wallSegments.Count + 1];
        var normals = new Vector3[wallSegments.Count + 1];
        var triangles = new int[(wallSegments.Count +1) * 3];

        vertices[0] = findWallCenter();
        normals[0] = new Vector3(0.0f, 1.0f,0.0f);

        for (int n = 0; n < wallSegments.Count; n++)
        {
            vertices[n + 1] = new Vector3(wallSegments[n].startCell.obj.transform.position.x, wallSegments[n].startCell.obj.transform.position.y, wallSegments[n].startCell.obj.transform.position.z);
            normals[ n + 1] = new Vector3(0.0f, 1.0f, 0.0f);
        }

        for (int n = 0; n < wallSegments.Count; n++)
        {
            triangles[n * 3 + 0] = n + 1;

            if( n < (wallSegments.Count-1))
                triangles[n * 3 + 1] = n + 2; 
            else
                triangles[n * 3 + 1] = 1;

            triangles[n * 3 + 2] = 0;
        }


        mesh.vertices = vertices;
        mesh.uv = computeUvs(vertices);
        mesh.normals = normals;
        mesh.triangles = triangles;

        roomFloor.GetComponent<MeshFilter>().mesh = mesh;
        roomFloor.AddComponent<MeshCollider>().sharedMesh = mesh;
        roomFloor.AddComponent<TeleportationArea>();

        roomFloor.transform.position = new Vector3(0.0f, -1.0f, 0.0f);

        return vertices[0];

       


    }

    
    void ToggleEditRoom(bool state)
    {
        if (!state)
        {
            Vector3 camPos;
            if(wallSegments.Count>=2)
                camPos = createFloor();
            else
            {
               floorPlane.SetActive(true);
                camPos = new Vector3(0.0f, 1.0f, 0.0f);
            }

            var CameraOffset = GameObject.Find("Camera Offset");
            CameraOffset.transform.position = camPos;
            CameraOffset.transform.rotation = Quaternion.Euler(new Vector3(-12.0f, 0, 0));

            Destroy(EditWallPanel);
            ResetGrid();

            EditRoomWall = false;

            var rightCont = GameObject.Find("RightHand Controller");
            if (rightCont != null)
            {
                XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
                xrri.raycastMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Walls") | LayerMask.GetMask("UI");
            }
        }
        else
        {
            var CameraOffset = GameObject.Find("Camera Offset");
            CameraOffset.transform.position = new Vector3(-0, 100.0f, -80.0f);
            CameraOffset.transform.rotation = Quaternion.Euler(new Vector3(45, 0, 0));

            GameObject.Find("ToggleWall").GetComponent<Toggle>().enabled = true;
        }
    }

    int prevLayerMask;
    bool SetWallObj;
    bool lastTrigger;
    bool hasHeadset;


    void ToggleEditWall(bool state)
    {
        var rightCont = GameObject.Find("RightHand Controller");

        if (state)
        {
            if (rightCont != null)
            {
                XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
                prevLayerMask = xrri.raycastMask;
                xrri.raycastMask = LayerMask.GetMask("Walls") | LayerMask.GetMask("UI");
            }

            GameObject.Find("ToggleWall").GetComponent<Toggle>().isOn = false;
            SetWallObj = true;

        }
        else
        {
            SetWallObj = false;
            if (rightCont != null)
            {
                XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
                xrri.raycastMask = prevLayerMask;
            }
        }
    }

    void ToggleEditWalls(bool state)
    {
        var rightCont = GameObject.Find("RightHand Controller");

        if (rightCont != null)
        {
            XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
            xrri.raycastMask = LayerMask.GetMask("Default") | LayerMask.GetMask("UI");
        }

        if (!state)
        {
            DisableCreateWalls();
        }
        else
        {
            GameObject.Find("ToggleSet").GetComponent<Toggle>().isOn = false;
            EnableCreateWalls();
        }
    }

     
  
    bool hasHMD()
    {
        var HMDControllers = new List<UnityEngine.XR.InputDevice>();
        var desiredCharacteristics = UnityEngine.XR.InputDeviceCharacteristics.HeadMounted;
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, HMDControllers);

        if (HMDControllers.Count > 0)
            return true;
        else
            return false;
    }

    void updateGrid()
    {
        Boolean button = false;
        
        RaycastHit hit = new RaycastHit();
        GameObject hitObj = null;

        hasHeadset = hasHMD();


        List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();

        UnityEngine.XR.InputDevices.GetDevicesWithRole(UnityEngine.XR.InputDeviceRole.RightHanded, devices);

        foreach (var device in devices)
        {
            bool Trigger;

            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out Trigger))
            {
                if (Trigger != lastTrigger)
                    button = Trigger;

                lastTrigger = Trigger;
            }
        }

    
        if(!button)    
            button = Input.GetMouseButtonDown(0);

        if (button) 
        {
            if (selectedCell != null)
                selectedCell.obj.GetComponentInChildren<Renderer>().material = defaultSphereMat;

            if (CreateWalls)
            {
                if (hoveredCell != null)
                {
                    if (currentSeg != null)
                    {
                        WallSegment seg = new WallSegment();

                        seg.defObj = currentSeg;
                        seg.startCell = selectedCell;
                        seg.endCell = hoveredCell;

                        seg.defObj.name = "Wall " + wallSegments.Count;

                        wallSegments.Add(seg);

                        currentSeg = null;
                    }

                    selectedCell = hoveredCell;
                    selectedCell.obj.GetComponentInChildren<Renderer>().material = selectedSphereMat;
                }
            }
            else if (SetWallObj)
            {
                if ((HoveredWallSeg < 0) && (currentWallObj != null))
                {
                    hitObj = null;

                    if (hasHeadset)
                    {
                        var rightCont = GameObject.Find("RightHand Controller");
                        if (rightCont != null)
                        {
                            XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
                            if (xrri.TryGetCurrent3DRaycastHit(out hit))
                                hitObj = hit.collider.gameObject;
                        }
                    }
                    else
                    {
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        if (Physics.Raycast(ray, out hit, 1000.0f, LayerMask.GetMask("Walls")))
                            hitObj = hit.collider.gameObject;
                    }


                    if (hitObj != null)
                    {
                        int segNum = Int32.Parse(hitObj.name.Substring(5));
                        Vector3[] pos = wallSegments[segNum].getArray(10.0f);
                        double angle = wallSegments[segNum].getAngle();

                        float scaleZ = wallSegments[segNum].getDirection().magnitude/(10.0f * pos.Length);

                        wallSegments[segNum].gltfObjs = new gltfRef(currentWallHash);

                        for (int n = 0; n < pos.Length; n++)
                        {
                            objNode node = new objNode(GameObject.Instantiate<GameObject>(currentWallObj));
                            var mesh = currentWallObj.GetComponentInChildren<MeshFilter>().mesh;

                            node.obj.transform.position = new Vector3(pos[n].x, -mesh.bounds.min.y, pos[n].z);
                            node.obj.transform.localScale = new Vector3(10.0f * scaleZ, 10.0f, 10.0f );
                            node.obj.transform.rotation = Quaternion.Euler(new Vector3(0, (float)((angle * 180.0) / Math.PI) + 90.0f, 0));

                            node.obj.AddComponent<BoxCollider>().size = mesh.bounds.size;

                            wallSegments[segNum].gltfObjs.objs.Add(node);
                        }

                        wallSegments[segNum].defObj.SetActive(false);
                    }

                }
            }
            else
            {
                if ((SelectedWallSeg < 0) && (HoveredWallSeg >= 0))
                    SelectedWallSeg = HoveredWallSeg;
                else
                    SelectedWallSeg = -1;
            }
        }

        if (selectedCell != null)
            selectedCell.obj.GetComponentInChildren<Renderer>().material = selectedSphereMat;

        if (hoveredCell != null)
        {
            hoveredCell.obj.GetComponentInChildren<Renderer>().material = defaultSphereMat;
            hoveredCell = null;
        }


        hitObj = null;

        if (hasHeadset)
        {
            var rightCont = GameObject.Find("RightHand Controller");
            if (rightCont != null)
            {
                XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
                if (xrri.TryGetCurrent3DRaycastHit(out hit))
                {
                    hitObj = hit.collider.gameObject;
                }
            }
        }
        else
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit, 1000.0f, LayerMask.GetMask("Default")))
            {
                hitObj = hit.collider.gameObject;
            }
        }

        if (hitObj != null)
        { 
            if (outScope == true)
            {
                if (CreateWalls)
                {
                    removeLastWall();
                }
                outScope = false;
            }

            hoveredCell = grid.FindCellByName(hitObj.name);
        }
        else
        {
            if (outScope == false)
            {
                if (CreateWalls)
                    MakeLastSeg();

                outScope = true;
            }
        }


        if (hoveredCell != null)
        {
            int myx = hoveredCell.X;
            int myy = hoveredCell.Y;

            hoveredCell.obj.GetComponentInChildren<Renderer>().material = selectedSphereMat;

            if (CreateWalls)
            {
                if (selectedCell != null)
                    computeNewSeg();
            }
            else
            {
                if (SelectedWallSeg < 0)
                {
                    HoveredWallSeg = FindWallSegment(myx, myy);

                    if (HoveredWallSeg >= 0)
                    {
                        hoveredCell.obj.GetComponentInChildren<Renderer>().material = SphereEditMat;
                    }
                }
                else
                {
                    updateWallSeg(SelectedWallSeg);
                }
            }
        }
    }


 
    void closeObjPanel()
    {
        if (ObjPannel != null)
        {
            ObjPannel.GetComponent<ObjSelection>().Close();
            Destroy(ObjPannel);
            ObjPannel = null;
        }
    }

    void showObjPannel()
    {
        if(ObjPannel == null)
            ObjPannel = Instantiate(Resources.Load("ObjCanvas")) as GameObject;

        ObjPannel.GetComponent<ObjSelection>().SelectRoomObject = hoverRoomObject;
        ObjPannel.GetComponent<ObjSelection>().wallet = wallet;

        GameObject.Find("CloseObjPanel").GetComponent<Button>().onClick.AddListener(closeObjPanel);
    }

    bool flip = false;
    Quaternion Q1, Q2, QT;


        // Update is called once per frame
    void Update()
    {
        if (EditRoomWall)
            updateGrid();
        else
        {
            var Cam = GameObject.Find("Camera Offset");

            for (int cc = 0; cc < 2; cc++)
            {
                var HandControllers = new List<UnityEngine.XR.InputDevice>();
                var desiredCharacteristics = UnityEngine.XR.InputDeviceCharacteristics.HeldInHand;

                if (cc == 0)
                    desiredCharacteristics |= UnityEngine.XR.InputDeviceCharacteristics.Left;
                else
                    desiredCharacteristics |= UnityEngine.XR.InputDeviceCharacteristics.Right;

                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, HandControllers);

                foreach (var device in HandControllers)
                {
                    Vector2 Axis;
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Axis))
                    {
                        if (canSnap[cc])
                        {
                            if (Axis.x < -0.5f)
                            {
                                currentRotation.x -= 45.0f;
                                canSnap[cc] = false;
                            }

                            if (Axis.x > 0.5f)
                            {
                                currentRotation.x += 45.0f;
                                canSnap[cc] = false;
                            }
                        }

                        if (Axis.magnitude < 0.5f)
                            canSnap[cc] = true;
                    }
                }
                lastSnapTime = Time.fixedUnscaledTime;
            }


            GameObject hitObj = null;
            RaycastHit hit = new RaycastHit();

           
            if ( ((ObjPannel == null ) || (ObjPannel.GetComponent<ObjSelection>().MoveObj == 0)) && (Input.GetMouseButton(0)))
            {
                currentRotation.x += Input.GetAxis("Mouse X") * sensitivity;
                currentRotation.y -= Input.GetAxis("Mouse Y") * sensitivity;
                currentRotation.x = Mathf.Repeat(currentRotation.x, 360);
                currentRotation.y = Mathf.Clamp(currentRotation.y, -maxYAngle, maxYAngle);
                Cam.transform.rotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
            }

            if (hasHeadset)
            {
                var rightCont = GameObject.Find("RightHand Controller");
                if (rightCont != null)
                {
                    XRRayInteractor xrri = rightCont.GetComponent<XRRayInteractor>();
                    if (xrri.TryGetCurrent3DRaycastHit(out hit))
                    {
                        hitObj = hit.collider.gameObject;
                    }
                }
            }
            else
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);


               if (Physics.Raycast(ray, out hit, 1000.0f, LayerMask.GetMask("Objetcs")))
                {
                    if (hoverRoomObject == null)
                    {
                        hoverRoomObject = hit.collider.gameObject;
                        hoverRoomObject.AddComponent<Blinker>();
                    }
                    else if (hoverRoomObject.GetHashCode() != hit.collider.gameObject.GetHashCode())
                    {
                        Destroy(hoverRoomObject.GetComponent<Blinker>());

                        hoverRoomObject = hit.collider.gameObject;
                        hoverRoomObject.AddComponent<Blinker>();
                    }
                    
                    Debug.Log("hit obj " + hit.collider.gameObject.name);

                    Debug.Log("hit obj scale1 " + hoverRoomObject.transform.localScale.ToString());
                    Debug.Log("hit obj scale2 " + hoverRoomObject.GetComponentInChildren<MeshFilter>().transform.localScale.ToString());

                }
                else if (hoverRoomObject != null)
                {
                    Destroy(hoverRoomObject.GetComponent<Blinker>());
                    hoverRoomObject = null;
                }
            }


            if(Input.GetMouseButtonDown(0))
            {
                if (hoverRoomObject != null)
                {
                    showObjPannel();
                }
            }

            Vector3 dir = Cam.transform.rotation * Vector3.forward;
            dir.y = 0.0f;
            Vector3 dir2 = Quaternion.Euler(0.0f, 90.0f, 0.0f) * dir;
            Vector3 lastPos;

            lastPos = new Vector3(Cam.transform.position.x, Cam.transform.position.y, Cam.transform.position.z);
            if (Input.GetKey("up"))
            {
                Cam.transform.position += dir * Time.deltaTime * speed;
            }
            if (Input.GetKey("down"))
            {
                Cam.transform.position -= dir * Time.deltaTime * speed;
            }
            if (Input.GetKey("left"))
            {
                Cam.transform.position -= dir2 * Time.deltaTime * speed;
            }
            if (Input.GetKey("right"))
            {
                Cam.transform.position += dir2 * Time.deltaTime * speed;
            }
            
            int layerMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Walls");

            Collider[] hitColliders = Physics.OverlapBox(Cam.transform.position, Cam.transform.localScale / 2, Quaternion.identity, layerMask);
            //Check when there is a new collider coming into contact with the box
            if(hitColliders.Length>0)
            {
                //Output all of the collider names
                Cam.transform.position = new Vector3(lastPos.x, lastPos.y, lastPos.z);
            }
            
        }
    }
}
