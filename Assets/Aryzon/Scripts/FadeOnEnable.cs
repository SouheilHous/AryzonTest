using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class FadeOnEnable : MonoBehaviour {

	private CanvasGroup cGroup;
    public float delay = 0f;
	public float fadeTime = 0.5f;
	public bool fading = false;

	// Use this for initialization
	void Awake () {
		cGroup = GetComponent<CanvasGroup> ();
		cGroup.alpha = 0f;
	}
	

	private void OnEnable()
	{
		StartCoroutine (fadeToAlpha(1f));
	}

	private void OnDisable()
	{
		cGroup.alpha = 0f;
	}

	IEnumerator fadeToAlpha (float newAlpha) {
		fading = true;
		yield return new WaitForSeconds(delay);
		float startAlpha = cGroup.alpha;
		float timer = 0f;

		while (timer <= fadeTime && fading) {
			cGroup.alpha = Mathf.Lerp (startAlpha, newAlpha, timer / fadeTime);
			timer += Time.deltaTime;
			yield return null;
		}
		if (fading)
		{
			cGroup.alpha = newAlpha;
		}
		fading = false;
	}
}
