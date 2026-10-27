import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/features/auth/domain/session.dart';

import '../../support/fakes.dart';

void main() {
  testWidgets('sem sessão salva, abre o login; entrar leva à home e sair volta ao login', (tester) async {
    final auth = FakeAuthRepository(signInAs: 'maria.demo@email.com');
    await pumpApp(tester, auth: auth);

    expect(find.text('Entrar'), findsOneWidget);

    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Olá, maria.demo@email.com'), findsOneWidget);
    expect(auth.saved?.username, 'maria.demo@email.com');

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();

    expect(find.text('Entrar'), findsOneWidget);
    expect(auth.saved, isNull);
  });

  testWidgets('com sessão salva no aparelho, abre direto na home', (tester) async {
    await pumpApp(
      tester,
      auth: FakeAuthRepository(saved: const Session(username: 'bruno@email.com')),
    );

    expect(find.text('Minhas contas'), findsOneWidget);
  });

  testWidgets('fechar a tela do Keycloak volta ao login sem mensagem de erro', (tester) async {
    await pumpApp(tester, auth: FakeAuthRepository(cancelSignIn: true));

    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Entrar'), findsOneWidget);
    expect(find.textContaining('Não foi possível entrar'), findsNothing);
  });

  testWidgets('falha no login mostra mensagem e permanece na tela', (tester) async {
    await pumpApp(tester, auth: FakeAuthRepository(failSignIn: true));

    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Não foi possível entrar. Verifique a conexão e tente novamente.'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });
}
