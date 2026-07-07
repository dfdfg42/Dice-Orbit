using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>런 저장 데이터 — 노드 단위 스냅샷 (전투 중 저장 없음).</summary>
    [System.Serializable]
    public class RunSaveData
    {
        public int Seed;                       // 같은 시드 → MapGenerator가 같은 맵 재생성
        public int CurrentNodeId = -1;
        public List<int> VisitedNodeIds = new List<int>();
        public int BattlesCleared;

        public int Gold;
        public List<string> RelicNames = new List<string>();
        public List<string> PotionNames = new List<string>();
        public List<string> BanishedPresetNames = new List<string>();
        public List<CharacterSave> Party = new List<CharacterSave>();
    }

    [System.Serializable]
    public class CharacterSave
    {
        public string PresetName;
        public int CurrentHp;
        public int MaxHp;
        public int RevivalStock;
        public List<string> ModifierNames = new List<string>();
    }

    /// <summary>
    /// 런 저장/불러오기 (이어하기). 로그라이크 정석:
    /// 맵 화면 진입마다 자동 저장 → 사망/승리/새 게임 시 삭제.
    /// SO/모디파이어는 이름 매칭으로 복원 (풀·레지스트리가 원본 소스).
    /// </summary>
    public static class RunSaveService
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        public static bool HasSave() => File.Exists(SavePath);

        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }

        public static RunSaveData Load()
        {
            try
            {
                if (!HasSave()) return null;
                return JsonUtility.FromJson<RunSaveData>(File.ReadAllText(SavePath));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RunSave] 세이브 로드 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>현재 런 상태를 스냅샷해 저장. 런이 없으면 no-op.</summary>
        public static void SaveCurrent()
        {
            var run = RunManager.Instance;
            if (run == null || !run.RunActive) return;

            var data = new RunSaveData
            {
                Seed = run.CurrentSeed,
                CurrentNodeId = run.CurrentNode?.Id ?? -1,
                BattlesCleared = run.BattlesCleared,
                Gold = GoldManager.Instance?.Gold ?? 0,
            };

            foreach (var node in run.Map.Nodes)
                if (node.Visited) data.VisitedNodeIds.Add(node.Id);

            data.BanishedPresetNames.AddRange(run.BanishedNames);

            if (RelicManager.Instance != null)
                data.RelicNames.AddRange(RelicManager.Instance.Owned.Where(r => r != null).Select(r => r.RelicName));

            if (PotionManager.Instance != null)
                data.PotionNames.AddRange(PotionManager.Instance.Slots.Where(p => p != null).Select(p => p.PotionName));

            var party = PartyManager.Instance?.Party;
            if (party != null)
            {
                foreach (var ch in party)
                {
                    if (ch == null || ch.Stats == null) continue;
                    var save = new CharacterSave
                    {
                        PresetName = ch.Stats.SourcePreset != null ? ch.Stats.SourcePreset.CharacterName : ch.Stats.CharacterName,
                        CurrentHp = ch.Stats.CurrentHP,
                        MaxHp = ch.Stats.MaxHP,
                        RevivalStock = ch.Stats.RevivalStock,
                    };
                    var mods = ch.Stats.Modifiers?.Modifiers;
                    if (mods != null)
                        save.ModifierNames.AddRange(mods.Where(m => m != null).Select(m => m.ModifierName));
                    data.Party.Add(save);
                }
            }

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Debug.Log($"[RunSave] 저장됨 — 노드 {data.CurrentNodeId}, 파티 {data.Party.Count}명");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RunSave] 저장 실패: {ex.Message}");
            }
        }
    }
}
