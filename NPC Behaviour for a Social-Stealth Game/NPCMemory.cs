/*using GameCreator.Runtime.Perception;
using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using GameCreator.Runtime.Characters;
using Unity.VisualScripting;
using System.Security.Cryptography;

// MOVE TO CORTEX or PERCEPTION!!

namespace DELETEME {
	public enum MemoryTerm {
		Short = 1 << 0,
		Long = 1 << 1,
		Permanent = 1 << 2,
		Invalid = 1 << 3
	}

	public enum MemoryTermIndex {
		Short = 0,
		Long = 1,
		Permanent = 2,
		LAST = Permanent,
		FIRST = 0,
		COUNT = LAST + 1,
		Invalid = -1 // should never get this
	}

	[Flags]
	public enum MemoryTermMask {
		None = 0,
		Short = MemoryTerm.Short,
		Long = MemoryTerm.Long,
		Permanent = MemoryTerm.Permanent
	}

	public class NPCMemory : MonoBehaviour {
		// === Statics ===================
		static MemoryTerm TermFromIndex(MemoryTermIndex idx) { return (MemoryTerm)(1 << (int)idx); }
		static MemoryTermIndex IndexFromTerm(MemoryTerm term) { return (MemoryTermIndex)(int)Math.Log((int)term, 2); }

		// === Subclasses ================
		public class TermMemory {
			public int m_memoryLimit = -1; // If less than zero, treat as infinite

			public TermMemory(int memoryLimit = -1) {
				m_memoryLimit = memoryLimit;
			}

			List<ClueMemory> m_memories = new List<ClueMemory>();

			public ClueMemory RecallMemory(Clue clue) {
				return m_memories.Find(x => x.IsMatch(clue));
			}

			public ClueMemory ForgetClue(Clue clue) {
				ClueMemory storedMemory = RecallMemory(clue);
				if (storedMemory != null) {
					m_memories.Remove(storedMemory);
				}
				return storedMemory;
			}

			public void ForgetMemory(ClueMemory clue) {
				m_memories.Remove(clue);
			}

			public ClueMemory GetOldestMemory() {
				if(m_memories.Count == 0) { return null; }

				float earliestTimestamp = Mathf.Infinity;
				ClueMemory oldestMemory = null;
				foreach(ClueMemory mem in m_memories) {
					if (mem.m_timeStamp < earliestTimestamp) {
						earliestTimestamp = mem.m_timeStamp;
						oldestMemory = mem;
					}
				}
				return oldestMemory;
			}

			public ClueMemory GetNewestMemory() {
				if (m_memories.Count == 0) { return null; }

				float mostRecentTimestamp = Mathf.NegativeInfinity;
				ClueMemory newestMemory = null;
				foreach (ClueMemory mem in m_memories) {
					if (mem.m_timeStamp > mostRecentTimestamp) {
						mostRecentTimestamp = mem.m_timeStamp;
						newestMemory = mem;
					}
				}
				return newestMemory;
			}

			public ClueMemory RemoveOldestMemory() {
				ClueMemory oldest = GetOldestMemory();
				if (oldest == null) { return null; }

				m_memories.Remove(oldest);
				return oldest;
			}

			// Remove any memories over a certain age and return them
			public bool PopExpiredMemories(float currentTime, float memoryLifetime, out List<ClueMemory> expiredMemories) {
				expiredMemories = new List<ClueMemory>();
				for (int i = m_memories.Count; i > 0; --i) {
					ClueMemory mem = m_memories[i];
					if ((currentTime - mem.m_timeStamp) > memoryLifetime) {
						expiredMemories.Add(mem);
						m_memories.RemoveAt(i);
					}
				}
				return expiredMemories.Count > 0;
			}

			public void Remember(Clue clue) {
				ClueMemory clueMemory = ClueMemoryFactory.CreateMemory(clue); // Timestamp will be set automatically to current time
				Remember(clueMemory);
			}

			public void Remember(ClueMemory clueMemory) {
				if (clueMemory != null) {
					clueMemory.m_timeStamp = Time.time; // Update timestamp as things move between terms
					m_memories.Add(clueMemory);

					if (m_memoryLimit > 0 && m_memories.Count > m_memoryLimit) {
						RemoveOldestMemory();
					}
				} else {
					Debug.LogError($"Failed to remember Clue Memory {clueMemory}");
				}
			}
		};


		// === Properties =================
		[SerializeField] int m_longTermMemoryLength = 5;
		[SerializeField] float m_shortTermCheckInterval = 2.0f;	// Every X seconds, move any expired memories out of short term
		[SerializeField] float m_shortTermMemoryLifetime = 10.0f;       // How long each short term memory lasts

		// === Fields =====================
		const float kUpdateHandlingInterval = 1.0f;
		TermMemory[] m_termMemory = new TermMemory[(int)MemoryTermIndex.COUNT];
		float m_lastShortTermDecay = 0.0f;
		float m_lastHandlingUpdate = 0.0f;
		Clue m_currentlyHandlingClue = null;
		MemoryTerm m_currentlyHandlingClueOfTerm = MemoryTerm.Invalid;
		
		bool m_canHandleNewClues = true;
		bool m_ignoreAllClues = false;

		// When the memory system orders the behaviour system to start dealing with a clue
		public event Action<GameObject> m_eventOnStartHandlingNewClue;
		public event Action<GameObject> m_eventOnStartHandlingExistingClueShortTerm;
		public event Action<GameObject> m_eventOnStartHandlingExistingClueLongTerm;
		public event Action<GameObject> m_eventOnStartHandlingExistingCluePermanentTerm;
		public event Action<GameObject> m_eventOnFinishHandlingClue;
		public event Action<GameObject> m_eventOnPerceiveHandledClue; // seeing a clue being handled by someone else

		private void Start() {
			m_termMemory[(int)MemoryTermIndex.Short] = new TermMemory();						// Recently handled clues (decays into long- or permanent-term)
			m_termMemory[(int)MemoryTermIndex.Long] = new TermMemory(m_longTermMemoryLength);	// Clues we handled a long time ago; can be forgotten
			m_termMemory[(int)MemoryTermIndex.Permanent] = new TermMemory();					// Clues we handled a long time ago; cannot be forgotten
		}

		private void Update() {
			if (Time.time - m_lastShortTermDecay > m_shortTermCheckInterval) {
				m_lastShortTermDecay = Time.time;
				DecayShortTermMemory();
			}

			if (Time.time - m_lastHandlingUpdate > kUpdateHandlingInterval) {
				m_lastHandlingUpdate = Time.time;
				CheckHandling();
			}
		}

		// === Memory Management ===========
		void DecayShortTermMemory() {
			if (m_termMemory[(int)(MemoryTermIndex.Short)].PopExpiredMemories(Time.time, m_shortTermMemoryLifetime, out List<ClueMemory> poppedMemories)) {
				Debug.Log($"{poppedMemories.Count} memories have decayed from Short to Long/Permanent.");

				foreach (ClueMemory decayedMemory in poppedMemories) {
					if (decayedMemory.IsUnforgettable()) {
						// Cannot be forgotten; move to permanent store
						m_termMemory[(int)MemoryTermIndex.Permanent].Remember(decayedMemory);
					} else {
						// Can be forgotten; move to long term memory
						m_termMemory[(int)MemoryTermIndex.Permanent].Remember(decayedMemory);
					}
				}
			}
		}


		ClueMemory GetClueMemory(Clue clue, out MemoryTerm term) {
			for (MemoryTermIndex idx = MemoryTermIndex.FIRST; idx < MemoryTermIndex.COUNT; ++idx) {
				ClueMemory memory = m_termMemory[(int)idx].RecallMemory(clue);
				if (memory != null) {
					term = TermFromIndex(idx);
					return memory;
				}
			}

			term = MemoryTerm.Invalid;
			return null;
		}

		// Searches in a specific term-memory
		ClueMemory GetClueMemoryInTerm(Clue clue, MemoryTerm term) {
			return m_termMemory[(int)IndexFromTerm(term)].RecallMemory(clue);
		}

		public void OnClueDetected(Clue clue) {
			// todo check priority to see if we can interrupt?

			// Check if we can even detect new clues right now
			if(m_ignoreAllClues) return;

			// Check the clue against the current clue being handled
			if (m_currentlyHandlingClue != null) {
				Debug.Log($"{gameObject.name} ignored a clue due to already dealing with another!");
				return; // Skip if we're dealing with something important already
			}

			// Then, check if the clue is being handled by someone else
			if (clue.m_currentHandler != null) {
				Debug.Log($"{gameObject.name} ignored a clue being handled by someone else; sending event.");
				m_eventOnPerceiveHandledClue?.Invoke(clue.gameObject);
				return;
			}


			// If we're not busy:
			// First, double check if this clue is known to us already:
			ClueMemory memory = GetClueMemory(clue, out MemoryTerm termLocation);

			if (memory != null) {
				Debug.Log($"Detected a clue that was already in memory! (term:{termLocation})");

				// Update our current clue
				m_currentlyHandlingClue = clue;
				m_currentlyHandlingClueOfTerm = termLocation;
				m_termMemory[(int)IndexFromTerm(termLocation)].ForgetMemory(memory);

				OnCurrentClueChanged();
				// todo check priority here...

			} else {
				// Clue isn't known to us yet
				// First check if it's currently being handled by another individual
				if(clue)

				// todo check priority here...

			}

			//Check if it's being handled by someone else
			//if (trespassClue.m_currentHandler != null) { return; }
		}

		// === Clue Handling ===============
		public void OnCurrentClueHandled() {
			TermMemory shortMemory = m_termMemory[(int)MemoryTermIndex.Short];

			// Move it to short term
			shortMemory.Remember(m_currentlyHandlingClue);

			m_eventOnFinishHandlingClue?.Invoke(m_currentlyHandlingClue.gameObject);

			m_currentlyHandlingClue = null;
		}

		void OnCurrentClueChanged() {
			if (m_canHandleNewClues) {
				switch (m_currentlyHandlingClueOfTerm) {
					case MemoryTerm.Invalid:
						// Not in memory
						m_eventOnStartHandlingNewClue?.Invoke(m_currentlyHandlingClue.gameObject);
						break;
					case MemoryTerm.Short:
						m_eventOnStartHandlingExistingClueShortTerm?.Invoke(m_currentlyHandlingClue.gameObject);
						break;
					case MemoryTerm.Long:
						m_eventOnStartHandlingExistingClueLongTerm?.Invoke(m_currentlyHandlingClue.gameObject);
						break;
					case MemoryTerm.Permanent:
						m_eventOnStartHandlingExistingCluePermanentTerm?.Invoke(m_currentlyHandlingClue.gameObject);
						break;
				}
			}
		}

		void CheckHandling() {
			// If our current clue is set and we can now handle clues
			if (m_currentlyHandlingClue != null && m_canHandleNewClues) {
				
			}
		}
	}

	public class ClueMemoryFactory {
		public static ClueMemory CreateMemory(Clue clue) {
			switch (clue) {
				case ClueTrespass:
					return new ClueMemoryTrespass(clue as ClueTrespass);
				default:
					Debug.LogError("Tried to create a memory of a base-type clue!! CLUE IS ABSTRACT!!");
					return null;
			}
		}
	}

	public abstract class ClueMemory {
		//protected int m_priority;
		protected bool m_wasHandled;
		public float m_timeStamp;

		public ClueMemory(Clue clue) {
			//m_priority = clue.GetPriority();
			m_wasHandled = false;
			m_timeStamp = Time.time;
		}

		public abstract bool IsMatch(Clue referenceClue);

		// Whether this memory should be moved to Permanent memory after falling out of Long Term
		public virtual bool IsUnforgettable() { return false; }
	}

	public class ClueMemoryTrespass : ClueMemory {
		public Character m_character { get; protected set; }

		public ClueMemoryTrespass(ClueTrespass clue) : base(clue) {
			m_character = clue.m_associatedNPC.GetCharacter();
		}

		override public bool IsMatch(Clue referenceClue) {
			ClueTrespass otherTrespass = referenceClue as ClueTrespass;
			if (otherTrespass != null) {
				return m_character == otherTrespass.m_associatedNPC.GetCharacter();
			}
			return false;
		}
	}
}*/