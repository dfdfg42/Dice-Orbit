using System.Collections.Generic;
using DiceOrbit.Core.Run.Save;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 런(한 판) 상태의 단일 출처: 현재 막, 맵, 현재 위치, 진행 카운터.
    /// GameFlowManager가 노드 타입에 따라 상태 전환을 결정할 때 이걸 읽는다.
    ///
    /// 씬에 배치하고 firstAct를 지정 (없으면 노드맵 비활성 → 기존 순차 웨이브 흐름 폴백).
    /// </summary>
    public class RunManager : MonoBehaviour, IRunSaveParticipant
    {
        public static RunManager Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private ActDefinition firstAct;
        [Tooltip("0이면 랜덤 시드")]
        [SerializeField] private int seed = 0;

        public ActDefinition CurrentAct { get; private set; }
        public MapGraph Map { get; private set; }
        public bool RunActive => Map != null;

        /// <summary>현재 서 있는 노드 (-1 = 아직 출발 전).</summary>
        public MapNode CurrentNode => _currentNodeId >= 0 ? Map?.Get(_currentNodeId) : null;

        /// <summary>클리어한 전투류 노드 수 — 자동 모집 판단(스펙 §3: 노드 1·2 클리어 후 모집)에 사용.</summary>
        public int BattlesCleared { get; private set; }

        /// <summary>이번 런의 맵 시드 (세이브 → 같은 맵 재생성).</summary>
        public int CurrentSeed { get; private set; }

        private int _currentNodeId = -1;

        // 상점 교체로 내보낸 캐릭터 — 이번 런에서 재영입 불가 (스펙 §3: 리롤 세탁 방지)
        private readonly List<CharacterPreset> _banishedPresets = new List<CharacterPreset>();

        public void RegisterBanished(CharacterPreset preset)
        {
            if (preset != null && !_banishedPresets.Contains(preset))
                _banishedPresets.Add(preset);
        }

        public bool IsBanished(CharacterPreset preset) => preset != null && _banishedPresets.Contains(preset);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 런 수명 ────────────────────────────────────────────

        /// <summary>런 시작: 맵 생성, 출발 전 상태로. act 미지정 시 firstAct.</summary>
        public bool StartRun(ActDefinition act = null)
        {
            CurrentAct = act != null ? act : firstAct;
            if (CurrentAct == null)
            {
                Debug.LogWarning("[RunManager] ActDefinition이 없어 런을 시작할 수 없습니다 (기존 웨이브 흐름 폴백).");
                return false;
            }

            int usedSeed = seed != 0 ? seed : Random.Range(int.MinValue, int.MaxValue);
            CurrentSeed = usedSeed;
            Map = MapGenerator.Generate(CurrentAct, usedSeed);
            _currentNodeId = -1;
            BattlesCleared = 0;
            _banishedPresets.Clear();

            Debug.Log($"[RunManager] 런 시작 — {CurrentAct.ActName}, 시드 {usedSeed}\n{MapGenerator.Dump(Map)}");
            return true;
        }

        public void EndRun()
        {
            Map = null;
            CurrentAct = null;
            _currentNodeId = -1;
            BattlesCleared = 0;
            _banishedPresets.Clear();
        }

        // ── 세이브 참가자 ──────────────────────────────────────

        public void Capture(Save.RunSaveData data)
        {
            var p = data.Progress;
            p.Seed = CurrentSeed;
            p.CurrentNodeId = CurrentNode != null ? CurrentNode.Id : -1;
            p.BattlesCleared = BattlesCleared;

            p.VisitedNodeIds.Clear();
            if (Map != null)
                foreach (var node in Map.Nodes)
                    if (node.Visited) p.VisitedNodeIds.Add(node.Id);

            p.BanishedPresetIds.Clear();
            foreach (var preset in _banishedPresets)
                if (preset != null) p.BanishedPresetIds.Add(preset.SaveId);
        }

        public void Validate(Save.RunSaveData data, RunRestoreContext ctx)
        {
            if (firstAct == null)
            {
                ctx.Report.Fail("RunManager.firstAct가 지정되지 않아 맵을 복원할 수 없습니다.");
                return;
            }

            // 맵 재생성은 순수 함수(시드 → 그래프)라 부작용이 없다. Apply가 쓸 맵을 미리 만들어
            // 저장된 노드 ID가 실제로 해결되는지 본다 — 막 정의가 바뀐 세이브를 여기서 걸러야
            // Apply가 현재 노드 없는 런을 만들어 놓는 사태를 막는다.
            var map = MapGenerator.Generate(firstAct, data.Progress.Seed);
            if (map == null)
            {
                ctx.Report.Fail("맵 재생성에 실패했습니다 — 막 정의를 확인하세요.");
                return;
            }

            if (data.Progress.CurrentNodeId >= 0 && map.Get(data.Progress.CurrentNodeId) == null)
                ctx.Report.Fail($"현재 노드 {data.Progress.CurrentNodeId}를 재생성된 맵에서 찾지 못했습니다 — 맵 정의가 변경된 세이브입니다.");

            foreach (int id in data.Progress.VisitedNodeIds)
                if (map.Get(id) == null)
                    ctx.Report.Fail($"방문 노드 {id}를 재생성된 맵에서 찾지 못했습니다 — 맵 정의가 변경된 세이브입니다.");
        }

        public void Apply(Save.RunSaveData data, RunRestoreContext ctx)
        {
            var p = data.Progress;

            CurrentAct = firstAct;
            CurrentSeed = p.Seed;
            Map = MapGenerator.Generate(CurrentAct, p.Seed);   // 같은 시드 → 같은 맵
            _banishedPresets.Clear();

            foreach (int id in p.VisitedNodeIds)
            {
                var node = Map.Get(id);
                if (node != null) node.Visited = true;
            }

            _currentNodeId = p.CurrentNodeId;
            BattlesCleared = p.BattlesCleared;

            // 소멸 캐릭터를 못 찾아도 런은 정상 진행된다 — 그 캐릭터가 다시 영입 가능해질 뿐.
            foreach (string saveId in p.BanishedPresetIds)
            {
                var preset = ctx.FindPreset(saveId);
                if (preset != null) _banishedPresets.Add(preset);
                else ctx.Report.Warn($"소멸 캐릭터 ID '{saveId}'를 찾지 못했습니다 — 재영입 가능해집니다.");
            }

            Debug.Log($"[RunManager] 런 복원 — 시드 {p.Seed}, 현재 노드 {p.CurrentNodeId}, 전투 {p.BattlesCleared}회 클리어");
        }

        // ── 이동 ──────────────────────────────────────────────

        /// <summary>지금 이동 가능한 노드들: 출발 전이면 0층 전체, 아니면 현재 노드의 Next.</summary>
        public List<MapNode> GetSelectableNodes()
        {
            var result = new List<MapNode>();
            if (Map == null) return result;

            if (_currentNodeId < 0)
            {
                result.AddRange(Map.GetFloor(0));
                return result;
            }

            foreach (int id in CurrentNode.Next)
            {
                var node = Map.Get(id);
                if (node != null) result.Add(node);
            }
            return result;
        }

        /// <summary>노드로 이동 (이동 가능 목록에 있어야 함). 성공 시 해당 노드 반환.</summary>
        public MapNode MoveToNode(int nodeId)
        {
            var selectable = GetSelectableNodes();
            var target = selectable.Find(n => n.Id == nodeId);
            if (target == null)
            {
                Debug.LogWarning($"[RunManager] 노드 {nodeId}로 이동 불가 (이동 가능 목록에 없음).");
                return null;
            }

            target.Visited = true;
            _currentNodeId = nodeId;
            Debug.Log($"[RunManager] 노드 이동 → #{nodeId} ({target.Type}, 층 {target.Floor})");
            return target;
        }

        /// <summary>전투류 노드 클리어 시 GameFlowManager가 호출.</summary>
        public void OnBattleCleared()
        {
            BattlesCleared++;
        }

        // ── 디버그 ────────────────────────────────────────────

        [ContextMenu("맵 생성 테스트 (콘솔 덤프)")]
        private void DebugGenerate()
        {
            var act = CurrentAct != null ? CurrentAct : firstAct;
            if (act == null) { Debug.LogWarning("[RunManager] firstAct가 비어 있습니다."); return; }
            var testMap = MapGenerator.Generate(act, seed != 0 ? seed : System.Environment.TickCount);
            Debug.Log($"[RunManager] 테스트 맵:\n{MapGenerator.Dump(testMap)}");
        }
    }
}
