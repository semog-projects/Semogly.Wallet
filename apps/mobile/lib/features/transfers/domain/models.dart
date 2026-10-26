import '../../../core/realtime/transfer_updates.dart';

/// Destinatário encontrado pela chave (e-mail): só o necessário para o pagador confirmar.
final class Recipient {
  const Recipient({required this.accountId, required this.displayName, required this.email});

  final String accountId;
  final String displayName;
  final String email;
}

final class TransferSnapshot {
  const TransferSnapshot({required this.id, required this.amount, required this.progress, this.failureReason});

  final String id;
  final double amount;
  final TransferProgress progress;
  final String? failureReason;

  bool get isFinal => progress != TransferProgress.pending;
}

final class NotificationItem {
  const NotificationItem({required this.id, required this.subject, required this.body, required this.createdAt});

  final String id;
  final String subject;
  final String body;
  final DateTime createdAt;
}
