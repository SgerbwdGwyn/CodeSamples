using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MalbersAnimations.Events;

// Reveals dialogue text on a text box by fading characters from transparent to opaque,
// making text appear as if it is magically coming into being on the text box surface.

public class TextFader : MonoBehaviour {
	[HideInInspector] public TMP_Text m_textComponent;
	[SerializeField] float m_fadeSpeed_FadeIn = 1.0f;
	[SerializeField] int m_characterSpread_FadeIn = 4;
	[SerializeField] float m_fadeSpeed_FadeOut = 1.0f;
	[SerializeField] int m_characterSpread_FadeOut = 4;
	[SerializeField] float m_fadeSpeed_SkipText = 10.0f;

	float m_fadeSpeed { get { return m_skip ? m_fadeSpeed_SkipText : (m_reverse ? m_fadeSpeed_FadeOut : m_fadeSpeed_FadeIn); } }
	float m_characterSpread { get { return m_reverse ? m_characterSpread_FadeOut : m_characterSpread_FadeIn; } }

	float fadeTime = 0.0f;
	int m_charMaxIndex = 0;
	bool m_reverse = false;
	[HideInInspector] public bool m_skip = false;

	int m_prevStartIdx = -1;
	int m_prevEndIdx = -1;

	Color32[] newVertexColors;
	byte[] m_newAlphas;

	TMP_TextInfo m_textInfo;

	[SerializeField] UnityEventRaiser m_fadeinCompletedEvent;
	[SerializeField] UnityEventRaiser m_fadeoutCompletedEvent;

	public void OnTextChanged(bool forceFadedIn = false) {
		m_textInfo = m_textComponent.textInfo;
		m_newAlphas = new byte[m_textInfo.characterCount];
		m_charMaxIndex = m_textInfo.characterCount - 1;

		// Hide text
		if (forceFadedIn) {
			m_textComponent.color = new Color(m_textComponent.color.r, m_textComponent.color.g, m_textComponent.color.b, 1.0f);

			fadeTime = 1.0f;
		} else {
			m_textComponent.color = new Color(m_textComponent.color.r, m_textComponent.color.g, m_textComponent.color.b, 0);
		}

		int materialIndex = m_textInfo.characterInfo[0].materialReferenceIndex;
		m_textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
		newVertexColors = m_textInfo.meshInfo[materialIndex].colors32;
	}

	private void Start() {
		fadeTime = 1.0f;
		enabled = false;
		m_skip = false;
	}

	public void FadeIn() {
		fadeTime = 0.0f;
		m_reverse = false;
		enabled = true;
		m_skip = false;
	}

	public void FadeOut() {
		m_reverse = true;
		enabled = true;
		m_skip = false;
	}

	public void SkipText() {
		m_skip = true;
	}

	private void Update() {
		fadeTime += ((Time.deltaTime / Mathf.Max(m_charMaxIndex + 1, 1)) * m_fadeSpeed) * (m_reverse ? -1.0f : 1.0f);

		float T = Mathf.Clamp01(fadeTime);

		FadeCharacters(T);

		// Be aware: enabling the event first causes FadeIn/FadeOut to be called before this method's code is finished running.
		if (!m_reverse && fadeTime > 1.0f) {
			// FIRST, disable the component.
			enabled = false;
			m_skip = false;

			// LAST, send the event. 
			m_fadeinCompletedEvent.enabled = true;
		} else if (m_reverse && fadeTime < 0.0f) {
			enabled = false;
			m_skip = false;
			m_fadeoutCompletedEvent.enabled = true;
		}
	}

	void FadeCharacters(float T) {
		if (m_textInfo == null) return;

		// Use the previous start/end indices (if set)
		// This avoids skipping characters before they're fully faded in or out
		int startIdx, endIdx;

		if (m_prevStartIdx > 0) {
			startIdx = m_prevStartIdx;
			m_prevStartIdx = GetStartIndex(T);
		} else {
			m_prevStartIdx = startIdx = GetStartIndex(T);
		}

		if (m_prevEndIdx > 0) {
			endIdx = m_prevEndIdx;
			m_prevEndIdx = GetEndIndex(T);
		} else {
			m_prevEndIdx = endIdx = GetEndIndex(T);
		}

		for (int i = 0; i <= m_charMaxIndex; ++i) {
			// Skip characters that are not visible
			if (!m_textInfo.characterInfo[i].isVisible) continue;

			// Get the index of the first vertex used by this text element.
			int vertexIndex = m_textInfo.characterInfo[i].vertexIndex;

			if (i < startIdx) {
				m_newAlphas[i] = (byte)255;
			} else if (i > endIdx) {
				m_newAlphas[i] = (byte)0;
			} else {
				m_newAlphas[i] = GetAlpha(T, i);
			}

			// Left comes in first
			byte leftAlpha = (byte)Mathf.Clamp(m_newAlphas[i] * 2, 0, 255);
			// Right comes after first is completed with its fadein
			byte rightAlpha = (byte)Mathf.Clamp((m_newAlphas[i] * 2) - 255, 0, 255);

			// Set new alpha values.
			// Left
			newVertexColors[vertexIndex + 0].a = leftAlpha;
			newVertexColors[vertexIndex + 1].a = leftAlpha;
			// Right
			newVertexColors[vertexIndex + 2].a = rightAlpha;
			newVertexColors[vertexIndex + 3].a = rightAlpha;
		}

		m_textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}

	int GetStartIndex(float T) {
		return Mathf.Clamp(Mathf.FloorToInt(T * (m_charMaxIndex + m_characterSpread) - m_characterSpread), 0, m_charMaxIndex);
	}

	int GetEndIndex(float T) {
		return Mathf.Clamp(Mathf.CeilToInt(T * (m_charMaxIndex + m_characterSpread)), 0, m_charMaxIndex);
	}

	byte GetAlpha(float T, int index) {
		return (byte)Mathf.RoundToInt(255 * Mathf.Clamp01((T * (m_charMaxIndex + m_characterSpread) / (float)m_characterSpread) - (index / (float)m_characterSpread)));
	}
}
