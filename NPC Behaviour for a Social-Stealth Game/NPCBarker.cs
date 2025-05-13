using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NPCBarker : MonoBehaviour {
	public class BarkDialogue {
		public string m_dialogueText;
		public float m_duration;

		public BarkDialogue(string text, float duration) {
			m_dialogueText = text;
			m_duration = duration;
		}
	}

	// Simple component for managing overhead text dialogue ("barking")
	[SerializeField] TextMeshPro m_textObject;

	[SerializeField] float m_ellipsisInterval = 0.6f;

	bool m_isBarking = false;
	float m_currentBarkExpiry = -1.0f;
	float m_nextEllipsisTime = -1.0f;
	int m_currentEllipsisIndex = 0;
	const int kMaxEllipsisIdx = 3;
	const string kEllipsis = "...";

	private void Awake() {
		if(m_textObject != null) m_textObject.text = "";
	}

	public void Bark(BarkDialogue dialogue) {
		m_isBarking = true;
		m_currentBarkExpiry = Time.time + dialogue.m_duration;

		if (m_textObject != null) {
			m_textObject.text = dialogue.m_dialogueText;
		}
	}

	void ResetToDefault() {
		m_isBarking = false;

		if (m_textObject != null) {
			m_textObject.text = "";
			m_currentEllipsisIndex = 3;
			m_nextEllipsisTime = Time.time + m_ellipsisInterval;
		}
	}

	private void Update() {
		if (m_isBarking) {
			if (Time.time > m_currentBarkExpiry) {
				ResetToDefault();
			}
		} else {
			if (Time.time > m_nextEllipsisTime) {
				if (++m_currentEllipsisIndex > kMaxEllipsisIdx) {
					m_currentEllipsisIndex = 0;
				}

				if (m_textObject != null) m_textObject.text = kEllipsis.Substring(0, m_currentEllipsisIndex);
				m_nextEllipsisTime = Time.time + m_ellipsisInterval;
			}
		}
	}
}
