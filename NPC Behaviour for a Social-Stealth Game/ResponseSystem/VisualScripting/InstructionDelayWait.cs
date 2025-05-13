using System;
using System.Threading;
using System.Threading.Tasks;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.ResponseSystem {
	[Title("Delay Wait")]
	[Description("Tells the given actor to delay their wait for this actor")]

	[Category("Responses/Delay Wait")]

	[Keywords("Response", "Wait", "Delay")]
	[Image(typeof(IconProcessor), ColorTheme.Type.Yellow)]
	[Serializable]
	public class InstructionDelayWait : Instruction {

		public override string Title => $"{m_source} tells {m_target} to continue waiting for {m_delayWaitBySeconds}";

		[SerializeField] PropertyGetGameObject m_source = GetGameObjectSelf.Create();
		[SerializeField] PropertyGetGameObject m_target = GetGameObjectInstance.Create();
		[SerializeField] float m_delayWaitBySeconds = 1.0f;

		protected override Task Run(Args args) {
			NPC_ResponseActor self = m_source.Get<NPC_ResponseActor>(args);
			NPC_ResponseActor target = m_target.Get<NPC_ResponseActor>(args);

			if (self == null) return DefaultResult;
			if (target == null) return DefaultResult;

			target.DelayWaitForActor(m_delayWaitBySeconds, self);
			
			return DefaultResult;
		}
	}
}