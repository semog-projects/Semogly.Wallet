import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/app/app.dart';
import 'package:semogly_wallet/features/accounts/data/accounts_repository.dart';
import 'package:semogly_wallet/features/accounts/domain/models.dart';
import 'package:semogly_wallet/features/auth/data/auth_repository.dart';
import 'package:semogly_wallet/features/auth/domain/session.dart';

final class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.saved, this.failSignIn = false});

  Session? saved;
  final bool failSignIn;

  @override
  Future<Session?> restore() async => saved;

  @override
  Future<Session> signIn(String username) async {
    if (failSignIn) {
      throw StateError('falhou');
    }
    return saved = Session(username: username);
  }

  @override
  Future<void> signOut() async => saved = null;
}

final class FakeAccountsRepository implements AccountsRepository {
  FakeAccountsRepository({this.accounts = const [], this.balance = 0, List<StatementEntry>? entries})
    : entries = entries ?? [];

  final List<Account> accounts;
  final double balance;
  final List<StatementEntry> entries;
  final requestedPages = <int>[];

  @override
  Future<List<Account>> myAccounts() async => accounts;

  @override
  Future<StatementPage> statement(String accountId, {required int page, required int pageSize}) async {
    requestedPages.add(page);
    return StatementPage(balance: balance, entries: entries.skip((page - 1) * pageSize).take(pageSize).toList());
  }

  @override
  Future<DailyUsage> dailyUsage(String accountId) async => const DailyUsage(used: 2500, limit: 10000, remaining: 7500);
}

StatementEntry entry(int index, {EntryKind kind = EntryKind.credit}) => StatementEntry(
  id: 'e$index',
  kind: kind,
  amount: 10,
  balanceAfter: 100,
  description: 'Lançamento $index',
  occurredAt: DateTime.utc(2026, 10, 26, 12),
);

Future<void> pumpApp(WidgetTester tester, {required FakeAuthRepository auth, FakeAccountsRepository? accounts}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authRepositoryProvider.overrideWithValue(auth),
        accountsRepositoryProvider.overrideWithValue(accounts ?? FakeAccountsRepository()),
      ],
      child: const SemoglyWalletApp(),
    ),
  );
  await tester.pumpAndSettle();
}
