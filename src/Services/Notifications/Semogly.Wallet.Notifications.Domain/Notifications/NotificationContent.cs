using System.Globalization;
using Semogly.Wallet.Notifications.Domain.Contacts;

namespace Semogly.Wallet.Notifications.Domain.Notifications;

public sealed record NotificationContent(string Subject, string Body)
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static NotificationContent Welcome(Contact contact) => new(
        "Bem-vindo à Semogly Wallet",
        $"Olá, {contact.FirstName}! Sua conta foi aberta e já pode receber depósitos e transferências.");

    public static NotificationContent TransferSent(Contact contact, decimal amount) => new(
        "Transferência enviada",
        $"Olá, {contact.FirstName}! Sua transferência de {Money(amount)} foi concluída.");

    public static NotificationContent TransferReceived(Contact contact, decimal amount) => new(
        "Você recebeu uma transferência",
        $"Olá, {contact.FirstName}! Você recebeu {Money(amount)} na sua conta.");

    public static NotificationContent TransferFailed(Contact contact, decimal amount, string reason) => new(
        "Transferência não realizada",
        $"Olá, {contact.FirstName}! Sua transferência de {Money(amount)} não foi realizada. Motivo: {reason}");

    private static string Money(decimal amount) => amount.ToString("C", PtBr);
}
