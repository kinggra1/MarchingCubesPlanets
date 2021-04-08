using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class BillboardOrient : MonoBehaviour
{
    public Camera targetCamera; 
    // Start is called before the first frame update
    void Start()
    {
        targetCamera = targetCamera ? targetCamera : Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.rotation = targetCamera.transform.rotation;
    }
}
