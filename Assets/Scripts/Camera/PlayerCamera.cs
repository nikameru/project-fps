using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public float sensitivityX, sensitivityY;
    public Transform playerOrientation;

    // Container that holds the camera along with the other things linked to it
    public GameObject container;

    private float _xRotation, _yRotation;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        float mouseInputX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * sensitivityX;
        float mouseInputY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * sensitivityY;

        _xRotation -= mouseInputY;
        _yRotation += mouseInputX;

        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

        // Rotate the camera container (along with the weapon, etc.)
        container.transform.rotation = Quaternion.Euler(_xRotation, _yRotation, 0);
        // Change player orientation
        playerOrientation.rotation = Quaternion.Euler(0, _yRotation, 0);
    }
}