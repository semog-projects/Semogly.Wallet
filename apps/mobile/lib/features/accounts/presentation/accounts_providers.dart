import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/accounts_repository.dart';
import '../domain/models.dart';

final myAccountsProvider = FutureProvider.autoDispose<List<Account>>(
  (ref) => ref.watch(accountsRepositoryProvider).myAccounts(),
);

final dailyUsageProvider = FutureProvider.autoDispose.family<DailyUsage, String>(
  (ref, accountId) => ref.watch(accountsRepositoryProvider).dailyUsage(accountId),
);

final class StatementState {
  const StatementState({required this.balance, required this.entries, required this.hasMore, this.loadingMore = false});

  final double balance;
  final List<StatementEntry> entries;
  final bool hasMore;
  final bool loadingMore;

  StatementState copyWith({List<StatementEntry>? entries, bool? hasMore, bool? loadingMore}) => StatementState(
    balance: balance,
    entries: entries ?? this.entries,
    hasMore: hasMore ?? this.hasMore,
    loadingMore: loadingMore ?? this.loadingMore,
  );
}

/// Extrato paginado: a primeira página traz o saldo do Ledger; "carregar mais" acrescenta as seguintes.
final statementProvider = AsyncNotifierProvider.autoDispose.family<StatementController, StatementState, String>(
  StatementController.new,
);

final class StatementController extends AsyncNotifier<StatementState> {
  StatementController(this.accountId);

  static const pageSize = 15;

  final String accountId;
  int _page = 1;

  AccountsRepository get _repository => ref.read(accountsRepositoryProvider);

  @override
  Future<StatementState> build() async {
    _page = 1;
    final first = await _repository.statement(accountId, page: 1, pageSize: pageSize);
    return StatementState(balance: first.balance, entries: first.entries, hasMore: first.entries.length == pageSize);
  }

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || current.loadingMore) {
      return;
    }

    state = AsyncData(current.copyWith(loadingMore: true));
    try {
      final next = await _repository.statement(accountId, page: _page + 1, pageSize: pageSize);
      _page++;
      state = AsyncData(
        current.copyWith(
          entries: [...current.entries, ...next.entries],
          hasMore: next.entries.length == pageSize,
          loadingMore: false,
        ),
      );
    } catch (_) {
      // Mantém o que já foi carregado; o usuário pode tentar de novo
      state = AsyncData(current.copyWith(loadingMore: false));
      rethrow;
    }
  }
}
