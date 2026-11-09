namespace ShadeFlow.Services;

public enum HelpSection { Displays, Automation, Presets, Settings }
public sealed record HelpTopic(string Glyph, string Title, string Description);

public static class ContextHelp
{
    public static string Title(HelpSection section) => section switch
    {
        HelpSection.Displays => "Monitores",
        HelpSection.Automation => "Automação",
        HelpSection.Presets => "Presets",
        HelpSection.Settings => "Configurações",
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };
    public static IReadOnlyList<HelpTopic> Topics(HelpSection section) => section switch
    {
        HelpSection.Displays => [
            new(AppIcons.Display, "Ajuste cada monitor", "Use o controle de brilho de cada tela. O efeito é visual; o ShadeFlow não altera a iluminação física do monitor."),
            new(AppIcons.Link, "Vincular monitores", "Em Configurações, escolha se ajustar uma tela também muda o brilho das demais."),
            new(AppIcons.Restore, "Restaurar 100%", "O botão remove imediatamente o escurecimento. O atalho de recuperação aparece em Configurações."),
            new(AppIcons.Clock, "Manual e automático", "Um ajuste manual durante a automação vale até o próximo horário. Presets também podem manter o ajuste até você escolher retomar.")],
        HelpSection.Automation => [
            new(AppIcons.Clock, "Programe o dia", "Adicione horários com o brilho desejado. A programação se repete diariamente; use o lápis para editar e a lixeira vermelha para excluir."),
            new(AppIcons.Play, "Ative a programação", "A alavanca habilita os horários. Confira o estado e o próximo ajuste exibidos na página."),
            new(AppIcons.Pause, "Ajuste manual e pausa", "O ajuste manual temporário termina no próximo horário. Um preset marcado para manter até retomar suspende os horários até você clicar em Retomar automação."),
            new(AppIcons.Restore, "Recuperação rápida", "Restaurar 100% continua disponível na janela, na bandeja e pelo atalho configurado.")],
        HelpSection.Presets => [
            new(AppIcons.Presets, "Salve uma combinação", "Presets guardam níveis individuais de brilho. Escolha nome, ícone e valores para os monitores."),
            new(AppIcons.Play, "Aplicar ou atualizar", "Aplicar usa os valores salvos. Atualizar com brilho atual substitui os valores dos monitores conectados e preserva os desconectados."),
            new(AppIcons.Edit, "Edite com o lápis", "Altere nome, ícone e brilho. A lixeira exclui somente o preset selecionado."),
            new(AppIcons.Pause, "Manter até retomar", "Essa opção mantém o preset enquanto os horários ficam suspensos. Use Retomar automação para voltar à programação.")],
        HelpSection.Settings => [
            new(AppIcons.Power, "Iniciar com o Windows", "O ShadeFlow pode abrir discretamente na bandeja ao entrar na conta."),
            new(AppIcons.Link, "Brilho e transição", "Configure o vínculo entre monitores, a duração das transições e o passo dos atalhos."),
            new(AppIcons.Keyboard, "Capture um atalho", "Clique no botão do atalho e pressione a combinação. Esc cancela; X remove. Aplicar atalhos confirma as mudanças e informa conflitos na execução normal."),
            new(AppIcons.Restore, "Atalho de recuperação", "Mantenha uma combinação fácil para Restaurar 100%. Se removê-la, o botão e a bandeja continuam disponíveis."),
            new(AppIcons.Info, "Sobre e diagnóstico", "Versão, autoria e licença ficam ao final desta página. Informações e diagnóstico reúne os dados para suporte.")],
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };
}
