using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;
using Microsoft.CognitiveServices.Speech.Audio;

public class NetworkedSpeechTranslatorPush : NetworkBehaviour
{
    [Header("Subtitle Settings")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Azure Settings")]
    [SerializeField] private AzureConfig azureConfig;
    public string sourceLanguage = "pt-BR";
    public string targetLanguage = "en-US";

    private TranslationRecognizer recognizer;
    private PushAudioInputStream pushStream;

    private string localSubtitle;
    private string previousSubtitle = "";

    private NetworkVariable<FixedString128Bytes> sharedSubtitle =
        new(writePerm: NetworkVariableWritePermission.Server);

    private SynchronizationContext unityContext;

    private void Awake()
    {
        unityContext = SynchronizationContext.Current;
    }

    private void Update()
    {
        subtitleText.text = IsOwner ? localSubtitle : sharedSubtitle.Value.ToString();
    }

    public override async void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        await InitRecognizer();
    }

    private async Task InitRecognizer()
    {
        if (recognizer != null)
        {
            recognizer.Recognized -= OnRecognized;
            try { await recognizer.StopContinuousRecognitionAsync(); } catch { }
            recognizer.Dispose();
            recognizer = null;
        }

        if (azureConfig == null)
        {
            Debug.LogError("AzureConfig não configurado! Adicione um AzureConfig ScriptableObject.");
            return;
        }

        var config = SpeechTranslationConfig.FromSubscription(azureConfig.SubscriptionKey, azureConfig.Region);
        config.SpeechRecognitionLanguage = sourceLanguage;
        config.AddTargetLanguage(targetLanguage);

        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1); // 16kHz, mono
        pushStream = AudioInputStream.CreatePushStream(audioFormat);
        var audioConfig = AudioConfig.FromStreamInput(pushStream);

        recognizer = new TranslationRecognizer(config, audioConfig);
        recognizer.Recognized += OnRecognized;

        await recognizer.StartContinuousRecognitionAsync();
        Debug.Log($"Reconhecimento iniciado com PushStream: {sourceLanguage} ➜ {targetLanguage}");
    }

    private void OnRecognized(object sender, TranslationRecognitionEventArgs e)
    {
        if (e.Result.Reason != ResultReason.TranslatedSpeech) return;

        string subtitle = e.Result.Translations.ContainsKey(targetLanguage)
            ? e.Result.Translations[targetLanguage]
            : e.Result.Text;

        localSubtitle = subtitle;

        unityContext.Post(_ => SubmitSubtitleServerRpc(localSubtitle), null);

        if (localSubtitle != previousSubtitle)
        {
            Debug.Log($"[Azure] Tradução: {localSubtitle}");
            previousSubtitle = localSubtitle;
        }
    }

    [ServerRpc]
    private void SubmitSubtitleServerRpc(string subtitle)
    {
        sharedSubtitle.Value = new FixedString128Bytes(subtitle);
    }

    // 🔥 Método para alimentar o Azure com áudio capturado em outro lugar (Vivox, Microfone, etc.)
    public void FeedAudio(float[] audioData)
    {
        // Converte float [-1,1] para PCM16 (short)
        byte[] buffer = new byte[audioData.Length * 2];
        int offset = 0;
        foreach (float sample in audioData)
        {
            short pcm16 = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
            buffer[offset++] = (byte)(pcm16 & 0xFF);
            buffer[offset++] = (byte)((pcm16 >> 8) & 0xFF);
        }

        pushStream?.Write(buffer);
    }
}
