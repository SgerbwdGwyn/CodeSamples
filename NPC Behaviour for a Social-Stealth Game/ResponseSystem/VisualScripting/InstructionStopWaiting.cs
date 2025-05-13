using System;
using System.Threading;
using System.Threading.Tasks;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.ResponseSystem {
	[Title("Stop Waiting")]
	[Description("Tells the given actor to act as though they've received a reply from us, with no stimulus")]

	[Category("Responses/Stop Waiting")]

	[Keywords("Response", "Wait", "Stop")]
	[Image(typeof(IconProcessor), ColorTheme.Type.Yellow)]
	[Serializable]
	public class InstructionStopWaiting : Instruction {

		public override string Title => $"{m_source} tells {m_target} to stop waiting for it";

		[SerializeField] PropertyGetGameObject m_source = GetGameObjectSelf.Create();
		[SerializeField] PropertyGetGameObject m_target = GetGameObjectInstance.Create();

		protected override Task Run(Args args) {
			NPC_ResponseActor self = m_source.Get<NPC_ResponseActor>(args);
			NPC_ResponseActor target = m_target.Get<NPC_ResponseActor>(args);

			if (self == null) return DefaultResult;
			if (target == null) return DefaultResult;

			target.CancelWaiting(self);
			
			return DefaultResult;
		}
	}
}