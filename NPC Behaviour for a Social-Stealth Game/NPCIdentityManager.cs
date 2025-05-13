using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	public class NPCIdentityManager {
		private readonly static NPCIdentityManager m_Instance = new NPCIdentityManager();

		//private NPCIdentityManager() { }

		public static NPCIdentityManager Instance => m_Instance;

		private HashSet<NPCIdentity> m_registeredNPCs = new HashSet<NPCIdentity>();

		public HashSet<NPCIdentity> RegisteredNPCs => m_registeredNPCs;

		public void RegisterNPC(NPCIdentity npc) {
			if (m_registeredNPCs.Add(npc)) {
				Debug.Log($"Registered NPC {npc.GetName()} ({npc.gameObject.name})");
			}
		}

		public void UnregisterNPC(NPCIdentity npc) {
			if (m_registeredNPCs.Remove(npc)) {
				Debug.Log($"Unregistered NPC {npc.GetName()} ({npc.gameObject.name})");
			}
		}

		public NPCIdentity GetClosestNPCTo(NPCIdentity npc, NPCRoleMask roles) {
			if(npc == null) return null;

			float nearestDistance = Mathf.Infinity;
			NPCIdentity closest = null;
			foreach (NPCIdentity n in m_registeredNPCs) {
				if (n == npc) continue;
				NPCRole role = n.GetRole();
				if(((int)roles & (int)role) != (int)role) {
					continue;
				}

				float dist = Vector3.Distance(npc.transform.position, n.transform.position);
				if(dist < nearestDistance) {
					closest = n; nearestDistance = dist;
				}
			}

			return closest;
		}

		public NPCIdentity GetClosestNPCWithinDistance(NPCIdentity self, float distance, NPCRoleMask roles) {
			NPCIdentity closest = GetClosestNPCTo(self, roles);
			
			if(closest == null) return null;

			if (Vector3.Distance(closest.transform.position, self.transform.position) <= distance) {
				return closest;
			}

			return null;
		}

		public bool HasNPCWithinDistance(NPCIdentity self, float distance, NPCRoleMask roles) {
			return GetClosestNPCWithinDistance(self, distance, roles) != null;
		}
	}
}