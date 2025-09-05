using UnityEngine;

[ExecuteAlways]
public class RectFollower : MonoBehaviour
{
    [SerializeField] private RectTransform target;

    private RectTransform rect;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void Update()
    {
        rect.sizeDelta = target.rect.size;
        rect.position = target.position;
    }
}