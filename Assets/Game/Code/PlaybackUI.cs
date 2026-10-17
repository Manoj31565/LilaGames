using UnityEngine;
using UnityEngine.UI;

public class PlaybackUI : MonoBehaviour
{
    [SerializeField] private MatchPlaybackController playback;
    private Slider timelineSlider;

    private void OnEnable()
    {
        timelineSlider = this.GetComponent<Slider>();

        timelineSlider.onValueChanged.AddListener(OnTimelineChanged);
    }

    private void OnDisable()
    {
        timelineSlider.onValueChanged.RemoveListener(OnTimelineChanged);
    }

    private void Update()
    {
        timelineSlider.SetValueWithoutNotify(playback.Duration > 0 ? (float)(playback.CurrentTime / playback.Duration) : 0f);
    }

    private void OnTimelineChanged(float value)
    {
        playback.SeekNormalized(value);
    }
}