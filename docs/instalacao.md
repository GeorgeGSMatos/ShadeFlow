# Instalação e atualização

## Requisitos

Windows 10/11 x64. O ZIP oficial é autocontido e inclui .NET; não é necessário instalar ferramentas de desenvolvimento.

## Primeira execução

1. Baixe a Release estável do repositório oficial e o arquivo SHA256SUMS correspondente.
2. Confira o pacote com `Get-FileHash .\nome-do-pacote.zip -Algorithm SHA256` e compare o resultado com o arquivo publicado.
3. Extraia o ZIP inteiro para uma pasta **local**, fora do compartilhamento WSL.
4. Abra `ShadeFlow.exe`. Não separe o executável dos demais arquivos.

Os executáveis ainda não são assinados digitalmente. Leia qualquer aviso do Windows e confirme a origem do download; não desative mecanismos de segurança do sistema.

## Atualizar

Encerre pelo ícone da bandeja → **Sair**. O X apenas recolhe a janela. Preserve uma cópia da versão anterior até confirmar a nova.

Extraia a nova versão em outra pasta e execute `Atualizar-ShadeFlow.cmd`. O atualizador instala em `%LOCALAPPDATA%\Programs\ShadeFlow`, mantém backup e corrige atalhos e a entrada de inicialização já habilitada.

## Dados e inicialização

As configurações ficam em `%LOCALAPPDATA%\ShadeFlow`, fora da pasta do executável. Aliases, presets, horários e preferências ficam nesse perfil.

Ative **Iniciar com o Windows** na instalação local. O aplicativo inicia na bandeja, na sua conta de usuário, sem exigir administrador. Os atalhos e a automação são registrados na versão de uso normal.

Para desinstalar uma cópia portátil, encerre e desative o início com Windows antes de remover a pasta. O perfil pode ser mantido para reinstalação. A limpeza desse perfil é uma decisão separada e remove suas configurações.
