using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class SubtitleDisplay : NetworkBehaviour
{
    [Header("Referências")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private GameObject subtitleBackground;

    private bool isLocalPlayer = false;

    private void Start()
    {
        // Verifica se este objeto pertence ao jogador local
        isLocalPlayer = IsOwner;

        // Oculta legenda no jogador local
        if (isLocalPlayer)
        {
            if (subtitleBackground != null)
                subtitleBackground.SetActive(false);

            if (subtitleText != null)
                subtitleText.text = "";
        }
    }

    /// <summary>
    /// Exibe a legenda por 5 segundos.
    /// Só será executado se o player **não for o dono** da fala.
    /// </summary>
    public void ShowSubtitle(string text)
    {
        if (isLocalPlayer) return;

        if (subtitleText != null)
            subtitleText.text = text;
        Debug.Log($"[SubtitleDisplay] Legenda exibida: {subtitleText}");

        if (subtitleBackground != null)
            subtitleBackground.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(ClearAfterSeconds(5f));
    }

    private IEnumerator ClearAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (subtitleText != null)
            subtitleText.text = "";

        if (subtitleBackground != null)
            subtitleBackground.SetActive(false);
    }
}