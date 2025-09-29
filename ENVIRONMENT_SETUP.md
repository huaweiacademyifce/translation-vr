# Configuração de Variáveis de Ambiente para Azure Speech Services

## Linux/Mac

Adicione as seguintes linhas ao seu arquivo `~/.bashrc`, `~/.zshrc` ou `~/.profile`:

```bash
export AZURE_SPEECH_KEY="sua_chave_do_azure_aqui"
export AZURE_SPEECH_REGION="brazilsouth"
```

Depois execute:
```bash
source ~/.bashrc  # ou ~/.zshrc dependendo do seu shell
```

## Windows (PowerShell)

```powershell
$env:AZURE_SPEECH_KEY="sua_chave_do_azure_aqui"
$env:AZURE_SPEECH_REGION="brazilsouth"
```

Para tornar permanente, adicione no seu perfil do PowerShell ou use:
```powershell
[System.Environment]::SetEnvironmentVariable("AZURE_SPEECH_KEY", "sua_chave_do_azure_aqui", "User")
[System.Environment]::SetEnvironmentVariable("AZURE_SPEECH_REGION", "brazilsouth", "User")
```

## Unity Editor

Se preferir configurar no Unity, você pode criar um arquivo `launch.json` na pasta `.vscode` com:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Unity Editor",
            "type": "unity",
            "request": "launch",
            "env": {
                "AZURE_SPEECH_KEY": "sua_chave_do_azure_aqui",
                "AZURE_SPEECH_REGION": "brazilsouth"
            }
        }
    ]
}
```

## Verificação

Para verificar se as variáveis estão configuradas corretamente:

**Linux/Mac:**
```bash
echo $AZURE_SPEECH_KEY
echo $AZURE_SPEECH_REGION
```

**Windows:**
```powershell
echo $env:AZURE_SPEECH_KEY
echo $env:AZURE_SPEECH_REGION
```

## Importante

- **NUNCA** commite suas chaves reais no repositório
- Use nomes diferentes para desenvolvimento/produção
- Mantenha suas chaves seguras e não as compartilhe
- Considere usar Azure Key Vault para produção