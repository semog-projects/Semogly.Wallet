import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/format/formatters.dart';
import 'package:semogly_wallet/features/accounts/domain/models.dart';
import 'package:semogly_wallet/features/accounts/presentation/accounts_providers.dart';
import 'package:semogly_wallet/features/auth/domain/session.dart';

import '../../support/fakes.dart';

const _session = Session(username: 'ana@email.com');

void main() {
  testWidgets('home lista as contas do titular com o saldo formatado', (tester) async {
    await pumpApp(
      tester,
      auth: FakeAuthRepository(saved: _session),
      accounts: FakeAccountsRepository(
        accounts: const [Account(id: 'a1', ownerName: 'Ana Souza', balance: 1534.56, isActive: true)],
      ),
    );

    expect(find.text('Ana Souza'), findsOneWidget);
    expect(find.text(formatCurrency(1534.56)), findsOneWidget);
  });

  testWidgets('sem contas para o e-mail, explica o motivo', (tester) async {
    await pumpApp(tester, auth: FakeAuthRepository(saved: _session));

    expect(find.text('Nenhuma conta vinculada a este e-mail.'), findsOneWidget);
  });

  testWidgets('detalhe mostra saldo do Ledger, limite diário e extrato com "carregar mais"', (tester) async {
    final pageSize = StatementController.pageSize;
    final repository = FakeAccountsRepository(
      accounts: const [Account(id: 'a1', ownerName: 'Ana Souza', balance: 100, isActive: true)],
      balance: 980.5,
      entries: [for (var i = 1; i <= pageSize + 3; i++) entry(i, kind: i.isEven ? EntryKind.debit : EntryKind.credit)],
    );
    await pumpApp(tester, auth: FakeAuthRepository(saved: _session), accounts: repository);

    await tester.tap(find.text('Ana Souza'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('balance')), findsOneWidget);
    expect(tester.widget<Text>(find.byKey(const Key('balance'))).data, formatCurrency(980.5));
    expect(find.textContaining('Disponível hoje: ${formatCurrency(7500)}'), findsOneWidget);

    final loadMore = find.text('Carregar mais');
    await tester.scrollUntilVisible(loadMore, 300);
    await tester.tap(loadMore);
    await tester.pumpAndSettle();

    expect(repository.requestedPages, [1, 2]);
    await tester.scrollUntilVisible(find.text('Lançamento ${pageSize + 3}'), 300);
    expect(find.text('Lançamento ${pageSize + 3}'), findsOneWidget);
    expect(find.text('Carregar mais'), findsNothing);
  });
}
