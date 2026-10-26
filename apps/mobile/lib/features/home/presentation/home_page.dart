import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/format/formatters.dart';
import '../../accounts/domain/models.dart';
import '../../accounts/presentation/accounts_providers.dart';
import '../../accounts/presentation/error_view.dart';
import '../../auth/presentation/session_controller.dart';

/// Contas do titular autenticado.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final username = ref.watch(sessionControllerProvider).value?.username ?? '';
    final accounts = ref.watch(myAccountsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Minhas contas'),
        actions: [
          IconButton(
            tooltip: 'Sair',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(sessionControllerProvider.notifier).signOut(),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(myAccountsProvider.future),
        child: accounts.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ErrorView(error: error, onRetry: () => ref.invalidate(myAccountsProvider)),
          data: (items) => ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              Text('Olá, $username', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 16),
              if (items.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(top: 48),
                  child: Text(
                    'Nenhuma conta vinculada a este e-mail.',
                    textAlign: TextAlign.center,
                  ),
                )
              else
                for (final account in items) _AccountCard(account: account),
            ],
          ),
        ),
      ),
    );
  }
}

class _AccountCard extends StatelessWidget {
  const _AccountCard({required this.account});

  final Account account;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => context.push('/accounts/${account.id}'),
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: Text(account.ownerName, style: theme.textTheme.titleMedium)),
                  if (!account.isActive) Chip(label: const Text('Inativa'), backgroundColor: theme.colorScheme.errorContainer),
                ],
              ),
              const SizedBox(height: 12),
              Text('Saldo', style: theme.textTheme.labelMedium),
              Text(formatCurrency(account.balance), style: theme.textTheme.headlineMedium),
            ],
          ),
        ),
      ),
    );
  }
}
