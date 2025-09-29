using UnityEngine;
using UnityEngine.Events;


[CreateAssetMenu(menuName = "Settings/User Settings")]
public class UserSettings : ScriptableObject
{
    [Header("Language")]
    [Tooltip("Source Language (ex: pt-BR, en-US, en-ES, zh-CN)")]
    public string SourceLanguage = "pt-BR";

    [Tooltip("Target Language (ex: pt, en, en, zh)")]
    public string TargetLanguage = "en";

    [Header("Events")]
    public UnityEvent<string> OnSourceChanged;
    public UnityEvent<string> OnTargetChanged;

    public void SetSource(string source)
    {
        if (SourceLanguage == source) return;
        SourceLanguage = source;
        OnSourceChanged.Invoke(source);
    }

    public void SetTarget(string target)
    {
        if (TargetLanguage == target) return;
        TargetLanguage = target;
        OnTargetChanged.Invoke(target);
    }
}