using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	[Version(0, 1, 1)]

	[Title("Apply Evidence Memory")]
	[Description("Records the usage of an Evidence in a Perception's memory, tracking the time and use count")]

	[Category("Perception/Evidence/Apply Memory")]

	[Keywords("Perception", "Evidence", "Memory", "Use")]
	[Image(typeof(IconEvidenceTamper), ColorTheme.Type.Green)]

	[Serializable]
	public class InstructionApplyMemory : Instruction {
		[SerializeField] private PropertyGetGameObject m_Perception = GetGameObjectPerception.Create;
		[SerializeField] private PropertyGetGameObject m_Evidence = GetGameObjectEvidence.Create;

		public override string Title => $"{this.m_Perception} applies memory of {this.m_Evidence}";

		protected override Task Run(Args args) {
			Perception perception = this.m_Perception.Get<Perception>(args);
			if (perception == null) return DefaultResult;

			Evidence evidence = this.m_Evidence.Get<Evidence>(args);
			if (evidence == null) return DefaultResult;

			perception.OnEvidenceApplied(evidence);

			return DefaultResult;
		}
	}
}