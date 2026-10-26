import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/auth_repository.dart';
import '../domain/session.dart';

/// Estado da sessão: carregando (só na restauração inicial), anônimo (null) ou autenticado.
final sessionControllerProvider = AsyncNotifierProvider<SessionController, Session?>(SessionController.new);

final class SessionController extends AsyncNotifier<Session?> {
  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  Future<Session?> build() => _repository.restore();

  /// Lança em caso de falha; o progresso e o erro ficam a cargo da tela de login,
  /// para a sessão não "piscar" para o estado de carregamento durante a tentativa.
  Future<void> signIn(String username) async {
    final session = await _repository.signIn(username);
    state = AsyncData(session);
  }

  Future<void> signOut() async {
    await _repository.signOut();
    state = const AsyncData(null);
  }
}
