# Arquitetura do ShadeFlow

Aplicativo Windows x64 com interface WPF e .NET 8. Não há backend remoto obrigatório.

| Área | Responsabilidade |
| --- | --- |
| `MainWindow.*` | Composição da interface e coordenação das páginas |
| `Controls` e `Styles` | Controles reutilizáveis, tokens e estilos |
| `Models` | Preferências, monitores, horários e presets |
| `Services` | Descoberta de monitores, overlays, automação, atalhos e persistência |
| `Views` | Painel rápido, editores, ajuda e diagnóstico |
| `Development` | Prévia isolada e recarga de desenvolvimento |
| `Tests` e `scripts/VisualChecks` | Verificações de lógica e WPF isoladas |

Os overlays fazem escurecimento visual por monitor e devem ser removidos ao encerrar. O agendador coordena horários e intervenções manuais. A captura de atalhos usa um hook de teclado apenas durante a captura explícita com a janela em primeiro plano; não mantém histórico de digitação.

## Perfis e ciclo de vida

O perfil normal fica em `%LOCALAPPDATA%\ShadeFlow`. Desenvolvimento usa `.dev/profile`, com serviços restritos para evitar interferência no uso normal. Fechar a janela principal recolhe a versão normal à bandeja; **Sair** conclui o encerramento.

Os estilos usam recursos compartilhados para cores, medidas e estados. Ajuda contextual fica na navegação; informações do produto e diagnóstico ficam no cartão Sobre de Configurações. Recursos de composição respeitam as preferências e capacidades do Windows.
