using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ARYZON_VUFORIA
using Vuforia;
#endif

public class InitVuforia : MonoBehaviour
{
#if ARYZON_VUFORIA
    private void Awake()
    {
        VuforiaRuntime.Instance.InitVuforia();
    }
#endif
}
