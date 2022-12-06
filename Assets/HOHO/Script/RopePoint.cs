using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RopePoint : MonoBehaviour
{
    public GameObject idleObj, activeObj;

    [Header("Active Slow motion to guide player")]
    public bool slowMotion = false;
    public GameObject activeHelperObj;
    public GameObject showReleaseTutObj;

    private void Start()
    {
        if (activeHelperObj)
            activeHelperObj.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance.Player.currentAvailableRope != null && GameManager.Instance.Player.currentAvailableRope.gameObject == gameObject)
        {
            idleObj.SetActive(false);
            activeObj.SetActive(true);

            if (slowMotion)
            {
                if (activeHelperObj)
                    activeHelperObj.SetActive(true);

                if (showReleaseTutObj && !showReleaseTutObj.activeInHierarchy)
                    if (GameManager.Instance.Player.isGrabingRope && GameManager.Instance.Player.transform.position.x > transform.position.x && GameManager.Instance.Player.transform.position.y > (transform.position.y - (Vector2.Distance(transform.position, GameManager.Instance.Player.transform.position) * 0.5f)))
                    {
                        showReleaseTutObj.SetActive(true);
                        Time.timeScale = 0.1f;
                    }
            }
        }
        else
        {
            idleObj.SetActive(true);
            activeObj.SetActive(false);

            if (slowMotion)
            {
                if (activeHelperObj)
                    activeHelperObj.SetActive(false);
                if (showReleaseTutObj)
                    showReleaseTutObj.SetActive(false);
            }
        }
    }
}
