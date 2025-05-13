using System;
using System.Collections.Generic;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Perception;
using GameCreator.Runtime.ResponseSystem;
using UnityEngine;

[Title("Nearest Available Actor")]
[Category("Responses/Nearest Available Actor")]

[Description("Returns the nearest NPC Identity to the NPC")]
[Image(typeof(IconProcessor), ColorTheme.Type.Green)]
[Serializable]
public class PropertyGetNearestAvailableActor : PropertyTypeGetGameObject {
	[SerializeField] private PropertyGetGameObject m_source = GetGameObjectSelf.Create();
	//[SerializeField] private NPCRoleMask m_roles = (NPCRoleMask)~0;
	[SerializeField] private float m_distance = 3.0f;
	[SerializeField] private string m_stimulusTag = "my-stimulus-tag";

	public override GameObject Get(Args args) {
		NPCIdentity self = m_source.Get<NPCIdentity>(args);
		if (self == null) return null;

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

		if (closestActor == null) return null;

		return closestActor.gameObject;
	}

	public override string String => $"{this.m_source}'s nearest available actor";
}
