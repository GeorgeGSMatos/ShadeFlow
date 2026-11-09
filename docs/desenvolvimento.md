# Desenvolvimento no Windows com fontes no WSL

Abra o arquivo original **Iniciar-ShadeFlow-Dev.cmd** dentro da pasta completa do
projeto, pelo Explorer:

```text
\\wsl.localhost\Ubuntu-24.04\home\seu-usuario\dev\ShadeFlow
```

Não baixe ou copie somente o CMD: os scripts e os fontes precisam estar ao lado
dele. Mantenha o terminal aberto durante o desenvolvimento. Fechar a janela do
ShadeFlow encerra a sessão; Ctrl+C no terminal solicita encerramento normal.

Também é possível iniciar no terminal WSL:

```bash
bash scripts/dev.sh
```

O supervisor compila Debug incrementalmente no WSL e abre a interface com o
runtime .NET Desktop 8 do Windows. Não publica, não gera instalador e não executa
uma bateria de testes a cada alteração. Na primeira execução, prepara o SDK
.NET 8 local com SHA-512 verificado; o runtime Desktop do Windows precisa estar
instalado. Caches, compilações e sessões ficam em `.dev`, fora do código-fonte.

## Atualizações

- `Styles/Colors.xaml` e `Styles/Controls.xaml`: recarga na janela aberta.
- C#, estrutura das telas XAML, ícones, manifesto e arquivos de projeto:
  compilação incremental seguida de encerramento e reinício cooperativos.
- Erro de compilação: a janela anterior permanece aberta; corrija e salve.
- XAML de estilos inválido: preserva os recursos anteriores e informa o erro
  no terminal; corrija e salve novamente.
- Durante transições, arrastes ou diálogos abertos, a atualização espera.
- A página selecionada, o tamanho e a posição da janela são restaurados após
  reiniciar. Ajustes de configuração permanecem no perfil de desenvolvimento.

A recarga de estilos não equivale ao Hot Reload completo do Visual Studio:
reestruturar uma tela requer compilação e reinício. Alterações no próprio
supervisor ou nos scripts de inicialização exigem reiniciar o CMD.

## Separação da versão de uso normal

O perfil fica em `.dev/profile/settings.json`, com backup e logs próprios.
A aplicação usa nome de instância separado e pode coexistir com a versão
instalada. O modo abre a janela de configurações com identificação de
**Desenvolvimento**, sem bandeja ou atalhos globais, e não altera a entrada de
inicialização do Windows.

Os controles de brilho e a automação operam em prévia: atualizam o estado da
interface, mas não criam overlays reais de escurecimento. Isso evita acumular o
efeito da versão instalada. Para conferir escurecimento, atalhos globais e
composição real do Windows, ainda é necessário validar a versão normal.
As configurações de desenvolvimento não são copiadas para a instalação.

A instalação, o perfil normal e os pacotes publicados não são atualizados por
esse fluxo. A distribuição Release rejeita os argumentos de desenvolvimento.

## Verificações pontuais

```bash
python3 scripts/test-dev.py
python3 scripts/test-dev-integration.py --dotnet /caminho/do/dotnet
```

O teste de integração exige Windows/WSL e runtime Desktop, cria uma cópia
provisória do projeto e testa o observador, recarga real de recursos WPF,
recuperação após XAML inválido e reinício cooperativo sem usar o perfil normal.
É possível informar `--nuget-cache /caminho/do/cache` aos scripts para reutilizar
um cache existente. O supervisor aceita `--dotnet /caminho/do/dotnet` para
utilizar um SDK .NET 8 já instalado.
