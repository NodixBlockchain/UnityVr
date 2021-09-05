using System.Collections;
using System.Collections.Generic;
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

    public string server = "nodix.eu";
    public string address = "BPgb5m5HGtNMXrUX9w1a8FfRE1GdGLM8P4";
    

    GameObject buttonPrefab;


    // Start is called before the first frame update
    IEnumerator Start()
    {
        string URL;

        

        URL = "http://" + server + baseURL;

        string ldgal = URL + "/objlst/47/"+ address;

        Debug.Log("url " + ldgal);

        WWW www = new WWW(ldgal);
        yield return www;
        if (www.error == null)
        {
            Debug.Log("data: " + www.text);
            galleries = ParseJsonArray<Gallery[]>(www.text);

            if (GalleriesButton != null)
            {
                for (int n = 0; n < GalleriesButton.Length; n++)
                {
                    Destroy(GalleriesButton[n]);
                }

            }

            GalleriesButton = new GameObject[galleries.Length];

            for (int n=0;n<galleries.Length;n++)
            {
                string galleryHash= galleries[n].objHash;

                GalleriesButton[n] = Instantiate(Resources.Load("ButtonGallery")) as GameObject;
                GalleriesButton[n].transform.SetParent(this.transform, false);
                GalleriesButton[n].transform.position = this.transform.position + new Vector3(n * 2, 0.0f, 0);
                GalleriesButton[n].GetComponentInChildren<Text>().text = galleries[n].name;
                GalleriesButton[n].name = "gallery " + galleries[n].name;

                GalleriesButton[n].GetComponent<Button>().onClick.AddListener( () => GalleryClicked(galleryHash));
            }
        }
        else
        {
            Debug.Log("GALLERY LIST ERROR: " + www.error);
        }
    }
    
    void loadGLTF(string hash)
    {
        string URL;

        URL = "http://" + server + baseURL;

        UnityGLTF.GLTFComponent GLTF;
        string ou = URL + "/obj/" + hash;

        Debug.Log("loading scene " + ou);

        GameObject obj = new GameObject("Mesh "+ hash);
        //obj.transform.SetParent(this.transform);
        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = ou;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        obj.GetComponent<UnityGLTF.GLTFComponent>().transform.position = new Vector3(0.0f, 2.0f, 0.0f);
        


        obj.AddComponent<BoxCollider>().size= new Vector3(0.5f, 0.5f, 0.5f);
        obj.AddComponent<Rigidbody>();
        obj.AddComponent<XRGrabInteractable>();
        
    }

    IEnumerator loadWebGallery(string hash)
    {
        string URL;

        URL = "http://" + server + baseURL;

        string gu = URL + "/obj/" + hash+"/2";
        Debug.Log("loading gallery " + gu);

        WWW www = new WWW(gu);
        yield return www;
        if (www.error == null)
        {
            float itemX, itemY;
            float xStart = 0.1f;

            Debug.Log("gallery " + hash + " data " + www.text);

            if (ItemsButton != null)
            {
                for (int n = 0; n < ItemsButton.Length; n++)
                {
                    Destroy(ItemsButton[n]);
                }

            }

           

            myGallery = JsonUtility.FromJson<Gallery>(www.text);

            if (myGallery != null)
            {
                ItemsButton = new GameObject[myGallery.scenes.Length];

                itemX = xStart;
                itemY = -0.4f;

                for (int n = 0; n < myGallery.scenes.Length; n++)
                {
                    string itemHash = myGallery.scenes[n].scene.objHash;
                    ItemsButton[n] = Instantiate(Resources.Load("ButtonItem")) as GameObject;
                    ItemsButton[n].transform.SetParent(this.transform, false);
                    ItemsButton[n].transform.position = this.transform.position + new Vector3(itemX, itemY, 0.0f);
                    ItemsButton[n].GetComponentInChildren<Text>().text = myGallery.scenes[n].name;
                    ItemsButton[n].name = "item " + myGallery.scenes[n].name;

                    ItemsButton[n].GetComponent<Button>().onClick.AddListener(() => loadGLTF(itemHash));


                    itemX += 1.0f;

                    if (itemX >= 5.0f)
                    {
                        itemY -= 0.5f;
                        itemX = xStart;
                    }
                }
            }
            else
                ItemsButton = null;


        }
        else
        {
            Debug.Log("GALLERY ERROR: " + www.error);
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
