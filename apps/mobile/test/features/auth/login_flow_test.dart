import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/features/auth/domain/session.dart';

import '../../support/fakes.dart';

void main() {
  testWidgets('sem sessão salva, abre o login; entrar leva à home e sair volta ao login', (tester) async {
    final auth = FakeAuthRepository();
    await pumpApp(tester, auth: auth);

    expect(find.text('Entrar'), findsOneWidget);

    await tester.enterText(find.byKey(const Key('username')), 'ana@email.com');
    await tester.enterText(find.byKey(const Key('password')), 'Senha@123');
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Olá, ana@email.com'), findsOneWidget);
    expect(auth.saved?.username, 'ana@email.com');

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();

    expect(find.text('Entrar'), findsOneWidget);
    expect(auth.saved, isNull);
  });

  testWidgets('com sessão salva no aparelho, abre direto na home', (tester) async {
    await pumpApp(tester, auth: FakeAuthRepository(saved: const Session(username: 'bruno@email.com')));

    expect(find.text('Minhas contas'), findsOneWidget);
  });

  testWidgets('login sem e-mail válido não chama o Gateway', (tester) async {
    final auth = FakeAuthRepository();
    await pumpApp(tester, auth: auth);

    await tester.enterText(find.byKey(const Key('username')), 'ana');
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Informe um e-mail válido.'), findsOneWidget);
    expect(auth.saved, isNull);
  });

  testWidgets('falha no login mostra mensagem e permanece na tela', (tester) async {
    await pumpApp(tester, auth: FakeAuthRepository(failSignIn: true));

    await tester.enterText(find.byKey(const Key('username')), 'ana@email.com');
    await tester.enterText(find.byKey(const Key('password')), 'Senha@123');
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Não foi possível entrar. Tente novamente.'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });
}
