using UnityEngine;

public class HandExtension : MonoBehaviour
{
    public GameObject ChatArea;

    public void ExtendHand()
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
