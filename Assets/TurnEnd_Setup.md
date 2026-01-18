# 턴 종료 버튼 설정 가이드

## 🎮 Turn End 버튼 추가

### UI 구조

**DicePanel 또는 UI Root에:**

1. **UI → Button** 생성 → 이름: "EndTurnButton"
2. **Position**: Roll Dice 버튼 옆 또는 아래
3. **텍스트**: "End Turn" 또는 "턴 종료"

---

### TurnManager 연결

**GameManager GameObject:**

1. **TurnManager** 컴포넌트 확인
2. **Inspector 설정**:
   - Roll Dice Button: RollDiceButton 연결
   - **End Turn Button**: EndTurnButton 연결
   - Turn Count Text: TurnText (있으면)

---

## 🎯 게임 플로우 (업데이트)

### 플레이어 턴
1. **"Roll Dice"** 클릭 → 주사위 생성
2. Roll Dice 버튼 **비활성화**
3. **"End Turn"** 버튼 **활성화**
4. 주사위 사용 (캐릭터 선택, 이동/스킬)
5. **"End Turn"** 클릭

### 몬스터 턴 (자동)
1. CombatManager.ExecuteMonsterTurn() 호출
2. 모든 몬스터가 의도 실행
3. 1초 후 자동으로 다음 플레이어 턴

---

## ⚔️ 기본 스킬

**Character 컴포넌트:**
- 스킬이 없으면 자동으로 "Basic Attack" 추가
- Damage Multiplier: 1
- Target Type: Single Enemy

**Inspector에서 스킬 추가 가능:**
1. Character → Skills → Size: 2
2. Element 0: Basic Attack (기본)
3. Element 1: 
   - Skill Name: "Fireball"
   - Target Type: All Enemies
   - Damage Multiplier: 1
   - Min Dice Value: 4

---

## ✅ 테스트

1. **Play**
2. **Roll Dice** → 주사위 생성
3. 캐릭터 클릭 → **Skill** 선택 → 몬스터 공격!
4. **End Turn** 클릭
5. **Console 확인**:
   ```
   === Player Turn End ===
   === Monster Turn ===
   Slime attacks Hero for 8 damage!
   === Turn 2 - Player Turn ===
   ```

---

**이제 턴이 정상적으로 진행됩니다!** 🎉
