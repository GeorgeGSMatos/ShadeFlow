# Captura de atalhos

Em Configurações, cada ação exibe o atalho em um botão. Clique para iniciar a
captura e pressione uma tecla ou combinação. Modificadores aparecem enquanto
são pressionados; Esc cancela mantendo o valor anterior. Sair do aplicativo ou
mudar de foco cancela a captura. A ação de limpar remove o rascunho do atalho.

Combinações inválidas ou repetidas mostram uma explicação junto ao controle.
A captura mantém o valor anterior até uma combinação válida ser escolhida.
Remover o atalho de Restaurar 100% gera aviso explícito; o botão de recuperação
e a bandeja permanecem disponíveis.

Aplicar atalhos valida e registra a configuração completa. Conflitos externos
continuam sendo informados pela camada existente de RegisterHotKey; registrar
ou persistir uma configuração não é confundido com capturar teclas. Durante a captura, uma interceptação temporária lê a combinação antes de
RegisterHotKey, inclusive quando a versão normal ou outro programa já usa o
atalho. A tecla principal é consumida para não executar o comando externo;
eventos de modificadores continuam chegando ao Windows. Isso não torna a
combinação livre para registro: Aplicar atalhos ainda informa conflitos reais.
A interceptação só processa eventos com a janela editora em primeiro plano e
é removida ao aceitar, cancelar, perder foco ou descarregar o controle. Não
mantém histórico de teclas. Atalhos que pertencem à própria instância também
são encaminhados ao capturador, em vez de executar a ação de brilho.

O modo de desenvolvimento salva os rascunhos apenas em seu perfil separado e
não registra teclas globais. A conferência de conflitos reais com Windows e
outros aplicativos exige a execução normal. Combinações protegidas pelo
sistema, como Ctrl+Alt+Del, não são capturáveis dessa maneira; Fn depende do
teclado e não é necessariamente uma tecla entregue ao Windows.

Controls/HotkeyCaptureButton concentra a captura, cancelamento, foco e estado
visual. Services/HotkeyDisplay formata os nomes. MainWindow.Preferences mantém
a validação entre ações e a aplicação explícita. A persistência continua usando
HotkeyBinding; o formato do arquivo de configuração não foi alterado.

Nenhuma suíte de testes foi executada nesta alteração. A atualização é feita
no ambiente de desenvolvimento já solicitado; a instalação normal permanece
preservada. Durante uma captura, o supervisor aguarda antes de reiniciar.

## Correção da combinação Ctrl + Alt + A

Foi identificada a versão normal em execução com Ctrl+Alt+A registrado para
retomar automação. A captura anterior dependia de PreviewKeyDown no WPF;
o evento pode não chegar quando RegisterHotKey já consome a combinação.
ForegroundShortcutCapture elimina essa dependência durante a captura.
Não foram executados testes de reprodução ou regressão nesta rodada; a
compilação incremental e a abertura pertencem ao fluxo de desenvolvimento.
