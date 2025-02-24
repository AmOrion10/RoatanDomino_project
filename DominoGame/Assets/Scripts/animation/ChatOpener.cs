using UnityEngine;

public class ChatOpener : MonoBehaviour
{
    public GameObject ChatArea;

    public void OpenChat()
    {
        if (ChatArea != null)
        {
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                bool isOpen = animator.GetBool("open");
                animator.SetBool("open", !isOpen);
            }
        }
    }
}
