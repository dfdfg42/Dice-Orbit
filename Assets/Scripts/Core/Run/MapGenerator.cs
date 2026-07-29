using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// ActDefinition → MapGraph 생성기 (StS식 분기 트리).
    ///
    /// 간선은 "비례 창 매핑"으로 연결: 층 n개 → 다음 층 m개일 때 노드 i는
    /// [i*m/n, ((i+1)*m-1)/n] 범위의 다음 층 노드들과 연결된다.
    /// → 간선 교차 없음 + 모든 노드가 최소 1개의 진입/진출 간선 보장.
    /// </summary>
    public static class MapGenerator
    {
        public static MapGraph Generate(ActDefinition act, int seed)
        {
            var rng = new System.Random(seed);
            var graph = new MapGraph { FloorCount = act.FloorCount };

            int floors = act.FloorCount;
            int[] counts = BuildFloorCounts(act, rng);

            // ── 노드 생성 ──
            var byFloor = new List<MapNode>[floors];
            for (int f = 0; f < floors; f++)
            {
                byFloor[f] = new List<MapNode>();
                for (int lane = 0; lane < counts[f]; lane++)
                {
                    var node = new MapNode
                    {
                        Id = graph.Nodes.Count,
                        Floor = f,
                        Lane = lane,
                        Type = MapNodeType.Battle,
                    };
                    graph.Nodes.Add(node);
                    byFloor[f].Add(node);
                }
            }

            // ── 간선 (비례 창 매핑, 비교차) ──
            for (int f = 0; f < floors - 1; f++)
            {
                int n = counts[f], m = counts[f + 1];
                for (int i = 0; i < n; i++)
                {
                    int lo = i * m / n;
                    int hi = ((i + 1) * m - 1) / n;
                    for (int j = lo; j <= hi; j++)
                        byFloor[f][i].Next.Add(byFloor[f + 1][j].Id);
                }
            }

            // ── 타입 배치 ──
            AssignTypes(act, graph, byFloor, rng);

            // ── 몹 세트 배정 (층 구간별 티어 풀에서 노드마다 랜덤 — 같은 층도 다양) ──
            foreach (var node in graph.Nodes)
            {
                switch (node.Type)
                {
                    case MapNodeType.Battle: node.Encounter = act.ResolveBattleEncounter(node.Floor, rng); break;
                    case MapNodeType.Elite:  node.Encounter = act.ResolveEliteEncounter(rng); break;
                    case MapNodeType.Boss:   node.Encounter = act.ResolveBossEncounter(rng); break;
                }

                if (node.IsCombat && node.Encounter == null)
                    Debug.LogWarning($"[MapGenerator] 노드 {node.Id}({node.Type}, 층 {node.Floor})에 배정할 몹 세트가 없습니다 — ActDefinition의 티어 풀/폴백 DB를 확인하세요.");
            }

            return graph;
        }

        private static int[] BuildFloorCounts(ActDefinition act, System.Random rng)
        {
            int floors = act.FloorCount;
            var counts = new int[floors];
            for (int f = 0; f < floors; f++)
            {
                if (f < act.IntroFloors)          counts[f] = 1;                                  // 인트로: 단일 전투
                else if (f == floors - 1)          counts[f] = 1;                                  // 보스층
                else if (f == floors - 2 && act.PreBossChoice) counts[f] = 2;                      // 보스 앞 2택
                else counts[f] = rng.Next(act.MinNodesPerFloor, act.MaxNodesPerFloor + 1);
            }
            return counts;
        }

        private static void AssignTypes(ActDefinition act, MapGraph graph, List<MapNode>[] byFloor, System.Random rng)
        {
            int floors = act.FloorCount;

            // 보스
            var boss = byFloor[floors - 1][0];
            boss.Type = MapNodeType.Boss;
            graph.BossNodeId = boss.Id;

            // 보스 앞 층: 엘리트 or 휴식 2택
            if (act.PreBossChoice && byFloor[floors - 2].Count >= 2)
            {
                byFloor[floors - 2][0].Type = MapNodeType.Elite;
                byFloor[floors - 2][1].Type = MapNodeType.Rest;
            }

            // 보장 엘리트
            if (act.FirstEliteFloor >= act.IntroFloors && act.FirstEliteFloor < floors - 2)
            {
                var floor = byFloor[act.FirstEliteFloor];
                floor[rng.Next(floor.Count)].Type = MapNodeType.Elite;
            }

            // 상점/휴식/이벤트 분배: 중반 층의 전투 노드 중에서,
            // "그 층에 전투가 최소 1개 남는" 조건으로만 치환 (전투 밀도 유지 — 스펙 §1)
            var pool = new List<(MapNodeType type, int count)>
            {
                (MapNodeType.Shop, act.ShopCount),
                (MapNodeType.Rest, act.RestCount),
                (MapNodeType.Event, act.EventCount),
            };

            foreach (var (type, count) in pool)
            {
                for (int k = 0; k < count; k++)
                {
                    var candidates = graph.Nodes.Where(nd =>
                            nd.Type == MapNodeType.Battle &&
                            nd.Floor >= act.IntroFloors && nd.Floor < floors - 2 &&
                            byFloor[nd.Floor].Count(o => o.Type == MapNodeType.Battle) > 1)
                        .ToList();
                    if (candidates.Count == 0) break;
                    candidates[rng.Next(candidates.Count)].Type = type;
                }
            }
        }

        /// <summary>디버그용 ASCII 덤프.</summary>
        public static string Dump(MapGraph graph)
        {
            var sb = new System.Text.StringBuilder();
            for (int f = graph.FloorCount - 1; f >= 0; f--)
            {
                sb.Append($"층{f,2}: ");
                foreach (var n in graph.GetFloor(f))
                {
                    string icon = n.Type switch
                    {
                        MapNodeType.Battle => "⚔",
                        MapNodeType.Elite => "💀",
                        MapNodeType.Shop => "🛒",
                        MapNodeType.Rest => "🏕",
                        MapNodeType.Event => "🎲",
                        MapNodeType.Boss => "👑",
                        _ => "?",
                    };
                    string enc = n.Encounter != null ? $" m{n.Encounter.MonsterPresets?.Count ?? 0}" : "";
                    sb.Append($"[{n.Id}:{icon}{enc}→({string.Join(",", n.Next)})]  ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
