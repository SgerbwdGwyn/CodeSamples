using System;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	[Title("Evidence Application Count")]
	[Category("Perception/Evidence Application Count")]

	[Description("The number of times an Evidence was \"applied\" to something by a Perception component")]
	[Image(typeof(IconEvidenceTamper), ColorTheme.Type.Blue)]

	[Serializable]
	public class GetDecimalEvidenceApplicationCount : PropertyTypeGetDecimal {
		[SerializeField] private PropertyGetGameObject m_Perception = GetGameObjectPerception.Create;
		[SerializeField] private PropertyGetGameObject m_Evidence = GetGameObjectEvidence.Create;
		
		public override double Get(Args args) {
			Perception perception = this.m_Perception.Get<Perception>(args);
			if (perception == null) return -1;

			Evidence evidence = this.m_Evidence.Get<Evidence>(args);
			if (m_Evidence == null) return -1;

			EvidenceMemory memory = perception.LocateEvidenceInMemory(evidence);
			return memory != null ? memory.m_timesApplied : -1;
		}

		public override string String => $"{this.m_Perception}[{this.m_Evidence}] application count";
	}
}