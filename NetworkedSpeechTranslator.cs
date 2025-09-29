using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;
using TMPro;
using Unity.Netcode;
using Unity.Collections;
using System.Collections;
using Unity.Services.Vivox;


public class NetworkedSpeechTranslator : NetworkBehaviour
{
    [Header("Subtitle Settings")]
    [SerializeField] private TextMeshProUGUI subtitleText;

    public bool showTranslation = true;

    [Header("Azure Settings")]
    [SerializeField] private AzureConfig azureConfig;
    public string sourceLanguage = "pt-BR";
    public string targetLanguage = "en-US";

    private TranslationRecognizer recognizer;

    private string localSubtitle;
    private string previousSubtitle = "";

    // NetworkVariable para sincronizar legendas entre clientes
    private NetworkVariable<FixedString128Bytes> sharedSubtitle = new(writePerm: NetworkVariableWritePermission.Owner);


    private SynchronizationContext unityContext;

    private bool isRecognizerInitialized = false;

    private void Awake()
    {
        unityContext = SynchronizationContext.Current;
    }

    public void Update()
    {
        // Atualiza o texto no canvas
        if (subtitleText != null)
        {
            subtitleText.text = IsOwner ? localSubtitle : sharedSubtitle.Value.ToString();
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"OnNetworkSpawn chamado para {gameObject.name}. IsOwner: {IsOwner}, IsClient: {IsClient}");
        if (IsOwner)
        {
            StartCoroutine(WaitUntilPlayerAndLobbyAreFullyConnected());
        }
        else
        {
            // Para clientes não-proprietários, apenas garanta que a NetworkVariable esteja sendo observada
            sharedSubtitle.OnValueChanged += OnSharedSubtitleChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            sharedSubtitle.OnValueChanged -= OnSharedSubtitleChanged;
        }
        if (recognizer != null)
        {
            recognizer.StopContinuousRecognitionAsync().Wait();
            recognizer.Dispose();
            recognizer = null;
            isRecognizerInitialized = false;
            Debug.Log("Reconhecedor descartado em OnNetworkDespawn.");
        }
        base.OnNetworkDespawn();
    }

    private void OnSharedSubtitleChanged(FixedString128Bytes oldVal, FixedString128Bytes newVal)
    {
        // Este callback é executado em todos os clientes quando a NetworkVariable muda
        // O cliente proprietário já atualiza localSubtitle diretamente
        if (!IsOwner && subtitleText != null)
        {
            subtitleText.text = newVal.ToString();
            Debug.Log($"[Client] Legenda compartilhada atualizada: {newVal}");
        }
    }

    private IEnumerator WaitUntilPlayerAndLobbyAreFullyConnected()
    {
        Debug.Log("⏳ Aguardando conexão completa do jogador e carregamento do lobby...");

        // Espera até o client estar na lista e ser dono do objeto
        while (!IsClient || !IsOwner || !NetworkManager.Singleton.IsConnectedClient
               || !NetworkManager.Singleton.ConnectedClients.ContainsKey(NetworkManager.Singleton.LocalClientId))
        {
            yield return null;
        }

        // Adicionar uma verificação para o carregamento da cena do lobby, se aplicável
        // Exemplo: Se o lobby for uma cena específica, você pode verificar:
        // while (!NetworkManager.Singleton.SceneManager.IsSceneLoaded("NomeDaCenaDoLobby"))
        // {
        //     yield return null;
        // }

        // Adicionar um pequeno atraso para garantir que todos os scripts de rede tenham inicializado
        yield return new WaitForSeconds(1.0f);

        Debug.Log("✅ Jogador e lobby (presumidamente) totalmente conectados. Tentando iniciar reconhecimento de voz.");
        if (!isRecognizerInitialized)
        {
            _ = InitRecognizer();
        }
        else
        {
            Debug.Log("Reconhecedor já inicializado. Pulando inicialização duplicada.");
        }
    }

    private async Task InitRecognizer()
    {
        if (isRecognizerInitialized)
        {
            Debug.LogWarning("Tentativa de inicializar o reconhecedor novamente. Ignorando.");
            return;
        }

        try
        {
            if (recognizer != null)
            {
                await recognizer.StopContinuousRecognitionAsync();
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

            Debug.Log($"Configuração de idioma: {sourceLanguage} para {targetLanguage}. Idioma de destino: {targetLanguage}");

            recognizer = new TranslationRecognizer(config);

            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.TranslatedSpeech)
                {
                    string subtitle = e.Result.Translations.ContainsKey(targetLanguage)
                        ? e.Result.Translations[targetLanguage]
                        : e.Result.Text;

                    unityContext.Post(_ =>
                    {
                        localSubtitle = subtitle;
                        if (localSubtitle != previousSubtitle)
                        {
                            Debug.Log($"[Owner] Nova tradução: {localSubtitle}");
                            previousSubtitle = localSubtitle;
                        }
                        // Envia a legenda para os outros clientes via NetworkVariable
                        SubmitSubtitleServerRpc(subtitle);
                    }, null);
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Debug.Log($"[Owner] Reconhecimento sem correspondência: {e.Result.Text}");
                }
            };

            recognizer.Canceled += (s, e) =>
            {
                Debug.LogError($"[Owner] Reconhecimento cancelado. Razão: {e.Reason}. Erro: {e.ErrorCode} - {e.ErrorDetails}");
                if (e.Reason == CancellationReason.Error)
                {
                    Debug.LogError($"[Owner] Verifique sua chave de assinatura e região do Azure.");
                }
            };

            recognizer.SessionStarted += (s, e) =>
            {
                Debug.Log("[Owner] Sessão de reconhecimento iniciada.");
            };

            recognizer.SessionStopped += (s, e) =>
            {
                Debug.Log("[Owner] Sessão de reconhecimento parada.");
            };

            await recognizer.StartContinuousRecognitionAsync();
            isRecognizerInitialized = true;
            Debug.Log($"Reconhecimento iniciado com idioma de destino: {targetLanguage}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Erro ao iniciar reconhecimento: " + ex.Message + "\n" + ex.StackTrace);
            isRecognizerInitialized = false;
        }
    }

    [ServerRpc]
    private void SubmitSubtitleServerRpc(string subtitle)
    {
        // A NetworkVariable é sincronizada automaticamente para todos os clientes
        sharedSubtitle.Value = new FixedString128Bytes(subtitle);
        Debug.Log($"[Server] NetworkVariable atualizada para: {subtitle}");
        // Não precisamos mais de BroadcastSubtitleClientRpc se a NetworkVariable for suficiente
        // BroadcastSubtitleClientRpc(subtitle);
    }

    // Remove BroadcastSubtitleClientRpc pois a NetworkVariable já faz a sincronização
    // [ClientRpc]
    // private void BroadcastSubtitleClientRpc(string subtitle)
    // {
    //     if (!IsOwner)
    //     {
    //         // Opcional: ações adicionais quando outros clientes recebem a legenda
    //     }
    // }

    private async void OnApplicationQuit()
    {
        if (recognizer != null)
        {
            await recognizer.StopContinuousRecognitionAsync();
            recognizer.Dispose();
            recognizer = null;
            isRecognizerInitialized = false;
            Debug.Log("Reconhecedor descartado em OnApplicationQuit.");
        }
    }

    public void ChangeTargetLanguage(string newLangCode)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("Apenas o proprietário pode mudar o idioma de tradução.");
            return;
        }

        targetLanguage = newLangCode;
        Debug.Log($"Alterando idioma de tradução para: {targetLanguage}");
        isRecognizerInitialized = false; // Força a reinicialização do reconhecedor
        _ = InitRecognizer();
    }

    public void SetEnglish()
    {
        if (!IsOwner) return;
        ChangeTargetLanguage("en-US");
    }
    public void SetSpanish()
    {
        if (!IsOwner) return;
        ChangeTargetLanguage("es-ES");
    }

    public void SetPortuguese()
    {
        if (!IsOwner) return;
        ChangeTargetLanguage("pt-BR");
    }
    public void SetFrench()
    {
        if (!IsOwner) return;
        ChangeTargetLanguage("fr-FR");
    }
}


