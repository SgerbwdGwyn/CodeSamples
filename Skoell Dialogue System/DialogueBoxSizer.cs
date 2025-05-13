using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MalbersAnimations.Events;

// Resizes a 3D skinned dialogue box to fit its text appropriately.
// This script is intended for use with a scalable asset that can be deformed to arbitrary proportions.
// An additional bone should be available on the rig to represent the "tail" that points towards the speaker.

public class DialogueBoxSizer : DialogueBoxSizerBase {
	enum EAnchorIndex {
		BottomLeft = 0,
		TopLeft,
		TopRight,
		BottomRight
	}

	[Tooltip("From BottomLeft clockwise")]
	[SerializeField] Transform[] m_anchorBones = new Transform[4];
	[SerializeField] Transform m_speakerIndicatorBone;

	[SerializeField] float m_timeToTransitionSize = 1.0f;
	float m_timer = 0.0f;

	[SerializeField] Vector2 m_marginSize = new Vector2(0.0f, 0.0f);
	[SerializeField] float m_indicatorOffsetVertical = -0.88f;

	Vector3[] m_anchorPointsPrevious = new Vector3[4];
	Vector3[] m_anchorPoints = new Vector3[4];

	[SerializeField] Vector3 m_rightAxis = new Vector3(-1.0f, 0.0f, 0.0f);
	[SerializeField] Vector3 m_upAxis = new Vector3(0.0f, 0.0f, -1.0f);

	public override void Init() {
		m_currentContentTransform = m_currentContent.GetComponent<RectTransform>();
		m_currentContentTransform.ForceUpdateRectTransforms();

		CalculateAnchorPoints();

		Array.Copy(m_anchorPoints, m_anchorPointsPrevious, 4);

		UpdateAnchorBonePositions(1.0f);

		Debug.Log("Init (timer " + m_timer);

		enabled = false;
	}

	public override void OnContentUpdated(bool forceSnap = false) {

		if (!forceSnap) {
			// When transitioning content, save the current positions of the anchors (this means if we change shape halfway through resizing, it smoothly transitions)
			for (int i = (int)EAnchorIndex.BottomLeft; i <= (int)EAnchorIndex.BottomRight; ++i) {
				m_anchorPointsPrevious[i] = Vector3.Lerp(m_anchorPointsPrevious[i], m_anchorPoints[i], m_timeToTransitionSize > 0.0f ? Mathf.Clamp01(m_timer / m_timeToTransitionSize) : 1.0f);
			}
		}

		m_currentContentTransform.ForceUpdateRectTransforms();

		CalculateAnchorPoints();

		if (forceSnap) {
			Array.Copy(m_anchorPoints, m_anchorPointsPrevious, 4);	
		}

		UpdateAnchorBonePositions(0.0f);
		m_timer = 0.0f;

		Debug.Log("OnContentUpdated (timer " + m_timer + "), force? " + forceSnap);
	}

	public override void ForceContentSnap() {
		UpdateAnchorBonePositions(1.0f);
	}

	private void Update() {
		UpdateAnchorBonePositions(m_timeToTransitionSize > 0.0f ? Mathf.Clamp01(m_timer / m_timeToTransitionSize) : 1.0f);

		Debug.Log(m_timer);

		if (m_timer > m_timeToTransitionSize) {
			if (m_resizeCompleteEvent != null) {
				m_resizeCompleteEvent.enabled = true;
			}
			enabled = false;
		}

		m_timer += Time.deltaTime;
	}

	void CalculateAnchorPoints() {
		Rect transformRect = m_currentContentTransform.rect;

		// We assume here that -X is the "real" X axis for text, and that -Z is the "real" Y axis for text
		m_anchorPoints[(int)EAnchorIndex.BottomLeft] = GetBonePosition(new Vector2(transformRect.xMin - m_marginSize.x, transformRect.yMin - m_marginSize.y));
		m_anchorPoints[(int)EAnchorIndex.TopLeft] = GetBonePosition(new Vector2(transformRect.xMin - m_marginSize.x, transformRect.yMax + m_marginSize.y));
		m_anchorPoints[(int)EAnchorIndex.TopRight] = GetBonePosition(new Vector2(transformRect.xMax + m_marginSize.x, transformRect.yMax + m_marginSize.y));
		m_anchorPoints[(int)EAnchorIndex.BottomRight] = GetBonePosition(new Vector2(transformRect.xMax + m_marginSize.x, transformRect.yMin - m_marginSize.y));
	}

	Vector3 GetBonePosition(Vector2 rectTransformPoint) {
		return ((rectTransformPoint.x) * m_rightAxis) + ((rectTransformPoint.y) * m_upAxis);
	}

	void UpdateAnchorBonePositions(float T) {
		for (int i = (int)EAnchorIndex.BottomLeft; i <= (int)EAnchorIndex.BottomRight; ++i) {
			m_anchorBones[i].localPosition = Vector3.Lerp(m_anchorPointsPrevious[i], m_anchorPoints[i], T);
		}

		m_speakerIndicatorBone.localPosition = (m_anchorBones[(int)EAnchorIndex.BottomLeft].localPosition + m_anchorBones[(int)EAnchorIndex.BottomRight].localPosition) / 2.0f;
		m_speakerIndicatorBone.localPosition += m_upAxis * m_indicatorOffsetVertical;
	}
}
