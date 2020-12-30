using System;

using UnityEngine;

namespace Aryzon
{
    public class PosePredict
    {
        private int _count = 10;

        public int count
        {
            get { return _count; }
            set { 
                if (_count != value)
                {
                    _count = value;
                    Setup();
                }
            }
        }

        private int initCount = 0;

        private float[] x;
        private float[] v;
        private float[] coefficients;

        public PosePredict(int smoothCount)
        {
            _count = smoothCount;
            Setup();
        }

        public PosePredict()
        {
            Setup();
        }

        private void Setup()
        {
            coefficients = DerivativeCoefficients.Coefficients(_count);
            initCount = 0;
            x = new float[_count];
            v = new float[_count];
            for (int i = 0; i < _count; i++)
            {
                x[i] = 0f;
                v[i] = 0f;
            }
        }

        public float NextValue(float value, int predictSteps)
        {
            if (initCount < _count || predictSteps == 0)
            {
                UpdateArray(value);
                initCount++;
            }
            else
            {
                value += UpdateArrayAndCalculate(value, predictSteps);
            }

            return value;
        }

        private float UpdateArrayAndCalculate(float value, int t)
        {
            float vSum = 0f;
            //float aSum = 0f;

            int i = 0;
            for (i = 0; i < _count - 1; i++)
            {
                float curX = x[i + 1];
                float curV = v[i + 1];

                x[i] = curX;
                v[i] = curV;

                float c = coefficients[_count - i - 1];
                vSum += curX * c;
                //aSum += curV * c;
            }
            //i++;

            x[i] = value;
            vSum += value * coefficients[0];

            v[i] = vSum;
            //aSum += vSum * coefficients[0];

            return vSum * t;// + 0.5f*t*t*aSum;
        }

        private void UpdateArray(float value)
        {
            float vSum = 0f;

            int i = 0;
            for (i = 0; i < _count - 1; i++)
            {
                float curX = x[i + 1];
                float curV = v[i + 1];

                x[i] = curX;
                v[i] = curV;

                vSum += curX * coefficients[_count - i - 1];
            }
            //i++;

            x[i] = value;
            vSum += value * coefficients[0];

            v[i] = vSum;
        }
    }

    internal static class DerivativeCoefficients
    {
        internal static float[] Coefficients(int count)
        {
            if (count > 13)
            {
                count = 13;
            }
            if (count < 2)
            {
                count = 2;
            }

            switch(count) {
                case 2:
                    return new float[] { 1f, -1f };
                case 3:
                    return new float[] { 0.5f, 0f, -0.5f };
                case 4:
                    return new float[] { 0.3f, 0.1f, -0.1f, -0.3f };
                case 5:
                    return new float[] { 0.2f, 0.1f, 0f, -0.1f, -0.2f };
                case 6:
                    return new float[] { 0.14286f, 0.08571f, 0.02857f, -0.02857f, -0.08571f, -0.14286f };
                case 7:
                    return new float[] { 0.10714f, 0.07143f, 0.03571f, 0f, -0.03571f, -0.07143f, -0.10714f };
                case 8:
                    return new float[] { 0.08333f, 0.05952f, 0.03571f, 0.0119f, -0.0119f, -0.03571f, -0.05952f, -0.08333f };
                case 9:
                    return new float[] { 0.06667f, 0.05f, 0.03333f, 0.01667f, 0f, -0.01667f, -0.03333f, -0.05f, -0.06667f };
                case 10:
                    return new float[] { 0.05455f, 0.04242f, 0.0303f, 0.01818f, 0.00606f, -0.00606f, -0.01818f, -0.0303f, -0.04242f, -0.05455f };
                case 11:
                    return new float[] { 0.04545f, 0.03636f, 0.02727f, 0.01818f, 0.00909f, 0f, -0.00909f, -0.01818f, -0.02727f, -0.03636f, -0.04545f };
                case 12:
                    return new float[] { 0.03846f, 0.03147f, 0.02448f, 0.01748f, 0.01049f, 0.0035f, -0.0035f, -0.01049f, -0.01748f, -0.02448f, -0.03147f, -0.03846f };
                case 13:
                    return new float[] { 0.03297f, 0.02747f, 0.02198f, 0.01648f, 0.01099f, 0.00549f, 0f, -0.00549f, -0.01099f, -0.01648f, -0.02198f, -0.02747f, -0.03297f };
                default:
                    Debug.LogError("[Aryzon] Incorrect prediction settings");
                    break;
            }
            return null;
        }
    }
}
