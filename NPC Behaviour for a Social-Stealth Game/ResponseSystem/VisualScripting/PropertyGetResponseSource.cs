using System;
using System.Collections.Generic;
using GameCreator.Runtime.Behavior;
using GameCreator.Runtime.Common;
using GameCreator.Runtime.Perception;
using GameCreator.Runtime.ResponseSystem;
using UnityEngine;

[Title("Response Source")]
[Category("Responses/Response Source")]

[Description("Returns the nearest NPC Actor/GameObject that triggered this response")]
[Image(typeof(IconProcessor), ColorTheme.Type.Green)]
[Serializable]
public class PropertyGetResponseSource : PropertyTypeGetGameObject {
	[SerializeField] private PropertyGetGameObject m_response = GetGameObjectInstance.Create();

	public override GameObject Get(Args args) {
		NPC_Response self = m_response.Get<NPC_Response>(args);
		if (self == null) return null;

		return self.Source;
	}

	public override string String => $"{this.m_response}'s source";
}
