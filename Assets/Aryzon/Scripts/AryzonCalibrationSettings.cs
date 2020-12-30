using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace Aryzon {
	public class AryzonCalibrationSettings : MonoBehaviour {

		public InputField screenWidthInputField;
		public InputField horizontalShiftInputField;
		public InputField verticalShiftInputField;
		public InputField IPDInputField;

		public GameObject screenWidthReset;
		public GameObject horizontalShiftReset;
		public GameObject verticalShitReset;
		public GameObject IPDReset;

		public Text calibrationCodeText;
		public Text statusText;
		public UnityEvent OnSuccess;
		private SettingsRetrievedEventHandler settingsRetrieved;

		private bool changeCameFromSelf = false;

		public void Paste () {
			settingsRetrieved = SettingsRetrieved;
			AryzonSettings.Instance.SettingsRetrieved += settingsRetrieved;
			statusText.text = "";
			string id = UniClipboard.GetText ();
			id = id.Replace (System.Environment.NewLine, "");
			calibrationCodeText.text = id;
			AryzonSettings.Instance.RetrieveSettingsForCode (id);
			statusText.text = "Loading your personal settings..";
		}

        private void Start()
        {
			SetValues();
		}

        private void SettingsRetrieved (string status, bool success) {
			
			statusText.text = status;
			if (!success) {
				calibrationCodeText.text = "Tap to paste code";
			} else {
				OnSuccess.Invoke ();
				SetValues();
			}
			AryzonSettings.Instance.SettingsRetrieved -= settingsRetrieved;
		}

		private void SetValues()
        {
			changeCameFromSelf = true;
			screenWidthInputField.text = "" + (int)(AryzonSettings.Phone.ScreenWidth * 1000f);
			horizontalShiftInputField.text = "" + (int)(AryzonSettings.Calibration.XShift * 1000f);
			verticalShiftInputField.text = "" + (int)(AryzonSettings.Calibration.YShift * 1000f);
			IPDInputField.text = "" + (int)(AryzonSettings.Calibration.IPD * 1000f);
			changeCameFromSelf = false;

			screenWidthReset.SetActive(AryzonSettings.Phone.manualScreenWidth);
			horizontalShiftReset.SetActive(AryzonSettings.Calibration.manualxShift);
			verticalShitReset.SetActive(AryzonSettings.Calibration.manualyShift);
			IPDReset.SetActive(AryzonSettings.Calibration.manualIPD);
		}

		public void ScreenWidthInputFieldChanged()
        {
			if (changeCameFromSelf)
            {
				return;
            }

			if (!string.IsNullOrWhiteSpace(screenWidthInputField.text))
            {
				int value = Convert.ToInt32(screenWidthInputField.text);
				if (value > 0 && value < 300)
                {
					AryzonSettings.Phone.ScreenWidth = ((float)value) / 1000f;
					AryzonSettings.Instance.Apply();
				}
			} else
            {
				changeCameFromSelf = true;
				screenWidthInputField.text = "" + (int)(AryzonSettings.Phone.ScreenWidth * 1000f);
				changeCameFromSelf = false;
			}
			screenWidthReset.SetActive(AryzonSettings.Phone.manualScreenWidth);
		}

		public void HorizontalShiftInputFieldChanged()
		{
			if (changeCameFromSelf)
			{
				return;
			}
			if (!string.IsNullOrWhiteSpace(horizontalShiftInputField.text))
			{
				try
				{
					int value = Convert.ToInt32(horizontalShiftInputField.text);
					if (value > -100 && value < 100)
					{
						AryzonSettings.Calibration.XShift = ((float)value) / 1000f;
						AryzonSettings.Instance.Apply();
					}
				} catch
                {

                }
			} else
            {
				changeCameFromSelf = true;
				horizontalShiftInputField.text = "" + (int)(AryzonSettings.Calibration.XShift * 1000f);
				changeCameFromSelf = false;
			}
			horizontalShiftReset.SetActive(AryzonSettings.Calibration.manualxShift);
		}

		public void VerticalShiftInputFieldChanged()
		{
			if (changeCameFromSelf)
			{
				return;
			}
			if (!string.IsNullOrWhiteSpace(verticalShiftInputField.text))
			{
				try
				{
					int value = Convert.ToInt32(verticalShiftInputField.text);
					if (value > -200 && value < 200)
					{
						AryzonSettings.Calibration.YShift = ((float)value) / 1000f;
						AryzonSettings.Instance.Apply();
					}
				} catch
                {

                }
			}
			else
			{
				changeCameFromSelf = true;
				verticalShiftInputField.text = "" + (int)(AryzonSettings.Calibration.YShift* 1000f);
				changeCameFromSelf = false;
			}
			verticalShitReset.SetActive(AryzonSettings.Calibration.manualyShift);
		}

		public void IPDInputFieldChanged()
		{
			if (changeCameFromSelf)
			{
				return;
			}
			if (!string.IsNullOrWhiteSpace(IPDInputField.text))
			{
				int value = Convert.ToInt32(IPDInputField.text);
				if (value > 10 && value < 200)
				{
					AryzonSettings.Calibration.IPD = ((float)value) / 1000f;
					AryzonSettings.Instance.Apply();
				}
			}
			else
			{
				changeCameFromSelf = true;
				IPDInputField.text = "" + (int)(AryzonSettings.Calibration.IPD * 1000f);
				changeCameFromSelf = false;
			}
			IPDReset.SetActive(AryzonSettings.Calibration.manualIPD);
		}

		public void ResetScreenWidth()
        {
			AryzonSettings.Phone.manualScreenWidth = false;
			SetValues();
		}

		public void ResetHorizontalShift()
		{
			AryzonSettings.Calibration.manualxShift = false;
			SetValues();
		}

		public void ResetVerticalShift()
		{
			AryzonSettings.Calibration.manualyShift = false;
			SetValues();
		}

		public void ResetIPD()
		{
			AryzonSettings.Calibration.manualIPD = false;
			SetValues();
		}
	}
}