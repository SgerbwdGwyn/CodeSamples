using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MalbersAnimations.Events;
public abstract class DialogueBoxSizerBase : MonoBehaviour {

	[HideInInspector] public TMP_Text m_currentContent;
	protected RectTransform m_currentContentTransform;

	[SerializeField] protected UnityEventRaiser m_resizeCompleteEvent;

	public abstract void Init();

	public abstract void OnContentUpdated(bool forceSnap = false);

	public virtual void ForceContentSnap() { }
}
