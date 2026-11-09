# Releases

O projeto usa tags `vMAJOR.MINOR.PATCH`. A versão da tag deve corresponder à
versão do ShadeFlow.csproj e do atualizador de packaging.

## Preparação

1. Revise alterações, documentação, licença e changelog.
2. Execute `scripts/Validate.ps1` em Windows com dados temporários.
3. Execute `scripts/Build-Release.ps1` para gerar o pacote autocontido x64.
4. Confira o ZIP, os documentos de licença e os hashes SHA-256.
5. Crie o commit e a tag somente com autorização do mantenedor.
6. Publique o ZIP e SHA256SUMS em uma Release correspondente à tag.

Os binários ficam em Releases; caches, perfis, logs e diretórios de build não
são versionados. O workflow de release é acionado manualmente, repete a validação
e publica primeiro como rascunho para revisão. A publicação inicial pode ser
feita pelo mantenedor com os artefatos validados localmente.

Os executáveis atuais não têm assinatura digital de editor. A versão pública
não deve alegar certificação, registro no INPI ou proteção técnica contra cópia.
