# Validação do ShadeFlow

## Rodada Windows

Requisitos: PowerShell 5.1+, SDK .NET 8, Python 3.12 e acesso aos pacotes NuGet na primeira restauração.

Na raiz do checkout, execute `powershell -NoProfile -File scripts/Validate.ps1`.
O script encerra com erro quando uma verificação obrigatória falha. O GitHub
Actions executa a mesma entrada em Windows, sem usar o perfil instalado.

A rodada inclui verificações da lógica e WPF em prévia. Não registra atalhos globais de produção nem altera o brilho real dos monitores.

Capturas renderizadas da interface usam dados de teste. Não comprovam o efeito
DWM, Windhawk, composição do desktop, monitores físicos ou o comportamento de um
login real do Windows. Esses pontos exigem conferência manual no equipamento.

## Política de execução

No trabalho assistido por agente, executar testes, lint e build exige `/test`
ou solicitação explícita. A preparação inicial deste repositório foi autorizada
com as três tasks de preparação, validação e publicação. Não publicar resultados
antigos como se tivessem sido obtidos sobre o código atual.

## Supervisor WSL

O supervisor depende de `fcntl` e é validado no Linux/WSL, em job próprio do CI:

```bash
python3 scripts/test-dev.py
```
