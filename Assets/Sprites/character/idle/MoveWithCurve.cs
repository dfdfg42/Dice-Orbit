using UnityEngine;

public class MoveWithCurve : MonoBehaviour
{
    public AnimationCurve transitionCurve; // 전환 효과를 위한 곡선
    public Sprite[] animationFrames; // 애니메이션 프레임 배열
    public float frameDuration = 0.1f; // 각 프레임의 지속 시간

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float timeElapsed = 0f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = animationFrames[currentFrame];
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        // 현재 프레임에서 다음 프레임으로 전환
        if (timeElapsed >= frameDuration)
        {
            timeElapsed -= frameDuration;
            currentFrame = (currentFrame + 1) % animationFrames.Length;
        }

        // 부드러운 전환 효과 적용
        float t = timeElapsed / frameDuration; // 현재 프레임에서 다음 프레임으로의 진행 비율
        float alpha = transitionCurve.Evaluate(t); // 곡선에 따라 전환 비율 계산

        // 현재 프레임과 다음 프레임을 보간하여 전환
        int nextFrame = (currentFrame + 1) % animationFrames.Length;
        spriteRenderer.sprite = BlendSprites(animationFrames[currentFrame], animationFrames[nextFrame], alpha);
    }

    // 두 스프라이트를 보간하는 함수 (예제용, 실제로는 별도 구현 필요)
    private Sprite BlendSprites(Sprite current, Sprite next, float alpha)
    {
        // 스프라이트 보간 로직 구현 필요 (Unity에서는 기본적으로 지원하지 않음)
        // 여기서는 단순히 다음 프레임으로 전환하는 예제
        return alpha > 0.5f ? next : current;
    }
}