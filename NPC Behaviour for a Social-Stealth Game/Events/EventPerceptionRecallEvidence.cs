using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	[Title("On Recall Evidence")]
	[Category("Perception/Evidence/On Recall Evidence")]

	[Description("Executed when an agent with Perception notices an Evidence component already stored in its memory")]

	[Image(typeof(IconEvidence), ColorTheme.Type.Yellow)]
	[Keywords("See", "Detect", "Remember", "Memory", "Evidence")]

	[Serializable]
	public class EventPerceptionRecallEvidence : VisualScripting.Event {
		// EXPOSED MEMBERS: -----------------------------------------------------------------------

		[SerializeField]
		private PropertyGetGameObject m_Perception = GetGameObjectPerception.Create;

		[SerializeField]
		private CompareStringOrAny m_Tag = new CompareStringOrAny(
			true,
			GetStringId.Create("my-evidence-tag")
		);

		[SerializeField]
		private EvidenceMemory.MemoryTermMask m_memoryTerm = EvidenceMemory.MemoryTermMask.Short;

		// MEMBERS: -------------------------------------------------------------------------------

		[NonSerialized] private GameObject m_Source;
		[NonSerialized] private Args m_Args;

		// INITIALIZERS: --------------------------------------------------------------------------

		protected override void OnEnable(Trigger trigger) {
			base.OnEnable(trigger);

			Perception perception = this.m_Perception.Get<Perception>(trigger);
			if (perception == null) return;

			this.m_Source = perception.gameObject;
			this.m_Args = new Args(perception.gameObject);

			perception.EventMemoryRecallEvidence -= this.OnRecallEvidence;
			perception.EventMemoryRecallEvidence += this.OnRecallEvidence;
		}

		protected override void OnDisable(Trigger trigger) {
			base.OnDisable(trigger);

			if (ApplicationManager.IsExiting) return;

			Perception perception = this.m_Source != null ? this.m_Source.Get<Perception>() : null;
			if (perception == null) return;

			perception.EventMemoryRecallEvidence -= this.OnRecallEvidence;
		}

		// PRIVATE METHODS: -----------------------------------------------------------------------

		private void OnRecallEvidence(GameObject gameObject) {
			Evidence evidence = gameObject.Get<Evidence>();
			string evidenceTag = evidence.GetTag(this.m_Source);

			Perception perception = this.m_Source != null ? this.m_Source.Get<Perception>() : null;
			EvidenceMemory memory = perception?.LocateEvidenceInMemory(evidence);

			if (memory == null) return;

			if (this.m_Args.Target != gameObject) {
				this.m_Args.ChangeTarget(gameObject);
			}

			if (this.m_Tag.Match(evidenceTag, this.m_Args) && ((int) m_memoryTerm & (int) memory.m_memoryTerm) != 0) {
				_ = this.m_Trigger.Execute(this.m_Args);
			}
		}
	}
}