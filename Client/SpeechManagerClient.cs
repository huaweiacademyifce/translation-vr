using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class SpeechManagerClient : NetworkBehaviour
{
    [Header("User Settings (local do cliente)")]
    [SerializeField] private UserSettings userSettings;

    [Header("Áudio")]
    [Range(10, 100)]
    [SerializeField] private int chunkMs = 20; // 20ms por pacote
    [SerializeField] private int captureFrequency = 48000; // Quest normalmente 48 kHz
    private const int TargetFrequency = 16000; // Azure STT (mono 16kHz recomendado)

    private AudioClip _mic;
    private string _deviceName;
    private float[] _floatBuffer;     // buffer de leitura do AudioClip (float -1..1)
    private short[] _pcm16Buffer;     // buffer convertido para PCM16
    private int _lastPos;
    private int _samplesPerChunk48k;  // amostras por chunk na taxa de captura
    private int _outSamplesPerChunk16k;// amostras alvo por chunk (ex.: 320 para 20ms@16k)
    private bool _streaming;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            Permission.RequestUserPermission(Permission.Microphone);
#endif
        StartCapture();
        // Notifica o servidor das preferências do cliente
        NotifyServerMyPrefsServerRpc(userSettings.SourceLanguage, userSettings.TargetLanguage);
    }

    private void StartCapture()
    {
        if (Microphone.devices.Length == 0) { Debug.LogWarning("Sem microfone disponível."); return; }
        _deviceName = Microphone.devices[0];
        _mic = Microphone.Start(_deviceName, true, 1, captureFrequency); // loop 1s
        while (Microphone.GetPosition(_deviceName) <= 0) { } // aguarda iniciar

        _samplesPerChunk48k = (int)(captureFrequency * (chunkMs / 1000f));
        _outSamplesPerChunk16k = (int)(TargetFrequency * (chunkMs / 1000f));

        _floatBuffer = new float[_samplesPerChunk48k];
        _pcm16Buffer = new short[_outSamplesPerChunk16k];

        _streaming = true;
    }

    private void OnDisable()
    {
        _streaming = false;
        if (_mic != null && Microphone.IsRecording(_deviceName)) Microphone.End(_deviceName);
    }

    private void Update()
    {
        if (!IsOwner || !_streaming || _mic == null) return;

        int pos = Microphone.GetPosition(_deviceName);
        int diff = pos - _lastPos;
        if (diff < 0) diff += _mic.samples; // loop

        // enquanto houver ao menos um chunk
        while (diff >= _samplesPerChunk48k)
        {
            _mic.GetData(_floatBuffer, _lastPos);
            _lastPos = (_lastPos + _samplesPerChunk48k) % _mic.samples;
            diff -= _samplesPerChunk48k;

            // Resample 48k->16k (fator 3:1). Simples: pega 1 a cada 3 amostras (nearest).
            // Para produção, use um resampler melhor (linear/speexdsp/soxr).
            int outIdx = 0;
            for (int i = 0; i < _floatBuffer.Length && outIdx < _pcm16Buffer.Length; i += 3)
            {
                float s = Mathf.Clamp(_floatBuffer[i], -1f, 1f);
                _pcm16Buffer[outIdx++] = (short)Mathf.RoundToInt(s * short.MaxValue);
            }

            // Serializa e envia (Unreliable) — ~640 bytes por chunk (20ms@16k)
            var bytes = new byte[_outSamplesPerChunk16k * 2];
            Buffer.BlockCopy(_pcm16Buffer, 0, bytes, 0, bytes.Length);
            SendAudioChunkServerRpc(bytes);
        }
    }

    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    private void SendAudioChunkServerRpc(byte[] pcm16Chunk)
    {
        // Consumido no servidor (AzureSpeechServer)
        SpeechManagerServer.Instance?.OnAudioChunkReceived(OwnerClientId, pcm16Chunk);
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerMyPrefsServerRpc(string sourceLang, string targetLang)
    {
        SpeechManagerServer.Instance?.OnClientPrefs(OwnerClientId, sourceLang, targetLang);
    }

    // Chamado localmente quando o usuário troca os idiomas no menu (opcional)
    public void OnChangeSource(string newCode)
    {
        if (!IsOwner) return;
        userSettings.SetSource(newCode);
        NotifyServerMyPrefsServerRpc(userSettings.SourceLanguage, userSettings.TargetLanguage);
    }

    public void OnChangeTarget(string newCode)
    {
        if (!IsOwner) return;
        userSettings.SetTarget(newCode);
        NotifyServerMyPrefsServerRpc(userSettings.SourceLanguage, userSettings.TargetLanguage);
    }
}
