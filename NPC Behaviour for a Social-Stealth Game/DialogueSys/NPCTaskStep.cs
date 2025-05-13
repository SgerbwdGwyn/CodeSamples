using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace GameCreator.Runtime.TaskSystem {
	public class NPCTaskStep : MonoBehaviour {
		[SerializeField] string m_taskStepTag = "my-task-step-1";

		[Header("Task Instructions")]
		[SerializeField] private InstructionList m_Instructions = new InstructionList();
	}
}