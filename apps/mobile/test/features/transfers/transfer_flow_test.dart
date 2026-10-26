import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/core/format/formatters.dart';
import 'package:semogly_wallet/core/realtime/transfer_updates.dart';
import 'package:semogly_wallet/features/accounts/domain/models.dart';
import 'package:semogly_wallet/features/auth/domain/session.dart';
import 'package:semogly_wallet/features/transfers/domain/models.dart';
import 'package:semogly_wallet/features/transfers/presentation/transfer_status_controller.dart';

import '../../support/fakes.dart';

const _recipient = Recipient(accountId: 'b1', displayName: 'Joao D.', email: 'joao@email.com');

Future<void> openTransfer(
  WidgetTester tester, {
  required FakeTransfersRepository transfers,
  FakeTransferUpdates? updates,
}) async {
  await pumpApp(
    tester,
    auth: FakeAuthRepository(saved: const Session(username: 'ana@email.com')),
    accounts: FakeAccountsRepository(
      accounts: const [Account(id: 'a1', ownerName: 'Ana Souza', balance: 500, isActive: true)],
      balance: 500,
    ),
    transfers: transfers,
    updates: updates,
  );

  await tester.tap(find.text('Ana Souza'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('Transferir'));
  await tester.pumpAndSettle();
}

Future<void> fillAndReview(WidgetTester tester, {String amount = '1.234,56'}) async {
  await tester.enterText(find.byKey(const Key('recipient-email')), 'joao@email.com');
  await tester.enterText(find.byKey(const Key('amount')), amount);
  await tester.tap(find.text('Continuar'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('confirma o destinatário mascarado e acompanha a conclusão pelo tempo real', (tester) async {
    final transfers = FakeTransfersRepository(recipient: _recipient);
    final updates = FakeTransferUpdates();
    await openTransfer(tester, transfers: transfers, updates: updates);

    await fillAndReview(tester);
    expect(find.text('Joao D.'), findsOneWidget);
    expect(find.text(formatCurrency(1234.56)), findsOneWidget);

    await tester.tap(find.text('Confirmar transferência'));
    await tester.pump();
    await tester.pump();
    expect(find.text('Processando…'), findsOneWidget);

    updates.emit(const TransferUpdate(transferId: 't1', progress: TransferProgress.completed));
    await tester.pumpAndSettle();

    expect(find.text('Transferência concluída'), findsOneWidget);
    expect(find.text('Voltar para a conta'), findsOneWidget);
  });

  testWidgets('falha da saga mostra o motivo (ex.: estorno)', (tester) async {
    final updates = FakeTransferUpdates();
    await openTransfer(tester, transfers: FakeTransfersRepository(recipient: _recipient), updates: updates);
    await fillAndReview(tester, amount: '50');
    await tester.tap(find.text('Confirmar transferência'));
    await tester.pump();
    await tester.pump();

    updates.emit(
      const TransferUpdate(
        transferId: 't1',
        progress: TransferProgress.failed,
        reason: 'Estornada: Conta de destino bloqueada',
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Transferência não realizada'), findsOneWidget);
    expect(find.text('Estornada: Conta de destino bloqueada'), findsOneWidget);
  });

  testWidgets('sem tempo real, o status chega pela consulta periódica', (tester) async {
    final transfers = FakeTransfersRepository(recipient: _recipient);
    await openTransfer(tester, transfers: transfers, updates: FakeTransferUpdates(failConnection: true));
    await fillAndReview(tester, amount: '10');
    await tester.tap(find.text('Confirmar transferência'));
    await tester.pump();
    await tester.pump();

    transfers.current = const TransferSnapshot(id: 't1', amount: 10, progress: TransferProgress.completed);
    await tester.pump(TransferStatusController.pollInterval);
    await tester.pumpAndSettle();

    expect(find.text('Transferência concluída'), findsOneWidget);
  });

  testWidgets('nova tentativa após falha de rede reutiliza a mesma Idempotency-Key', (tester) async {
    final transfers = FakeTransfersRepository(recipient: _recipient, failCreateTimes: 1);
    await openTransfer(tester, transfers: transfers);
    await fillAndReview(tester);

    await tester.tap(find.text('Confirmar transferência'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('transfer-error')), findsOneWidget);

    await tester.tap(find.text('Confirmar transferência'));
    await tester.pump();
    await tester.pump();

    expect(transfers.idempotencyKeys, hasLength(2));
    expect(transfers.idempotencyKeys.toSet(), hasLength(1));
  });

  testWidgets('valor inválido não consulta o destinatário', (tester) async {
    await openTransfer(tester, transfers: FakeTransfersRepository(recipient: _recipient));

    await fillAndReview(tester, amount: '10,999');

    expect(find.text('Informe um valor válido, com até duas casas decimais.'), findsOneWidget);
    expect(find.text('Joao D.'), findsNothing);
  });

  testWidgets('notificações da conta', (tester) async {
    await openTransfer(tester, transfers: FakeTransfersRepository(recipient: _recipient));
    await tester.pageBack();
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Notificações'));
    await tester.pumpAndSettle();

    expect(find.text('Transferência enviada'), findsOneWidget);
  });
}
