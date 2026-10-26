import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/format/formatters.dart';
import '../../../core/realtime/transfer_updates.dart';
import '../../accounts/presentation/accounts_providers.dart';
import 'transfer_status_controller.dart';

/// Status ao vivo da transferência enquanto a saga roda (antifraude → débito → crédito).
class TransferStatusPage extends ConsumerWidget {
  const TransferStatusPage({required this.args, super.key});

  final TransferStatusArgs args;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final transfer = ref.watch(transferStatusProvider(args));

    // Ao terminar, saldo, limite e extrato da conta de origem mudaram
    ref.listen(transferStatusProvider(args), (previous, next) {
      if (next.isFinal && !(previous?.isFinal ?? false)) {
        ref
          ..invalidate(statementProvider(args.accountId))
          ..invalidate(dailyUsageProvider(args.accountId))
          ..invalidate(myAccountsProvider);
      }
    });

    final theme = Theme.of(context);
    final (icon, color, title) = switch (transfer.progress) {
      TransferProgress.pending => (Icons.hourglass_top_rounded, theme.colorScheme.primary, 'Processando…'),
      TransferProgress.completed => (Icons.check_circle_rounded, const Color(0xFF2E9D4F), 'Transferência concluída'),
      TransferProgress.failed => (Icons.cancel_rounded, theme.colorScheme.error, 'Transferência não realizada'),
    };

    return Scaffold(
      appBar: AppBar(title: const Text('Transferência'), automaticallyImplyLeading: transfer.isFinal),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (transfer.isFinal) Icon(icon, size: 72, color: color) else const CircularProgressIndicator(),
              const SizedBox(height: 24),
              Text(title, key: const Key('transfer-status'), style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              Text(formatCurrency(transfer.amount), style: theme.textTheme.titleLarge),
              if (transfer.progress == TransferProgress.pending)
                const Padding(
                  padding: EdgeInsets.only(top: 12),
                  child: Text('Análise antifraude, débito e crédito em andamento.', textAlign: TextAlign.center),
                ),
              if (transfer.failureReason case final reason?)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(reason, key: const Key('failure-reason'), textAlign: TextAlign.center),
                ),
              const SizedBox(height: 32),
              if (transfer.isFinal)
                FilledButton(onPressed: () => context.pop(), child: const Text('Voltar para a conta')),
            ],
          ),
        ),
      ),
    );
  }
}
