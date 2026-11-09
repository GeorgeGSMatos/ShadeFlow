<div align="center">
  <img src="Assets/Brand/ShadeFlow.svg" alt="Ícone do ShadeFlow" width="88" />
  <h1>ShadeFlow</h1>
  <p>Controle o brilho visual de cada monitor, no seu ritmo.</p>
  <p><a href="https://github.com/GeorgeGSMatos/ShadeFlow/releases/latest">Baixar para Windows</a> · <a href="docs/instalacao.md">Instalação</a> · <a href="docs/desenvolvimento.md">Desenvolvimento</a> · <a href="https://github.com/GeorgeGSMatos/ShadeFlow/issues">Suporte</a></p>
  <img src="https://github.com/GeorgeGSMatos/ShadeFlow/actions/workflows/ci.yml/badge.svg" alt="Validação Windows" />
</div>

## Recursos

- Ajuste o brilho visual de cada monitor ou vincule os ajustes.
- Programe níveis de brilho ao longo do dia.
- Salve presets para trabalho, jogos e uso noturno.
- Capture atalhos globais pressionando a combinação desejada.
- Acesse o painel rápido pela bandeja e restaure 100% quando precisar.

Interface nativa WPF, tema escuro, ícones consistentes e animações que respeitam as preferências do Windows. Ajuda contextual na navegação; versão e autoria em **Configurações → Sobre**.

## Interface

Capturas renderizadas dos controles reais com dados de teste. O desfoque do desktop depende da composição do Windows e não é reproduzido nestas imagens.

![ShadeFlow — Monitores](docs/assets/Displays.png)

![ShadeFlow — Configurações](docs/assets/Preferences.png)

## Comece a usar

1. Baixe o pacote Windows x64 em [Releases](https://github.com/GeorgeGSMatos/ShadeFlow/releases/latest).
2. Extraia **todo o ZIP** para uma pasta local e abra `ShadeFlow.exe`.
3. Consulte o [guia de instalação e atualização](docs/instalacao.md) antes de substituir uma versão existente.

O pacote inclui os runtimes necessários. O escurecimento usa uma camada visual: não altera o brilho físico ou economiza a energia da luz de fundo do monitor.

## Compatibilidade e limites

- Windows 10/11 x64. Materiais nativos e desfoque dependem da versão e das configurações do Windows; há fundo sólido de fallback.
- Superfícies protegidas, UAC e certos modos de tela cheia podem ficar acima do filtro. Há um comando de restauração imediata para 100%.
- Os executáveis desta distribuição não têm assinatura digital de editor. Os hashes SHA-256 permitem conferir a integridade do download.

## Documentação

| Guia | Conteúdo |
| --- | --- |
| [Instalação](docs/instalacao.md) | Requisitos, atualização, bandeja e localização dos dados |
| [Desenvolvimento](docs/desenvolvimento.md) | Compilação e edição com perfil separado |
| [Arquitetura](docs/arquitetura.md) | Responsabilidades e integração com o Windows |
| [Validação](docs/validacao.md) | Comandos, testes e limites da cobertura |
| [Releases](docs/releases.md) | Versionamento, pacotes e publicação |
| [Changelog](CHANGELOG.md) | Evolução das versões |

## Suporte e segurança

Para bugs e sugestões, use os [modelos de issue](https://github.com/GeorgeGSMatos/ShadeFlow/issues/new/choose). Remova nomes de usuário, caminhos pessoais e documentos das capturas e logs. Vulnerabilidades seguem a [política de segurança](SECURITY.md).

## Autoria e licença

Criado e mantido por **[George GS Matos](https://github.com/GeorgeGSMatos)**. Copyright © 2026. Todos os direitos reservados.

Este repositório tem **código visível e licença proprietária**; não é distribuído sob uma licença open source. Consulte [LICENSE.txt](LICENSE.txt), [EULA.txt](EULA.txt), [AUTHORS.md](AUTHORS.md) e [NOTICE.md](NOTICE.md). As permissões de visualização e fork da plataforma não equivalem a uma licença geral de reutilização. Contribuições seguem [CONTRIBUTING.md](CONTRIBUTING.md).
