using UnityEngine;
using Unity.Netcode;

public class LanguageMenu : NetworkBehaviour
{
    private NetworkedSpeechTranlator2 speechTranslator;

    private void Start()
    {
        // Sempre ouvir quando um player entra na cena
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // só conecta se for o próprio player local
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        // pega todos tradutores da cena e escolhe o do local player
        foreach (var translator in FindObjectsOfType<NetworkedSpeechTranlator2>())
        {
            if (translator.IsOwner)
            {
                speechTranslator = translator;
                Debug.Log($"[LanguageMenu] Tradutor do Player local encontrado: {translator.name}");
                return;
            }
        }

        Debug.LogError("[LanguageMenu] Nenhum tradutor do Player local encontrado!");
    }

    // ==== Chamados direto pelo botão (via OnClick no Inspector) ====
    public void OnEnglishClicked()
    {
        if (speechTranslator == null)
        {
            Debug.LogWarning("[LanguageMenu] Nenhum tradutor conectado!");
            return;
        }

        Debug.Log("[LanguageMenu] EN clicado");
        speechTranslator.SetEnglish();
    }

    public void OnSpanishClicked()
    {
        if (speechTranslator == null)
        {
            Debug.LogWarning("[LanguageMenu] Nenhum tradutor conectado!");
            return;
        }

        Debug.Log("[LanguageMenu] ES clicado");
        speechTranslator.SetSpanish();
    }

    public void OnFrenchClicked()
    {
        if (speechTranslator == null)
        {
            Debug.LogWarning("[LanguageMenu] Nenhum tradutor conectado!");
            return;
        }

        Debug.Log("[LanguageMenu] FR clicado");
        speechTranslator.SetFrench();
    }
}
