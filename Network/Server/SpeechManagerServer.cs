using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

// Azure
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Translation;
using Microsoft.CognitiveServices.Speech.Audio;

public class SpeechManagerServer : NetworkBehaviour
{
    [Header("Azure Speech (Servidor)")]
    [SerializeField] private string region = "SUA_REGIAO_AQUI";
    [SerializeField] private string subscriptionKey = "SUA_CHAVE_AQUI";

    public static SpeechManagerServer Instance { get; private set; }

    private class SpeakerSession
    {
        public ulong ClientId;
        public string SourceLang;
        public HashSet<string> TargetLangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public TranslationRecognizer Recognizer;
        public PushAudioInputStream PushStream;
        public AudioConfig AudioCfg;
        public SpeechConfig STTConfig; // agora � SpeechConfig, n�o mais SpeechTranslationConfig
    }

    private readonly Dictionary<ulong, string> _clientPreferredTarget = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, string> _clientSource = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, SpeakerSession> _sessions = new Dictionary<ulong, SpeakerSession>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) enabled = false;
    }

    public void OnClientPrefs(ulong clientId, string sourceLang, string targetLang)
    {
        if (!IsServer) return;

        _clientSource[clientId] = sourceLang;
        _clientPreferredTarget[clientId] = NormalizeTarget(targetLang);
        RebuildTargetsForAllSessions();
    }

    private string NormalizeTarget(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "en";
        var low = t.ToLowerInvariant();
        if (low.Contains("-")) return low.Split('-')[0];
        return low;
    }

    public void OnAudioChunkReceived(ulong speakerClientId, byte[] pcm16Chunk)
    {
        if (!IsServer) return;
        if (!EnsureSession(speakerClientId)) return;

        var session = _sessions[speakerClientId];
        session.PushStream.Write(pcm16Chunk);
    }

    private bool EnsureSession(ulong speakerClientId)
    {
        if (!_clientSource.TryGetValue(speakerClientId, out var src))
            return false;

        if (_sessions.TryGetValue(speakerClientId, out var existing))
        {
            var desiredTargets = CollectTargetsExcluding(speakerClientId);
            if (!SetEquals(existing.TargetLangs, desiredTargets))
            {
                Debug.Log($"[AzureServer] Recriando sess�o do falante {speakerClientId} por mudan�a de targets.");
                DisposeSession(existing);
                _sessions.Remove(speakerClientId);
            }
            else return true;
        }

        var session = CreateSession(speakerClientId, src, CollectTargetsExcluding(speakerClientId));
        _sessions[speakerClientId] = session;
        return true;
    }

    private HashSet<string> CollectTargetsExcluding(ulong speakerId)
    {
        var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _clientPreferredTarget)
        {
            if (kv.Key == speakerId) continue;
            hs.Add(NormalizeTarget(kv.Value));
        }
        if (hs.Count == 0) hs.Add("en");
        return hs;
    }

    private bool SetEquals(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var t in a) if (!b.Contains(t)) return false;
        return true;
    }

    private SpeakerSession CreateSession(ulong speakerClientId, string sourceLang, HashSet<string> targetLangs)
    {
        var cfg = SpeechTranslationConfig.FromSubscription(subscriptionKey, region);
        cfg.SpeechRecognitionLanguage = sourceLang;
        foreach (var t in targetLangs) cfg.AddTargetLanguage(t);

        var pushStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1));
        var audioCfg = AudioConfig.FromStreamInput(pushStream);
        var recognizer = new TranslationRecognizer(cfg, audioCfg);

        var sess = new SpeakerSession
        {
            ClientId = speakerClientId,
            SourceLang = sourceLang,
            TargetLangs = targetLangs,
            Recognizer = recognizer,
            PushStream = pushStream,
            AudioCfg = audioCfg,
            STTConfig = cfg // upcast autom�tico para SpeechConfig
        };

        recognizer.Recognized += (s, e) =>
        {
            if (e.Result == null) return;
            if (e.Result.Reason == ResultReason.TranslatedSpeech || e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                string original = e.Result.Text ?? string.Empty;

                foreach (var kv in _clientPreferredTarget)
                {
                    if (kv.Key == speakerClientId) continue;

                    var target = NormalizeTarget(kv.Value);
                    string textForClient = original;

                    if (e.Result.Translations != null &&
                        e.Result.Translations.TryGetValue(target, out var translated))
                        textForClient = translated;

                    SendSubtitleClientRpc(
                        speakerClientId, textForClient,
                        new ClientRpcParams
                        {
                            Send = new ClientRpcSendParams { TargetClientIds = new[] { kv.Key } }
                        });
                }
            }
        };

        recognizer.Canceled += (s, e) =>
            Debug.LogError($"[AzureServer] Cancelled {e.Reason} {e.ErrorCode} {e.ErrorDetails}");

        recognizer.SessionStarted += (s, e) =>
            Debug.Log($"[AzureServer] Sess�o iniciada falante {speakerClientId}");

        recognizer.StartContinuousRecognitionAsync();
        return sess;
    }

    private void DisposeSession(SpeakerSession s)
    {
        try { s.Recognizer?.StopContinuousRecognitionAsync().Wait(200); } catch { }
        try { s.Recognizer?.Dispose(); } catch { }
        try { s.AudioCfg?.Dispose(); } catch { }
        try { s.PushStream?.Close(); } catch { }
        //try { s.STTConfig?.Dispose(); } catch { }
    }

    private void OnDestroy()
    {
        if (!IsServer) return;
        foreach (var s in _sessions.Values) DisposeSession(s);
        _sessions.Clear();
    }

    private void RebuildTargetsForAllSessions()
    {
        var toRebuild = new List<ulong>(_sessions.Keys);
        foreach (var speaker in toRebuild)
        {
            if (_sessions.TryGetValue(speaker, out var sess))
            {
                var desired = CollectTargetsExcluding(speaker);
                if (!SetEquals(sess.TargetLangs, desired))
                {
                    DisposeSession(sess);
                    _sessions.Remove(speaker);
                }
            }
        }
    }

    [ClientRpc]
    private void SendSubtitleClientRpc(ulong speakerClientId, string text, ClientRpcParams sendParams = default)
    {
        SubtitleReceiver.Instance?.OnSubtitleFromServer(speakerClientId, text);
    }
}
