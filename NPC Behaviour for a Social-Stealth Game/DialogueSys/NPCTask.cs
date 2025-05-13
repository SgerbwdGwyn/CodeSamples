using System.Collections.Generic;
using UnityEngine;

namespace GameCreator.Runtime.TaskSystem {
    public class NPCTask : MonoBehaviour {
		public enum ENPCTaskActor {
			INVALID = -1,
			Primary = 0,
			Secondary = 1,
			Tertiary = 2,
			COUNT = Tertiary + 1
		}

		public enum EActorStatus {
			INVALID = -1,
			Acting,     // Is actively running a step
			Waiting,    // Is waiting for someone else to prompt them or is awaiting a reply
			Cancelled   // Has dropped out of the task to do something else
		}

		Dictionary<ENPCTaskActor, EActorStatus> m_statuses;
		Dictionary<ENPCTaskActor, NPCActor> m_actors;
		
        [SerializeField ]string m_taskTag = "my-task-tag";

        [SerializeField] NPCTaskStep[] m_steps = new NPCTaskStep[1];

		public void InitialiseTaskInstance(NPCActor primary, NPCActor secondary, NPCActor tertiary) {
			m_actors = new Dictionary<ENPCTaskActor, NPCActor>((int) ENPCTaskActor.COUNT);
			m_statuses = new Dictionary<ENPCTaskActor, EActorStatus>((int)ENPCTaskActor.COUNT);

			AddActor(primary, ENPCTaskActor.Primary);
			AddActor(secondary, ENPCTaskActor.Secondary);
			AddActor(tertiary, ENPCTaskActor.Tertiary);
		}

		void AddActor(NPCActor actor, ENPCTaskActor role) {
			m_actors.Add(role, actor);
			m_statuses.Add(role, actor != null ? EActorStatus.Waiting : EActorStatus.INVALID);
		}
	}
}