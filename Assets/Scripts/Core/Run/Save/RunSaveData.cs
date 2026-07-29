using System.Collections.Generic;

namespace DiceOrbit.Core.Run.Save
{
    /// <summary>
    /// 런 저장 데이터 v2 — 노드 단위 스냅샷 (전투 중 저장 없음).
    /// 각 필드의 주인 참가자는 하나로 고정한다. 두 참가자가 같은 필드를 건드리지 않는다.
    /// </summary>
    [System.Serializable]
    public class RunSaveData
    {
        public int Version = 2;

        public RunProgressSave Progress = new RunProgressSave();                  // 주인: RunManager
        public int Gold;                                                          // 주인: GoldManager
        public List<ArtifactSaveData>  Artifacts = new List<ArtifactSaveData>();  // 주인: ArtifactManager
        public List<PotionSaveData>    Potions   = new List<PotionSaveData>();    // 주인: PotionManager
        public List<CharacterSaveData> Party     = new List<CharacterSaveData>(); // 주인: PartyManager
    }

    [System.Serializable]
    public class RunProgressSave
    {
        public int Seed;                    // 같은 시드 → MapGenerator가 같은 맵 재생성
        public int CurrentNodeId = -1;
        public List<int> VisitedNodeIds = new List<int>();
        public int BattlesCleared;

        // 엔티티 인스턴스가 아니라 집합 소속 여부라 부가 상태가 붙을 자리가 없다 → string 그대로.
        public List<string> BanishedPresetIds = new List<string>();
    }

    // 지금은 Id 하나뿐이어도 래퍼를 둔다. RuntimeArtifact가 상태(발동 횟수, 충전 등)를 갖게 되면
    // List<string> → List<...> 전환은 저장·복원 양쪽을 다시 쓰는 일이 되지만, 래퍼가 있으면
    // 필드 한 줄 추가로 끝난다. JsonUtility는 List<중첩 [Serializable] 클래스>를 문제없이 다룬다.

    [System.Serializable] public class ArtifactSaveData { public string Id; }   // ArtifactData.SaveId
    [System.Serializable] public class PotionSaveData   { public string Id; }   // Potion.SaveId
    [System.Serializable] public class ModifierSaveData { public string Id; }   // 모디파이어 클래스 타입명

    [System.Serializable]
    public class CharacterSaveData
    {
        public string PresetId;                                                // CharacterPreset.SaveId
        public int CurrentHp;
        public int MaxHp;
        public int RevivalStock;
        public List<ModifierSaveData> Modifiers = new List<ModifierSaveData>();
    }
}
