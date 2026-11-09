# Publicação inicial — 8 de novembro de 2026

A preparação preservou a licença proprietária e a autoria de George GS Matos.
O repositório registra o estado atual dos fontes, sem reconstruir commits anteriores.

## Validação local

- Lógica: 161 verificações aprovadas.
- Isolamento da prévia de desenvolvimento: cinco verificações aprovadas.
- WPF Windows: 51 verificações aprovadas, incluindo Ajuda e Diagnóstico.
- Supervisor WSL: seis testes aprovados.
- Builds de produção Windows x64 gerados com SDK .NET 8 no WSL.
- Sintaxe dos scripts PowerShell conferida pelo parser do Windows.
- Workflows conferidos com actionlint 1.7.12, fixação de Actions por SHA e permissões mínimas.
- ZIP autocontido conferido quanto a integridade, licença e ausência de perfis, logs e credenciais.
- Hash SHA-256 gerado para o pacote binário.
- Capturas renderizadas dos controles reais usam dados de teste e caminhos pessoais omitidos.

## Limites

Esta rodada não comprova o efeito físico do DWM/Windhawk, superfícies protegidas,
modo exclusivo em jogos, múltiplos monitores físicos ou o próximo login do Windows.
Os executáveis não têm assinatura digital de editor. Os testes não foram executados
sobre documentos ou perfis de produção.

## Automação

CI usa Windows para os componentes nativos e Linux para o supervisor WSL.
Releases futuras podem ser preparadas pelo workflow manual, que exige tag e versão
consistentes, repete as verificações e cria um rascunho para revisão.
