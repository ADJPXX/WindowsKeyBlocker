# WindowsKeyBlocker

Utilitário para Windows escrito em C# que bloqueia as teclas Windows esquerda e direita quando um dos processos configurados está em primeiro plano.

## Como funciona

O aplicativo identifica o processo dono da janela ativa. Quando o nome do processo está na lista `Executables`, um hook de teclado impede que as teclas Windows sejam enviadas ao sistema.

A comparação é feita pelo nome do processo, sem a extensão `.exe`. Por exemplo, use `cs2`, não `cs2.exe`.

## Requisitos

- Windows
- .NET 10 SDK para compilar

## Configuração

Na primeira execução, o programa cria `config.json` na pasta base do executável. O arquivo deve ter este formato:

```json
{
  "Executables": [
    "cs2",
    "rocketleague"
  ]
}
```

Os nomes precisam corresponder aos processos dos jogos. Consulte a aba **Detalhes** do Gerenciador de Tarefas para encontrar esses nomes.

Ao criar a configuração com os valores de exemplo `game 1` e `game 2`, o programa espera que pelo menos um deles seja removido ou substituído antes de continuar.

## Compilar

Na pasta do projeto:

```powershell
dotnet restore
dotnet build -c Release
```

Para publicar um executável autocontido de 64 bits:

```powershell
dotnet publish .\WindowsKeyBlocker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish
```

O `config.json` usado deve ficar na pasta do executável publicado.

## Estado conhecido
O projeto também está configurado como aplicativo de console (`OutputType` igual a `Exe`). Ainda não há um argumento implementado para solicitar o encerramento pelo evento de parada.