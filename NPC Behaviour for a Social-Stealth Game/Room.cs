using MalbersAnimations;
using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;
namespace GameCreator.Runtime.Perception {
	public class Room : MonoBehaviour {
		[SerializeField] public string m_roomName; // For debugging purposes
		[SerializeField] protected NPCRoleMask m_allowedRoles; // Which roles are considered permitted by other NPCs in this room.

		HashSet<NPCIdentity> m_occupants { get; } = new HashSet<NPCIdentity>();

		public bool IsNPCTrespassing(NPCIdentity identity/*, defeatDisguise = false*/) {
			// Compare flags. If allowance is set to 0/None, that means nobody's welcome.
			return ((int)identity.GetRole() & (int)m_allowedRoles) == 0;
		}

		private void OnTriggerEnter(Collider other) {
			if (other.TryGetComponent(out NPCIdentity identity)) {
				if (m_occupants.Add(identity)) {
					identity.OnRoomTransition(this, true);
				}
			}
		}

		private void OnTriggerExit(Collider other) {
			if (other.TryGetComponent(out NPCIdentity identity)) {
				if (m_occupants.Remove(identity)) {
					identity.OnRoomTransition(this, false);
				}
			}
		}
	}
}