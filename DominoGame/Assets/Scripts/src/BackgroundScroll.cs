using UnityEngine;
using UnityEngine.UI;

public class BackgroundScroll : MonoBehaviour
{
    [SerializeField] private int SpeedDivider = 5;
    [SerializeField] private RawImage img;
    [SerializeField] private float x, y;

    // Update is called once per frame
    void Update()
    {
        img.uvRect = new Rect(img.uvRect.position + new Vector2(x, y) * Time.deltaTime / SpeedDivider, img.uvRect.size);
    }
}
