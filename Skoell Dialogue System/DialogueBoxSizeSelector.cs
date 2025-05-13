using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MalbersAnimations.Events;
using UnityEngine.UI;

// An alternative to the Dialogue Box Resizer, this script is intended for use with multiple sizes of fixed-art boxes.
// Each box should be its own sprite, and the transition will be done by scaling the next box desired to the dimensions of the current box, then quickly animating them
// to the default size of that box.
public class DialogueBoxSizeSelector : DialogueBoxSizerBase {

	[Tooltip("Ideally, these sprites should be sorted from smallest first to largest last")]
	[SerializeField] DialogueBoxSprite[] m_dialogueBoxArtSprites;
	[SerializeField] Image m_imageComponent;
	HorizontalLayoutGroup m_layoutGroup;

	[SerializeField, ConditionalHide("m_useSwipeTransition", Invert = true)] float m_transitionDuration = 0.5f;
	[SerializeField, ConditionalHide("m_useSwipeTransition"), Tooltip("Fade out and fade in durations")] Vector2 m_transitionDurationSwipe = new Vector2(0.1f, 0.5f);
	[SerializeField] AnimationCurve m_transitionCurve;
	float m_timer = 0.0f;
	bool m_fadingOut = false;

	[SerializeField] public bool m_useSwipeTransition;
	[SerializeField, ConditionalHide("m_useSwipeTransition", false), Tooltip("The min and max for the swipe offset material parameter")] public Vector2 m_swipeOffsetMinMax;
	[SerializeField] bool m_useCurveForSwipe;

	Material m_swipeMaterial;
	int m_swipeOffsetProperty;

	Vector2 m_previousContentSize;	// Lerp from this...
	Vector2 m_currentContentSize;   // ...to this

	int m_currentBoxIndex = 0;

	float m_defaultFontSize;
	TextAlignmentOptions m_defaultTextAlignment;

	bool m_sendEvent = false;

	
	public override void Init() {
		m_layoutGroup = m_imageComponent.GetComponent<HorizontalLayoutGroup>();
		m_currentContentTransform = m_currentContent.GetComponent<RectTransform>();
		m_currentContentTransform.ForceUpdateRectTransforms();

		m_swipeMaterial = m_imageComponent.material;
		m_swipeOffsetProperty = Shader.PropertyToID("_SwipeOffset");
		m_swipeMaterial.SetFloat(m_swipeOffsetProperty, m_swipeOffsetMinMax.y);

		m_fadingOut = false;

		m_defaultFontSize = m_currentContent.fontSize;
		m_defaultTextAlignment = m_currentContent.alignment;

		m_timer = 0.0f;

		enabled = false;
	}

	public override void OnContentUpdated(bool forceSnap = false) {
		m_timer = forceSnap ? m_transitionDuration : 0.0f;

		// For swipe transitions
		if (m_useSwipeTransition && !m_fadingOut) {
			m_fadingOut = true;
			enabled = true;
			return;
		}

		m_currentContentTransform.ForceUpdateRectTransforms();

		m_sendEvent = !forceSnap;

		ChooseDialogueBox();

		Debug.Log("OnContentUpdated (timer " + m_timer + "), force? " + forceSnap);

		enabled = true;
	}

	private void Update() {
		Transition();

		m_timer += Time.deltaTime;
	}

	void Transition() {
		if (m_useSwipeTransition) {
			float durationToUse = m_fadingOut ? m_transitionDurationSwipe.x : m_transitionDurationSwipe.y;

			float timeValue = m_useCurveForSwipe ? m_transitionCurve.Evaluate(m_timer / durationToUse) : m_timer / durationToUse;

			if (m_fadingOut) {
				m_swipeMaterial.SetFloat(m_swipeOffsetProperty, Mathf.Lerp(m_swipeOffsetMinMax.y, m_swipeOffsetMinMax.x, timeValue));
			} else {
				m_swipeMaterial.SetFloat(m_swipeOffsetProperty, Mathf.Lerp(m_swipeOffsetMinMax.x, m_swipeOffsetMinMax.y, timeValue));
			}

			if (m_timer > durationToUse) {
				if (m_fadingOut) {
					// We've completed fading out, unset the flag and select the new box
					OnContentUpdated();
					m_fadingOut = false;
				} else {
					if (m_resizeCompleteEvent != null && m_sendEvent) {
						m_resizeCompleteEvent.enabled = true;
					}
					enabled = false;
				}
			}

		} else {
			m_imageComponent.rectTransform.sizeDelta = Vector2.Lerp(m_previousContentSize, m_currentContentSize, m_transitionCurve.Evaluate(m_timer / m_transitionDuration));

			if (m_timer > m_transitionDuration) {
				if (m_resizeCompleteEvent != null && m_sendEvent) {
					m_resizeCompleteEvent.enabled = true;
				}
				enabled = false;
			}
		}
	}

	void SelectSprite(DialogueBoxSprite sprite) {
		// Set the image
		m_imageComponent.sprite = sprite.m_sprite;

		// Resize to image dimensions
		m_imageComponent.SetNativeSize();

		// Set margins in layout component
		m_layoutGroup.padding.left = Mathf.RoundToInt(sprite.m_margins.x);
		m_layoutGroup.padding.right = Mathf.RoundToInt(sprite.m_margins.y);
		m_layoutGroup.padding.top = Mathf.RoundToInt(sprite.m_margins.z);
		m_layoutGroup.padding.bottom = Mathf.RoundToInt(sprite.m_margins.w);

		// Rebuild layout
		UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(m_imageComponent.rectTransform);

		// Apply overrides
		m_currentContent.fontSize = sprite.m_overrideFontSize ? sprite.m_fontSize : m_defaultFontSize;
		m_currentContent.alignment = sprite.m_overrideAlignment ? sprite.m_alignment : m_defaultTextAlignment;

		m_currentContent.ForceMeshUpdate(false, true);
		m_currentContent.rectTransform.ForceUpdateRectTransforms();

		m_currentContentSize = m_imageComponent.rectTransform.rect.size;


		if (m_useSwipeTransition) {
			m_swipeMaterial.SetFloat(m_swipeOffsetProperty, m_swipeOffsetMinMax.x);
		} else { 
			m_imageComponent.rectTransform.sizeDelta = m_previousContentSize;
		}
	}

	void ChooseDialogueBox() {
		m_previousContentSize = m_imageComponent.rectTransform.rect.size;

		int bestFitIdx = 0;
		int highestOverflowStartIdx = -1;

		for (int i = 0; i < m_dialogueBoxArtSprites.Length; ++i) {
			SelectSprite(m_dialogueBoxArtSprites[i]);

			// Check if text fits these new dimensions
			if (!m_currentContent.isTextOverflowing) {
				// It fits! Or this is the last available box... Select this one.
				m_currentBoxIndex = i;
				return;
			} else if(m_currentContent.firstOverflowCharacterIndex > highestOverflowStartIdx) {
				highestOverflowStartIdx = m_currentContent.firstOverflowCharacterIndex;
				bestFitIdx = i;
			}
		}

		// If the text hasn't fit any of the boxes, use the one with the least amount of truncated text:
		SelectSprite(m_dialogueBoxArtSprites[bestFitIdx]);
		m_currentBoxIndex = bestFitIdx;
	}
}
