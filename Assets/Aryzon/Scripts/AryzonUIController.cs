using Aryzon;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AryzonUIController : MonoBehaviour {

	public GameObject main;
	public GameObject calibration;
	
	public GameObject settings;

	public string OnBackLoadScene;

    private List<int> ints = new List<int>();
	private bool didInit = false;

	private void Init () {
		if (!didInit)
		{
			didInit = true;
			ints.Clear();
			ints.Add(-1);			
		}
	}

    private void OnDisable()
    {
		Deinit();
	}

    private void Deinit()
	{
		didInit = false;
	}

	public void SetMain () {
		Init();
		main.SetActive (true);
		calibration.SetActive (false);
		settings.SetActive(false);
		//firstTime.SetActive (false);
		ints.Add (0);
	}

	public void SetCalibration () {
		Init();
		main.SetActive (false);
		calibration.SetActive (true);
		settings.SetActive(false);
		//firstTime.SetActive (false);
		ints.Add (1);
	}

	public void SetFirstTime () {
		Init();
		main.SetActive (false);
		calibration.SetActive (false);
		settings.SetActive(false);
		ints.Add (2);
	}

	public void SetSettings()
	{
		Init();
		settings.SetActive(true);
		main.SetActive(false);
		calibration.SetActive(false);
		ints.Add(3);
	}

	public void Inactivate () {
        main.SetActive (false);
        calibration.SetActive (false);
		settings.SetActive(false);
		Deinit();
	}

    public void Activate () {
		SetMain();
	}

	public void SetMainAfter (float seconds) {
		StartCoroutine (SetMainAfterEnumerator (seconds));
	}

	IEnumerator SetMainAfterEnumerator (float seconds) {
		yield return new WaitForSeconds (seconds);
		SetMain ();
	}

	public void SetSkipCalibration () {
		AryzonSettings.Calibration.skipCalibrate = true;
	}

	public void BackButtonPress () {
		int currentScreen = ints [ints.Count - 1];
		int previousScreen = ints[ints.Count-2];
		ints.RemoveAt (ints.Count-1);
		if (ints.Count >= 1) {
			ints.RemoveAt (ints.Count -1);
		}
		if (previousScreen == -1 || currentScreen == 0) {
			Screen.autorotateToLandscapeRight = false;
			Screen.autorotateToPortraitUpsideDown = false;
			Screen.autorotateToLandscapeLeft = false;
			Screen.orientation = ScreenOrientation.Portrait;

			if (OnBackLoadScene == "" || OnBackLoadScene == null) {
                Init();
                AryzonSettings.Instance.aryzonManager.StopAryzonMode();
			} else {
				SceneManager.LoadSceneAsync(OnBackLoadScene);
			}
		} else if (previousScreen == 0) {
			SetMain ();
		} else if (previousScreen == 1) {
			SetCalibration ();
		} else if (previousScreen == 2) {
			SetFirstTime ();
		} else if (previousScreen == 3)
		{
			SetSettings();
		}
	}

	void Update () {
		if (Input.GetKey (KeyCode.Escape)) {
			BackButtonPress ();
			return;
		}
	}
}
