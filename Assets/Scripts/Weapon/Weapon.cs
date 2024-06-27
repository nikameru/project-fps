using UnityEngine;

public class Weapon : MonoBehaviour
{
    public Camera playerCamera;
    public Transform projectileSpawn;

    public GameObject projectileObject;
    public float shootDirectionForce, shootUpwardForce;

    public int maxAmmo;
    public float delayBetweenShots, shootingDelay;
    public bool isAutomatic;

    private int _ammoUsed, _ammoLeft;

    private bool _isShootingRequested, _isAbleToShoot;

    private void Start()
    {
        _ammoLeft = maxAmmo;
        _isAbleToShoot = true;
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (isAutomatic)
        {
            _isShootingRequested = Input.GetKey(KeyCode.Mouse0);
        }
        else
        {
            _isShootingRequested = Input.GetKeyDown(KeyCode.Mouse0);
        }

        if (_isShootingRequested && _isAbleToShoot && _ammoLeft > 0)
        {
            _ammoUsed = 0;

            Shoot();
        }
    }

    private void Shoot()
    {
        _isAbleToShoot = false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;
        Vector3 target;

        if (Physics.Raycast(ray, out hit))
        {
            target = hit.point;
        }
        else
        {
            target = ray.GetPoint(100f);
        }

        Vector3 rawDirection = target - projectileSpawn.transform.position;
        GameObject projectile = Instantiate(
            projectileObject, projectileSpawn.transform.position, Quaternion.identity);

        projectile.transform.forward = rawDirection.normalized;

        Vector3 calculatedShootForce = rawDirection.normalized * shootDirectionForce + 
                                       playerCamera.transform.up * shootUpwardForce;

        projectile.GetComponent<Rigidbody>().AddForce(calculatedShootForce, ForceMode.Impulse);

        _ammoLeft--;
        _ammoUsed++;

        Invoke(nameof(RestoreShot), delayBetweenShots);
    }

    private void RestoreShot()
    {
        _isAbleToShoot = true;
    }
}
