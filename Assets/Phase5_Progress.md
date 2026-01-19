# Phase 5 Progress Update 🎯

## ✅ 완료된 기능

### 1. Effect System
- IEffect 인터페이스
- EffectManager
- BasicEffects (Damage, Heal, BuffAttack, BuffDefense)

### 2. Skill System
- SkillType enum (Active/Passive)
- DiceRequirement (패턴, 값 조건)  
- SkillData 확장 (Effects 리스트)

### 3. Character 스킬 관리
- `stats.ActiveSkills` / `PassiveSkills`
- `UseSkillByIndex()`
- 패시브 자동 적용

### 4. **Tile Effect System** ✨ NEW
- **TileEffect** 데이터:
  - Heal, Damage, BuffAttack, BuffDefense, LevelUp
- **TileData.ApplyEffects()**: 캐릭터 도착 시 자동 적용
- **Character**: 이동 완료 시 타일 효과 자동 실행

---

## 🎮 사용 예시

### 회복 타일 설정 (Unity Inspector):
```
TileData:
  Effects:
    - Type: Heal
      Value: 5
      Description: "Heal 5 HP"
```

### 함정 타일:
```
TileData:
  Effects:
    - Type: Damage
      Value: 3
      Description: "Take 3 damage"
```

### 버프 타일:
```
TileData:
  Effects:
    - Type: BuffAttack
      Value: 2
      Description: "+2 ATK"
```

---

## 📋 다음 단계

### Phase 5.5: Lap Counter & 자동 레벨업
- OrbitManager에 시작 타일 표시
- Character에 완주 카운터
- 완주 시 자동 레벨업 or 스킬 선택

### Phase 5.6: Monster Skill System
- MonsterSkill 데이터
- 조건부 스킬 사용
- Effect 기반 몬스터 스킬

---

**진행률: 4/7 완료 (57%)**
