import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../domain/models.dart';
import 'accounts_providers.dart';
import 'error_view.dart';

/// Saldo (Ledger), limite diário de transferências e extrato paginado de uma conta.
class AccountPage extends ConsumerWidget {
  const AccountPage({required this.accountId, super.key});

  final String accountId;

  Future<void> _refresh(WidgetRef ref) async {
    ref
      ..invalidate(dailyUsageProvider(accountId))
      ..invalidate(statementProvider(accountId));
    await ref.read(statementProvider(accountId).future);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final statement = ref.watch(statementProvider(accountId));

    return Scaffold(
      appBar: AppBar(title: const Text('Conta')),
      body: RefreshIndicator(
        onRefresh: () => _refresh(ref),
        child: statement.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ErrorView(error: error, onRetry: () => ref.invalidate(statementProvider(accountId))),
          data: (data) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              _BalanceCard(balance: data.balance),
              const SizedBox(height: 12),
              _DailyLimitCard(accountId: accountId),
              const SizedBox(height: 24),
              Text('Extrato', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              if (data.entries.isEmpty)
                const Padding(padding: EdgeInsets.symmetric(vertical: 24), child: Text('Nenhum lançamento.'))
              else
                for (final entry in data.entries) _EntryTile(entry: entry),
              if (data.hasMore)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Center(
                    child: data.loadingMore
                        ? const CircularProgressIndicator()
                        : TextButton(
                            onPressed: () => ref.read(statementProvider(accountId).notifier).loadMore(),
                            child: const Text('Carregar mais'),
                          ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _BalanceCard extends StatelessWidget {
  const _BalanceCard({required this.balance});

  final double balance;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      color: theme.colorScheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Saldo disponível', style: theme.textTheme.labelLarge),
            const SizedBox(height: 4),
            Text(formatCurrency(balance), key: const Key('balance'), style: theme.textTheme.headlineLarge),
          ],
        ),
      ),
    );
  }
}

class _DailyLimitCard extends ConsumerWidget {
  const _DailyLimitCard({required this.accountId});

  final String accountId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final usage = ref.watch(dailyUsageProvider(accountId));
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: usage.when(
          loading: () => const LinearProgressIndicator(),
          error: (_, _) => const Text('Limite diário indisponível no momento.'),
          data: (data) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Limite diário de transferências', style: theme.textTheme.labelLarge),
              const SizedBox(height: 8),
              LinearProgressIndicator(value: data.usedFraction, minHeight: 8, borderRadius: BorderRadius.circular(4)),
              const SizedBox(height: 8),
              Text('Disponível hoje: ${formatCurrency(data.remaining)} de ${formatCurrency(data.limit)}'),
            ],
          ),
        ),
      ),
    );
  }
}

class _EntryTile extends StatelessWidget {
  const _EntryTile({required this.entry});

  final StatementEntry entry;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final isDebit = entry.kind == EntryKind.debit;
    // Entrada de dinheiro em verde (convenção de apps financeiros), ajustado ao tema claro/escuro
    final credit = Theme.of(context).brightness == Brightness.dark ? const Color(0xFF81C784) : const Color(0xFF2E7D32);

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: CircleAvatar(
        backgroundColor: isDebit ? colors.errorContainer : credit.withValues(alpha: 0.2),
        child: Icon(isDebit ? Icons.arrow_upward_rounded : Icons.arrow_downward_rounded, size: 20),
      ),
      title: Text(entry.description),
      subtitle: Text(formatDateTime(entry.occurredAt)),
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            '${isDebit ? '−' : '+'} ${formatCurrency(entry.amount)}',
            style: TextStyle(fontWeight: FontWeight.w600, color: isDebit ? colors.error : credit),
          ),
          Text('Saldo ${formatCurrency(entry.balanceAfter)}', style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    );
  }
}
