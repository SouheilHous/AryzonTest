using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aryzon
{
    public static class SmoothValue
    {
        private static int pointCount = 240;
        private static bool initialized = false;
        private static float[] smoothInValues = new float[pointCount];
        private static float[] smoothOutValues = new float[pointCount];
        private static float[] smoothInOutValues = new float[pointCount];
        private static float[] overshootValues = new float[pointCount];

        //Returned smooth value is in range given by user
        public static float GetSmoothValueForRange(float value, float min, float max, AnimationType type)
        {
            float delta = max - min;

            float nValue = GetNormalizedSmoothValue((value - min) / delta, type);
            return nValue * delta + min;
        }

        //Returned smooth value depends on the timer value. Range is 0 - timeToTake;
        public static float GetSmoothValueForRange(float min, float max, float timer, float timeToTake, AnimationType type)
        {
            float delta = max - min;
            float nValue = GetNormalizedSmoothValue(timer / timeToTake, type);

            return nValue * delta + min;
        }

        //Returned smooth value depends on the timer value. Range is 0 - timeToTake;
        public static Vector2 GetSmoothValueForRange(Vector2 min, Vector2 max, float timer, float timeToTake, AnimationType type)
        {
            Vector2 delta = max - min;
            float nValue = GetNormalizedSmoothValue(timer / timeToTake, type);

            return nValue * delta + min;
        }

        //Returned smooth value depends on the timer value. Range is 0 - timeToTake;
        public static Vector3 GetSmoothValueForRange(Vector3 min, Vector3 max, float timer, float timeToTake, AnimationType type)
        {
            Vector3 delta = max - min;
            float nValue = GetNormalizedSmoothValue(timer / timeToTake, type);

            return nValue * delta + min;
        }

        //Returned smooth value is in range from 0 to 1
        public static float GetNormalizedSmoothValue(float value, AnimationType type)
        {
            int index = Mathf.RoundToInt(Mathf.Clamp01(value) * (pointCount - 1));

            if (!initialized)
            {
                Initialize();
            }

            return SmoothValuesForType(type)[index];
        }

        private static void Initialize()
        {
            float timeToTake = (float)pointCount;
            for (int i = 0; i < timeToTake; i++)
            {
                float x = i / timeToTake;

                smoothInOutValues[i] = 0.5f - Mathf.Cos(Mathf.PI*x) /2f;
                smoothInValues[i] = (i / timeToTake) * (i / timeToTake);
                smoothOutValues[i] = 1 - (i - timeToTake) * (i - timeToTake) / (timeToTake * timeToTake);

                if (x < 0.62f)
                {
                    overshootValues[i] = 0.7f - Mathf.Cos(1.62f * Mathf.PI * x) / 1.43f;
                }
                else
                {
                    overshootValues[i] = 1.2f + Mathf.Cos(2.6f * Mathf.PI * x + 0.4f * Mathf.PI) / 5f;
                }
            }

            initialized = true;
        }

        private static float[] SmoothValuesForType(AnimationType type)
        {
            if (type == AnimationType.SmoothIn)
            {
                return smoothInValues;
            } else if (type == AnimationType.SmoothOut)
            {
                return smoothOutValues;
            } else if (type == AnimationType.Overshoot)
            {
                return overshootValues;
            }
            return smoothInOutValues;
        }

        public enum AnimationType
        {
            SmoothIn,
            SmoothOut,
            SmoothInOut,
            Overshoot
        }
    }
}