using System;
using System.Collections.Generic;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.VisualScripting;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

namespace GameCreator.Runtime.Perception {
	public enum NPCRole {
		Unknown = 1 << 0,
		Civilian = 1 << 1,
		Security = 1 << 2,
		Science = 1 << 3
	};

	[Flags]
	public enum NPCRoleMask {
		None = 0,                                   // As this is a flag, we can't use it in a bitwise AND operation. This just indicates ALL other fields are absent
		Unknown = NPCRole.Unknown,
		Civilian = NPCRole.Civilian,
		Security = NPCRole.Security,
		Science = NPCRole.Science
	};

	public enum NPCStatus {
		Unknown,
		Alive,
		Deceased
	};

	public class NPCIdentity : MonoBehaviour {
		// Property
		[SerializeField] protected string m_name = "Andy";
		[SerializeField] protected Character m_character = null;
		[SerializeField] protected NPCRole m_role = NPCRole.Unknown;
		//[SerializeField] protected NPCRole? m_rolePerceptionOverride = null;
		[SerializeField] protected Evidence m_trespassEvidence;

		// Fields
		protected HashSet<Room> m_currentRooms = new HashSet<Room>(); // All rooms we currently occupy
		public bool m_isTrespassing { get { return m_trespassEvidence.isActiveAndEnabled; } }

		private NPCIdentity m_nearestTeamMember = null;

		//public NPCRole GetPerceivedRole(bool defeatDisguise = false) {
		//	if (defeatDisguise) {
		//		return m_role;
		//	}
		//
		//	return m_rolePerceptionOverride ?? m_role;
		//}

		public NPCRole GetRole() { return m_role; }
		public Character GetCharacter() { return m_character; }
		public string GetName() { return m_name; }

		public NPCIdentity GetNearestTeamMember() { return m_nearestTeamMember; }

		private void Start() {
			if(m_trespassEvidence != null) m_trespassEvidence.enabled = false;
		}

		private void OnEnable() {
			NPCIdentityManager.Instance.RegisterNPC(this);
		}

		private void OnDisable() {
			NPCIdentityManager.Instance.UnregisterNPC(this);
		}

		public bool HasTeamMemberNearby(float distance) {
			if (m_nearestTeamMember) {
				return Vector3.Distance(m_nearestTeamMember.transform.position, transform.position) < distance;
			}
			return false;
		}

		// === Room Methods =========

		public void OnRoomTransition(Room room, bool entering) {
			bool changed = false;
			if (entering) {
				changed = m_currentRooms.Add(room);
			} else {
				m_currentRooms.Remove(room);
			}

			if (changed) {
				CheckTrepassing();
			}
		}

		public void CheckTrepassing() {
			bool isTrespassing = false;
			foreach (Room room in m_currentRooms) {
				if(room.IsNPCTrespassing(this)) { isTrespassing = true; break; }
			}

			// Check value's changed, if so:
			if (isTrespassing != m_isTrespassing) {
				OnTrespassChange(isTrespassing);
			}
		}

		void OnTrespassChange(bool isTrespassing) {
			// Create component if needed. Otherwise, init + enable or disable based on whether we are trespassing
			if (isTrespassing) {
				if (!m_trespassEvidence) {
					Debug.LogError("No Trespass Evidence exists for character " + name);
				}
				//m_trespassEvidence.Initialize(this);
				m_trespassEvidence.enabled = true;
			} else {
				m_trespassEvidence.enabled = false;
			}

			if (isTrespassing != m_isTrespassing) {
				Debug.LogError("Oh no... isTrespassing didn't match the existence of evidence");
			}
		}
	}
}