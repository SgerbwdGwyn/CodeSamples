using GameCreator.Runtime.VisualScripting;
using System;
using UnityEngine;
namespace GameCreator.Runtime.TaskSystem {
	// OLD OUTDATED FOR OLD SYSTEM BLEH

	public class NPCTaskManager : MonoBehaviour {
		// Private members
		NPCTask m_currentTask;
		NPCTaskStep m_currentStep;

		bool m_isWaiting = false;
		float m_awaitExpires = 0;

		// Event System
		public event Action<string> EventTaskStarted;
		public event Action<string> EventTaskPrompt;
		public event Action<string> EventTaskFinish;

		public event Action<string> EventTaskInterrupted;
		public event Action<string> EventTaskNoReply;

		////
		public string m_currentTaskTag;
		public NPCTaskManager m_partner { get; private set; }
		public bool m_isBusy = false;
		public bool m_isPrimary = false;
		public NPCTaskManager Partner => m_partner;
////


		private void Update() {
			if (m_isWaiting) {
				if (Time.time > m_awaitExpires) {
					m_isWaiting = false;
					EventTaskNoReply?.Invoke(m_currentTaskTag);
					FinishCurrentTask();
				}
			}
		}

		// Public Methods

		public void PromptTaskReply(string tag, float awaitDuration) {
			// Request to our partner that they reply with something to the tag, waiting for awaitDuration
			if (m_partner == null) {
				Debug.LogError($"{name} tried to prompt its partner for a reply, but no such partner exists! tag: {tag}");
			} else {
				m_partner.OnPromptReceived(tag);
			}

			// Set the timer and wait
			m_isWaiting = true;
			m_awaitExpires = Time.time + awaitDuration;
		}

		protected void OnPromptReceived(string tag) {
			if (m_isWaiting) {
				Debug.Log($"{name} stopped waiting for a reply (REPLY RECEIVED!)");
				m_isWaiting = false;
			}

			// End our current task if we're prompted with a nothing tag
			if (tag == null || tag.Length == 0) {
				FinishCurrentTask();
				return;
			}

			EventTaskPrompt?.Invoke(tag);
		}

		public void RefreshPartnerTimer(float awaitDuration) {
			if (m_partner != null) {
				m_partner.OnRefreshAwaitPrompt(awaitDuration);
			}
		}

		protected void OnRefreshAwaitPrompt(float awaitDuration) {
			// Reset the timer
			if (m_isWaiting) {
				m_awaitExpires = Time.time + awaitDuration;
			}
		}

		public void StartTask(string taskTag, NPCTaskManager partner = null) {
			// Whichever TaskMgr catches the conversation start first executes this, making themselves the Primary and their partner the Secondary
			m_isPrimary = true;
			m_isBusy = true;
			m_currentTaskTag = taskTag;
			m_partner = partner;

			if (m_partner != null) {
				m_partner.InterruptCurrentTask(); // interrupt partner's task if we need to

				m_partner.m_isBusy = true;
				m_partner.m_partner = this;
				m_partner.m_isPrimary = false;
				m_partner.m_currentTaskTag = taskTag;
			}

			EventTaskStarted?.Invoke(taskTag);
		}

		public void FinishCurrentTask() {
			m_isBusy = false;
			m_isWaiting = false;
			//m_partner = null;

			//if (m_partner != null) {
			//	m_partner.m_isBusy = false;
			//	m_partner.m_isWaiting = false;
			//	m_partner.m_partner = null;
			//}

			EventTaskFinish?.Invoke(m_currentTaskTag);
		}

		public void InterruptCurrentTask() {
			if (m_isBusy) {
				m_isBusy = false;
				m_isWaiting = false;

				EventTaskInterrupted?.Invoke(m_currentTaskTag);
			}
		}
	}
}