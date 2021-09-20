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
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math.EC;


using UnityEngine;
using GLTF;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Networking;


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
public class WalletAddress
{
    public WalletAddress()
    {
        Name = null;
        PubAddr = null;
        PrivKey = null;
    }



    public WalletAddress(string name, AsymmetricCipherKeyPair keyPair)
    {
        Name = name;

        ECPublicKeyParameters pub = (ECPublicKeyParameters)keyPair.Public;
        ECPoint PubPoints = pub.Q;


        byte[] X = PubPoints.AffineXCoord.ToBigInteger().ToByteArray();
        byte[] Y = PubPoints.AffineYCoord.ToBigInteger().ToByteArray();

        byte[] total = new byte[X.Length + Y.Length];

        X.CopyTo(total, 0);
        Y.CopyTo(total, X.Length);

    
        var digest = new RipeMD160Digest();
        var result = new byte[digest.GetDigestSize()];
        digest.BlockUpdate(total, 0, total.Length);
        digest.DoFinal(result, 0);

        PubAddr = base58FromByteArray(result);

        PrivKey = base58FromByteArray(Org.BouncyCastle.Pkcs.PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private).ParsePrivateKey().GetDerEncoded());
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

    public Wallet()
    {
        addresses = new List<WalletAddress> ();

        X9ECParameters curve = ECNamedCurveTable.GetByName("secp256k1");
        ECDomainParameters domainParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

        SecureRandom secureRandom = new SecureRandom();
        ECKeyGenerationParameters keyParams = new ECKeyGenerationParameters(domainParams, secureRandom);

        generator = new ECKeyPairGenerator("ECDSA");
        generator.Init(keyParams);

    }

    public AsymmetricCipherKeyPair newKeyPair()
    {
        return generator.GenerateKeyPair();
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
            if(addresses[n].PrivKey != null)
            {
                return;
            }
        }

        addresses.Add(new WalletAddress("Master", newKeyPair()));
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

        IPHostEntry Entry = Dns.GetHostEntry(address);

        ip = Entry.AddressList[0];
        isSeed = seed;
        P2PPort = port;
        HTTPPort = 80;

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



public class loadGallery : MonoBehaviour
{
    Gallery myGallery;

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

    private const string baseURL = "/app/UnityApp";
    private Gallery[] galleries;
    private GameObject[] ItemsButton= null;
    private GameObject[] GalleriesButton = null;
    private GameObject[] NodesTexts = null;
    private GameObject galleriesAddress;


    private List<Node> Nodes;
    private itemTable nodesTable;
    private itemTable walletTable;
    private GameObject[] Headers;
    private GameObject emptyResult;

    private Wallet wallet;
    private GameObject newAddr;


    public string server = "nodix.eu";
    public string seedNodeAdress = "nodix.eu";
    public int seedNodePort = 16819;
    public string address = "BPgb5m5HGtNMXrUX9w1a8FfRE1GdGLM8P4";

    public List<GameObject> MenuItems;
    public Material selectedSphereMat;
    public Material defaultSphereMat;

    public Vector2 StartPos = new Vector2(-50.0f, 0.0f);
    public Vector2 Spacing = new Vector2(0.0f, -40.0f);

    public Vector2 ItemsStartPos = new Vector2(0.0f, 0.0f);
    public Vector2 ItemsSpacing = new Vector2(35.0f, -30.0f);

    public GameObject contentPanel;
    public Font textFont;

    GameObject buttonPrefab;
    


    void keyToPubAddr()
    {

    }


    // Start is called before the first frame update
    void Start()
    {


        wallet = new Wallet();
        wallet.load();



        galleriesAddress = GameObject.Find("Address Value");
        galleriesAddress.GetComponentInChildren<InputField>().text = address;

        Node SeedNode = new Node(seedNodeAdress, seedNodePort, true);

        Nodes = new List<Node>();
        Nodes.Add(SeedNode);

        nodesTable = new itemTable(new string[] { "adress", "ip", "port", "ping" }, 35.0f);
        walletTable = new itemTable(new string[] { "label", "adress", "owner" }, 65.0f);

        if (selectedSphereMat != null)
        {
            MenuItems[0].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        }

        if (defaultSphereMat != null)
        {
            MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
            MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        }

        
        MenuItems[0].GetComponentInChildren<Button>().onClick.AddListener(() => GalleryMenuClicked());
        MenuItems[1].GetComponentInChildren<Button>().onClick.AddListener(() => NodesMenuClicked());
        MenuItems[2].GetComponentInChildren<Button>().onClick.AddListener(() => WalletMenuClicked());

        StartCoroutine(loadGalleries());
    }

    void ClearTabs()
    {

        if(emptyResult != null)
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

    void GalleryMenuClicked() {

        MenuItems[0].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        StartCoroutine(loadGalleries());

    }


    void NodesMenuClicked() {

        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = selectedSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = defaultSphereMat;

        ClearTabs();

        Headers = new GameObject[nodesTable.Fields.Length];

        for (int n = 0; n < nodesTable.Fields.Length; n++)
        {
            Headers[n] = new GameObject();

            Headers[n].AddComponent<Text>().text = nodesTable.Fields[n];
            Headers[n].GetComponent<Text>().font = textFont;
            Headers[n].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            Headers[n].transform.position = new Vector3(StartPos[0] + nodesTable.Size * n, StartPos[1], 0.0f);
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

            nodesTable.NodeRow[n].Columns[0].transform.position = new Vector3(StartPos[0], StartPos[1] + (n + 1) * Spacing[1] , 0);
            nodesTable.NodeRow[n].Columns[0].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[0].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[1] = new GameObject();
            nodesTable.NodeRow[n].Columns[1].AddComponent<Text>().text = Nodes[n].ip.ToString();
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[1].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            nodesTable.NodeRow[n].Columns[1].transform.position = new Vector3(StartPos[0] + nodesTable.Size, StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[1].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[1].transform.SetParent(contentPanel.transform, false);


            nodesTable.NodeRow[n].Columns[2] = new GameObject();
            nodesTable.NodeRow[n].Columns[2].AddComponent<Text>().text = Nodes[n].P2PPort.ToString();
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[2].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            nodesTable.NodeRow[n].Columns[2].transform.position = new Vector3(StartPos[0] + nodesTable.Size * 2, StartPos[1] + (n + 1) * Spacing[1], 0);
            nodesTable.NodeRow[n].Columns[2].transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);
            nodesTable.NodeRow[n].Columns[2].transform.SetParent(contentPanel.transform, false);



            nodesTable.NodeRow[n].Columns[3] = new GameObject();
            nodesTable.NodeRow[n].Columns[3].AddComponent<Text>().text = Nodes[n].ping.ToString();
            nodesTable.NodeRow[n].Columns[3].GetComponent<Text>().font = textFont;
            nodesTable.NodeRow[n].Columns[3].GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            nodesTable.NodeRow[n].Columns[3].transform.position = new Vector3(StartPos[0] + nodesTable.Size * 3, StartPos[1] + (n + 1) * Spacing[1], 0);
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

        float startY = -30.0f;
        int nWallets, nContacts;
        string curAddr = galleriesAddress.GetComponentInChildren<InputField>().text;
        bool found;

        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = selectedSphereMat;

        ClearTabs();
      

       

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

    void loadGLTF(string hash)
    {
        string URL = "http://" + server + baseURL;
        string ou = URL + "/obj/" + hash;

        Debug.Log("loading scene " + ou);

        GameObject obj = new GameObject("Mesh "+ hash);
        
        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = ou;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        obj.GetComponent<UnityGLTF.GLTFComponent>().transform.position = new Vector3(0.0f, 2.0f, 0.0f);

        obj.AddComponent<BoxCollider>().size= new Vector3(0.5f, 0.5f, 0.5f);
        obj.AddComponent<Rigidbody>();
        obj.AddComponent<XRGrabInteractable>();
        
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


                if (galleries.Length > 0)
                {
                    GalleriesButton = new GameObject[galleries.Length];

                    for (int n = 0; n < galleries.Length; n++)
                    {
                        string galleryHash = galleries[n].objHash;

                        GalleriesButton[n] = Instantiate(Resources.Load("ButtonGallery")) as GameObject;
                        GalleriesButton[n].transform.position = new Vector3(StartPos[0] + n * Spacing[0], StartPos[1] + n * Spacing[1], 0);

                        GalleriesButton[n].transform.SetParent(contentPanel.transform, false);

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
                    emptyResult.transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);

                    emptyResult.transform.SetParent(contentPanel.transform, false);
                }

               
            break;
        }

    }

    void GalleryClicked(string hash)
    {
        Debug.Log("gallery " + hash + " selected");

        StartCoroutine(loadWebGallery(hash));
    }



    // Update is called once per frame
    void Update()
    {
          
    }
}
