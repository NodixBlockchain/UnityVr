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
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Math;


using UnityEngine;
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
public class RoomWall
{
    public float[] start;
    public RoomSceneRoot objHash;
}

[System.Serializable]
public class Room
{
    public string name;
    public RoomScene[] objects;
    public RoomWall[] walls;
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

    //MonoBehaviour mono;

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

        //mono = mainmono;

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
            /*Debug.Log("private " + addresses[n]);*/

            if (addresses[n].PrivKey != null)
            {
                /*mono.StartCoroutine(addresses[n].checkpub());*/
                mainKey = addresses[n];
                return;
            }
        }

        var newAddress = new WalletAddress("Master", generator);

        addresses.Add(newAddress);

        /*mono.StartCoroutine(newAddress.checkpub());*/

        save();
    }

    public List<WalletAddress> addresses;
}


/*
[System.Serializable]
public class Node
{

    public Node(string adr, int port, bool seed)
    {
        address = adr;

        try
        {
            IPHostEntry Entry = Dns.GetHostEntry(address);
            ip = Entry.AddressList[0].MapToIPv4();
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
*/


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

public struct SaveInfo
{
    public string appName;
    public int sceneTypeId;
    public int roomTypeId;
    public int nodeTypeId;
    public int wallTypeId;
    public WalletAddress mainKey;
}

class Blinker : MonoBehaviour
{
    double cur;
    Color[] matCols=null;
    void Start()
    {
        MeshRenderer mrs = GetComponent<MeshRenderer>();
        cur = 0;

        Debug.Log("blinker start " + mrs.gameObject.name);

        matCols = new Color[mrs.materials.Length];

        for (int n = 0; n < mrs.materials.Length; n++)
        {
            Debug.Log("blinker start mat col" + mrs.materials[n].color.ToString());

            matCols[n] = mrs.materials[n].color;
        }
    }
    void Update()
    {
        MeshRenderer mrs = GetComponent<MeshRenderer>();
        cur += Time.deltaTime;
        float alpha = (float)(Math.Sin(cur * 4.0f) + 1.0f) / 2.0f;

        for(int n=0;n< mrs.materials.Length;n++)
        {
            mrs.materials[n].SetColor("_Color", new Color(matCols[n].r * alpha, matCols[n].g * alpha, matCols[n].b * alpha, matCols[n].a * alpha));
        }

    }

    void OnDestroy()
    {
        MeshRenderer mrs = GetComponent<MeshRenderer>();

        if (matCols == null)
            return;

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
    public int wallTypeId = 0x01f;
    public int roomTypeId = 0x35;
    public int nodeTypeId = 0x08;
    public string server = "nodix.eu";
    public string address = "BPgb5m5HGtNMXrUX9w1a8FfRE1GdGLM8P4";
    

    public List<GameObject> MenuItems;

    public GameObject SaveButton;
    public Material selectedSphereMat;
    public Material SphereEditMat;
    public Material defaultSphereMat;

    public Vector2 StartPos = new Vector2(-50.0f, 0.0f);
    public Vector2 Spacing = new Vector2(0.0f, -40.0f);

    public Vector2 ItemsStartPos = new Vector2(0.0f, 0.0f);
    public Vector2 ItemsSpacing = new Vector2(35.0f, -30.0f);

    public GameObject contentPanel;
    public GameObject indexPanel;
    public Font textFont;

    public float sensitivity = 10.0f;
    public float speed = 500.0f;

    private cell hoveredCell;

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
    private Nodes nodes;



    private itemTable nodesTable;
    private itemTable walletTable;
    private GameObject[] Headers;
    private GameObject emptyResult;
    private GameObject roomMenu = null;
    private GameObject NodesAdd = null;
    private Wallet wallet;
    private GameObject newAddr;

    private GameObject buttonPrefab;
    private Material lineMaterial;

    private Vector3 lastCamPos;
    
    private float maxYAngle = 80f;
    private Vector2 currentRotation;

    private float lastSnapTime = 0.0f;
    private bool[] canSnap = new bool[2];
    private GameObject floorPlane;


    SaveInfo makeSaveInfos()
    {
        SaveInfo ret;

        ret.appName = appName;
        ret.mainKey = wallet.mainKey;
        ret.nodeTypeId = nodeTypeId;
        ret.roomTypeId = roomTypeId;
        ret.wallTypeId = wallTypeId;
        ret.sceneTypeId = sceneTypeId;

        return ret;
    }


    // Start is called before the first frame update
    void Start()
    {
        canSnap[0] = true;
        canSnap[1] = true;

        wallet = new Wallet(this);
        wallet.load();

        room = GameObject.Find("Room").GetComponent<vrRoom>();
        room.setECDomain(wallet.domainParams);

        nodes = GameObject.Find("Nodes").GetComponent<Nodes>();

        floorPlane = GameObject.Find("FloorPlane");
        
        galleriesAddress = GameObject.Find("Address Value");
        galleriesAddress.GetComponentInChildren<InputField>().text = address;

        /*
        Node SeedNode = new Node(nodes.seedNodeAdress, nodes.seedNodePort, true);

        NodesList = new List<Node>();
        NodesList.Add(SeedNode);
        */

        nodesTable = new itemTable(new string[] { "adress", "ip", "port", "ping" }, 40.0f);
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

        /*StartCoroutine(wallet.getnodepriv("BitAdmin", "B8mPBEg2XbYSUwEh5a7yrfehvMNijpAm1P"));*/
    }

    void ClearTabs()
    {
        if(roomMenu != null)
        {
            Destroy(roomMenu);
            roomMenu = null;
        }

        if (NodesAdd != null)
        {
            Destroy(NodesAdd);
            NodesAdd = null;
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

        contentPanel.GetComponent<GridLayoutGroup>().cellSize = new Vector2(40.0f, 80.0f);
        contentPanel.GetComponent<GridLayoutGroup>().spacing = new Vector2(10.0f, 10.0f);
        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        roomMenu = Instantiate(Resources.Load("Room Menu")) as GameObject;

        var panel = GameObject.Find("Panel");
        var newButton = roomMenu.transform.Find("New Button");
        var saveButton = roomMenu.transform.Find("Save Button");
        var editButton = roomMenu.transform.Find("Edit Button");
        var roomName = roomMenu.transform.Find("Room Name");

        roomName.GetComponent<InputField>().text = room.roomName;

        newButton.GetComponent<Button>().onClick.AddListener(() => room.newRoom());
        saveButton.GetComponent<Button>().onClick.AddListener(() => room.saveAllScenes(makeSaveInfos()));
        editButton.GetComponent<Button>().onClick.AddListener(() => room.roomEditor());
        roomName.GetComponent<InputField>().onValueChanged.AddListener(room.setName);

        roomMenu.transform.SetParent(panel.transform, false);

        StartCoroutine(loadRooms());
    }

    void GalleryMenuClicked() {
        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[3].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        contentPanel.GetComponent<GridLayoutGroup>().cellSize = new Vector2(80.0f, 40.0f);
        contentPanel.GetComponent<GridLayoutGroup>().spacing = new Vector2(10.0f, 10.0f);
        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        StartCoroutine(loadGalleries());

    }

    void addNewNode()
    {
        var NewNodeAddr = NodesAdd.transform.Find("NewNodeAddr");
        var NewNodePort = NodesAdd.transform.Find("NewNodePort");

        int port = Int32.Parse(NewNodePort.GetComponent<InputField>().text);

        if ((port > 0)&&(port <= 65535))
        {
            nodes.addNode(NewNodeAddr.GetComponent<InputField>().text, (ushort)port);
            NodesMenuClicked();
        }
    }

    void NodesMenuClicked() {
        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[3].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        contentPanel.GetComponent<GridLayoutGroup>().cellSize = new Vector2(nodesTable.Size, 30.0f);
        contentPanel.GetComponent<GridLayoutGroup>().spacing = new Vector2(0.0f, 0.0f);
        contentPanel.GetComponent<GridLayoutGroup>().padding.top = 20;

        contentPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        indexPanel.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

        var panel = GameObject.Find("Panel");

        NodesAdd = Instantiate(Resources.Load("NodesAdd")) as GameObject;

        NodesAdd.transform.SetParent(panel.transform,false);

        var addNodeButton = NodesAdd.transform.Find("Add Node");
        addNodeButton.GetComponent<Button>().onClick.AddListener(() => addNewNode());

        Headers = new GameObject[nodesTable.Fields.Length];
       

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

        nodesTable.NodeRow = new NodeTableRow[nodes.NodesList.Count];

        for (int n = 0; n < nodes.NodesList.Count; n++)
        {
            nodesTable.NodeRow[n] = new NodeTableRow();
            nodesTable.NodeRow[n].Columns = new GameObject[nodesTable.Fields.Length];

            nodesTable.NodeRow[n].Columns[0] = new GameObject();
            nodesTable.NodeRow[n].Columns[0].AddComponent<Text>().text = nodes.NodesList[n].address;
            nodesTable.NodeRow[n].Columns[0].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[0].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[0].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[0].transform.position = new Vector3(StartPos[0], StartPos[1] + (n + 1) * Spacing[1] , 0);
            nodesTable.NodeRow[n].Columns[0].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[0].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[1] = new GameObject();
            if(nodes.NodesList[n].ip !=null)
                nodesTable.NodeRow[n].Columns[1].AddComponent<Text>().text = nodes.NodesList[n].ip.ToString();
            else
                nodesTable.NodeRow[n].Columns[1].AddComponent<Text>().text = "error";
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[1].transform.position = new Vector3(StartPos[0] , StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[1].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[1].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[2] = new GameObject();
            nodesTable.NodeRow[n].Columns[2].AddComponent<Text>().text = nodes.NodesList[n].P2PPort.ToString();
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().resizeTextForBestFit = true;

            nodesTable.NodeRow[n].Columns[2].transform.position = new Vector3(StartPos[0] , StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[2].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[2].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[3] = new GameObject();
            nodesTable.NodeRow[n].Columns[3].AddComponent<Text>().text = nodes.NodesList[n].pingTime.ToString();
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


        contentPanel.GetComponent<GridLayoutGroup>().cellSize = new Vector2(50.0f, 30.0f);
        contentPanel.GetComponent<GridLayoutGroup>().spacing = new Vector2(10.0f, 10.0f);

        var panel = GameObject.Find("Panel");

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
        Headers[0].GetComponent<RectTransform>().sizeDelta = new Vector2(80.0f, 20.0f);
        Headers[0].transform.position = new Vector3(-32.0f, 10.0f, 0.0f);
        Headers[0].transform.localScale = new Vector3(0.4f, 0.2f, 1.0f);
        Headers[0].transform.SetParent(panel.transform, false);

        Headers[1] = new GameObject();

        Headers[1].AddComponent<Text>().text = "Contacts";
        Headers[1].GetComponent<Text>().font = textFont;
        Headers[1].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        Headers[1].GetComponent<RectTransform>().sizeDelta = new Vector2(80.0f, 30.0f);
        Headers[1].transform.position = new Vector3(15.0f, 10.0f, 0.0f);
        Headers[1].transform.localScale = new Vector3(0.4f, 0.2f, 1.0f);
        Headers[1].transform.SetParent(panel.transform, false);

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
                    walletTable.NodeRow[n].Columns[0].transform.SetParent(indexPanel.transform, false);

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

                        ItemsButton[n].GetComponent<Button>().onClick.AddListener(() => room.loadGLTF(itemHash));

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

                room.resetRoom();

                var mroom = JsonUtility.FromJson<Room>(webRequest.downloadHandler.text);
                if(mroom.objects == null)
                {
                    Debug.Log("ROOM  no objects");
                }
                else
                {
                    for (int n = 0; n < mroom.objects.Length; n++)
                    {
                        room.loadScene(mroom.objects[n].objHash);
                    }
                }

                if (mroom.walls == null)
                {
                    Debug.Log("ROOM  no walls");
                }
                else
                {
                    for (int n = 0; n < mroom.walls.Length; n++)
                    {
                        room.newWallSeg(new Vector2(mroom.walls[n].start[0], mroom.walls[n].start[1]), mroom.walls[n].objHash.objHash);
                    }

                    room.buildWalls();
                }
                room.showLoading();




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

    // Update is called once per frame
    void Update()
    {

        if (room.isEditingWall())
            return;

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

        
        if ( (!room.hasObjPanel()) && (Input.GetMouseButton(0)))
        {
            currentRotation.x += Input.GetAxis("Mouse X") * sensitivity;
            currentRotation.y -= Input.GetAxis("Mouse Y") * sensitivity;
            currentRotation.x = Mathf.Repeat(currentRotation.x, 360);
            currentRotation.y = Mathf.Clamp(currentRotation.y, -maxYAngle, maxYAngle);
            Cam.transform.rotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
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
