using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.TaskSystem {

	[Title("Prompt Reply")]
	[Description("Prompts an actor to reply, awaiting their response")]

	[Category("Tasks/Prompt Reply")]

	[Keywords("Task", "Reply", "Prompt", "Actor")]
	[Image(typeof(IconProcessor), ColorTheme.Type.Yellow)]

	[Serializable]
	public class InstructionTaskMgrPromptReply : Instruction {
		[SerializeField] private PropertyGetGameObject m_task = GetGameObjectInstance.Create();
		[SerializeField] private bool m_WaitForReply = true;
		[SerializeField] private PropertyGetDecimal m_waitTimout = GetDecimalDecimal.Create(1.0);
		[SerializeField] private NPCTask.ENPCTaskActor m_actorTarget = NPCTask.ENPCTaskActor.Primary;
		[SerializeField] private NPCTaskStep m_promptStep = null;

		protected override Task Run(Args args) {
			// Your code here...
			return DefaultResult;
		}
	}
}