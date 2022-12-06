using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ControllerInput : MonoBehaviour
{
    public static ControllerInput Instance;

    public Animator btnJump, btnSlide;

    private void Awake()
    {
        Instance = this;
        btnJump.enabled = false;
        btnSlide.enabled = false;
    }

    [ReadOnly] public bool allowJump = true;
    [ReadOnly] public bool allowSlide = true;

    public void SetJumpButton(bool active, bool allowWork)
    {
        btnJump.enabled = active;
        allowJump = allowWork;
        if (!active)
            btnJump.GetComponent<Image>().color = new Color(1, 1, 1, 0);
    }

    public void SetSlideButton(bool active, bool allowWork)
    {
        btnSlide.enabled = active;
        allowSlide = allowWork;
        if (!active)
            btnSlide.GetComponent<Image>().color = new Color(1, 1, 1, 0);
    }

    public void Jump()
    {
        if (allowJump)
            GameManager.Instance.Player.Jump();
    }

    public void SlideOn()
    {
        if (allowSlide)
            GameManager.Instance.Player.SlideOn();
    }
}
