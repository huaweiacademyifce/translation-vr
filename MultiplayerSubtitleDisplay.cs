using UnityEngine;
using TMPro;

public class MultiplayerSubtitleDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textUI;
    [SerializeField] private float holdSeconds = 3f;
    [SerializeField] private bool billboard = true;

    private float _timer;

    private void Update()
    {
        if (billboard && Camera.main != null)
        {
            var dir = transform.position - Camera.main.transform.position;
            transform.forward = dir.normalized;
        }

        if (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) Clear();
        }
    }

    public void ShowText(string t)
    {
        if (textUI == null) return;
        textUI.text = t;
        _timer = holdSeconds;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Clear()
    {
        if (textUI != null) textUI.text = "";
        // Você pode esconder o objeto, se preferir:
        // gameObject.SetActive(false);
    }
}
