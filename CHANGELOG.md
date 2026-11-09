# Histórico

## 1.2.1 — 2026-10-27

- Quick Panel usa material Acrylic, moldura de cliente estendida e uma cor translúcida discreta.
- Janela elegível para o Translucent Windows desde a criação e como popup após ocultar o título e os controles nativos.
- Reaplica o material depois de posicionar e reabrir o painel.
- Mantém o painel sem redimensionamento e trata o fechamento pelo sistema como dispensa, preservando a próxima abertura.
- Não altera os filtros de brilho, recuperação de overlays, presets salvos nem horários.

## 1.2.0 — 2026-10-27

- Transições curtas e consistentes na navegação, botões, switches, cartões, diálogos e Quick Panel.
- Destaque discreto em mensagens de presets e automação somente quando o texto muda.
- Respeita animações do Windows e alto contraste; libera relógios ao terminar e ao ocultar a interface.
- Mantém os componentes de overlay, brilho, presets e automação sem alterações funcionais.
- Amplia a verificação WPF com posição final de switches, reversão rápida, textos inalterados e liberação de efeitos.

## 1.1.2 — 2026-10-23

- Recuperação dirigida por eventos quando a janela ativa ou a barra de tarefas fica acima de um overlay escurecido.
- Verifica sobreposição física e ordem real, preserva foco, brilho e Quick Panel; limita tentativas para evitar disputa contínua de Z-order.
- Mantém logs diagnósticos, acrescenta o caminho do executável em execução e registros OVERLAY-RECOVERY.
- Pacote inclui atualização por usuário com backup que unifica instalação, startup existente e atalhos do ShadeFlow.
- Validação de composição gráfica, Alt+Tab, Explorer e ausência de piscadas depende de teste no Windows.

## 1.1.1-diagnostic — 2026-09-30

- Registra alterações de foreground e Z-order para diagnosticar overlays que ficam atrás dos aplicativos.
- Registra estilo topmost, posição física, opacidade e janela imediatamente acima de cada overlay.
- Não reposiciona, recria nem altera o comportamento visual dos overlays.

## 1.1.0 — 2026-09-17

- Retomar automação diretamente no Quick Panel.
- Presets com oito ícones escolhidos pelo usuário e brilho por StableId.
- Criar, editar, atualizar com os valores atuais e excluir presets na janela principal.
- Presets até próximo horário ou mantidos até retomar, com transição individual e cancelamento.
- Persistência, import/export, reset e reconexão integrados ao fluxo existente.

## 1.0.3 — 2026-09-14

- Recompila o aplicativo com autoria e copyright de George GS Matos nos metadados
  e na tela Sobre.
- Inclui licença, EULA e aviso legal no pacote portátil.
- Preserva o comportamento funcional da 1.0.2.

## 1.0.2 — 2026-09-11

- Remove o reforço global de Z-order introduzido na 1.0.1, que podia causar
  piscadas ao abrir conteúdo acelerado por GPU em navegadores e no VS Code.
- Restaura o comportamento de overlays validado na 1.0.0 e preserva todas as
  funcionalidades de brilho, scheduler, hotkeys, tray e hotplug.
- Prioriza estabilidade visual; a taskbar e algumas superfícies especiais do
  compositor do Windows podem permanecer sem dimming.

## 1.0.1 — 2026-09-10

- Reforça o Z-order dos overlays quando a taskbar, um vídeo ou outra janela de
  primeiro plano muda a composição do desktop.
- A guarda é dirigida por eventos Win32, com debounce, e permanece inativa quando
  o desktop não muda.
- Mantém o Quick Panel acima do dimming após o reforço.

## 1.0.0 — 2026-09-10

- Empacotamento Windows x64 com .NET incluído, ZIP portátil e instalador por usuário.
- Atalhos no menu Iniciar, atalho opcional na área de trabalho e desinstalação.
- Instalador verifica a instância ativa antes de substituir arquivos; não encerra overlays à força.
- Desinstalação preserva aliases, programação, preferências e logs; remove startup apenas quando aponta para esta instalação.
- Identidade Eclipse híbrido e interface Fluent da versão 0.95 preservadas.
- Núcleo aprovado: dimming preto, multi-monitor, scheduler, transições, override, hotkeys, tray e recuperação.
- Configuração permanece V3, incluindo importação do V4 experimental descartado.

Os resultados de build e testes acompanham os artefatos. A validação em hardware,
multi-DPI, Explorer e instalação/desinstalação deve ser realizada no Windows antes
de distribuir publicamente. Os binários não são assinados digitalmente pelo autor.

## Preparação do repositório público

- Documentação atualizada, guias de instalação e arquitetura, modelos de issue/PR e política de contribuição proprietária.
