import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/realtime/transfer_updates.dart';
import '../data/transfers_repository.dart';
import '../domain/models.dart';

typedef TransferStatusArgs = ({String transferId, String accountId, double amount});

final transferStatusProvider = NotifierProvider.autoDispose
    .family<TransferStatusController, TransferSnapshot, TransferStatusArgs>(TransferStatusController.new);

/// Acompanha a saga da transferência: eventos do SignalR quando disponíveis, com consulta periódica
/// como garantia (ex.: hub indisponível).
final class TransferStatusController extends Notifier<TransferSnapshot> {
  TransferStatusController(this.args);

  static const pollInterval = Duration(seconds: 3);

  final TransferStatusArgs args;

  @override
  TransferSnapshot build() {
    // Primeiro assina os eventos (o último recebido é entregue de imediato), depois consulta o status
    // uma vez: o que aconteceu antes da assinatura vem pela consulta, nada fica entre os dois.
    // Erro no tempo real é ignorado: o polling continua acompanhando.
    ref.listen(accountUpdatesProvider(args.accountId), (_, next) {
      if (next.value case final update? when update.transferId == args.transferId) {
        _apply(update.progress, update.reason);
      }
    }, fireImmediately: true);

    final polling = Timer.periodic(pollInterval, (_) => _poll());
    ref.onDispose(polling.cancel);
    Future.microtask(_poll);

    return TransferSnapshot(id: args.transferId, amount: args.amount, progress: TransferProgress.pending);
  }

  Future<void> _poll() async {
    if (state.isFinal) {
      return;
    }

    try {
      final snapshot = await ref.read(transfersRepositoryProvider).get(args.transferId);
      _apply(snapshot.progress, snapshot.failureReason);
    } on Object {
      // Falha pontual de rede: tenta de novo no próximo ciclo
    }
  }

  void _apply(TransferProgress progress, String? reason) {
    if (state.isFinal || !ref.mounted) {
      return;
    }

    state = TransferSnapshot(id: args.transferId, amount: args.amount, progress: progress, failureReason: reason);
  }
}
