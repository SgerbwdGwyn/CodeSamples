using System;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	[Title("Time Since Evidence Memorised")]
	[Category("Perception/Time Since Evidence Memorised")]

	[Description("The time since an Evidence was initially memorised by a Perception component")]
	[Image(typeof(IconEvidence), ColorTheme.Type.Blue)]

	[Serializable]
	public class GetEvidenceTimeSinceMemoryCreated : PropertyTypeGetDecimal {
		[SerializeField] private PropertyGetGameObject m_Perception = GetGameObjectPerception.Create;
		[SerializeField] private PropertyGetGameObject m_Evidence = GetGameObjectEvidence.Create;
		
		public override double Get(Args args) {
			Perception perception = this.m_Perception.Get<Perception>(args);
			if (perception == null) return -1.0f;

			Evidence evidence = this.m_Evidence.Get<Evidence>(args);
			if (m_Evidence == null) return -1.0f;

			return perception.GetEvidenceMemorisedTime(evidence);
		}

		public override string String => $"{this.m_Perception}[{this.m_Evidence}] application count";
	}
}