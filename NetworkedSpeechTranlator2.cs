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

public class NetworkedSpeechTranlator2 : NetworkBehaviour
{
    [Header("Subtitle Settings")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Azure Settings")]
    [SerializeField] private AzureConfig azureConfig;
    private string sourceLanguage = "pt-BR";
    private string targetLanguage = "en-US";

    private TranslationRecognizer recognizer;
    private string localSubtitle;
    private string previousSubtitle = "";

    // 🔑 Agora sincronizamos a legenda com todos
    private NetworkVariable<FixedString128Bytes> syncedSubtitle =
        new(writePerm: NetworkVariableWritePermission.Owner);

    private readonly SemaphoreSlim _reinitLock = new(1, 1);
    private CancellationTokenSource _cts;
    private bool _quitting;
    private SynchronizationContext unityContext;

    private void Awake()
    {
        unityContext = SynchronizationContext.Current;
    }

    private void Update()
    {
        // Todo mundo vê o mesmo texto sincronizado
        if (subtitleText)
            subtitleText.text = syncedSubtitle.Value.ToString();
    }

    public override async void OnNetworkSpawn()
    {
        Debug.Log($"[Translator:{name}] Spawnado. IsOwner={IsOwner}, IsServer={IsServer}");

        // só o dono inicializa o reconhecedor
        if (!IsOwner) return;

        // Atualiza o texto inicial pro resto
        syncedSubtitle.Value = "";
        await InitRecognizer();
    }

    private async Task InitRecognizer()
    {
        await _reinitLock.WaitAsync();
        try
        {
            if (_quitting) return;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            if (recognizer != null)
            {
                try
                {
                    recognizer.Recognized -= OnRecognized;
                    await recognizer.StopContinuousRecognitionAsync();
                    recognizer.Dispose();
                }
                catch { }
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

            var audio = AudioConfig.FromDefaultMicrophoneInput();
            recognizer = new TranslationRecognizer(config, audio);
            recognizer.Recognized += OnRecognized;

            await recognizer.StartContinuousRecognitionAsync();
            Debug.Log($"Reconhecimento iniciado. {sourceLanguage} ➜ {targetLanguage}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Erro ao iniciar reconhecimento: " + ex.Message);
        }
        finally
        {
            _reinitLock.Release();
        }
    }

    private void OnRecognized(object sender, TranslationRecognitionEventArgs e)
    {
        if (e.Result.Reason != ResultReason.TranslatedSpeech) return;
        if (_cts != null && _cts.IsCancellationRequested) return;

        foreach (var kvp in e.Result.Translations)
        {
            localSubtitle = kvp.Value;
            Debug.Log($"[Azure] Tradução final: {kvp.Key} = {kvp.Value}");
        }

        if (localSubtitle != previousSubtitle)
        {
            previousSubtitle = localSubtitle;

            // 🔑 Atualiza a NetworkVariable -> sincroniza pros outros
            syncedSubtitle.Value = new FixedString128Bytes(localSubtitle);

            Debug.Log($"[Translator:{name}] Legenda sincronizada: {localSubtitle}");
        }
    }

    private async void OnApplicationQuit()
    {
        _quitting = true;
        _cts?.Cancel();

        if (recognizer != null)
        {
            try
            {
                recognizer.Recognized -= OnRecognized;
                await recognizer.StopContinuousRecognitionAsync();
                recognizer.Dispose();
            }
            catch { }
            recognizer = null;
        }
    }

    public async void ChangeTargetLanguage(string newLangCode)
    {
        if (!IsOwner)
        {
            Debug.LogWarning($"[Translator:{name}] Ignorado: não sou o dono.");
            return;
        }

        if (targetLanguage == newLangCode) return;

        targetLanguage = newLangCode;
        Debug.Log($"[Translator:{name}] Alterando idioma para: {targetLanguage}");
        await InitRecognizer();
    }

    public void SetEnglish() => ChangeTargetLanguage("en");
    public void SetSpanish() => ChangeTargetLanguage("es");
    public void SetPortuguese() => ChangeTargetLanguage("pt");
    public void SetFrench() => ChangeTargetLanguage("fr");
}
