import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/app/app.dart';
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

Future<void> pumpApp(WidgetTester tester, FakeAuthRepository repository) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [authRepositoryProvider.overrideWithValue(repository)],
      child: const SemoglyWalletApp(),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('sem sessão salva, abre o login; entrar leva à home e sair volta ao login', (tester) async {
    final repository = FakeAuthRepository();
    await pumpApp(tester, repository);

    expect(find.text('Entrar'), findsOneWidget);

    await tester.enterText(find.byKey(const Key('username')), 'ana');
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Olá, ana!'), findsOneWidget);
    expect(repository.saved?.username, 'ana');

    await tester.tap(find.byTooltip('Sair'));
    await tester.pumpAndSettle();

    expect(find.text('Entrar'), findsOneWidget);
    expect(repository.saved, isNull);
  });

  testWidgets('com sessão salva no aparelho, abre direto na home', (tester) async {
    await pumpApp(tester, FakeAuthRepository(saved: const Session(username: 'bruno')));

    expect(find.text('Olá, bruno!'), findsOneWidget);
  });

  testWidgets('falha no login mostra mensagem e permanece na tela', (tester) async {
    await pumpApp(tester, FakeAuthRepository(failSignIn: true));

    await tester.enterText(find.byKey(const Key('username')), 'ana');
    await tester.tap(find.text('Entrar'));
    await tester.pumpAndSettle();

    expect(find.text('Não foi possível entrar. Tente novamente.'), findsOneWidget);
    expect(find.text('Entrar'), findsOneWidget);
  });
}
