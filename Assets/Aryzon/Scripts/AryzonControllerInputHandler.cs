using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Aryzon
{
    public class AryzonControllerInputHandler : MonoBehaviour
    {
        public UnityEvent OnInputDown;
        public UnityEvent OnInputUp;

        public void InputDown()
        {

            OnInputDown.Invoke();
        }

        public void InputUp()
        {
            OnInputUp.Invoke();
        }

        void Update()
        {
            bool didSetDown = false;
            bool didSetUp = false;
#if UNITY_EDITOR

            if (Input.GetMouseButtonDown(0))
            {
                InputDown();
                didSetDown = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                InputUp();
                didSetUp = true;
            }

#endif
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == UnityEngine.TouchPhase.Began && !didSetDown)
                {
                    InputDown();
                    didSetDown = true;
                }
                else if ((touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled) && !didSetUp)
                {
                    InputUp();
                    didSetUp = true;
                }
            }
        }
    }
}