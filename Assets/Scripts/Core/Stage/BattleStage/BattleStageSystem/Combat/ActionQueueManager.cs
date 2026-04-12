using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 중 발생하는 행동(공격, 턴 종료, 연출 등)을 순차적으로 대기하며 실행하기 위한 큐 시스템 (싱글톤)
/// </summary>
public class ActionQueueManager : MonoBehaviour
{
    public static ActionQueueManager Instance { get; private set; }

    private Queue<IEnumerator> actionQueue = new Queue<IEnumerator>();
    private bool isProcessing = false;
    private bool canExecute = true; // true일 때 처리 진행, false일 때 멈춤
    private Coroutine processCoroutine;

    public bool IsProcessing => isProcessing;
    public bool CanExecute
    {
        get => canExecute;
        set => canExecute = value;
    }
    public int QueueCount => actionQueue.Count;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple ActionQueueManagers detected! Destroying duplicate.");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 행동을 큐에 추가합니다. 현재 실행 중인 행동이 없다면 즉시 처리를 시작합니다.
    /// </summary>
    public void EnqueueAction(IEnumerator action)
    {
        actionQueue.Enqueue(action);

        if (!isProcessing)
        {
            processCoroutine = StartCoroutine(ProcessQueueCoroutine());
        }
    }

    /// <summary>
    /// 큐에 담긴 행동들을 하나씩 순차적으로 실행합니다. CanExecute가 false라면 대기합니다.
    /// </summary>
    private IEnumerator ProcessQueueCoroutine()
    {
        isProcessing = true;

        while (actionQueue.Count > 0)
        {
            // 실행이 허용되지 않은 상태라면(일시정지 등) 풀릴 때까지 대기
            while (!canExecute)
            {
                yield return null;
            }

            IEnumerator currentAction = actionQueue.Dequeue();
            // 현재 행동(코루틴)이 완전히 끝날 때까지 대기합니다.
            yield return StartCoroutine(currentAction);
            yield return new WaitForSeconds(1f);
        }
        
        isProcessing = false;
        processCoroutine = null;
    }

    /// <summary>
    /// 전투 종료 시 또는 강제 중단 시 큐를 비우고 실행 중인 코루틴을 멈춤니다.
    /// </summary>
    public void ClearQueue()
    {
        actionQueue.Clear();

        if (processCoroutine != null)
        {
            StopCoroutine(processCoroutine);
            processCoroutine = null;
        }

        isProcessing = false;
    }
}