//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    Rigidbody rb = null;
    Vector3 moveDir = Vector3.zero;

    [SerializeField]
    float moveSpeed = 0f;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        moveDir = new Vector3(Input.GetAxis("Horizontal"), 0.0f, Input.GetAxis("Vertical")).normalized;
    }

    private void FixedUpdate()
    {
        Debug.Log(moveDir);
        //rb.AddForce(moveDir, ForceMode.VelocityChange);
        rb.velocity = moveDir * moveSpeed;
    }
}
