# Ajuda e Sobre

A navegação mantém Ajuda no rodapé. Esse botão abre uma janela contextual,
sem mudar a página selecionada. Os tópicos de Monitores, Automação, Presets e
Configurações são definidos em Services/ContextHelp.cs.

A ajuda explica recuperação com Restaurar 100%, diferenças entre manual e
automático, presets mantidos até retomar, atalhos e bandeja. No desenvolvimento,
a introdução identifica a prévia e a separação dos dados. A janela segue o
material dos demais diálogos e fecha por Entendi ou Esc.

Sobre fica ao final de Configurações e mostra versão, autoria e licença.
Informações e diagnóstico permanece uma ação secundária desse cartão. A antiga
página Sobre foi removida da navegação; estados de desenvolvimento antigos que
a referenciam passam a abrir Configurações.

O Organiza segue a mesma divisão: Ajuda contextual no rodapé da navegação,
Sobre em Configurações e Informações e diagnóstico separado das orientações
de uso. Cada aplicativo mantém conteúdo correspondente às suas funções.

Esta alteração não executa testes. O ShadeFlow é atualizado pelo fluxo de
desenvolvimento solicitado; a instalação normal permanece preservada.

O cartão Sobre usa uma ação em formato de link à direita, com o mesmo estilo
visual do Organiza. A janela Informações e diagnóstico apresenta título com
ícone, descrição, relatório local e Copiar diagnóstico no corpo; Fechar fica
no rodapé separado. O material nativo é o dos demais diálogos do aplicativo.
O relatório copiado mantém os detalhes técnicos dos monitores e da automação,
além da identificação de licença. Falhas de acesso à área de transferência
aparecem no rodapé, sem fechar a janela ou simular sucesso.
