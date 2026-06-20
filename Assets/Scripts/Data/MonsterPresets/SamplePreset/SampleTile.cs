using DiceOrbit.Core;
using DiceOrbit.Core.Pipeline;
using UnityEngine;

namespace DiceOrbit.Data.Tile
{
    /// <summary>
    /// Ÿ�Ͽ� �ο��Ǵ� Ư���� �Ӽ�(����, ����, ��� ��)�� �����ϴ� ���� Ŭ�����Դϴ�.
    /// TileAttribute�� ��ӹ�����, ĳ���Ͱ� Ÿ���� ��ų� ������ ���� �̺�Ʈ�� ó���մϴ�.
    /// </summary>
    public class SampleTileAttribute : TileAttribute
    {
        /// <summary>
        /// �Ӽ� ������. (���� �нú곪 ��ų�� ���� Ÿ�Ͽ� ������ �� ȣ��˴ϴ�)
        /// </summary>
        /// <param name="type">�Ӽ��� Ÿ��(enum)</param>
        /// <param name="value">�������� �� ���� ��ġ (�⺻�� ���� ����)</param>
        /// <param name="duration">����� ���ӵǴ� �� �� (-1�̸� ����)</param>
        /// <param name="isStackable">���� Ÿ�Ͽ� ���� �� �ߺ� ��ġ ��������</param>
        public SampleTileAttribute(TileAttributeType type, int value, int duration, bool isStackable = false) 
            : base(type, value, duration, isStackable)
        {
        }

        /// <summary>
        /// ĳ���Ͱ� �� Ÿ���� ������ �� �ߵ��մϴ�. (���� ���� ����)
        /// </summary>
        /// <param name="character">�� Ÿ���� ���� ĳ���� ��ü</param>
        public override void OnTraverse(Character character)
        {
            // ����: ��� ���� ȿ�� �ߵ��� ���� Activate ȣ��
            Activate(character);
        }

        /// <summary>
        /// ĳ���Ͱ� �̵��� ��ġ�� �� Ÿ�� ������ ���� ������ �� �ߵ��մϴ�.
        /// </summary>
        public override void OnEndTurn(Character character)
        {
            // ����: ���� ������ ���� �� �� �� �ߵ��� ��� ���
            Activate(character);
        }

        /// <summary>
        /// ���� ȿ���� ó���ϴ� Ŀ���� ���� (���� �ֱ�, ���� �ο� ��)
        /// </summary>
        public void Activate(Character target)
        {
            // Ÿ���� ���� �����̰ų� ������ ����
            if (target == null || !target.IsAlive) return;

            // ���ظ� �ִ� ���� ���� (CombatPipeline ���)
            var context = new AttackContext(
                null, // Ÿ��(ȯ��) �����̹Ƿ� ���� ��ü(Source)�� null�� �� ����
                target,
"���� ���� �ߵ�", Value // Value (������ ��ġ) ����
            );
            
            // ���������ο� ���� ó���� ���
            CombatPipeline.Instance?.Process(context);

            Debug.Log($"[SampleTileAttribute] {target.name} ������ Ÿ���� ��� {Value} ��ŭ�� ȿ���� �޾ҽ��ϴ�!");

            // ��ȸ�� �����̶�� �ߵ� �� �ڽ��� ���� ó��
            // Owner.RemoveAttribute(this);
        }

        /// <summary>
        /// UI�� �������� Ÿ���� ������ ������ �� ȣ��Ǵ� ���ڿ��Դϴ�.
        /// </summary>
        public override string GetDescription()
        {
            string durationText = Duration < 0 ? "����" : $"{Duration}��";
            return $"�������ų� �� ���� �� {Value} ȿ��, ���� {durationText}";
        }
    }
}
