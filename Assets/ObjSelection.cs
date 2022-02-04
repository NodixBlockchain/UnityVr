using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;

using Org.BouncyCastle.Crypto.Parameters;

public class ObjSelection : MonoBehaviour
{
    public GameObject SelectRoomObject = null;
    public ECDomainParameters domainParams;
    public int MoveObj = 0;
    public float MoveSpeed = 10.0f;
    public float RotSpeed = 1000.0f;

    GameObject panel;

    bool hasPhysic = false;

    private void Start()
    {
        panel = this.transform.Find("ObjSelectPanel").gameObject;
        var objMesh = SelectRoomObject.GetComponent<MeshFilter>().mesh;
        string hpk = SelectRoomObject.GetComponentInParent<UnityGLTF.GLTFComponent>().getRootPubKey();
        if(hpk != null)
        {
            byte[] pubKey = Org.BouncyCastle.Utilities.Encoders.Hex.Decode(hpk);
            WalletAddress addr = new WalletAddress("my", pubKey, domainParams);
            panel.transform.Find("ObjAddr").GetComponent<Text>().text = addr.PubAddr;
        }
        MoveObj = 0;

        

        panel.transform.Find("Objname").GetComponent<Text>().text = objMesh.name;
        
        panel.transform.Find("InputX").GetComponent<InputField>().text = SelectRoomObject.transform.position.x.ToString();
        panel.transform.Find("InputY").GetComponent<InputField>().text = SelectRoomObject.transform.position.y.ToString();
        panel.transform.Find("InputZ").GetComponent<InputField>().text = SelectRoomObject.transform.position.z.ToString();

        panel.transform.Find("ToggleX").GetComponent<Toggle>().onValueChanged.AddListener(ToggleMoveX);
        panel.transform.Find("ToggleY").GetComponent<Toggle>().onValueChanged.AddListener(ToggleMoveY);
        panel.transform.Find("ToggleZ").GetComponent<Toggle>().onValueChanged.AddListener(ToggleMoveZ);
        panel.transform.Find("TogglePhys").GetComponent<Toggle>().onValueChanged.AddListener(TogglePhysic);


        if (SelectRoomObject.GetComponent<Rigidbody>().isKinematic == false)
        {
            hasPhysic = true;
            panel.transform.Find("TogglePhys").GetComponent<Toggle>().isOn = true;
            
        }
        else
        {
            hasPhysic = false;
            panel.transform.Find("TogglePhys").GetComponent<Toggle>().isOn = false;
        }
    }


    void TogglePhysic(bool state)
    {
        bool isMoving = panel.transform.Find("ToggleX").GetComponent<Toggle>().isOn || panel.transform.Find("ToggleY").GetComponent<Toggle>().isOn || panel.transform.Find("ToggleZ").GetComponent<Toggle>().isOn;

        if (state)
        {
            hasPhysic = true;

            if(!isMoving)
                SelectRoomObject.GetComponent<Rigidbody>().isKinematic = false;
        }
        else
        {
            hasPhysic = false;

            if (!isMoving)
                SelectRoomObject.GetComponent<Rigidbody>().isKinematic = true;
        }
            
    }

    void ToggleMoveX(bool state)
    {
        if (state)
        {
            panel.transform.Find("ToggleY").GetComponent<Toggle>().isOn = false;
            panel.transform.Find("ToggleZ").GetComponent<Toggle>().isOn = false;
            SelectRoomObject.GetComponent<Rigidbody>().isKinematic = true;
            MoveObj = 1;
        }
        else
        {
            if (hasPhysic)
                SelectRoomObject.GetComponent<Rigidbody>().isKinematic = false;
            MoveObj = 0;
        }

    }

    void ToggleMoveY(bool state)
    {
        if (state)
        {
            panel.transform.Find("ToggleX").GetComponent<Toggle>().isOn = false;
            panel.transform.Find("ToggleZ").GetComponent<Toggle>().isOn = false;
            SelectRoomObject.GetComponent<Rigidbody>().isKinematic = true;
            MoveObj = 2;
        }
        else
        {
            if (hasPhysic)
                SelectRoomObject.GetComponent<Rigidbody>().isKinematic = false;
            MoveObj = 0;
        }
    }

    void ToggleMoveZ(bool state)
    {
        if (state)
        {
            panel.transform.Find("ToggleX").GetComponent<Toggle>().isOn = false;
            panel.transform.Find("ToggleY").GetComponent<Toggle>().isOn = false;
            SelectRoomObject.GetComponent<Rigidbody>().isKinematic = true;
            MoveObj = 3;
        }
        else
        {

            if(hasPhysic)
                SelectRoomObject.GetComponent<Rigidbody>().isKinematic = false;
            MoveObj = 0;
        }
    }

    public void Close()
    {
        if (hasPhysic)
             SelectRoomObject.GetComponent<Rigidbody>().isKinematic = false;
        else
             SelectRoomObject.GetComponent<Rigidbody>().isKinematic = true;

        MoveObj = 0;
    }

    void Update()
    {
       

        if(SelectRoomObject != null)
        {
            var bottom = this.transform.localScale.y * this.GetComponent<RectTransform>().rect.height / 2.0f;
            var objMesh = SelectRoomObject.GetComponent<Collider>();

            var dir = SelectRoomObject.transform.position - Camera.main.transform.position; //a vector pointing from pointA to pointB
            var rot = Quaternion.LookRotation(dir, Vector3.up); //calc a rotation that

            this.transform.rotation = rot;
            this.transform.position = new Vector3(SelectRoomObject.transform.position.x, bottom + objMesh.bounds.max.y, SelectRoomObject.transform.position.z);
        }

        if (Input.GetMouseButton(0))
        {
            var drop = panel.transform.Find("SelectCoord");
            var dd = drop.GetComponent<Dropdown>();

            switch (dd.value)
            {
                case 0:
                    if (MoveObj == 1)
                        SelectRoomObject.transform.position= new Vector3(SelectRoomObject.transform.position.x + Input.GetAxis("Mouse X") * Time.deltaTime * MoveSpeed, SelectRoomObject.transform.position.y, SelectRoomObject.transform.position.z);
                    else if (MoveObj == 2)
                        SelectRoomObject.transform.position = new Vector3(SelectRoomObject.transform.position.x, SelectRoomObject.transform.position.y + Input.GetAxis("Mouse X") * Time.deltaTime * MoveSpeed,SelectRoomObject.transform.position.z);
                    else if (MoveObj == 3)
                        SelectRoomObject.transform.position = new Vector3(SelectRoomObject.transform.position.x, SelectRoomObject.transform.position.y, SelectRoomObject.transform.position.z + Input.GetAxis("Mouse X") * Time.deltaTime * MoveSpeed);

                    panel.transform.Find("InputX").GetComponent<InputField>().text = SelectRoomObject.transform.position.x.ToString();
                    panel.transform.Find("InputY").GetComponent<InputField>().text = SelectRoomObject.transform.position.y.ToString();
                    panel.transform.Find("InputZ").GetComponent<InputField>().text = SelectRoomObject.transform.position.z.ToString();
                    break;
                case 1:
                    if (MoveObj == 1)
                        SelectRoomObject.transform.Rotate(Input.GetAxis("Mouse X") * Time.deltaTime * RotSpeed, 0, 0);
                    else if (MoveObj == 2)
                        SelectRoomObject.transform.Rotate(0, Input.GetAxis("Mouse X") * Time.deltaTime * RotSpeed, 0);
                    else if (MoveObj == 3)
                        SelectRoomObject.transform.Rotate(0, 0, Input.GetAxis("Mouse X") * Time.deltaTime * RotSpeed);

                    panel.transform.Find("InputX").GetComponent<InputField>().text = SelectRoomObject.transform.rotation.x.ToString();
                    panel.transform.Find("InputY").GetComponent<InputField>().text = SelectRoomObject.transform.rotation.y.ToString();
                    panel.transform.Find("InputZ").GetComponent<InputField>().text = SelectRoomObject.transform.rotation.z.ToString();
               break;
                case 2:
                    float scaleC = 1.0f;

                    if (Input.GetAxis("Mouse X") < 0)
                        scaleC = 0.9f;
                    else if (Input.GetAxis("Mouse X") > 0)
                        scaleC = 1.1f;

                    var mesh = SelectRoomObject.GetComponentInChildren<MeshFilter>();

                    if (MoveObj == 1)
                        mesh.transform.localScale = new Vector3(mesh.transform.localScale.x * scaleC, mesh.transform.localScale.y, mesh.transform.localScale.z);
                    else if (MoveObj == 2)
                        mesh.transform.localScale = new Vector3(mesh.transform.localScale.x, mesh.transform.localScale.y * scaleC , mesh.transform.localScale.z);
                    else if (MoveObj == 3)
                        mesh.transform.localScale = new Vector3(mesh.transform.localScale.x, mesh.transform.localScale.y, mesh.transform.localScale.z * scaleC );

                    panel.transform.Find("InputX").GetComponent<InputField>().text = SelectRoomObject.transform.localScale.x.ToString();
                    panel.transform.Find("InputY").GetComponent<InputField>().text = SelectRoomObject.transform.localScale.y.ToString();
                    panel.transform.Find("InputZ").GetComponent<InputField>().text = SelectRoomObject.transform.localScale.z.ToString();
                break;
            }
        }
    }

}
