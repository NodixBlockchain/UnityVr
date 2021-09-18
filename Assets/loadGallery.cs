using System.Collections;
using System.Collections.Generic;
using System.Net;
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


public class NodeTable
{
    public NodeTable(string[] fields, float size)
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
    private NodeTable nodesTable;

    private GameObject[] Headers;

    private GameObject emptyResult;


    public string server = "nodix.eu";
    public string seedNodeAdress = "nodix.eu";
    public int seedNodePort = 16819;
    public string address = "BPgb5m5HGtNMXrUX9w1a8FfRE1GdGLM8P4";

    public List<GameObject> MenuItems;
    public Material selectedSphereMat;
    public Material defaultSphereMat;

    public Vector2 StartPos = new Vector2(-60.0f, 60.0f);
    public Vector2 Spacing = new Vector2(0.0f, -40.0f);

    public Vector2 ItemsStartPos = new Vector2(-10.0f, 60.0f);
    public Vector2 ItemsSpacing = new Vector2(35.0f, -30.0f);

    public GameObject contentPanel;
    public Font textFont;

    GameObject buttonPrefab;


    // Start is called before the first frame update
    void Start()
    {
        galleriesAddress = GameObject.Find("Address Value");
        galleriesAddress.GetComponentInChildren<InputField>().text = address;

        Node SeedNode = new Node(seedNodeAdress, seedNodePort, true);

        Nodes = new List<Node>();
        Nodes.Add(SeedNode);

        nodesTable = new NodeTable(new string[] { "adress", "ip", "port", "ping" }, 35.0f);

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
        }

        if (GalleriesButton != null)
        {
            for (int n = 0; n < GalleriesButton.Length; n++)
            {
                Destroy(GalleriesButton[n]);
            }
        }

        if (ItemsButton != null)
        {
            for (int n = 0; n < ItemsButton.Length; n++)
            {
                Destroy(ItemsButton[n]);
            }
        }

        if (NodesTexts != null)
        {
            for (int n = 0; n < NodesTexts.Length; n++)
            {
                Destroy(NodesTexts[n]);
            }
        }

        if (Headers != null)
        {
            for (int n = 0; n < Headers.Length; n++)
            {
                Destroy(Headers[n]);
            }
        }

        if (nodesTable.NodeRow != null)
        {
            for (int n = 0; n < nodesTable.NodeRow.Length; n++)
            {
                for (int nn = 0; nn < nodesTable.Fields.Length; nn++)
                {
                    Destroy(nodesTable.NodeRow[n].Columns[nn]);
                }
            }
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

    void WalletMenuClicked() {

        MenuItems[0].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[1].GetComponentInChildren<Renderer>().material = defaultSphereMat;
        MenuItems[2].GetComponentInChildren<Renderer>().material = selectedSphereMat;

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
                Debug.LogError("GALLERY LIST  " + myAddr + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.LogError("GALLERY LIST  " + myAddr + " : HTTP Error: " + webRequest.error);
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
                }

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
                    emptyResult = new GameObject();
                    emptyResult.AddComponent<Text>().text = "no items";
                    emptyResult.GetComponent<Text>().font = textFont;
                    emptyResult.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

                    emptyResult.transform.position = new Vector3(ItemsStartPos[0], StartPos[1], 0.0f);
                    emptyResult.transform.localScale = new Vector3(0.4f, 1.0f, 1.0f);

                    emptyResult.transform.SetParent(contentPanel.transform, false);

                    ItemsButton = null;
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
