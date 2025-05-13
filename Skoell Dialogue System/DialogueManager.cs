using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour {
	[SerializeField] TMP_Text m_textComponent;
	[SerializeField] string m_placeHolderText = "...";
	[SerializeField] DialogueBoxSizerBase m_dialogueBoxSizer;
	[SerializeField] TextFader m_textFader;
	[SerializeField] string[] m_conversationParagraphs;

	private enum EDialogueState {
		Idle = 0,
		FadeIn,
		ResizingBox,
		FadeOut
	}

	bool m_changingStates = false;
	Queue<EDialogueState> m_queuedStates = new Queue<EDialogueState>();

	EDialogueState m_currentState;

	int m_currentParagraph = -1;
	
	bool m_tryingExit;

	bool m_canTransition { get { return m_currentState == EDialogueState.FadeIn || m_currentState == EDialogueState.Idle; } }

	private void Start() {
		m_textComponent.ForceMeshUpdate();

		m_dialogueBoxSizer.m_currentContent = m_textFader.m_textComponent = m_textComponent;

		m_currentParagraph = -1;
		m_tryingExit = false;

		m_dialogueBoxSizer.Init();

		m_currentState = EDialogueState.Idle;
		SetTextContent(m_placeHolderText, true);
	}

	public void OnPlayerLeaveInteractArea() {
		if (m_currentParagraph == -1) {
			m_tryingExit = false; // Set this to false in case no longer valid
			return;
		}

		// Reset to placeholder text when leaving
		if (m_canTransition) {
			m_currentParagraph = -1;
			SetState(EDialogueState.FadeOut);
			m_tryingExit = false;

		} else if (m_currentState == EDialogueState.ResizingBox){
			// If we're in the middle of resizing, just start resizing to the placeholder text
			m_currentParagraph = -1;
			SetTextContent(m_placeHolderText);
			m_tryingExit = false;
			
			// set this LAST
			m_dialogueBoxSizer.enabled = true;
		} else {
			// If we can't currently transition to the placeholder, queue it for the next state change when we can transition
			m_tryingExit = true;
		}
	}

	public void OnInteract() {
		// Special case if we're currently fading in and not skipping yet: skip text by fading faster
		// (But not if we're transitioning to the placeholder text)
		if (m_currentState == EDialogueState.FadeIn && !m_textFader.m_skip && m_currentParagraph != -1) {
			m_textFader.SkipText();
			return;
		}

		if (m_canTransition) {
			m_currentParagraph++;
			if (m_currentParagraph >= m_conversationParagraphs.Length) {
				m_currentParagraph = -1;
			}
			SetState(EDialogueState.FadeOut);
		}
	}

	public void OnTextFadeoutCompleted() {
		SetState(EDialogueState.ResizingBox);
	}

	public void OnDialogueBoxResized() {
		// Due to the dialogue box fade in-out system, we need to update the text here again
		// so it is aware of the new text box dimensions
		m_textFader.OnTextChanged(false);

		SetState(EDialogueState.FadeIn);
	}

	public void OnTextFadeinCompleted() {
		SetState(EDialogueState.Idle);
	}

	string GetNextText() {
		if (m_currentParagraph == -1) {
			return m_placeHolderText;
		}

		return m_conversationParagraphs[m_currentParagraph];
	}

	public void SetTextContent(string content, bool forceSnapUpdate = false) {
		m_textComponent.text = content;

		UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(m_textComponent.rectTransform);
		
		m_textComponent.ForceMeshUpdate();

		// Box sizer must be updated first for the new box to be selected and all to be updated before fading in text, otherwise going from a small to medium box may truncate characters based on the small box's size
		m_dialogueBoxSizer.OnContentUpdated(forceSnapUpdate);
		m_textFader.OnTextChanged(forceSnapUpdate);
	}

	void SetState(EDialogueState newState) {
		// To resolve collision issues with events being sent in the middle of this method's execution:
		if (m_changingStates) {
			m_queuedStates.Enqueue(newState);
			return;
		}

		m_changingStates = true;

		switch (newState) {
			case EDialogueState.FadeOut:
				m_textFader.FadeOut();
				// await fadeout completed evt
				break;
			case EDialogueState.FadeIn:
				m_textFader.FadeIn();
				// await fadein completed evt
				break;
			case EDialogueState.ResizingBox:
				SetTextContent(GetNextText());

				// Set enabled LAST so it doesn't send its event prematurely
				m_dialogueBoxSizer.enabled = true;
				break;
			// await resize completed evt
			case EDialogueState.Idle:
				// nothin :)
				break;
		}

		m_currentState = newState;

		if (m_tryingExit) {
			OnPlayerLeaveInteractArea();
		}

		m_changingStates = false;
		// Try execute queued states...
		if (m_queuedStates.Count > 0) {
			SetState(m_queuedStates.Dequeue());
		}
	}
}
