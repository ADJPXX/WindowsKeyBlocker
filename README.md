# WindowsKeyBlocker

Utilitário para Windows, escrito em C#, que bloqueia as teclas Windows esquerda e direita enquanto um jogo configurado está em primeiro plano. O aplicativo roda silenciosamente em segundo plano, sem janela, console ou ícone na bandeja.

## Como funciona

O programa consulta o processo dono da janela em primeiro plano. Se o nome do processo estiver na lista `Executables`, um hook de teclado impede que as teclas Windows sejam enviadas ao sistema. Ao mudar para outra janela, as teclas voltam a funcionar.

O programa verifica o processo em primeiro plano, não apenas se um jogo está aberto. Ele não aciona o botão físico Game Mode de teclados Logitech.

## Requisitos

- Windows
- .NET 10 SDK para compilar o projeto

O fluxo de release gera um executável autocontido para Windows x64.

## Configuração

O arquivo `config.json` fica na pasta base do executável. Na primeira execução, o programa cria esse arquivo com os placeholders `game 1` e `game 2` e aguarda até que pelo menos um deles seja removido ou substituído.

Exemplo:

```json
{
  "Executables": [
    "cs2",
    "rocketleague"
  ]
}
```

Use os nomes dos processos, sem `.exe`. Consulte a aba **Detalhes** do Gerenciador de Tarefas. A chave JSON deve ser `Executables`, com essa capitalização.

O caminho depende de qual executável você iniciar. No Debug, normalmente é `bin/Debug/net10.0/config.json`; no executável publicado, fica ao lado do `.exe`. Se editar um arquivo de configuração em outra pasta, essa instância não o usará.

## Compilar e publicar

Na pasta do projeto, execute:

```powershell
dotnet restore
dotnet publish .\WindowsKeyBlocker.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o publish
```

O executável será criado em `publish/WindowsKeyBlocker.exe`. Na primeira execução, ele cria `publish/config.json` e aguarda a edição. Abra esse arquivo, substitua ou remova pelo menos um placeholder e salve; o processo continua automaticamente e carrega a configuração.

## Encerrar

Atualmente não há opção de linha de comando implementada para sinalizar o evento de parada. Para encerrar o processo, use o Gerenciador de Tarefas do Windows.

## Solução de problemas

- Se o processo continuar aguardando, confira se ainda estão presentes os dois placeholders (`game 1` e `game 2`) ou se o JSON está válido. O programa aguarda a edição em intervalos de 1,5 segundo.
- Se as teclas Windows ainda funcionarem no jogo, confirme que editou o `config.json` ao lado do executável em execução e que o nome corresponde ao processo que possui a janela do jogo.
- O projeto usa `WinExe`, então mensagens escritas com `Console.WriteLine` não ficam visíveis em uma janela de console. No momento, não há arquivo de log para erros de leitura da configuração ou instalação do hook.
