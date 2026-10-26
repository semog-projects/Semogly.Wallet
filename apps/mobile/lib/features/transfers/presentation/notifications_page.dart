import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/format/formatters.dart';
import '../../accounts/presentation/error_view.dart';
import '../data/transfers_repository.dart';
import '../domain/models.dart';

final notificationsProvider = FutureProvider.autoDispose.family<List<NotificationItem>, String>(
  (ref, accountId) => ref.watch(transfersRepositoryProvider).notifications(accountId),
);

/// Mensagens enviadas ao titular (boas-vindas, transferências enviadas, recebidas ou recusadas).
class NotificationsPage extends ConsumerWidget {
  const NotificationsPage({required this.accountId, super.key});

  final String accountId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final notifications = ref.watch(notificationsProvider(accountId));

    return Scaffold(
      appBar: AppBar(title: const Text('Notificações')),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(notificationsProvider(accountId).future),
        child: notifications.when(
          loading: () => const Center(child: CircularProgressIndicator()),
          error: (error, _) => ErrorView(error: error, onRetry: () => ref.invalidate(notificationsProvider(accountId))),
          data: (items) => items.isEmpty
              ? ListView(children: const [Padding(padding: EdgeInsets.all(32), child: Text('Nenhuma notificação.'))])
              : ListView.separated(
                  physics: const AlwaysScrollableScrollPhysics(),
                  itemCount: items.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (_, index) {
                    final item = items[index];
                    return ListTile(
                      leading: const Icon(Icons.mail_outline_rounded),
                      title: Text(item.subject),
                      subtitle: Text('${item.body}\n${formatDateTime(item.createdAt)}'),
                      isThreeLine: true,
                    );
                  },
                ),
        ),
      ),
    );
  }
}
