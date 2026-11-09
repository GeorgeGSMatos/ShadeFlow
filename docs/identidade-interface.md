# Identidade compartilhada com o Organiza

## Base

A interface usa fontes Segoe, superfícies grafite, medidas compartilhadas e
ícones Fluent. Estilos ficam em Styles/Controls.xaml; cores e medidas em
Styles/Colors.xaml. ThemeService resolve os recursos de tema e alto contraste.
AppIcons centraliza os ícones de ação. InterfacePresentation centraliza os
significados visuais e evita cores escolhidas por texto de mensagem.

| Tom | Significado |
| --- | --- |
| Azul | Automação ativa, ação principal e andamento |
| Lilás | Ajuste manual e preset/intervenção manual |
| Amarelo | Atenção, pausa/impedimento, configuração incompleta |
| Verde | Confirmação de operação concluída |
| Vermelho | Exclusão, restauração de padrões e falhas |
| Cinza | Informação neutra e automação desativada |

Cor acompanha texto e, nos indicadores de modo, ícone. A porcentagem de brilho
não indica erro ou sucesso. Estados de ajuste temporário, pausa até retomar e
automação desativada continuam descritos separadamente.

## Controles e dropdowns

- Campos de uma linha com altura de 32 px, alinhamento vertical centralizado e
  aplicação única do Padding.
- Botões principais com azul; ações secundárias neutras; lixeiras vermelhas.
- Edição de horário/preset usa lápis com nome acessível e dica contextual.
- Restaurar 100% e Aplicar mantêm texto visível.
- Hover cobre o botão inteiro, respeita desabilitado e não desloca conteúdo.
- Foco por teclado permanece visível; redução de movimento e alto contraste
  são respeitados.
- Dropdowns têm superfície cinza translúcida por pixel, borda, cantos e item
  selecionado distinto do hover. A abertura usa transição curta.
- DropdownSurface consulta a preferência de transparência do Windows e aplica
  fundo sólido quando necessário. Transparência por pixel não implementa blur.
- Os dropdowns WPF de Configurações e do editor de presets compartilham o estilo.
  O menu da bandeja continua sendo um menu WinForms/nativo.

## Páginas

Configurações usa linhas com ícone, título/descrição e controle. Geral inclui
inicialização; Brilho e transição inclui vínculo, duração e passo de ajuste;
Atalhos mantém campos alinhados e Aplicar atalhos explícito; Dados e configuração
separa importação/exportação da restauração de padrões.

Monitores preserva nome, resolução, percentual e slider, com detalhes em área
secundária. Automação usa ações compactas de editar/excluir nos horários.
Presets conserva Aplicar e deixa edição, atualização e exclusão como ações
secundárias. Sobre mantém versão e autoria em um cartão compacto.

O painel rápido mantém sua densidade própria, usa o catálogo de ícones e os
mesmos estados da janela. Gerenciar presets usa ícone de coleção; lápis é
reservado à edição de um item. Textos de modo têm espaço para quebrar linha.

Os diálogos de horário, preset e diagnóstico usam a identidade e o material
transitório do Windows com alternativa sólida. O editor de presets tem ações
fixas fora da área rolável. Cada diálogo conserva seus campos e validações. Confirmações de excluir horário,
excluir preset, importar e restaurar padrões compartilham ConfirmationDialog,
assim como os avisos desses fluxos. Ações destrutivas têm confirmação vermelha e
Cancelamento como opção inicial. O seletor de arquivos do Windows e a mensagem
de recuperação de erro fatal permanecem nativos.

## Execução e limites desta alteração

As mudanças foram aplicadas ao ambiente de desenvolvimento já solicitado.
A compilação incremental e a reabertura foram acompanhadas nesse fluxo; não
foi executada uma suíte de testes, lint ou análise de tipos independente.

Durante a atualização, um recurso de largura de coluna foi definido inicialmente
como Double, causando falha de abertura; foi corrigido para GridLength.
Também foram ajustadas as referências dos novos componentes compartilhados.

A apresentação física de transparência, legibilidade, foco e medidas ainda
precisa ser conferida no uso da interface. A instalação Windows e os pacotes
de distribuição não foram atualizados. Os dados normais, atalhos globais,
algoritmos de brilho, presets e agendamento foram preservados.

Achado preexistente, separado do ajuste visual: quando a janela falha antes de
terminar o construtor, o tratamento de erro fatal presume que _transition já
existe e pode produzir uma exceção secundária. Esse caminho foi observado ao
investigar a falha de abertura e não foi alterado nesta demanda.
