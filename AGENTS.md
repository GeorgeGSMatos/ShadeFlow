# ShadeFlow: instruções do projeto

- Responder em português do Brasil e preservar alterações existentes.
- Ler README.md e docs/arquitetura.md antes de alterar responsabilidades.
- Aplicar Clean Code, erros explícitos e mudanças de escopo limitado.
- Nunca versionar perfis, credenciais, logs, caches ou binários gerados.
- Não executar testes, lint, análise de tipos ou build sem `/test` ou pedido explícito dessas verificações. Os workflows do GitHub executam as verificações documentadas por configuração aprovada do repositório.
- Usar dados temporários; não alterar startup, brilho real ou documentos pessoais em testes.
- Commit, push, publicação de Release e merge exigem autorização própria. Não presumir autorização por corrigir código.
- Usar Conventional Commits em português.

## /pr

A plataforma é GitHub e o destino padrão é `main`. Antes de abrir, verificar branch, diff e PRs existentes para evitar duplicatas. Usar `.github/PULL_REQUEST_TEMPLATE.md`, com problema, resultado, validação e limitações. Não executar testes implicitamente, não incluir segredos e não fazer merge automático.
