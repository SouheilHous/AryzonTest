using UnityEngine;

namespace Aryzon
{
    public class RotateWithCamera : MonoBehaviour
    {
        public Transform objectToFollow;
        public bool rotate = true;
        private bool rotating = false;

        [HideInInspector]
        public float _maxRotationY = 180f;
        [HideInInspector]
        public float _deltaRotationY = 360f;
        public float _minimumAngleY = 60f;
        [HideInInspector]
        public float _maxRotationX = 180f;
        [HideInInspector]
        public float _deltaRotationX = 360f;
        public float _minimumAngleX = 5f;

        [Tooltip("Angular speed in radians per second.")]
        public float speed;

        void Update()
        {
            if (rotate)
            {
                float fromRotation = gameObject.transform.rotation.eulerAngles.y;
                float toRotation = objectToFollow.rotation.eulerAngles.y;
                float deltaRotation = toRotation - fromRotation;

                if (deltaRotation > _maxRotationY)
                {
                    deltaRotation -= _deltaRotationY;
                }
                else if (deltaRotation < -_maxRotationY)
                {
                    deltaRotation += _deltaRotationY;
                }

                float minAngle = _minimumAngleY;

                if (Mathf.Abs(deltaRotation) > minAngle || rotating)
                {
                    rotating = true;
                    float newRotation = fromRotation + deltaRotation / 20f;
                    gameObject.transform.rotation = Quaternion.Euler(new Vector3(gameObject.transform.rotation.eulerAngles.x, newRotation, gameObject.transform.rotation.eulerAngles.z));
                    if (Mathf.Abs(deltaRotation) < 1f)
                    {
                        rotating = false;
                    }
                }
                gameObject.transform.position = objectToFollow.position;
            }
        }
    }
}