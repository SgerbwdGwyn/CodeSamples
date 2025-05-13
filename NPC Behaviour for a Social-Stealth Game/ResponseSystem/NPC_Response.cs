using GameCreator.Runtime.Common;
using GameCreator.Runtime.Dialogue;
using GameCreator.Runtime.ResponseSystem;
using GameCreator.Runtime.VisualScripting;
using System.Threading.Tasks;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public class NPC_Response : MonoBehaviour {
	[SerializeField] string m_stimulusTag = "my-stimulus-tag";
	[SerializeField] int m_responseScore = 0;
	[SerializeField] public int m_priority = 0;

	[Header("Response Conditions")]
	[SerializeField] ConditionList m_responseConditions = new ConditionList();
	[Header("Response Instructions")]
	[SerializeField] InstructionList m_responseInstructions = new InstructionList();
	[Header("On Cancel")]
	[SerializeField] InstructionList m_onCancelInstructions = new InstructionList();

	public string StimulusTag => m_stimulusTag;
	public int ConditionsScore => m_responseScore;

	//[HideInInspector] public GameObject m_auxObject;
	//[HideInInspector] public GameObject m_auxObject2;
	GameObject m_source = null;

	[HideInInspector] public float m_waitUntil = -1.0f;
	[HideInInspector] public bool m_isWaiting = false;
	private NPC_ResponseActor m_actor = null;

	public GameObject Source => m_source;

	public void OnStartResponse(GameObject source, NPC_ResponseActor actor, Args args) {
		m_source = source;
		m_actor = actor;
		m_responseInstructions.EventEndRunning += OnInstructionsFinished;

		_ = m_responseInstructions.Run(args);
	}

	void OnInstructionsFinished() {
		m_responseInstructions.EventEndRunning -= OnInstructionsFinished;
		if (m_actor != null) {
			m_actor.OnFinishedTask(this);
			m_actor = null;
		}
	}

	public void StopRunning(Args args) {
		m_responseInstructions.Cancel();
		m_isWaiting = false;
		m_waitUntil = -1.0f;
	}

	public void CancelRunning(Args args) {
		StopRunning(args);
		_ = m_onCancelInstructions.Run(args);
	}

	public bool TryConditions(Args args) {
		return m_responseConditions.Check(args, CheckMode.And);
	}
}
