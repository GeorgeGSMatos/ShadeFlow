# Relatório histórico de validação

Os resultados abaixo descrevem entregas anteriores. Para a publicação atual, consulte docs/publicacao.md e docs/validacao.md.

# Validação 1.2.1 — Quick Panel

Build/publicação Windows x64 com SDK .NET 8.0.426. Foram aprovados 161 checks de lógica e 49 checks WPF isolados no Windows. Nenhum filtro de produção foi iniciado pelo teste de interface.

As verificações novas conferem a configuração da janela na criação do HWND, os estilos nativos finais de popup aceitos pelo critério do Translucent Windows, ausência de título e redimensionamento, WindowChrome sem botões, camada de cor translúcida e dispensa pelo comando de fechar sem destruir a janela reutilizada. Os testes anteriores de sliders, presets, retomada, diálogos e animações foram preservados.

O teste renderiza os controles em um host isolado e inspeciona o HWND real do Quick Panel. Ele não abre o painel do aplicativo instalado, não modifica preferências do Windows/Windhawk e não comprova a aparência do blur no desktop. Reabertura física, mouse, escalas de DPI e resultado do mod precisam ser conferidos pelo usuário.

As alterações funcionais se limitam ao Quick Panel. Nenhum arquivo de gerenciamento de overlays, transição de brilho, scheduler ou configurações foi modificado. O pacote é portátil, sem assinatura digital. O instalador Inno Setup não foi gerado nesta entrega.

Referências: [elegibilidade de janelas do Translucent Windows](https://github.com/ramensoftware/windhawk-mods/blob/main/mods/translucent-windows.wh.cpp), [materiais DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type).

## Histórico: ShadeFlow 1.1.2 — recuperação do overlay

Preparada em 2026-10-23 a partir da 1.1.1 diagnóstica, preservando presets.

- Build Release e publish self-contained Windows x64 concluídos com SDK .NET 8.0.426;
  zero erros e zero warnings.
- 161 checks de lógica aprovados (148 anteriores + 13 de recuperação).
- 33 checks WPF isolados aprovados no Windows, sem iniciar serviços de produção.
- Sintaxe do atualizador validada no parser do Windows PowerShell. A atualização
  da instalação em uso não foi executada; requer Sair pelo tray antes de rodar o CMD.

- Critério de reparo: janela ativa ou taskbar visível, não cloaked, acima do filtro
  e com interseção no mesmo monitor. Ignora opacidade zero e amostras sem Z válido.
- Reposição somente com HWND_TOPMOST, NOACTIVATE, NOMOVE, NOSIZE e NOOWNERZORDER.
- Filtro de eventos próprios, janela de coalescência de 350 ms sem starvation,
  cooldown de 2 s e circuito de proteção contra disputa contínua.
- 13 checks novos: reprodução lógica dos logs, sobreposição, outro monitor,
  taskbar, transparência, amostra desconhecida, cooldown e circuito.
- Atualizador explícito com backup para instalação/startup/atalhos consistentes.
- Hardware, cobertura de superfícies especiais e efeito da correção no DWM
  precisam de teste manual no Windows. Não há garantia de cobertura do Alt+Tab.

Roteiro prioritário: brilho 50%, Chrome/VS Code/Bloco de Notas, presets, Win+D,
Alt+Tab, Explorer reiniciado, transição + hotplug, Ctrl+Alt+0 e saída a 100%.

## Histórico da entrega 1.1.0

Preparada em 2026-09-17. Presets por StableId com oito ícones e retomada no Quick Panel.

- Restore, build e publish Release Windows x64 com SDK .NET 8.0.425.
- 148 checks de lógica (109 anteriores e 39 novos).
- 33 checks WPF isolados executados no Windows: navegação, ícones, bindings,
  evento de aplicação de preset, retomada e formulário. Sem iniciar serviços reais.
- Imagens renderizadas de Presets, Quick Panel e editor revisadas.
- Pacote portátil self-contained; sem instalador ou assinatura digital de editor.

Validação manual prioritária:
1. Ajustar dois monitores, salvar Jogo com ícone de controle e valores diferentes.
2. Criar Noite com ícone de lua; aplicar ambos pelo Quick Panel.
3. Testar transição de 5 segundos e cancelamento por slider/Ctrl+Alt+0.
4. Testar um preset até próximo horário e outro até retomar, incluindo sleep/resume.
5. Retomar no Quick Panel e confirmar o evento vigente em ambos os monitores.
6. Reconectar Odyssey durante preset, durante transição e após ajuste manual.
7. Reiniciar e confirmar presets salvos, sem restaurar preset ativo/pausa.
8. Importar/exportar e testar saída durante transição: 100% imediatamente.

Hardware, compositor, MPO e backdrop continuam dependendo desses testes manuais.

## Histórico da validação 1.0.3

Preparada em 2026-09-14.

- Restore, build Release Windows x64 e publish self-contained concluídos com SDK .NET 8.0.425.
- Build: zero erros e zero warnings.
- 109 checks de lógica aprovados no WSL.
- Metadados de ShadeFlow.exe e ShadeFlow.dll conferidos pelo Windows via FileVersionInfo:
  FileVersion 1.0.3.0, ProductVersion 1.0.3, CompanyName George GS Matos e copyright correto.
- Texto de autoria da tela Sobre incorporado à nova compilação WPF.
- Pacote inclui LICENSE.txt, EULA.txt e NOTICE.md.
- Não foram repetidos testes visuais nem de hardware nesta entrega.
- Não foi gerado instalador 1.0.3 nem assinatura digital de editor.

Para atualizar: encerrar pelo tray, extrair todo o ZIP e executar ShadeFlow.exe.
Confirmar autoria na tela Sobre, testar Ctrl+Alt+0 e saída segura.

## Histórico da entrega anterior (não executado novamente na 1.0.3)

Preparada em 2026-09-10, com configuração V3 e identidade Eclipse híbrido.

## Executado

- Clean e restore Windows x64.
- Publicação Release self-contained (inclui .NET 8), sem trimming.
- 109 checks de lógica aprovados no WSL.
- 23 checks nativos WPF aprovados no Windows via executável isolado.
- Corrigido o isolamento do verificador: PreviewApp suprime App.OnStartup, além da
  supressão de inicialização nativa de PreviewWindow. A versão anterior do teste
  podia inicializar serviços reais ao processar a fila do Dispatcher.
- Compilação do instalador Inno Setup 6.7.3 concluída.
- Reforço global de Z-order da 1.0.1 removido para evitar piscadas com superfícies
  aceleradas; overlays voltaram ao comportamento visual validado na 1.0.0.
- Busca de APIs fora do escopo: nenhuma implementação encontrada. As ocorrências
  do filtro descartado ficam apenas no comentário de downgrade e seus testes.

Logs de execução ficam em `artifacts/release-1.0.2`. O script de empacotamento
verifica os arquivos do runtime, a integridade do ZIP e gera SHA256SUMS.txt.
O snapshot de fontes permite recuperar a entrega; o diretório original não é
um repositório Git e nenhuma tag Git foi criada.

## Validação manual pendente

1. Sair da versão anterior pelo tray e instalar o Setup por usuário.
2. Abrir pelo menu Iniciar e verificar ícone, Quick Panel e configurações existentes.
3. Testar dois monitores, linked displays, aliases e scheduler.
4. Transição de 5 segundos, cancelamento por slider e Ctrl+Alt+0.
5. Ctrl+Alt+A, hotkeys +/−, X para tray e segunda instância.
6. Startup, reinício do Explorer, sleep/resume, multi-DPI e Odyssey off/on.
7. Encerrar durante transição: 100%, nenhum overlay/tray restante.
8. Instalar novamente com o app fechado: aliases e horários preservados.
9. Desinstalar com o app fechado: atalhos/arquivos removidos, settings/logs preservados,
   startup removido somente quando apontar para a instalação removida.
10. Testar ZIP extraído em pasta permanente e convivência com a Luz Noturna.

Não foi executada instalação/desinstalação do ShadeFlow nesta sessão. Os checks WPF
validam controles e bindings; não comprovam backdrop, hardware, HDR ou comportamento
do desktop. Os pacotes não têm assinatura digital do autor. Não houve publicação externa.
# Validação 1.2.0 — 2026-10-27

- Build Release e publicação Windows x64 independentes de instalação do .NET, com SDK 8.0.426.
- 161 verificações de configuração, presets, agendamento e recuperação passaram com dados temporários no WSL.
- 42 verificações de interface WPF passaram no Windows, em janelas isoladas fora da área visível, usando o runtime .NET 8 instalado. O teste suprime a inicialização dos serviços de produção.
- Verificados: cinco páginas, controles, bindings, switches com reversão rápida, posição final, liberação de relógios, mensagens inalteradas, Quick Panel, editor de presets e camada de escurecimento do dono dos diálogos.
- A política de efeitos lê as preferências de animação e alto contraste do Windows. Não foram alteradas preferências globais do sistema durante os testes.
- Os efeitos se limitam a janelas com `UiMotion.Scope` explicitamente habilitado. `OverlayWindow`, gerenciamento de overlays e transições de brilho não foram modificados. Nenhum filtro foi iniciado pelo teste nativo.
- Essa validação de controles não comprova composição do desktop, cobertura do overlay, HDR/MPO/VRR ou ausência de falhas no uso físico dos monitores. Esses pontos continuam dependendo da validação de uso.
- O pacote portátil não possui assinatura digital e não substitui automaticamente uma instalação em execução. O instalador Inno Setup não foi gerado nesta entrega.

Referência da preferência nativa: https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.clientareaanimation
