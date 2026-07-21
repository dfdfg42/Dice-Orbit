using System;
using System.Collections.Generic;
using DiceOrbit.Data.Waves;

namespace DiceOrbit.Core.Run
{
    /// <summary>
    /// 노드맵의 노드 하나. Id = MapGraph.Nodes 리스트 인덱스.
    /// </summary>
    [Serializable]
    public class MapNode
    {
        public int Id;
        public int Floor;                        // 0 = 시작층, FloorCount-1 = 보스층
        public int Lane;                         // 층 내 가로 위치 (UI 배치용)
        public MapNodeType Type;
        [NonSerialized] public WaveDefinition Encounter;   // 이 노드의 몹 세트 (전투류만 — 생성 시 티어 풀에서 배정)
        public bool DiceModReward;               // 주사위 개조 드랍 예고 (맵에 아이콘 표시)
        public bool Visited;
        public List<int> Next = new List<int>(); // 다음 층에서 이동 가능한 노드 Id들

        public bool IsCombat => Type == MapNodeType.Battle || Type == MapNodeType.Elite || Type == MapNodeType.Boss;
    }

    /// <summary>
    /// 한 막(Act)의 노드맵 그래프. MapGenerator가 ActDefinition으로부터 생성.
    /// </summary>
    [Serializable]
    public class MapGraph
    {
        public List<MapNode> Nodes = new List<MapNode>();
        public int FloorCount;
        public int BossNodeId = -1;

        public MapNode Get(int id) => id >= 0 && id < Nodes.Count ? Nodes[id] : null;

        public List<MapNode> GetFloor(int floor)
        {
            var result = new List<MapNode>();
            foreach (var n in Nodes)
                if (n.Floor == floor) result.Add(n);
            return result;
        }
    }
}
