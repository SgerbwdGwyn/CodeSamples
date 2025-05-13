using System;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	[Title("Time Since Last Evidence Application")]
	[Category("Perception/Time Since Evidence Applied")]

	[Description("The time since an evidence was last \"applied\" to something by a Perception component")]
	[Image(typeof(IconEvidenceTamper), ColorTheme.Type.Blue)]

	[Serializable]
	public class GetDecimalEvidenceTimeSinceApplied : PropertyTypeGetDecimal {
		[SerializeField] private PropertyGetGameObject m_Perception = GetGameObjectPerception.Create;
		[SerializeField] private PropertyGetGameObject m_Evidence = GetGameObjectEvidence.Create;

		public override double Get(Args args) {
			Perception perception = this.m_Perception.Get<Perception>(args);
			if (perception == null) return -1.0f;

			Evidence evidence = this.m_Evidence.Get<Evidence>(args);
			if (m_Evidence == null) return -1.0f;

			EvidenceMemory memory = perception.LocateEvidenceInMemory(evidence);
			return memory != null ? Time.time - memory.m_lastTimeApplied : -1.0f;
		}

		public override string String => $"Time since {this.m_Perception}[{this.m_Evidence}] last applied";
	}
}