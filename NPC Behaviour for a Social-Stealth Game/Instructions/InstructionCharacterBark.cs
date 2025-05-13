using System;
using System.Threading.Tasks;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace GameCreator.Runtime.VisualScripting {
	[Version(0, 1, 1)]

	[Title("Bark Dialogue")]
	[Description("Sets the dialogue for a character's Bark component")]

	[Category("Characters/Dialogue/Bark")]

	[Parameter("Barker", "The character that barks the dialogue (must have the component)")]
	[Parameter("Duration", "The base duration for the dialogue in seconds")]
	//[Parameter("DurationPerCharacter", "The additional duration for the dialogue in seconds per char")]

	[Keywords("Characters", "Dialogue", "Dialog", "Speak", "Bark")]
	[Image(typeof(IconVolume), ColorTheme.Type.Green)]

	[Serializable]
	public class InstructionCharacterBark : Instruction {
		[SerializeField] private PropertyGetGameObject m_Barker = GetGameObjectNone.Create();

		[Space]
		[SerializeField] private PropertyGetString m_Dialogue = GetStringString.Create;
		[Space]
		[SerializeField] private PropertyGetDecimal m_DurationBase = new PropertyGetDecimal(1.0f);
		//[SerializeField] private PropertyGetDecimal m_DurationPerCharacter = new PropertyGetDecimal(0.0f);

		public override string Title => $"{this.m_Barker} barks {this.m_Dialogue}";

		protected override Task Run(Args args) {
			NPCBarker barker = this.m_Barker.Get<NPCBarker>(args);
			if (barker == null) return DefaultResult;

			float duration = (float)this.m_DurationBase.Get(args);
			float durationPerChar = 0.0f;//(float)this.m_DurationPerCharacter.Get(args);
			string dialogue = (string)this.m_Dialogue.Get(args);

			NPCBarker.BarkDialogue bark = new NPCBarker.BarkDialogue(dialogue, duration + (dialogue.Length * durationPerChar));
			barker.Bark(bark);
			return DefaultResult;
		}
	}
}