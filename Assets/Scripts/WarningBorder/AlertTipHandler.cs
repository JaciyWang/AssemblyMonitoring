using Microsoft.MixedReality.Toolkit.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlertTipHandler : MonoBehaviour
{
    //public GameObject alertTip;

    // Start is called before the first frame update
    void Start()
    {
        gameObject.SetActive(false);
    }

    float m_timeCounter = 0.0f;
    public float m_refreshTime = 3f;

    // Update is called once per frame
    void Update()
    {
        
        /*if (gameObject.activeSelf && m_timeCounter < m_refreshTime)
        {
            m_timeCounter += Time.deltaTime;
        }
        else
        {
            gameObject.SetActive(false);
            m_timeCounter = 0.0f;
        }*/
    }

    public void SetAlertTip(GameObject alertObject)
    {

        gameObject.transform.position = alertObject.transform.position;
        /*gameObject.transform.SetParent(alertObject.transform, false);
        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localScale = new Vector3(3,3,3);*/
        gameObject.GetComponent<ToolTipConnector>().Target = alertObject;
        //gameObject.SetActive(true);
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

}
