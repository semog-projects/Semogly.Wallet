import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/accounts/export.dart' as accounts_api;
import '../../../core/api/generated/notifications/export.dart' as notifications_api;
import '../../../core/api/generated/transfers/export.dart' as transfers_api;
import '../../../core/realtime/transfer_updates.dart';
import '../domain/models.dart';

abstract interface class TransfersRepository {
  Future<Recipient> findRecipient(String email);

  /// [idempotencyKey]: a mesma chave em novas tentativas garante que o dinheiro saia uma única vez.
  Future<TransferSnapshot> create({
    required String sourceAccountId,
    required String destinationAccountId,
    required double amount,
    required String idempotencyKey,
  });

  Future<TransferSnapshot> get(String transferId);

  Future<List<NotificationItem>> notifications(String accountId);
}

final transfersRepositoryProvider = Provider<TransfersRepository>(
  (ref) => ApiTransfersRepository(
    ref.watch(accountsClientProvider),
    ref.watch(transfersClientProvider),
    ref.watch(notificationsClientProvider),
  ),
);

final class ApiTransfersRepository implements TransfersRepository {
  ApiTransfersRepository(this._accounts, this._transfers, this._notifications);

  final accounts_api.AccountsClient _accounts;
  final transfers_api.TransfersClient _transfers;
  final notifications_api.NotificationsClient _notifications;

  @override
  Future<Recipient> findRecipient(String email) async {
    final recipient = await _accounts.lookupAccount(email: email);
    return Recipient(accountId: recipient.accountId, displayName: recipient.displayName, email: email);
  }

  @override
  Future<TransferSnapshot> create({
    required String sourceAccountId,
    required String destinationAccountId,
    required double amount,
    required String idempotencyKey,
  }) async => _toSnapshot(
    await _transfers.createTransfer(
      body: transfers_api.CreateTransferRequest(
        sourceAccountId: sourceAccountId,
        destinationAccountId: destinationAccountId,
        amount: amount,
      ),
      idempotencyKey: idempotencyKey,
    ),
  );

  @override
  Future<TransferSnapshot> get(String transferId) async => _toSnapshot(await _transfers.getTransfer(id: transferId));

  @override
  Future<List<NotificationItem>> notifications(String accountId) async => [
    for (final item in await _notifications.getNotifications(accountId: accountId))
      NotificationItem(id: item.id, subject: item.subject, body: item.body, createdAt: item.createdAt),
  ];

  static TransferSnapshot _toSnapshot(transfers_api.TransferResponse transfer) => TransferSnapshot(
    id: transfer.id,
    amount: transfer.amount,
    progress: switch (transfer.status) {
      transfers_api.TransferStatus.completed => TransferProgress.completed,
      transfers_api.TransferStatus.failed => TransferProgress.failed,
      transfers_api.TransferStatus.pending => TransferProgress.pending,
    },
    failureReason: transfer.failureReason,
  );
}
