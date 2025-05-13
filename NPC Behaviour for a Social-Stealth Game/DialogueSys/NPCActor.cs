using UnityEngine;

namespace GameCreator.Runtime.TaskSystem {
	public class NPCActor : MonoBehaviour {
		NPCTask m_currentTask = null;
		NPCTask m_previousTask = null;

		public bool IsBusy => m_currentTask != null;

		public void SetCurrentTask(NPCTask task) {
			if (m_currentTask != null) {
				Debug.LogError($"OY! This actor {gameObject.name} is currently busy but SetCurrentTask was called");
				return;
			}

			if (task == null) {
				Debug.LogError($"OY! We can't set a task directly to null, it must be interrupted properly (actor {gameObject.name})");
				return;
			}

			m_currentTask = task;
		}

		public void FinishTask() {
			if (m_currentTask == null) {
				Debug.LogError($"OY! We probably shouldn't be calling FinishTask on something without a task ({gameObject.name}) (could be a valid thing to do, but not sure yet)");
				return;
			}

			m_previousTask = m_currentTask;
			m_currentTask = null;
		}
	}
}