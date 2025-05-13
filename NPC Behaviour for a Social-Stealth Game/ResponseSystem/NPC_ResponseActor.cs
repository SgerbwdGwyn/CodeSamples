using System;
using System.Collections.Generic;
using UnityEngine;
using GameCreator.Runtime.Common;

namespace GameCreator.Runtime.ResponseSystem {
	public class NPC_ResponseActor : MonoBehaviour {
		NPC_Response m_currentResponse;

		Dictionary<string, List<NPC_Response>> m_responses;

		[NonSerialized] private Args m_Args;

		public Action m_OnWaitTimeout;
		public Action m_OnWaitCancelled;

		private string m_debugString = "";
		public string DebugString => m_debugString;

		bool m_isWaiting = false;
		float m_waitUntil = -1.0f;
		NPC_ResponseActor m_waitingForActor = null;

		private void OnEnable() {
			m_Args = new Args(gameObject);

			// TODO make this super efficient by counting from n+1 onward the number of responses for a given tag, and making an array in the dict of that size instead of a list
			m_responses = new Dictionary<string, List<NPC_Response>>();

			NPC_Response[] allResponses = GetComponentsInChildren<NPC_Response>();

			foreach (NPC_Response response in allResponses) {
				if (!m_responses.ContainsKey(response.StimulusTag)) {
					m_responses[response.StimulusTag] = new List<NPC_Response>();
				}
				m_responses[response.StimulusTag].Add(response);
			}

			UpdateDebugString();
		}

		private void Update() {
			if (m_isWaiting) {
				if (Time.time > m_waitUntil) {
					m_isWaiting = false;
					m_waitingForActor = null;
					m_OnWaitTimeout?.Invoke();

					UpdateDebugString();
				}
			}
		}


		public void StartWaiting(float timeToWait, NPC_ResponseActor target) {
			m_isWaiting = true;
			m_waitUntil = Time.time + timeToWait;
			m_waitingForActor = target;

			UpdateDebugString();
		}

		public void DelayWaitForActor(float newTimeToWait, NPC_ResponseActor target) {
			// If we're waiting for the incoming actor, delay our timer
			if (target == m_waitingForActor) {
				m_waitUntil = Time.time + newTimeToWait;
			}

			UpdateDebugString();
		}

		public void CancelWaiting(NPC_ResponseActor target) {
			if (target == m_waitingForActor) {
				m_isWaiting = false;
				m_waitingForActor = null;
				m_OnWaitCancelled?.Invoke(); // cancel remaining instructions
			}

			UpdateDebugString();
		}

		public bool CanRespondToStimulus(string stimulusTag, out NPC_Response response) {
			response = null;

			// First, check if we have any responses to this tag
			if (!m_responses.ContainsKey(stimulusTag)) {
				// Ignored stimulus without matching response
				return false;
			}

			// Then, check if our current response is awaiting a stimulus
			bool hasActiveResponse = m_currentResponse != null;
			bool isWaitingForStimulus = hasActiveResponse && m_isWaiting;

			// We can go ahead, so find the best match
			// TODO select randomly if equal scores exist
			List<NPC_Response> matchingResponses = m_responses[stimulusTag];
			int highestScore = -1;
			NPC_Response bestResponse = null;
			for (int i = 0; i < matchingResponses.Count; ++i) {

				// skip if priority doesn't beat our current one
				if (isWaitingForStimulus) {
					// If we're waiting for a stimulus, accept anything of the same priority or higher
					if (matchingResponses[i].m_priority < m_currentResponse.m_priority) {
						return false;
					}
				} else if (hasActiveResponse) {
					// If we're not waiting but have an active stimulus, only accept higher priority
					if (matchingResponses[i].m_priority <= m_currentResponse.m_priority) {
						return false;
					}
				}

				if (matchingResponses[i].TryConditions(m_Args)) {
					int score = matchingResponses[i].ConditionsScore;
					if (score > highestScore) {
						highestScore = score;
						bestResponse = matchingResponses[i];
					}
				}
			}

			if (bestResponse == null) {
				// No passing response found; ignore stimulus
				return false;
			}

			response = bestResponse;
			return true;
		}

		public void OnReceiveStimulus(string stimulusTag, GameObject source) {
			// Then, check if our current response is awaiting a stimulus
			bool hasActiveResponse = m_currentResponse != null;
			bool isWaitingForStimulus = hasActiveResponse && m_isWaiting;

			NPC_Response response = null;
			if (!CanRespondToStimulus(stimulusTag, out response)) {
				return; // no response
			}

			// When we've reached here, we can start the new stimulus.
			if (isWaitingForStimulus) {
				// send an event to the current stimulus to indicate it can cancel its remaining list
				m_currentResponse.StopRunning(m_Args);
			} else if (hasActiveResponse) {
				// cancel current's list, but also execute its on-cancel stuff
				m_currentResponse.CancelRunning(m_Args);
			}
			// set previous to current

			m_currentResponse = response;
			m_currentResponse.OnStartResponse(source, this, m_Args);

			m_OnWaitCancelled?.Invoke();
			m_isWaiting = false;
			m_waitingForActor = null;

			UpdateDebugString();
		}

		public void OnFinishedTask(NPC_Response response) {
			if (response != m_currentResponse) {
				//Debug.LogError($"WTF!!! actor {gameObject.name} received a FinishedTask event from something that wasn't its current task");
				// this can get called by the old task so it's fine i guess, just return
				return;
			}

			m_currentResponse = null;

			UpdateDebugString();
		}

		private void UpdateDebugString() {
			m_debugString = "";
			string currentStateName = m_currentResponse != null ? m_currentResponse.name : "NULL";
			m_debugString += $"Current Response: {currentStateName} \n";
			
			m_debugString += $"Waiting?: {m_isWaiting} \n";
		}
	}
}