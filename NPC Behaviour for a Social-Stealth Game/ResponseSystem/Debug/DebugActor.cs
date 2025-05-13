using GameCreator.Runtime.ResponseSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebugActor : MonoBehaviour {
	[SerializeField] NPC_ResponseActor actorDebug;
	[SerializeField] TextMeshProUGUI debugTextBox;
	[SerializeField] Vector2 screenSpaceOffset = new Vector2(-5, 0);

	Camera mainCam;
	private void Start() {
		mainCam = Camera.main;
	}

	void Update() {
		debugTextBox.text = actorDebug.DebugString;

		Vector2 screenSpacePos = mainCam.WorldToScreenPoint(actorDebug.transform.position);
		screenSpacePos += screenSpaceOffset;
		debugTextBox.rectTransform.position = screenSpacePos;
	}
}
