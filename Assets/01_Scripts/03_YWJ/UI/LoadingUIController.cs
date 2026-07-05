using UnityEngine;

public class LoadingUIController : MonoBehaviour
{
    [Header("Loading Icon")]
    [SerializeField] private GameObject _loadingIcon;

    [Header("Rotation Settings")]
    [SerializeField] private float rotateSpeed = 180f;

    private void Update()
    {
        _loadingIcon.transform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
    }
}
