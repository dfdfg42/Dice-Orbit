using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DiceOrbit.UI
{
	/// <summary>
	/// 캐릭터 선택 UI (Legacy Scene Support)
	/// - RecruitUI가 씬에 없을 때 임시 fallback으로 사용
	/// </summary>
	public class CharacterSelectionUI : MonoBehaviour
	{
		[Header("Character Pool")]
		[SerializeField] private List<Data.CharacterPreset> allCharacters = new List<Data.CharacterPreset>();
		[SerializeField] private int numberOfChoices = 4;

		[Header("UI References")]
		[SerializeField] private Transform cardContainer;
		[SerializeField] private GameObject characterCardPrefab;
		[SerializeField] private Canvas selectionCanvas;

		[Header("Prefabs (Spawning)")]
		[SerializeField] private GameObject characterUIPrefab; // CharacterUI 프리팹

		private void Start()
		{
			// 기본 씬에서는 GameFlowManager에서 Show 호출
		}

		public void Show()
		{
			if (selectionCanvas != null)
			{
				selectionCanvas.gameObject.SetActive(true);
				selectionCanvas.transform.localScale = Vector3.one;
			}

			RefreshUI();
		}

		public void Hide()
		{
			if (selectionCanvas != null)
			{
				selectionCanvas.gameObject.SetActive(false);
			}
		}

		private void RefreshUI()
		{
			if (allCharacters == null || allCharacters.Count == 0)
			{
				Debug.LogWarning("[CharacterSelectionUI] Character list is empty.");
				return;
			}

			if (cardContainer == null || characterCardPrefab == null)
			{
				Debug.LogWarning("[CharacterSelectionUI] Missing cardContainer or characterCardPrefab.");
				return;
			}

			foreach (Transform child in cardContainer) Destroy(child.gameObject);

			var options = allCharacters.OrderBy(_ => Random.value)
									  .Take(Mathf.Min(numberOfChoices, allCharacters.Count))
									  .ToList();

			foreach (var preset in options)
			{
				var cardObj = Instantiate(characterCardPrefab, cardContainer);
				var card = cardObj.GetComponent<CharacterCard>();
				if (card != null)
				{
					card.Setup(preset, OnCharacterSelected);
				}
			}
		}

		private void OnCharacterSelected(Data.CharacterPreset preset)
		{
			Debug.Log($"[CharacterSelectionUI] Selected: {preset.CharacterName}");

			CreatePlayerCharacter(preset);

			if (Core.GameFlowManager.Instance != null)
			{
				Core.GameFlowManager.Instance.OnRecruitComplete();
			}

			Hide();
		}

		private void CreatePlayerCharacter(Data.CharacterPreset preset)
		{
			if (preset == null) return;

			var orbitManager = Object.FindFirstObjectByType<Core.OrbitManager>();
			if (orbitManager == null) return;

			var startTile = orbitManager.GetTile(0);
			var characterObj = new GameObject($"Player_{preset.CharacterName}");
			characterObj.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

			var sr = characterObj.AddComponent<SpriteRenderer>();
			if (preset.CharacterSprite != null) sr.sprite = preset.CharacterSprite;
			sr.color = preset.SpriteColor;

			var character = characterObj.AddComponent<Core.Character>();
			var collider = characterObj.AddComponent<BoxCollider>();
			FitColliderToSprite(collider, sr, characterObj.transform);

			var stats = preset.CreateStats();
			character.InitializeStats(stats);

			if (startTile != null)
			{
				characterObj.transform.position = startTile.Position + new Vector3(0, 1.5f, 1.0f);
			}

			if (Core.PartyManager.Instance != null)
			{
				Core.PartyManager.Instance.AddCharacter(character);
			}

			if (characterUIPrefab != null)
			{
				var uiObj = Instantiate(characterUIPrefab, characterObj.transform);
				uiObj.transform.localPosition = new Vector3(0, 1.2f, 0);
				var ui = uiObj.GetComponent<CharacterUI>();
				if (ui != null) ui.SetCharacter(character);
			}
		}

		private void FitColliderToSprite(BoxCollider collider, SpriteRenderer renderer, Transform target)
		{
			if (collider == null || renderer == null || renderer.sprite == null || target == null) return;

			var bounds = renderer.bounds;
			var localSize = new Vector3(
				bounds.size.x / target.localScale.x,
				bounds.size.y / target.localScale.y,
				0.1f
			);

			collider.size = localSize;
			collider.center = new Vector3(0f, localSize.y * 0.5f, 0f);
		}
	}
}
