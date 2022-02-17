using System.Collections;
using System.Collections.Generic;

using System;
using System.IO;


using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Networking;

using UnityGLTF.Extensions;


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
        obj.SetActive(true);
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
    float sizeX = 10.0f, sizeY = 10.0f;

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
                cells[(ny * w) + nx].obj.transform.position = new Vector3((nx + x) * sizeX, 0.0f, (ny + y) * sizeY);
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

    public Vector2 CoordToPos(Vector2Int pos)
    {
        return new Vector2((pos.x + 0.5f) * sizeX + x, (pos.y + 0.5f) * sizeY + y );
    }

    public Vector2Int PosToCoord(Vector2 pos)
    {
        return new Vector2Int((int)Math.Floor(pos.x / sizeX) - x, (int)Math.Floor(pos.y / sizeY) - y);
    }

    public cell FindCellByPos(Vector2 pos)
    {
        Vector2Int coord = PosToCoord(pos);
        return cells[w * coord.y + coord.x];
    }
}


class WallSegment
{

    public GameObject defObj;
    public GameObject wallObj;
    public gltfRef gltfObjs;
    public Vector2 start, end;
    public Transaction wallTx;
 

    public Vector2 getDirection()
    {
        return (end - start);
    }


    public double getAngle()
    {
        double angle = 0.0;
        Vector2 dir = getDirection();
        dir.Normalize();

        if (dir.sqrMagnitude > 0.001f)
        {
            angle = Math.Atan2(dir.x, dir.y);
        }

        return angle;
    }


    public Vector2[] getArray(float step)
    {
        Vector2 dir = getDirection();
        float l = dir.magnitude;
        int nstep = (int)Math.Floor(l / step);
        Vector2[] ret = new Vector2[nstep];

        step = l / nstep;
        dir.Normalize();
        Vector2 vstart = new Vector2(start.x + (dir.x * step / 2.0f), start.y + (dir.y * step / 2.0f));

        for (int n = 0; n < nstep; n++)
        {
            ret[n] = new Vector2(vstart.x + n * dir.x * step, vstart.y + n * dir.y * step);
        }

        return ret;
    }

    public void recompute(float height)
    {

        this.defObj.SetActive(true);
        Vector2 delta = getDirection();

        this.defObj.transform.position = new Vector3(start.x + delta.x / 2.0f, height / 2.0f, start.y + delta.y / 2.0f);
        this.defObj.transform.localScale = new Vector3(4.0f, height, delta.magnitude);

        delta.Normalize();
        if (delta.sqrMagnitude > 0.001f)
        {
            double langle;

            langle = Math.Atan2(delta.x, delta.y);

            this.defObj.transform.rotation = Quaternion.Euler(0.0f, (float)(langle * 180.0f / Math.PI), 0.0f);
        }

    }
}

class Blinker : MonoBehaviour
{
    double cur;
    Color[] matCols = null;
    void Start()
    {
        MeshRenderer mrs = GetComponent<MeshRenderer>();
        cur = 0;


        matCols = new Color[mrs.materials.Length];

        for (int n = 0; n < mrs.materials.Length; n++)
        {
            matCols[n] = mrs.materials[n].color;
        }
    }
    void Update()
    {
        MeshRenderer mrs = GetComponent<MeshRenderer>();
        cur += Time.deltaTime;
        float alpha = (float)(Math.Sin(cur * 4.0f) + 1.0f) / 2.0f;

        for (int n = 0; n < mrs.materials.Length; n++)
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
            mrs.materials[n].SetColor("_Color", new Color(matCols[n].r, matCols[n].g, matCols[n].b, matCols[n].a));
        }
    }
}

public class roomUser
{
    public byte[] pkey;
    public string addr;
    public string name;
    public byte[] avatar;
    public Vector3 pos;
    public Quaternion rot;

    public GameObject AvatarObj;
}

public class vrRoom : MonoBehaviour
{
    public Material wallSegMat;
    public Material defaultSphereMat;
    public Material selectedSphereMat;
    public Material SphereEditMat;
    public string roomName;

    public float wallHeight = 10.0f;
    public string server;
    public GameObject floorPlane;
    public GameObject mainPanel;
    public byte[] roomHash;

    public int sceneTypeId = 0x34;
    public int wallTypeId = 0x01f;
    public int roomTypeId = 0x37;
    public int avatarTypeId = 0x36;
    public int userTypeId = 0x2B;
    public int nodeTypeId = 0x08;

    public List<roomUser> users;

    


    private Transaction roomTx;
    private Transaction userTx;
    

    private Grid grid;

    private string appName = "UnityApp";
    private string baseURL = "/app/UnityApp";

    private string myaddr;
    

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
    private GameObject Loading = null;

    private int prevLayerMask = 0;

    private bool EditRoomWall = false;
    private bool CreateWalls = false;
    private bool outScope = false;
    private bool SetWallObj = false;
    private bool lastTrigger = false;
    private bool lastgripButton = false;
    private bool hasHeadset = false;


    private Vector3 origPos ;

    private XRRayInteractor RightInteractor;

    void Start()
    {
        roomName = "my room";
        sceneObjects = new List<gltfRef>();

        hasHeadset = hasHMD();

        wallSegments = new List<WallSegment>();
        users = new List<roomUser>();

        var rightCont = GameObject.Find("RightHand Controller");
        if(rightCont != null)
            RightInteractor = rightCont.GetComponent<XRRayInteractor>();

        this.roomHash = null;
    }
    public bool isEditingWall()
    {
        return EditRoomWall;
    }


    public void loadUserAvatar(roomUser user)
    {
        if (Nodes.isNullHash(user.avatar))
            return;

      


        string URL = "http://" + server + baseURL + "/obj/" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(user.avatar);

        Debug.Log("loading user avatar " + URL);

        user.AvatarObj = new GameObject();
        user.AvatarObj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        user.AvatarObj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        user.AvatarObj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
        user.AvatarObj.transform.position = user.pos;
        user.AvatarObj.transform.rotation = user.rot;
    }

    public void newUser(roomUser user)
    {
        

        for(int n=0;n<users.Count;n++)
        {
            if (users[n].addr == user.addr)
            {
                /*users[n] = user;*/

                if(Nodes.compareHash(user.avatar, users[n].avatar) != 0)
                {
                    users[n].avatar = user.avatar;
                    loadUserAvatar(user);
                }
                else if(users[n].AvatarObj)
                {
                    users[n].AvatarObj.transform.position = new Vector3(user.pos.x, user.pos.y, user.pos.z); ;
                    users[n].AvatarObj.transform.rotation = new Quaternion(user.rot.x, user.rot.y, user.rot.z, user.rot.w);
                    users[n].AvatarObj.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f); ;
                }
                return;
            }
        }

        
        loadUserAvatar(user);
        

        users.Add(user);
    }
    public byte[] userToBytes(roomUser user)
    {
        MemoryStream m = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(m);

        Nodes.writeVINT((ulong)user.name.Length, writer);
        writer.Write(user.name.ToCharArray());
        writer.Write(user.avatar);

        writer.Write(user.pos.x);
        writer.Write(user.pos.y);
        writer.Write(user.pos.z);


        writer.Write((double)user.rot.x);
        writer.Write((double)user.rot.y);
        writer.Write((double)user.rot.z);
        writer.Write((double)user.rot.w);

        byte[] objBuffer = new byte[m.Position];
        Buffer.BlockCopy(m.ToArray(), 0, objBuffer, 0, (int)m.Position);


        return objBuffer;
    }
    public roomUser  BytesTouser(byte[] data)
    {
        float x;
        float y;
        float z;

        double dx;
        double dy;
        double dz;
        double w;
        roomUser user = new roomUser();

        MemoryStream buffer = new MemoryStream(data);
        BinaryReader reader = new BinaryReader(buffer);

        long nlen = Nodes.readVINT(reader);
        user.name = new string(reader.ReadChars((int)nlen));
        user.avatar = reader.ReadBytes(32);

        x = reader.ReadSingle();
        y = reader.ReadSingle();
        z = reader.ReadSingle();

        user.pos = new Vector3(x, y, z);

        dx = reader.ReadDouble();
        dy = reader.ReadDouble();
        dz = reader.ReadDouble();
        w = reader.ReadDouble();

        user.rot = new Quaternion((float)dx, (float)dy, (float)dz, (float)w);

        return user;
    }

    public bool newObj(appObj parent, appObj child)
    {
        if (this.roomHash == null)
        {
            Debug.Log("no room hash");
            return false;
        }
      
        if ((parent.type & 0x00FFFFFF) != roomTypeId)
            return false;

        if (Nodes.compareHash(parent.txh, this.roomHash) != 0)
            return false;

        if ((child.type & 0x00FFFFFF) == userTypeId)
        {
            roomUser user = BytesTouser(child.objData);

            user.pkey = child.pubkey;

            Debug.Log("new user " + user.name + " av : " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(user.avatar) + " key : " + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(user.pkey) + " " + user.pos.ToString() + " " + user.rot.eulerAngles.ToString());

            user.addr = Wallet.pub2addr(new ECPublicKeyParameters(Wallet.domainParams.Curve.DecodePoint(user.pkey), Wallet.domainParams));

            if (user.addr != Wallet.mainKey.PubAddr)
                newUser(user);

            

            return true;
        }
            


        return false;

    }

    public void resetRoom()
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
        this.roomHash = null;

        if (ObjPannel != null)
            closeObjPanel();
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

    public void roomEditor()
    {
        grid = new Grid(-10, -10, 20, 20, selectedSphereMat, SphereEditMat, defaultSphereMat);

        EditWallPanel = Instantiate(Resources.Load("EditWalls")) as GameObject;
        GameObject.Find("ToggleWall").GetComponent<Toggle>().onValueChanged.AddListener(ToggleEditWalls);
        GameObject.Find("ToggleRoom").GetComponent<Toggle>().onValueChanged.AddListener(ToggleEditRoom);
        GameObject.Find("ToggleSet").GetComponent<Toggle>().onValueChanged.AddListener(ToggleEditWall);

        CreateWalls = true;
        EditRoomWall = true;

        var CameraOffset = GameObject.Find("Camera Offset");

        origPos = new Vector3(CameraOffset.transform.position.x, CameraOffset.transform.position.y, CameraOffset.transform.position.z);

       
        CameraOffset.transform.position = new Vector3(-0, 100.0f, -80.0f);
        CameraOffset.transform.rotation = Quaternion.Euler(new Vector3(45, 0, 0));
        
        floorPlane.SetActive(false);

        if(roomFloor != null)
        {
            Destroy(roomFloor);
            roomFloor = null;
        }
    }

    public void setName(string name)
    {
        this.roomName = name;
    }

    public void newRoom()
    {
        if(grid != null)
            ResetGrid();

        resetRoom();
        roomEditor();
    }

    public void newWallSeg(Vector2 start, string hash)
    {
        

        if (wallSegments.Count>0)
        {
            wallSegments[wallSegments.Count-1].end = new Vector2(start.x, start.y);
        }

        WallSegment newSeg=new WallSegment();




        /*
        Debug.Log("load wall seg " + URL);
        string URL = "http://" + server + baseURL + "/obj/" + hash;
        newSeg.wallObj = new GameObject("Wall " + hash);
        newSeg.wallObj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        newSeg.wallObj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        newSeg.wallObj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
        newSeg.wallObj.SetActive(false);
        */

        newSeg.defObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newSeg.defObj.GetComponentInChildren<Renderer>().material = wallSegMat;
        newSeg.defObj.layer = 6;
        newSeg.defObj.transform.SetParent(this.transform, false);
        newSeg.defObj.name = "Wall " + wallSegments.Count;


        newSeg.start = new Vector2(start.x, start.y);
        newSeg.gltfObjs = new gltfRef(hash);
        wallSegments.Add(newSeg);
    }

    public void buildWalls()
    {
        if (wallSegments.Count < 1)
            return;

        wallSegments[wallSegments.Count - 1].end = wallSegments[0].start;

        for(int nseg = 0; nseg< wallSegments.Count;nseg ++)
        { 
            //Vector2[] pos = wallSegments[nseg].getArray(10.0f);
            Vector2 delta = wallSegments[nseg].getDirection();
            double angle = wallSegments[nseg].getAngle();
            

            wallSegments[nseg].defObj.transform.position = new Vector3(wallSegments[nseg].start.x + delta.x / 2.0f, wallHeight / 2.0f, wallSegments[nseg].start.y + delta.y / 2.0f);
            wallSegments[nseg].defObj.transform.localScale = new Vector3(4.0f, wallHeight, delta.magnitude);
            wallSegments[nseg].defObj.transform.rotation = Quaternion.Euler(0.0f, (float)(angle * 180.0f / Math.PI), 0.0f);

            /*
            Vector2[] pos = wallSegments[nseg].getArray(10.0f);
            float scaleZ = delta.magnitude / (10.0f * pos.Length);
            for (int n = 0; n < pos.Length; n++)
            {
                objNode node = new objNode(GameObject.Instantiate<GameObject>(wallSegments[nseg].wallObj));


                node.obj.transform.SetParent(this.transform, false);

                node.obj.transform.position = new Vector3(pos[n].x, wallHeight / 2.0f, pos[n].y);
                node.obj.transform.localScale = new Vector3(10.0f * scaleZ, wallHeight, 10.0f);
                node.obj.transform.rotation = Quaternion.Euler(new Vector3(0, (float)((angle * 180.0) / Math.PI) + 90.0f, 0));


                wallSegments[nseg].gltfObjs.objs.Add(node);
                
            }
            Destroy(wallSegments[nseg].wallObj);
            */


        }

        createFloor();

        floorPlane.SetActive(false);
    }

    public void loadScene(string hash)
    {
        string URL = "http://" + server + baseURL + "/page/unity.site/scene/" + hash;

        Debug.Log("loading scene " + URL);

        GameObject obj = new GameObject("Scene " + hash);

        obj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
        obj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
        obj.transform.SetParent(this.transform, false);

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
        obj.transform.SetParent(this.transform, false);


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
            if (this.wallSegments.Count >= 2)
                this.createFloor();
            else
                floorPlane.SetActive(true);

            var CameraOffset = GameObject.Find("Camera Offset");

            CameraOffset.GetComponent<CharacterController>().enabled = false;
            CameraOffset.transform.position = origPos;
            CameraOffset.GetComponent<CharacterController>().enabled = true;

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

        SetLayerRecursively(mainPanel, true);
    }

    void showObjPannel()
    {
        if (ObjPannel == null)
            ObjPannel = Instantiate(Resources.Load("ObjCanvas")) as GameObject;

        ObjPannel.GetComponent<ObjSelection>().SelectRoomObject = hoverRoomObject;
        

        GameObject.Find("CloseObjPanel").GetComponent<Button>().onClick.AddListener(closeObjPanel);

        SetLayerRecursively(mainPanel, false);
    }

    void EnableCreateWalls()
    {
        this.removeLastWall();

        if (this.wallSegments.Count > 0)
            selectedCell = grid.FindCellByPos(this.wallSegments[this.wallSegments.Count - 1].end);

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

    public static bool hasHMD()
    {
        var HMDControllers = new List<UnityEngine.XR.InputDevice>();
        var desiredCharacteristics = UnityEngine.XR.InputDeviceCharacteristics.HeadMounted;
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, HMDControllers);

        if (HMDControllers.Count > 0)
            return true;
        else
            return false;
    }


    void SetLayerRecursively(GameObject obj, bool on)
    {
        if (null == obj)
        {
            return;
        }

        if (obj.GetComponent<Button>())
            obj.GetComponent<Button>().interactable = on;

        foreach (Transform child in obj.transform)
        {
            if (null == child)
            {
                continue;
            }
            SetLayerRecursively(child.gameObject, on);
        }
    }
    /*
    void updateWalls()
    {
       for (int n = 0; n < wallSegments.Count; n++)
       {
           if (wallSegments[n].gltfObjs == null)
               continue;

           for (int nn = 0; nn < wallSegments[n].gltfObjs.objs.Count; nn++)
           {
               if (wallSegments[n].gltfObjs.objs[nn].loaded == true)
                   continue;

               var gl = wallSegments[n].gltfObjs.objs[nn].obj.GetComponent<UnityGLTF.GLTFComponent>();

               if (gl.LastLoadedScene == null)
                   continue;

               if (gl.LastLoadedScene.scene.isLoaded == true)
               {
                   var mesh = wallSegments[n].gltfObjs.objs[nn].obj.GetComponentInChildren<MeshFilter>().mesh;

                   wallSegments[n].gltfObjs.objs[nn].obj.transform.position = new Vector3(wallSegments[n].gltfObjs.objs[nn].obj.transform.position.x, -mesh.bounds.min.y * wallHeight, wallSegments[n].gltfObjs.objs[nn].obj.transform.position.z);
                   wallSegments[n].gltfObjs.objs[nn].loaded = true;
                   wallSegments[n].defObj.SetActive(false);

                   Debug.Log("Wall loaded " + n + " " + nn);
               }
           }
       }
    }
    */

    bool detectSceneObjHit(Ray ray)
    {
        RaycastHit hit=new RaycastHit();

        for (int n = 0; n < sceneObjects.Count; n++)
        {
            for (int nn = 0; nn < sceneObjects[n].objs.Count; nn++)
            {
                var meshes = sceneObjects[n].objs[nn].obj.GetComponentsInChildren<MeshFilter>();

                foreach (MeshFilter mesh in meshes)
                {
                    var box = mesh.GetComponent<BoxCollider>();
                    if (box.Raycast(ray, out hit, 1000.0f))
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
                        return true;
                    }
                }
            }
        }


        if (hoverRoomObject != null)
        {
            Destroy(hoverRoomObject.GetComponent<Blinker>());
            hoverRoomObject = null;
        }
        return false;
    }

    void Update()
    {
        bool button = false;
        RaycastHit hit = new RaycastHit();
        GameObject hitObj = null;


        updateLoading();


        if (EditRoomWall)
        {
            /*
            List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithRole(UnityEngine.XR.InputDeviceRole.RightHanded, devices);
            */

            var HandControllers = new List<UnityEngine.XR.InputDevice>();
            var desiredCharacteristics = UnityEngine.XR.InputDeviceCharacteristics.HeldInHand;

            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, HandControllers);

            foreach (var device in HandControllers)
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
                            seg.start = new Vector2(selectedCell.obj.transform.position.x, selectedCell.obj.transform.position.z);
                            seg.end = new Vector2(hoveredCell.obj.transform.position.x, hoveredCell.obj.transform.position.z);

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
                            Vector2[] pos = wallSegments[segNum].getArray(10.0f);
                            double angle = wallSegments[segNum].getAngle();

                            float scaleZ = wallSegments[segNum].getDirection().magnitude / (10.0f * pos.Length);

                            wallSegments[segNum].gltfObjs = new gltfRef(currentWallHash);

                            for (int n = 0; n < pos.Length; n++)
                            {
                                objNode node = new objNode(GameObject.Instantiate<GameObject>(currentWallObj));
                                var mesh = currentWallObj.GetComponentInChildren<MeshFilter>().mesh;

                                node.obj.transform.position = new Vector3(pos[n].x, -mesh.bounds.min.y* wallHeight,  pos[n].y);
                                node.obj.transform.localScale = new Vector3(10.0f * scaleZ, wallHeight, 10.0f);
                                node.obj.transform.rotation = Quaternion.Euler(new Vector3(0, (float)((angle * 180.0) / Math.PI) + 90.0f, 0));

                                /*node.obj.AddComponent<BoxCollider>().size = mesh.bounds.size;*/

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
            Ray ray;
            if ((hasHeadset)&& (RightInteractor != null))
            {
               Vector3[] points = new Vector3[2];
               int nPoints;

               RightInteractor.GetLinePoints(ref points, out nPoints);

               ray = new Ray(points[0], points[1] - points[0]);

               var HandControllers = new List<UnityEngine.XR.InputDevice>();
               var desiredCharacteristics = UnityEngine.XR.InputDeviceCharacteristics.HeldInHand | UnityEngine.XR.InputDeviceCharacteristics.Right;

               UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, HandControllers);

               foreach (var device in HandControllers)
               {
                   bool gripButton;

                   if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out gripButton))
                   {
                       if (gripButton != lastgripButton)
                           button = gripButton;

                       lastgripButton = gripButton;
                   }
               }

                /*
                if (RightInteractor.TryGetCurrent3DRaycastHit(out hit))
                {
                    hitObj = hit.collider.gameObject;
                }
                */

            }
            else
            {
                ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                button = Input.GetMouseButtonDown(0);
            }

            detectSceneObjHit(ray);

            if ((button)&& (hoverRoomObject != null))
            {
                showObjPannel();
            }
        }

    }

    public int FindWallSegment(int x, int y)
    {
        for (int n = 0; n < wallSegments.Count; n++)
        {
            Vector2Int coord = grid.PosToCoord(wallSegments[n].start);
            if ((coord.x == x) && (coord.y == y))
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
            currentSeg.transform.SetParent(this.transform, false);

            currentSeg.layer = 6;
        }

        if (wallSegments.Count >= 2)
        {
            if (lastSeg == null)
            {
                lastSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lastSeg.GetComponentInChildren<Renderer>().material = wallSegMat;
                lastSeg.transform.SetParent(this.transform, false);
                lastSeg.layer = 6;
            }

            Vector3 ldelta = new Vector3(wallSegments[0].start.x - hoveredCell.obj.transform.position.x, 0.0f, wallSegments[0].start.y - hoveredCell.obj.transform.position.z);

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

        wallSegments[seg1].start = new Vector2(hoveredCell.obj.transform.position.x, hoveredCell.obj.transform.position.z);
        wallSegments[seg2].end = new Vector2(hoveredCell.obj.transform.position.x, hoveredCell.obj.transform.position.z); 


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
            newSegObj.transform.SetParent(this.transform, false);
            newSegObj.layer = 6;

            Vector3 ldelta = new Vector3(wallSegments[0].start.x - wallSegments[wallSegments.Count - 1].end.x, 0.0f, wallSegments[0].start.y - wallSegments[wallSegments.Count - 1].end.y);

            newSegObj.transform.position = new Vector3(wallSegments[wallSegments.Count - 1].end.x + ldelta.x / 2.0f,  wallHeight / 2.0f, wallSegments[wallSegments.Count - 1].end.y + ldelta.z / 2.0f);
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
            seg.start = wallSegments[wallSegments.Count - 1].end;
            seg.end = wallSegments[0].start;

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


    Vector2 findWallCenter()
    {
        Vector2 center;
        Vector2 min, max;

        if (wallSegments.Count < 2)
            return new Vector2(0.0f, 0.0f);

        min = wallSegments[0].start;
        max = wallSegments[0].start;
        for (int n = 1; n < wallSegments.Count; n++)
        {
            if (wallSegments[n].start.x < min.x)
                min.x = wallSegments[n].start.x;

            if (wallSegments[n].start.y < min.y)
                min.y = wallSegments[n].start.y;

            if (wallSegments[n].start.x > max.x)
                max.x = wallSegments[n].start.x;

            if (wallSegments[n].start.y > max.y)
                max.y = wallSegments[n].start.y;

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
            vertices[n + 1] = new Vector3(wallSegments[n].start.x, 0.0f, wallSegments[n].start.y);
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

        roomFloor.name = "Room Floor";
        roomFloor.GetComponent<MeshFilter>().mesh = mesh;
        roomFloor.AddComponent<MeshCollider>().sharedMesh = mesh;
        roomFloor.AddComponent<TeleportationArea>();
        //roomFloor.layer = 6;

        

        roomFloor.transform.position = new Vector3(0.0f, 0.0f, 0.0f);

        roomFloor.transform.SetParent(this.transform,false);

        return vertices[0];
    }

    public void showLoading()
    {
        Loading = Instantiate(Resources.Load("Loading")) as GameObject;

        Loading.GetComponentInChildren<Slider>().minValue = 0;
        Loading.GetComponentInChildren<Slider>().maxValue = wallSegments.Count + sceneObjects.Count;

        for (int n = 0; n < Camera.allCameras.Length; n++)
        {
            if (Camera.allCameras[n].name == "UI Camera")
                Loading.GetComponent<Canvas>().worldCamera = Camera.allCameras[n];
        }

    }

    public void updateLoading()
    {
        if (Loading == null)
            return;

        if (loadingRoom.nObjLoaded < loadingRoom.objects.Length)
        {
            if (sceneObjects[loadingRoom.nObjLoaded].objs[0].obj.GetComponent<UnityGLTF.GLTFComponent>().LastLoadedScene != null)
            {
                loadingRoom.nObjLoaded++;

                if(loadingRoom.nObjLoaded < loadingRoom.objects.Length)
                    this.loadScene(loadingRoom.objects[loadingRoom.nObjLoaded].objHash);
            }
        }
        else
        {
            if(wallSegments[loadingRoom.curLoadWall].wallObj == null)
            {
                string URL = "http://" + server + baseURL + "/obj/" + wallSegments[loadingRoom.curLoadWall].gltfObjs.rootHash;

                wallSegments[loadingRoom.curLoadWall].wallObj = new GameObject("Wall " + wallSegments[loadingRoom.curLoadWall].gltfObjs.rootHash);
                wallSegments[loadingRoom.curLoadWall].wallObj.AddComponent<UnityGLTF.GLTFComponent>().GLTFUri = URL;
                wallSegments[loadingRoom.curLoadWall].wallObj.GetComponent<UnityGLTF.GLTFComponent>().Timeout = 120;
                wallSegments[loadingRoom.curLoadWall].wallObj.GetComponent<UnityGLTF.GLTFComponent>().Collider = UnityGLTF.GLTFSceneImporter.ColliderType.Box;
                //wallSegments[loadingRoom.curLoadWall].wallObj.SetActive(false);
            }
            else if(wallSegments[loadingRoom.curLoadWall].wallObj.GetComponent<UnityGLTF.GLTFComponent>().LastLoadedScene != null)
            {
                int nextwall = -1;
                for (int nc = 0; nc < wallSegments.Count; nc++)
                {
                    if (wallSegments[nc].gltfObjs.rootHash == wallSegments[loadingRoom.curLoadWall].gltfObjs.rootHash)
                    {
                        Vector2 delta = wallSegments[nc].getDirection();
                        double angle = wallSegments[nc].getAngle();
                        Vector2[] pos = wallSegments[nc].getArray(10.0f);
                        float scaleZ = delta.magnitude / (10.0f * pos.Length);
                        for (int n = 0; n < pos.Length; n++)
                        {
                            objNode node = new objNode(GameObject.Instantiate<GameObject>(wallSegments[loadingRoom.curLoadWall].wallObj));
                            node.obj.transform.SetParent(this.transform, false);
                            node.obj.transform.position = new Vector3(pos[n].x, wallHeight / 2.0f, pos[n].y);
                            node.obj.transform.localScale = new Vector3(10.0f * scaleZ, wallHeight, 10.0f);
                            node.obj.transform.rotation = Quaternion.Euler(new Vector3(0, (float)((angle * 180.0) / Math.PI) + 90.0f, 0));
                            var mesh = node.obj.GetComponentInChildren<MeshFilter>().mesh;
                            node.obj.transform.position = new Vector3(node.obj.transform.position.x, -mesh.bounds.min.y * wallHeight, node.obj.transform.position.z);
                            wallSegments[nc].defObj.SetActive(false);

                            wallSegments[nc].gltfObjs.objs.Add(node);
                        }
                        loadingRoom.nWallsLoaded++;
                    }
                    else if ((nextwall == -1)&&(wallSegments[nc].gltfObjs.objs.Count==0))
                        nextwall = nc;
                }
                Destroy(wallSegments[loadingRoom.curLoadWall].wallObj);
                wallSegments[loadingRoom.curLoadWall].wallObj = null;

                if (nextwall >= 0)
                    loadingRoom.curLoadWall = nextwall;

            }
        }
      
        Loading.GetComponentInChildren<Slider>().value = loadingRoom.nObjLoaded+ loadingRoom.nWallsLoaded;

        if((loadingRoom.nObjLoaded == sceneObjects.Count ) &&( loadingRoom.nWallsLoaded == wallSegments.Count ))
        {
            Destroy(Loading);
            Loading=null;
            loadingRoom = null;
        }
            
    }

    public bool isLoaded()
    {
        if (Loading != null)
            return false;

        return true;
    }
    public void addObj(GameObject obj, string hash)
    {
        obj.GetComponent<UnityGLTF.GLTFComponent>().transform.position = new Vector3(0.0f, 2.0f, 0.0f);

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
    Room loadingRoom;

    public IEnumerator loadRoom(string hash)
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

                loadingRoom = JsonUtility.FromJson<Room>(webRequest.downloadHandler.text);
                if (loadingRoom.objects == null)
                {
                    Debug.Log("ROOM  no objects");
                    loadingRoom = null;
                }
                if (loadingRoom.walls == null)
                {
                    Debug.Log("ROOM  no walls");
                    loadingRoom = null;
                }
                else
                {
                    for (int n = 0; n < loadingRoom.walls.Length; n++)
                    {
                        this.newWallSeg(new Vector2(loadingRoom.walls[n].start[0], loadingRoom.walls[n].start[1]), loadingRoom.walls[n].objHash.objHash);
                    }
                    this.buildWalls();
                }

                this.showLoading();

                loadingRoom.nObjLoaded = 0;
                loadingRoom.nWallsLoaded= 0;

                this.loadScene(loadingRoom.objects[0].objHash); 


                break;
        }
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
            byte[] derSign = Wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(tx.txsin[n].signHash), Wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + tx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
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
            string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + appName + "\",\"" + gltfref.sceneTx.txid + "\",\"nodes\",\"" + gltfref.objs[on].nodeTx.txid + "\"]}";
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
                byte[] derSign = Wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.objs[on].nodeTx.txsin[n].signHash), Wallet.domainParams);
                string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + gltfref.objs[on].nodeTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
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
            var mesh = obj.GetComponentInChildren<MeshFilter>();

            Quaternion TotalQ = obj.transform.localRotation * mesh.transform.localRotation;
            GLTF.Math.Quaternion myQ = TotalQ.ToGltfQuaternionConvert();
            Vector3 mP = new Vector3(mesh.transform.position.x * SchemaExtensions.CoordinateSpaceConversionScale.X, mesh.transform.position.y * SchemaExtensions.CoordinateSpaceConversionScale.Y, mesh.transform.position.z * SchemaExtensions.CoordinateSpaceConversionScale.Z);
            Vector3 Scale = mesh.transform.localScale;

            var nodeJson = "{name: \"node " + n.ToString() + "\", ";
            nodeJson += "translation: [" + mP[0].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + mP[1].ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + mP[2].ToString(System.Globalization.CultureInfo.InvariantCulture) + "], ";
            nodeJson += "scale: [" + Scale.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Scale.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Scale.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "], ";
            nodeJson += "rotation: [" + myQ.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + myQ.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + myQ.Z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + myQ.W.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]}";

            string makenodeobj = "{id:1 , jsonrpc: \"2.0\", method:\"makeappobjtx\", params : [\"" + this.appName + "\"," + this.nodeTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + nodeJson + "]}";

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
            string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + appName + "\",\"" + this.roomTx.txid + "\",\"objects\",\"" + this.sceneObjects[n].sceneTx.txid + "\"]}";
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
            byte[] derSign = Wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(gltfref.sceneTx.txsin[n].signHash), Wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + gltfref.sceneTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
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


    IEnumerator SubmitWallTx(WallSegment seg)
    {
        string URL = "http://" + this.server + "/jsonrpc";
        string submittx = "{id:1 , jsonrpc: \"2.0\", method:\"submittx\", params : [\"" + seg.wallTx.txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, submittx);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("SubmitWallTx  " + submittx + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("SubmitWallTx  " + submittx + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:
                Debug.Log("SubmitWallTx signtxinput" + submittx + " : \nReceived: " + webRequest.downloadHandler.text);
                break;
        }
    }

    IEnumerator addRoomSceneWall(WallSegment Seg)
    {
        Seg.wallTx.issigned = true;


        string URL = "http://" + this.server + "/jsonrpc";

        string addchild = "{id:1 , jsonrpc: \"2.0\", method:\"addchildobj\", params : [\"" + appName + "\",\"" + this.roomTx.txid + "\",\"walls\",\"" + Seg.wallTx.txid + "\"]}";
        UnityWebRequest webRequest = UnityWebRequest.Put(URL, addchild);
        webRequest.SetRequestHeader("Content-Type", "application/json");

        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();
        switch (webRequest.result)
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log("addRoomSceneWall  " + addchild + " : Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.ProtocolError:
                Debug.Log("addRoomSceneWall  " + addchild + " : HTTP Error: " + webRequest.error);
                break;
            case UnityWebRequest.Result.Success:

                Debug.Log("addRoomSceneWall " + addchild + " : \nReceived: " + webRequest.downloadHandler.text);
                Transaction myTransaction = JsonUtility.FromJson<RPCTransaction>(webRequest.downloadHandler.text).result.transaction;
                Debug.Log("addRoomSceneWall myTransaction " + myTransaction.txid + " " + this.roomTx.txid);
                myTransaction.issigned = false;
                StartCoroutine(signTxInputs(myTransaction));

                break;
        }
    }


    IEnumerator signWallTxInputs(WallSegment seg)
    {
        string URL = "http://" + this.server + "/jsonrpc";

        for (int n = 0; n < seg.wallTx.txsin.Count; n++)
        {
            byte[] derSign = Wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(seg.wallTx.txsin[n].signHash), Wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + seg.wallTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
            UnityWebRequest webRequest = UnityWebRequest.Put(URL, signtx);
            webRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("signWallTxInputs  " + signtx + " : Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("signWallTxInputs  " + signtx + " : HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log("signWallTxInputs signtxinput" + signtx + " : \nReceived: " + webRequest.downloadHandler.text);
                    seg.wallTx.txid = JsonUtility.FromJson<RPCSign>(webRequest.downloadHandler.text).result.txid;
                    Debug.Log("signWallTxInputs signtxinput" + seg.wallTx.txid);
                    break;
            }
        }
        StartCoroutine(addRoomSceneWall(seg));

        StartCoroutine(SubmitWallTx(seg));
    }


    IEnumerator makewalls()
    {
        for (int n = 0; n < this.wallSegments.Count; n++)
        {
            string URL = "http://" + this.server + "/jsonrpc";
            string wallJSon = "{start : [" + this.wallSegments[n].start.x.ToString() + "," + this.wallSegments[n].start.y.ToString() + "], objHash : \"" + this.wallSegments[n].gltfObjs.rootHash + "\" }";
            string makeappwall = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + appName + "\"," + wallTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + wallJSon + "]}";

            Debug.Log("makewall  " + URL + "  " + makeappwall);

            UnityWebRequest scenewebRequest = UnityWebRequest.Put(URL, makeappwall);
            scenewebRequest.SetRequestHeader("Content-Type", "application/json");
            // Request and wait for the desired page.
            yield return scenewebRequest.SendWebRequest();

            switch (scenewebRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log("makewall  " + makeappwall + " : Error: " + scenewebRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log("makewall  " + makeappwall + " : HTTP Error: " + scenewebRequest.error);
                    break;
                case UnityWebRequest.Result.Success:

                    Debug.Log("makewall " + makeappwall + " : \nReceived: " + scenewebRequest.downloadHandler.text);
                    this.wallSegments[n].wallTx = JsonUtility.FromJson<RPCTransaction>(scenewebRequest.downloadHandler.text).result.transaction;
                    this.wallSegments[n].wallTx.issigned = false;
                    Debug.Log("makeobj myTransaction" + this.wallSegments[n].wallTx.txid + " " + this.wallSegments[n].wallTx.txsin.Count);
                    StartCoroutine(signWallTxInputs(this.wallSegments[n]));
                    break;
            }
        }
    }

    IEnumerator makeobjs()
    {
        string URL = "http://" + this.server + "/jsonrpc";

        for (int n = 0; n < this.sceneObjects.Count; n++)
        {
            gltfRef gltfref = this.sceneObjects[n];
            string sceneJSon = "{root : \"" + gltfref.rootHash + "\"}";
            string makeappobj = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + appName + "\"," + sceneTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + sceneJSon + "]}";
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
                StartCoroutine(makewalls());
                break;
        }
    }

    IEnumerator signRoomTxInputs()
    {
        string URL = "http://" + this.server + "/jsonrpc";


        for (int n = 0; n < this.roomTx.txsin.Count; n++)
        {
            byte[] derSign = Wallet.mainKey.Sign(Org.BouncyCastle.Utilities.Encoders.Hex.Decode(this.roomTx.txsin[n].signHash), Wallet.domainParams);
            string signtx = "{id:1 , jsonrpc: \"2.0\", method: \"signtxinput\", params : [\"" + this.roomTx.txid + "\"," + n.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(derSign) + "\",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"]}";
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




    public IEnumerator makeroom()
    {
        string URL = "http://" + this.server + "/jsonrpc";
        string roomJSon = "{name : \"" + this.roomName + "\", wallHeight : " + wallHeight + "  }";
        string makeapproom = "{id:1 , jsonrpc: \"2.0\", method: \"makeappobjtx\", params : [\"" + appName + "\"," + roomTypeId.ToString() + ",\"" + Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(Wallet.mainKey.getPub().Q.GetEncoded(true)) + "\"," + roomJSon + "]}";
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

    public void saveAllScenes()
    {
        Debug.Log("save scenes " + this.sceneObjects.Count);
        StartCoroutine(this.makeroom());
    }


}


