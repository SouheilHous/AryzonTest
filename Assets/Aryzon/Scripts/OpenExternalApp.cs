using System.Collections;
using System.Collections.Generic;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif
using UnityEngine;

public class OpenExternalApp : MonoBehaviour {
#if UNITY_IOS
	[DllImport ("__Internal")]
	private static extern void callApp ();
#endif
	public void OpenApp () {
#if UNITY_ANDROID
		if (Application.platform == RuntimePlatform.Android) {
			Application.OpenURL("http://play.google.com/store/apps/details?id=com.Aryzon.AryzonDemo");
		}
#elif UNITY_IOS
		if (Application.platform == RuntimePlatform.IPhonePlayer) {
			callApp ();
		}
#endif
	}
}
