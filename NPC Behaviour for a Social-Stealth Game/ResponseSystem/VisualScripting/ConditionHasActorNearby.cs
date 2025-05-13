using System;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Perception;
using GameCreator.Runtime.VisualScripting;
using UnityEngine;
using System.Collections.Generic;
using GameCreator.Runtime.ResponseSystem;

namespace GameCreator.Runtime.TaskSystem {
	[Title("Has Available Actor Nearby")]
	[Description("Checks if there's a valid actor nearby to us")]

	[Category("Responses/Has Actor Nearby")]

	[Keywords("Nearby", "Actor")]
	[Image(typeof(IconCharacter), ColorTheme.Type.Green)]

	[Serializable]
	public class ConditionHasActorNearby : Condition {
		[SerializeField] private PropertyGetGameObject m_source = GetGameObjectSelf.Create();
		//[SerializeField] private NPCRoleMask m_roles = (NPCRoleMask)~0;
		[SerializeField] private float m_distance = 3.0f;
		[SerializeField] private string m_stimulusTag = "my-stimulus-tag";
		//[SerializeField] private int m_count = 1;

		protected override string Summary => $"{this.m_source} has actors within {m_distance} units that can respond to {m_stimulusTag}";

		protected override bool Run(Args args) {
			NPCIdentity self = m_source.Get<NPCIdentity>(args);
			if (self == null) return false;

			// Gather all NPCs within distance that can respond
			HashSet<NPCIdentity> allNPCs = NPCIdentityManager.Instance.RegisteredNPCs;
			
			NPC_ResponseActor closestActor = null;
			float closestDist = Mathf.Infinity;
			
			foreach (NPCIdentity npc in allNPCs) {
				if (npc == self) continue;

				float distance = Vector3.Distance(npc.transform.position, self.transform.position);
				if (distance < m_distance) {
					// They're within distance
					NPC_ResponseActor actor = npc.GetComponent<NPC_ResponseActor>();
					if (actor != null) {
						if (actor.CanRespondToStimulus(m_stimulusTag, out _)) {
							// They can respond to this
							if (distance < closestDist) {
								closestDist = distance;
								closestActor = actor;
							}
						}
					}
				}
			}

			return closestActor != null;
		}
	}
}