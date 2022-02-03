using System.Collections;
using System.Collections.Generic;

using System;

using UnityEngine;
using Org.BouncyCastle.Crypto.Parameters;

using UnityGLTF.Extensions;

using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Networking;




[System.Serializable]
class TxInput
{
    public string txid;
    public int index;
    public int value;
    public string srcaddr;
    public string script;
    public string signHash;
}

[System.Serializable]
class TxOutput
{
    public int value;
    public string script;
    public string dstaddr;
}

[System.Serializable]
class Transaction
{
    public string txid;
    public int version;
    public int time;
    public int locktime;
    public List<TxInput> txsin;
    public List<TxOutput> txsout;
    public Boolean issigned;
}


class objNode
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

class gltfRef
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
class RPCTx
{
    public Transaction transaction;
}
[System.Serializable]
class RPCTransaction
{
    int id;
    string jsonrpc;
    public RPCTx result;
}

[System.Serializable]
class RPCSignTx
{
    public string txid;
}
[System.Serializable]
class RPCSign
{
    int id;
    string jsonrpc;
    public RPCSignTx result;
}



public class cell
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
                cells[(ny * w) + nx].obj.transform.position = new Vector3((nx + x) * 10.0f, 0.0f, (ny + y) * 10.0f);
                cells[(ny * w) + nx].obj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                cells[(ny * w) + nx].X = nx;
                cells[(ny * w) + nx].Y = ny;
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

    public void recompute(float height)
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

public class vrRoom : MonoBehaviour
{
    public Material wallSegMat;
    public Material defaultSphereMat;
    public Material selectedSphereMat;
    public Material SphereEditMat;

    public float wallHeight = 10.0f;
    public string server;
    public GameObject floorPlane;

    private ECDomainParameters domainParams;


    private Transaction roomTx;
    private SaveInfo saveInfos;

    private Grid grid;

    private string baseURL = "/app/UnityApp";
    private string roomName;

    private List<WallSegment> wallSegments;
    private List<gltfRef> sceneObjects;

    private cell selectedCell = null;
    private cell hoveredCell = null;

    private int SelectedWallSeg = -1;
    private int HoveredWallSeg = -1;



    private GameObject currentSeg = null, lastSeg = null;
    private GameObject roomFloor = null;
    private GameObject currentWallObj = null;
    private string      currentWallHash;
    private GameObject hoverRoomObject = null;
    private GameObject ObjPannel = null;
    private GameObject EditWallPanel = null;

    private int prevLayerMask = 0;

    private bool EditRoomWall = false;
    private bool CreateWalls = false;
    private bool outScope = false;
    private bool SetWallObj = false;
    private bool lastTrigger = false;
    private bool hasHeadset = false;

    private XRRayInteractor RightInteractor;

    void Start()
    {
        roomName = "my room";
        sceneObjects = new List<gltfRef>();

        hasHeadset = hasHMD();

        var rightCont = GameObject.Find("RightHand Controller");
        if(rightCont != null)
            RightInteractor = rightCont.GetComponent<XRRayInteractor>();
    }
    public bool isEditingWall()
    {
        return EditRoomWall;
    }

    public void setECDomain(ECDomainParameters domainParams)
    {
        this.domainParams = domainParams;

    }

    void resetRoom()
    {
        for (int n = 0; n < this.sceneObjects.Count; n++)
        {
            for (int i = 0; i < this.sceneObjects[n].objs.Count; i++)
            {
                Destroy(this.sceneObjects[n].objs[i].obj);
            }
            this.sceneObjects[n].objs = new List<objNode>();
        }
        this.sceneObjects = new List<gltfRef>();

        if (this.wallSegments != null)
        {
            for (int n = 0; n < this.wallSegments.Count; n++)
            {
                Destroy(this.wallSegments[n].defObj);
                for (int nn = 0; nn < this.wallSegments[n].gltfObjs.objs.Count; nn++)
                {
                    Destroy(this.wallSegments[n].gltfObjs.objs[nn].obj);
                }
            }
        }
        this.wallSegments = new List<WallSegment>();
    }


    public void ResetGrid()
    {
        for (int n = 0; n < grid.cells.Length; n++)
        {
            Destroy(grid.cells[n].obj);
        }
    }

    public bool hasObjPanel()
    {
        if (ObjPannel == null)
            return false;

        if (ObjPannel.GetComponent<ObjSelection>().MoveObj == 0)
            return false;

        return true;
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

    public void newRoom()
    {
        if(grid != null)
            ResetGrid();

        resetRoom();
        roomEditor();
    }

    public void loadScene(string hash)
    {
        string URL = "http://" + server + baseURL + "/page/unity.site/scene/" + hash;

        Debug.Log("loading scene " + URL);

        GameObject obj = new GameObject("Scene " + hash);

        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;

        //obj.AddComponent<BoxCollider>();
        /*obj.AddComponent<Rigidbody>().isKinematic = true;*/
        /*obj.AddComponent<XRGrabInteractable>();*/

        obj.layer = 7;

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

    public void loadGLTF(string hash)
    {
        string URL = "http://" + server + baseURL + "/obj/" + hash;


        Debug.Log("loading scene " + URL);

        GameObject obj = new GameObject("Mesh " + hash);

        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
        //obj.AddComponent<BoxCollider>();



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
            this.addObj(obj, hash);

            obj.layer = 7;

            /*obj.AddComponent<Rigidbody>();*/
            /*obj.AddComponent<XRGrabInteractable>();*/
        }
    }





    void ToggleEditRoom(bool state)
    {
        if (!state)
        {
            Vector3 camPos;
            if (this.wallSegments.Count >= 2)
            {
                camPos = this.createFloor();
                camPos.y = 1.0f;
            }
            else
            {
                
                floorPlane.SetActive(true);
                camPos = new Vector3(0.0f, 1.0f, -10.0f);
            }

            var CameraOffset = GameObject.Find("Camera Offset");
            CameraOffset.transform.position = camPos;
            CameraOffset.transform.rotation = Quaternion.Euler(new Vector3(-12.0f, 0, 0));

            Destroy(EditWallPanel);
            ResetGrid();

            EditRoomWall = false;

            selectedCell = null;
            hoveredCell = null;

            if (RightInteractor != null)
            {
                RightInteractor.raycastMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Walls") | LayerMask.GetMask("UI") | LayerMask.GetMask("Objects");
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


    void ToggleEditWall(bool state)
    {
        if (state)
        {
            if (RightInteractor != null)
            {
                prevLayerMask = RightInteractor.raycastMask;
                RightInteractor.raycastMask = LayerMask.GetMask("Walls") | LayerMask.GetMask("UI");
            }

            GameObject.Find("ToggleWall").GetComponent<Toggle>().isOn = false;
            SetWallObj = true;

        }
        else
        {
            SetWallObj = false;
            if (RightInteractor != null)
                RightInteractor.raycastMask = prevLayerMask;
            
        }
    }

    void ToggleEditWalls(bool state)
    {
        if (RightInteractor != null)
            RightInteractor.raycastMask = LayerMask.GetMask("Default") | LayerMask.GetMask("UI");

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
        if (ObjPannel == null)
            ObjPannel = Instantiate(Resources.Load("ObjCanvas")) as GameObject;

        ObjPannel.GetComponent<ObjSelection>().SelectRoomObject = hoverRoomObject;
        ObjPannel.GetComponent<ObjSelection>().domainParams = domainParams;

        GameObject.Find("CloseObjPanel").GetComponent<Button>().onClick.AddListener(closeObjPanel);
    }

    void EnableCreateWalls()
    {
        this.removeLastWall();

        if (this.wallSegments.Count > 0)
            selectedCell = this.wallSegments[this.wallSegments.Count - 1].endCell;

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

    void Update()
    {
        bool button = false;

        RaycastHit hit = new RaycastHit();
        GameObject hitObj = null;

      

        if (EditRoomWall)
        {
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


            if (!button)
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
                            if (RightInteractor != null)
                            {
                                if (RightInteractor.TryGetCurrent3DRaycastHit(out hit))
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

                            float scaleZ = wallSegments[segNum].getDirection().magnitude / (10.0f * pos.Length);

                            wallSegments[segNum].gltfObjs = new gltfRef(currentWallHash);

                            for (int n = 0; n < pos.Length; n++)
                            {
                                objNode node = new objNode(GameObject.Instantiate<GameObject>(currentWallObj));
                                var mesh = currentWallObj.GetComponentInChildren<MeshFilter>().mesh;

                                node.obj.transform.position = new Vector3(pos[n].x, -mesh.bounds.min.y, pos[n].z);
                                node.obj.transform.localScale = new Vector3(10.0f * scaleZ, 10.0f, 10.0f);
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
                if (RightInteractor != null)
                {
                    if (RightInteractor.TryGetCurrent3DRaycastHit(out hit))
                        hitObj = hit.collider.gameObject;
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
                        removeLastWall();

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
                        computeNewSeg(hoveredCell);
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
                        updateWallSeg(SelectedWallSeg, hoveredCell);
                    }
                }
            }
        }
        else
        {
            if (hasHeadset)
            {
                if (RightInteractor != null)
                {
                    if (RightInteractor.TryGetCurrent3DRaycastHit(out hit))
                    {
                        hitObj = hit.collider.gameObject;
                    }
                }
            }
            else
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                bool hasHit=false;

                for (int n=0;n<sceneObjects.Count;n++)
                {
                    for (int nn = 0; nn < sceneObjects[n].objs.Count; nn++)
                    {
                        var meshes = sceneObjects[n].objs[nn].obj.GetComponentsInChildren<MeshFilter>();

                        foreach(MeshFilter mesh in meshes)
                        {
                            var box = mesh.GetComponent<BoxCollider>();
                            if(box.Raycast(ray, out hit, 1000.0f))
                            {
                                GameObject collid = hit.collider.gameObject;

                                if (hoverRoomObject == null)
                                {
                                    hoverRoomObject = collid;
                                    hoverRoomObject.AddComponent<Blinker>();
                                }
                                else if (hoverRoomObject != collid)
                                {
                                    Destroy(hoverRoomObject.GetComponent<Blinker>());
                                    hoverRoomObject = collid;
                                    hoverRoomObject.AddComponent<Blinker>();
                                }
                                hasHit = true;
                                break;
                            }
                        }
                    }
                }

                if ((!hasHit) &&(hoverRoomObject != null))
                {
                    Destroy(hoverRoomObject.GetComponent<Blinker>());
                    hoverRoomObject = null;
                }

                /*
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
                */
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (hoverRoomObject != null)
                {
                    showObjPannel();
                }
            }
        }

    }

    public int FindWallSegment(int x, int y)
    {
        for (int n = 0; n < wallSegments.Count; n++)
        {
            WallSegment mySeg = wallSegments[n];

            if ((mySeg.startCell.X == x) && (mySeg.startCell.Y == y))
            {
                return n;
            }
        }
        return -1;
    }

    void computeNewSeg(cell hoveredCell)
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

            lastSeg.transform.position = new Vector3(hoveredCell.obj.transform.position.x + ldelta.x / 2.0f,  wallHeight / 2.0f, hoveredCell.obj.transform.position.z + ldelta.z / 2.0f);
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

        currentSeg.transform.position = new Vector3(selectedCell.obj.transform.position.x + delta.x / 2.0f,  wallHeight / 2.0f, selectedCell.obj.transform.position.z + delta.z / 2.0f);
        currentSeg.transform.localScale = new Vector3(4.0f, wallHeight, delta.magnitude);

        delta.Normalize();
        if (delta.sqrMagnitude > 0.001f)
        {
            double angle;

            angle = Math.Atan2(delta.x, delta.z);
            currentSeg.transform.rotation = Quaternion.Euler(0.0f, (float)(angle * 180.0f / Math.PI), 0.0f);
        }
    }


    void updateWallSeg(int n, cell hoveredCell)
    {
        int seg1, seg2;

        seg1 = n;

        if (n == 0)
        {
            seg2 = wallSegments.Count - 1;
        }
        else
        {
            seg2 = n - 1;
        }

        wallSegments[seg1].startCell = hoveredCell;
        wallSegments[seg2].endCell = hoveredCell;


        if (wallSegments[seg1].gltfObjs != null)
        {
            for (int i = 0; i < wallSegments[seg1].gltfObjs.objs.Count; i++)
            {
                Destroy(wallSegments[seg1].gltfObjs.objs[i].obj);
            }
        }

        wallSegments[seg1].recompute(wallHeight);

        if (wallSegments[seg2].gltfObjs != null)
        {
            for (int i = 0; i < wallSegments[seg2].gltfObjs.objs.Count; i++)
            {
                Destroy(wallSegments[seg2].gltfObjs.objs[i].obj);
            }
        }

        wallSegments[seg2].recompute(wallHeight);


    }

    void MakeLastSeg()
    {
        if (wallSegments.Count >= 2)
        {
            var newSegObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            newSegObj.GetComponentInChildren<Renderer>().material = wallSegMat;
            newSegObj.layer = 6;

            Vector3 ldelta = new Vector3(wallSegments[0].startCell.obj.transform.position.x - wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.x, 0.0f, wallSegments[0].startCell.obj.transform.position.z - wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.z);

            newSegObj.transform.position = new Vector3(wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.x + ldelta.x / 2.0f,  wallHeight / 2.0f, wallSegments[wallSegments.Count - 1].endCell.obj.transform.position.z + ldelta.z / 2.0f);
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

    public void removeLastWall()
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


    Vector3 findWallCenter()
    {
        Vector3 center;
        Vector3 min, max;

        if (wallSegments.Count < 2)
            return new Vector3(0.0f, 0.0f, 0.0f);

        min = wallSegments[0].startCell.obj.transform.position;
        max = wallSegments[0].startCell.obj.transform.position;
        for (int n = 1; n < wallSegments.Count; n++)
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
        center.y = 0.0f;

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
            uvs[n].x = (vertices[n].x - min.x) / size.x;
            uvs[n].y = (vertices[n].z - min.z) / size.y;
        }


        return uvs;


    }


    public Vector3 createFloor()
    {
        if (roomFloor != null)
            Destroy(roomFloor);

        roomFloor = new GameObject();
        roomFloor.AddComponent<MeshFilter>();
        roomFloor.AddComponent<MeshRenderer>().material = Instantiate(Resources.Load("FloorMat")) as Material;

        var mesh = new Mesh();

        var vertices = new Vector3[wallSegments.Count + 1];
        var normals = new Vector3[wallSegments.Count + 1];
        var triangles = new int[(wallSegments.Count + 1) * 3];

        vertices[0] = findWallCenter();
        normals[0] = new Vector3(0.0f, 1.0f, 0.0f);

        for (int n = 0; n < wallSegments.Count; n++)
        {
            vertices[n + 1] = new Vector3(wallSegments[n].startCell.obj.transform.position.x, 0.0f, wallSegments[n].startCell.obj.transform.position.z);
            normals[n + 1] = new Vector3(0.0f, 1.0f, 0.0f);
        }

        for (int n = 0; n < wallSegments.Count; n++)
        {
            triangles[n * 3 + 0] = n + 1;

            if (n < (wallSegments.Count - 1))
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

        roomFloor.transform.position = new Vector3(0.0f, 0.0f, 0.0f);

        return vertices[0];
    }


    public void addObj(GameObject obj, string hash)
    {
        obj.GetComponent<UnityGLTF.GLTFComponent>().transform.position = new Vector3(0.0f, 2.0f, 0.0f);
        /*obj.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.5f, 0.5f);*/

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
        string URL = "http://" + this.server + "/jsonrpc";
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
        string URL = "http://" + this.server + "/jsonrpc";

        for (int n = 0; n < tx.txsin.Count; n++)
        {
            byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(tx.txsin[n].signHash), this.domainParams);
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
        StartCoroutine(SubmitTx(tx.txid));
    }

    IEnumerator addnodes(gltfRef gltfref)
    {
        string URL = "http://" + this.server + "/jsonrpc";

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
                    StartCoroutine(signTxInputs(gltfref.objs[on].nodePTx));

                    break;
            }
        }
    }

    IEnumerator SubmitNodesTx(gltfRef gltfref)
    {
        string URL = "http://" + this.server + "/jsonrpc";

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
        string URL = "http://" + this.server + "/jsonrpc";

        for (int on = 0; on < gltfref.objs.Count; on++)
        {
            for (int n = 0; n < gltfref.objs[on].nodeTx.txsin.Count; n++)
            {
                byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.objs[on].nodeTx.txsin[n].signHash), this.domainParams);
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
        StartCoroutine(SubmitNodesTx(gltfref));
    }

    IEnumerator makenodes(gltfRef gltfref)
    {
        string URL = "http://" + this.server + "/jsonrpc";

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
        StartCoroutine(signNodesTxInputs(gltfref));
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

        string URL = "http://" + this.server + "/jsonrpc";

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
                    StartCoroutine(signTxInputs(myTransaction));

                    break;
            }
        }
    }

    IEnumerator SubmitSceneTx(gltfRef gltfref)
    {
        string URL = "http://" + this.server + "/jsonrpc";
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

                StartCoroutine(makenodes(gltfref));
                break;
        }
    }

    IEnumerator signSceneTxInputs(gltfRef gltfref)
    {
        string URL = "http://" + this.server + "/jsonrpc";

        for (int n = 0; n < gltfref.sceneTx.txsin.Count; n++)
        {
            byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.sceneTx.txsin[n].signHash), this.domainParams);
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
        StartCoroutine(addRoomSceneObj(gltfref));

        StartCoroutine(SubmitSceneTx(gltfref));
    }

    IEnumerator makeobjs()
    {
        string URL = "http://" + this.server + "/jsonrpc";

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
                    StartCoroutine(signSceneTxInputs(gltfref));
                    break;
            }
        }
    }

    IEnumerator SubmitRoomTx()
    {
        string URL = "http://" + this.server + "/jsonrpc";
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

                StartCoroutine(makeobjs());
                break;
        }
    }

    IEnumerator signRoomTxInputs()
    {
        string URL = "http://" + this.server + "/jsonrpc";


        for (int n = 0; n < this.roomTx.txsin.Count; n++)
        {
            byte[] derSign = this.saveInfos.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(this.roomTx.txsin[n].signHash), this.domainParams);
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

        StartCoroutine(SubmitRoomTx());
    }


    public IEnumerator makeroom(SaveInfo infos)
    {
        this.saveInfos = infos;
        string URL = "http://" + this.server + "/jsonrpc";
        string roomJSon = "{name : \"" + this.roomName + "\"}";
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
                StartCoroutine(signRoomTxInputs());

                break;
        }
    }

    public void saveAllScenes(SaveInfo infos)
    {
        Debug.Log("save scenes " + this.sceneObjects.Count);
        StartCoroutine(this.makeroom(infos));
    }


}


