import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_providers.dart';
import '../../../core/api/generated/accounts/export.dart' as accounts_api;
import '../../../core/api/generated/fraud/export.dart' as fraud_api;
import '../../../core/api/generated/ledger/export.dart' as ledger_api;
import '../domain/models.dart';

abstract interface class AccountsRepository {
  /// Contas do titular autenticado (e-mail do token).
  Future<List<Account>> myAccounts();

  Future<StatementPage> statement(String accountId, {required int page, required int pageSize});

  Future<DailyUsage> dailyUsage(String accountId);
}

final accountsRepositoryProvider = Provider<AccountsRepository>(
  (ref) => ApiAccountsRepository(
    ref.watch(accountsClientProvider),
    ref.watch(ledgerClientProvider),
    ref.watch(fraudClientProvider),
  ),
);

/// Traduz os DTOs gerados para os modelos do app.
final class ApiAccountsRepository implements AccountsRepository {
  ApiAccountsRepository(this._accounts, this._ledger, this._fraud);

  final accounts_api.AccountsClient _accounts;
  final ledger_api.LedgerClient _ledger;
  final fraud_api.FraudClient _fraud;

  @override
  Future<List<Account>> myAccounts() async {
    final accounts = await _accounts.getMyAccounts();
    return [
      for (final account in accounts)
        Account(
          id: account.id,
          ownerName: account.ownerName,
          balance: account.balance,
          isActive: account.status == accounts_api.AccountStatus.active,
        ),
    ];
  }

  @override
  Future<StatementPage> statement(String accountId, {required int page, required int pageSize}) async {
    final statement = await _ledger.getStatement(accountId: accountId, page: page, pageSize: pageSize);
    return StatementPage(
      balance: statement.balance,
      entries: [
        for (final entry in statement.entries)
          StatementEntry(
            id: entry.entryId,
            kind: entry.type == ledger_api.EntryType.debit ? EntryKind.debit : EntryKind.credit,
            amount: entry.amount,
            balanceAfter: entry.balanceAfter,
            description: entry.description,
            occurredAt: entry.occurredAt,
          ),
      ],
    );
  }

  @override
  Future<DailyUsage> dailyUsage(String accountId) async {
    final usage = await _fraud.getDailyUsage(accountId: accountId);
    return DailyUsage(used: usage.used, limit: usage.limit, remaining: usage.remaining);
  }
}

