using UnityEngine;
using UnityEngine.UI;

public class ScrollbarScript : MonoBehaviour
{
    public Scrollbar scrollbar;

    // Start is called before the first frame update
    void Start()
    {
        scrollbar = GetComponent<Scrollbar>();
        if (scrollbar != null)
        {
            scrollbar.value = 0;
        }
    }
}
