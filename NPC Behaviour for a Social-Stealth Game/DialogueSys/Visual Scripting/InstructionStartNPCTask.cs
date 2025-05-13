using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.TaskSystem {

	[Title("Create Task")]
	[Description("Creates a task instance initialised with the given actors")]

	[Category("Tasks/Create Task")]

	[Keywords("Task", "Create", "Actor")]
	[Image(typeof(IconProcessor), ColorTheme.Type.Green)]

	[Serializable]
	public class InstructionStartNPCTask : Instruction {
		[Serializable]
		public class NPCActorSelectionOptions {
			[Flags]
			public enum EActorSelectionOptions {
				ClosestWithinRange = 1 << 0,
				FromPreviousTask = 1 << 1
			}

			[SerializeField] PropertyGetGameObject m_specificActor = GetGameObjectInstance.Create();
			[SerializeField] EActorSelectionOptions m_additionalSelectionOptions = (EActorSelectionOptions) 0;
			[SerializeField] PropertyGetDecimal m_maximumRange = GetDecimalDecimal.Create(3.0f);
			[SerializeField] NPCTask.ENPCTaskActor m_previousTaskActors = NPCTask.ENPCTaskActor.INVALID;
		}

		[Header("Primary Actor")]
		[SerializeField] private NPCActorSelectionOptions m_primaryCandidate = new NPCActorSelectionOptions();
		[Header("Secondary Actor")]
		[SerializeField] private NPCActorSelectionOptions m_secondaryCandidate = new NPCActorSelectionOptions();
		[Header("Tertiary Actor")]
		[SerializeField] private NPCActorSelectionOptions m_tertiaryCandidate = new NPCActorSelectionOptions();
		
		[Space]
		[SerializeField] private PropertyGetGameObject m_task = GetGameObjectInstance.Create();

		public override string Title => $"Create task {this.m_task}";

		protected override Task Run(Args args) {
			// First, find candidates for the task:


			return DefaultResult;
		}
	}
}