using System;
using System.Threading;
using System.Threading.Tasks;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.ResponseSystem {
	[Title("Send Stimulus")]
	[Description("Sends a stimulus to the given actor, cancelling the following instructions if a reply is received")]

	[Category("Responses/Send Stimulus")]

	[Keywords("Response", "Stimulus", "Send")]
	[Image(typeof(IconProcessor), ColorTheme.Type.Green)]
	[Serializable]
	public class InstructionSendStimulus : Instruction {
		private class StimulusResult {
			[NonSerialized] private bool m_Complete;
			[NonSerialized] private bool m_Success = false;

			public StimulusResult() {
				m_Success = false;
			}

			public void OnReplyReceived() {
				this.m_Complete = true;
				m_Success = true;
			}

			public void OnReplyTimeout() {
				m_Complete = true;
				m_Success = false;
			}

			public async Task<bool> Await() {
				while (this.m_Complete == false) {
					await Task.Yield();
				}

				return m_Success;
			}
		}

		public override string Title => $"{m_source} sends stimulus {this.m_stimulusTag} to {m_target}";

		public string m_stimulusTag = "my-stimulus-tag";
		[SerializeField] PropertyGetGameObject m_source = GetGameObjectSelf.Create();
		[SerializeField] PropertyGetGameObject m_target = GetGameObjectInstance.Create();
		[SerializeField] float m_waitForSeconds = 0.0f;

		[NonSerialized] StimulusResult m_result;

		protected override async Task Run(Args args) {
			GameObject source = m_source.Get(args);
			NPC_ResponseActor target = m_target.Get<NPC_ResponseActor>(args);

			if (source == null) return;

			m_result = new StimulusResult();

			if (m_waitForSeconds <= 0.0f) {
				// Send stimulus
				if (target != null) {
					target.OnReceiveStimulus(m_stimulusTag, source);
				}
				// Don't wait, just return now and continue list
				return;
			}

			NPC_ResponseActor self = m_source.Get<NPC_ResponseActor>(args);
			if (self != null) {
				self.m_OnWaitCancelled -= m_result.OnReplyReceived;
				self.m_OnWaitCancelled += m_result.OnReplyReceived;

				self.m_OnWaitTimeout -= m_result.OnReplyTimeout;
				self.m_OnWaitTimeout += m_result.OnReplyTimeout;

				if (target != null) {
					target.OnReceiveStimulus(m_stimulusTag, source);
				}

				self.StartWaiting(m_waitForSeconds, target);

				bool receivedReply = await m_result.Await();
				if (receivedReply) this.NextInstruction = int.MaxValue; // Skip to end of instructions list

				self.m_OnWaitTimeout -= m_result.OnReplyTimeout;
				self.m_OnWaitCancelled -= m_result.OnReplyReceived;
			}

			return;
		}
	}
}