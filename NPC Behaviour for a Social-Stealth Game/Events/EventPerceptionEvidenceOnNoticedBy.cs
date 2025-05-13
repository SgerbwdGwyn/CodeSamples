using System;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	[Title("On Evidence Noticed")]
	[Category("Perception/Evidence/On Evidence Processed")]

	[Description("Executed when an Evidence is processed in some way by a Perception")]

	[Image(typeof(IconEvidence), ColorTheme.Type.Yellow)]
	[Keywords("See", "Detect", "Notice", "Evidence")]

	[Serializable]
	public class EventPerceptionOnNoticedBy : VisualScripting.Event {
		[Flags]
		enum NoticeTypes {
			InitialNotice = 1 << 0,
			Handling = 1 << 1,
			Applied = 1 << 2,
			MarkedRecalled = 1 << 3
		}

		// EXPOSED MEMBERS: -----------------------------------------------------------------------

		[SerializeField]
		private PropertyGetGameObject m_Evidence = GetGameObjectSelf.Create();

		[SerializeField]
		private NoticeTypes m_Types = NoticeTypes.InitialNotice;

		//[SerializeField]
		//private PropertyGetGameObject m_Perception = GetGameObjectPerception.Create;

		// MEMBERS: -------------------------------------------------------------------------------

		[NonSerialized] private GameObject m_Source;
		[NonSerialized] private Args m_Args;

		// INITIALIZERS: --------------------------------------------------------------------------

		protected override void OnEnable(Trigger trigger) {
			base.OnEnable(trigger);

			Evidence evidence = this.m_Evidence.Get<Evidence>(trigger);
			if (evidence == null) return;

			this.m_Source = evidence.gameObject;
			this.m_Args = new Args(evidence.gameObject, evidence.gameObject);

			evidence.EventNoticed -= this.OnPerceivedBy;
			evidence.EventApplied -= this.OnPerceivedBy;
			evidence.EventHandlingStart -= this.OnPerceivedBy;
			evidence.EventMarkedRecalled -= this.OnPerceivedBy;

			if (m_Types.HasFlag(NoticeTypes.InitialNotice)) {
				evidence.EventNoticed += this.OnPerceivedBy;
			}

			if (m_Types.HasFlag(NoticeTypes.Applied)) {
				evidence.EventApplied += this.OnPerceivedBy;
			}

			if (m_Types.HasFlag(NoticeTypes.Handling)) {
				evidence.EventHandlingStart += this.OnPerceivedBy;
			}

			if (m_Types.HasFlag(NoticeTypes.MarkedRecalled)) {
				evidence.EventMarkedRecalled += this.OnPerceivedBy;
			}
		}

		protected override void OnDisable(Trigger trigger) {
			base.OnDisable(trigger);

			if (ApplicationManager.IsExiting) return;

			Evidence evidence = this.m_Source != null ? this.m_Source.Get<Evidence>() : null;
			if (evidence == null) return;

			evidence.EventNoticed -= this.OnPerceivedBy;
			evidence.EventApplied -= this.OnPerceivedBy;
			evidence.EventHandlingStart -= this.OnPerceivedBy;
			evidence.EventMarkedRecalled -= this.OnPerceivedBy;
		}

		// PRIVATE METHODS: -----------------------------------------------------------------------

		private void OnPerceivedBy(GameObject gameObject) {
			//Perception perception = gameObject.Get<Perception>();
			Evidence evidence = this.m_Source != null ? this.m_Source.Get<Evidence>() : null;

			if(evidence == null) return;

			if (this.m_Args.Target != gameObject) {
				this.m_Args.ChangeTarget(gameObject);
			}

			//Perception targetPerception = m_Perception.Get<Perception>();

			_ = this.m_Trigger.Execute(this.m_Args);
		}
	}
}