using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class SubtitleReceiver : NetworkBehaviour
{
    public static SubtitleReceiver Instance { get; private set; }

    [Header("Associação Falante -> Display")]
    [SerializeField] private List<SpeakerBinding> bindings = new List<SpeakerBinding>();

    private Dictionary<ulong, SubtitleDisplay> _map = new Dictionary<ulong, SubtitleDisplay>();

    [System.Serializable]
    public class SpeakerBinding
    {
        public ulong ClientId;            // Preenchido em runtime (ou dinamicamente)
        public SubtitleDisplay Display;   // Referência ao balão do avatar daquele falante
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Opcional: se você tem um gerenciador de jogadores, popular _map dinamicamente.
        foreach (var b in bindings)
        {
            if (b.Display != null)
                _map[b.ClientId] = b.Display;
        }
    }
    
    public void Register(ulong clientId, SubtitleDisplay display)
    {
        _map[clientId] = display;
    }

    public void Unregister(ulong clientId)
    {
        _map.Remove(clientId);
    }

    public void OnSubtitleFromServer(ulong speakerClientId, string text)
    {
        if (_map.TryGetValue(speakerClientId, out var disp) && disp != null)
        {
            ;// disp.ShowText(text);
        }
    }
}
