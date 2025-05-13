using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MalbersAnimations.Events;

// Reveals dialogue text on a text box by dilating the characters from thin to thick,
// making text appear as if it is being carved into a surface.
// This is used with the wooden and stone 3D dialogue boxes with glowing inlain
// lettering for a mystic aesthetic.

public class TextRevealScript : MonoBehaviour {
	[SerializeField] bool m_startCompleted = false;
	[SerializeField] TextMeshPro m_textInstance;
	[SerializeField] float m_characterFadeInTime = 1.0f;
	[SerializeField] float m_dilateFadeInTime = 1.0f;

	Material m_textMaterialInstance;
	float m_baseDilation;
	int m_cachedStringLength = 0;

	float m_timer = 0.0f;
	bool m_reverse = false;

	TextMeshPro m_textInstanceNext;

	[SerializeField] UnityEventRaiser m_fadeOutCompleteEvent;

	private void Awake() {

	}

	void Start() {
		InitCurrent();

		if (m_startCompleted) {
			m_textInstance.maxVisibleCharacters = m_cachedStringLength;
			m_textMaterialInstance.SetFloat("_FaceDilate", m_baseDilation);
			m_timer = float.MaxValue;
			m_reverse = true;
		}

		// Disable self, await event to turn us back on
		enabled = false;
	}

	void InitCurrent() {
		// Get references here
		m_textMaterialInstance = m_textInstance.fontMaterial;

		// Init stuff here
		m_baseDilation = m_textMaterialInstance.GetFloat("_FaceDilate");
		m_cachedStringLength = m_textInstance.text.Length;

		m_textInstance.maxVisibleCharacters = 0;
		m_textMaterialInstance.SetFloat("_FaceDilate", 0.0f);
	}

	// Update is called once per frame
	void Update() {
		float normalisedCharFadeTime = m_characterFadeInTime > 0.0f ? Mathf.Clamp01(m_timer / m_characterFadeInTime) : 1.0f;
		float normalisedDilateFadeTime = m_dilateFadeInTime > 0.0f ? Mathf.Clamp01(m_timer / m_dilateFadeInTime) : 1.0f;

		float dilateValue = Mathf.Lerp(0.0f, m_baseDilation, normalisedDilateFadeTime);
		int charCount = Mathf.RoundToInt( Mathf.Lerp(0.0f, (float)m_cachedStringLength, normalisedCharFadeTime) );

		m_textInstance.maxVisibleCharacters = charCount;
		m_textMaterialInstance.SetFloat("_FaceDilate", dilateValue);

		if (m_reverse) {
			m_timer -= Time.deltaTime;
			if (m_timer < 0.0f) {
				m_fadeOutCompleteEvent.enabled = true;
				GoToNext();
			}

		} else {
			m_timer += Time.deltaTime;

			if (m_timer > Mathf.Max(m_dilateFadeInTime, m_characterFadeInTime)) {
				enabled = false;
			}
		}
	}

	public void FadeTextIn() {
		m_timer = 0.0f;
	}

	public void FadeToNextText(GameObject nextText) {
		// Start fading out the current text
		m_timer = Mathf.Max(m_dilateFadeInTime, m_characterFadeInTime);
		m_reverse = true;

		m_textInstanceNext = nextText.GetComponent<TextMeshPro>();
	}

	void GoToNext() {
		m_textInstance.gameObject.SetActive(false);
		
		if (m_textInstanceNext) {
			m_textInstanceNext.gameObject.SetActive(true);
			m_textInstance = m_textInstanceNext;
			m_textInstanceNext = null;
			m_reverse = false;
			m_timer = 0.0f;

			InitCurrent();
		}

		// Await event
		enabled = false;
	}
}
