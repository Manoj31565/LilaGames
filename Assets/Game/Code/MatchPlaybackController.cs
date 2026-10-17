using UnityEngine;

public class MatchPlaybackController : MonoBehaviour
{
    public double CurrentTime { get; private set; }
    public double Duration { get; private set; }

    public bool IsPlaying { get; private set; }
    public float Speed { get; private set; } = 1f;

    public void Initialize(MatchData match)
    {
        Duration = System.Math.Max(0, match.durationSeconds);

        CurrentTime = 0;

        IsPlaying = Duration > 0;
    }

    private void FixedUpdate()
    {
        if (!IsPlaying) return;

        CurrentTime = System.Math.Min(CurrentTime + Time.fixedDeltaTime * Speed, Duration);

        if (CurrentTime >= Duration)
            IsPlaying = false;
    }

    public void Play()
    {
        if (CurrentTime < Duration)
            IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    // Connect a slider with Min = 0 and Max = 1.
    public void SeekNormalized(float value)
    {
        CurrentTime = Mathf.Clamp01(value) * Duration;
    }

    public void SetSpeed(float speed)
    {
        Speed = Mathf.Clamp(speed, 0.25f, 8f);
    }

    public void Restart()
    {
        CurrentTime = 0;
        IsPlaying = Duration > 0;
    }
}