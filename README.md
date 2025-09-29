# Translation VR

Sistema de tradução em tempo real para aplicações VR usando Azure Speech Services e Unity Netcode.

## Configuração do Azure Speech Services

⚠️ **IMPORTANTE: Nunca commite chaves de API diretamente no código!**

### Opção 1: Variáveis de Ambiente (Recomendado)

1. Configure as seguintes variáveis de ambiente:
   ```bash
   export AZURE_SPEECH_KEY="sua_chave_aqui"
   export AZURE_SPEECH_REGION="brazilsouth"
   ```

### Opção 2: ScriptableObject (Para desenvolvimento local)

1. No Unity, clique com o botão direito na pasta Project
2. Vá em "Create > Translation VR > Azure Config"
3. Nomeie o arquivo como "AzureConfig"
4. Insira sua chave e região do Azure
5. Arraste o ScriptableObject criado para os campos "Azure Config" nos scripts de tradução

**ATENÇÃO:** O arquivo AzureConfig.asset está no .gitignore para evitar commitar chaves acidentalmente.

### Como obter as chaves do Azure

1. Acesse o [Portal do Azure](https://portal.azure.com)
2. Crie um recurso "Speech Services"
3. Copie a chave e região da aba "Keys and Endpoint"

## Arquivos Principais

- `NetworkedSpeechTranslator.cs` - Tradutor principal com networking
- `NetworkedSpeechTranlator2.cs` - Versão alternativa do tradutor
- `NetworkedSpeechTranslatorPush.cs` - Tradutor com push audio stream
- `Server/SpeechManagerServer.cs` - Gerenciador de servidor
- `AzureConfig.cs` - Configuração segura do Azure