# Validação do modo de desenvolvimento — 03/11/2026

## Alterações

- Iniciar-ShadeFlow-Dev.cmd e adaptadores PowerShell/WSL.
- Supervisor Python com SDK local verificado, compilação incremental, observador por conteúdo, recarga de estilos e reinício cooperativo.
- Canal de controle local restrito a Debug, com revisão de sessão e perfil próprio em .dev/profile.
- Instância de desenvolvimento separada, janela de configurações identificada e atalhos/inicialização do Windows preservados.
- Prévia de brilho sem criação de overlays reais. A produção mantém o comportamento padrão do OverlayManager.
- Página, tamanho e posição da janela salvos para os reinícios. Alterações de estilos abrangem as janelas WPF da sessão.
- .dev e caches excluídos do projeto e do snapshot de empacotamento.

## Verificações concluídas

- 6 testes do supervisor: classificação de alterações, descoberta de fontes, exclusão de arquivos gerados, manifesto/propriedades e escrita atômica.
- 10 verificações Windows/WSL com cópia descartável: perfil isolado, ausência de overlays, recarga automática de recurso WPF sem reinício, preservação após XAML inválido, recuperação ao corrigir, reinício após alteração de código, persistência do estado, parada cooperativa e saída normal do supervisor.
- 5 verificações pontuais do OverlayManager: monitores em prévia, alteração de brilho representada no estado, ausência de overlays em prévia, restauração e manutenção dos overlays no modo normal.
- Compilações Debug e Release verificadas sem erros ou avisos durante a implementação.
- SDK e cache próprios preparados no caminho final do projeto; iniciador e arquivos de projeto conferidos.

O teste do observador foi executado novamente a partir do projeto final, utilizando seus caches próprios. As sessões temporárias foram encerradas normalmente. Um teste inicial de integração precisou ter sua sincronização corrigida: ele enviava consultas simultâneas ao comando de recarga no mesmo canal. A versão final aguarda o observador concluir antes de consultar o estado.

## Limites

Não foram alterados a instalação Windows, o perfil normal, os pacotes publicados ou a inicialização do ShadeFlow de uso normal. Não foi gerado instalador, commit ou publicação. Os testes não comprovam a aparência física do Windhawk nem o efeito de escurecimento real; o modo de desenvolvimento não cria esse efeito.

Instruções de uso: desenvolvimento.md.
