# 캐릭터 선택 후 게임 시작 플로우 ✅

## 🎮 동작 순서

1. **캐릭터 선택**
   - 4개 중 1개 선택

2. **캐릭터 생성**
   - GameObject 생성
   - 시작 타일에 배치
   - PartyManager 등록

3. **UI 전환**
   - Character Selection Canvas **비활성화**
   - Combat UI 활성화

4. **게임 시작**
   - CombatManager.StartCombat()
   - TurnManager.StartPlayerTurn()
   - 주사위 자동 굴림

---

## 🔧 수정 사항

### CharacterSelectionUI.cs
```csharp
private void OnCharacterSelected(CharacterPreset character)
{
    CreatePlayerCharacter(character);
    gameObject.SetActive(false);  // ← UI 비활성화
    GameFlowManager.Instance.OnCharacterSelected();
}
```

### GameFlowManager.cs
```csharp
private void StartCombat()
{
    CombatManager.Instance.StartCombat();
    TurnManager.Instance.StartPlayerTurn(); // ← 턴 시작
}
```

---

## ✅ 완료!

이제 캐릭터 선택 후:
- Selection Canvas 자동 숨김
- 캐릭터 시작 타일 배치
- 전투 시작
- 주사위 자동 굴림
