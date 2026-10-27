import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:semogly_wallet/app/app.dart';
import 'package:semogly_wallet/core/auth/oidc_client.dart';
import 'package:semogly_wallet/core/auth/token_storage.dart';
import 'package:semogly_wallet/core/realtime/transfer_updates.dart';
import 'package:semogly_wallet/features/accounts/data/accounts_repository.dart';
import 'package:semogly_wallet/features/accounts/domain/models.dart';
import 'package:semogly_wallet/features/auth/data/auth_repository.dart';
import 'package:semogly_wallet/features/auth/domain/session.dart';
import 'package:semogly_wallet/features/transfers/data/transfers_repository.dart';
import 'package:semogly_wallet/features/transfers/domain/models.dart';

final class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.saved, this.signInAs = 'ana@email.com', this.failSignIn = false, this.cancelSignIn = false});

  Session? saved;
  final String signInAs;
  final bool failSignIn;
  final bool cancelSignIn;

  @override
  Future<Session?> restore() async => saved;

  @override
  Future<Session> signIn() async {
    if (cancelSignIn) {
      throw const SignInCancelledException();
    }
    if (failSignIn) {
      throw StateError('falhou');
    }
    return saved = Session(username: signInAs);
  }

  @override
  Future<void> signOut() async => saved = null;
}

/// JWT sem assinatura válida (o app só lê as claims): usuário e expiração.
String jwt({String username = 'ana@email.com', required DateTime expiresAt}) {
  String encode(Map<String, Object?> json) => base64Url.encode(utf8.encode(jsonEncode(json))).replaceAll('=', '');
  final payload = {'preferred_username': username, 'exp': expiresAt.millisecondsSinceEpoch ~/ 1000};
  return '${encode({'alg': 'RS256'})}.${encode(payload)}.assinatura';
}

/// Keycloak falso: tokens do login, renovação (ou sessão expirada) e sessões encerradas.
final class FakeOidcClient implements OidcClient {
  FakeOidcClient({this.signInTokens, this.renewed, this.offline = false});

  AuthTokens? signInTokens;

  /// Resposta da renovação; null simula a sessão expirada no Keycloak.
  AuthTokens? renewed;

  /// Simula o Keycloak inacessível (erro de rede).
  bool offline;

  final refreshedWith = <String>[];
  final endedSessions = <String>[];

  @override
  Future<AuthTokens> signIn() async => signInTokens ?? (throw StateError('login não configurado'));

  @override
  Future<AuthTokens?> refresh(String refreshToken) async {
    refreshedWith.add(refreshToken);
    // Uma volta no event loop: renovações simultâneas se sobrepõem, como na rede
    await Future<void>.delayed(Duration.zero);
    if (offline) {
      throw DioException.connectionError(requestOptions: RequestOptions(), reason: 'offline');
    }
    return renewed;
  }

  @override
  Future<void> endSession(String refreshToken) async {
    if (offline) {
      throw DioException.connectionError(requestOptions: RequestOptions(), reason: 'offline');
    }
    endedSessions.add(refreshToken);
  }
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

final class FakeTransfersRepository implements TransfersRepository {
  FakeTransfersRepository({this.recipient, this.failCreateTimes = 0});

  final Recipient? recipient;
  int failCreateTimes;
  final idempotencyKeys = <String>[];

  /// Resposta do polling (GET /transfers/{id}).
  TransferSnapshot? current;

  @override
  Future<Recipient> findRecipient(String email) async =>
      recipient ?? (throw StateError('destinatário não configurado'));

  @override
  Future<TransferSnapshot> create({
    required String sourceAccountId,
    required String destinationAccountId,
    required double amount,
    required String idempotencyKey,
  }) async {
    idempotencyKeys.add(idempotencyKey);
    if (failCreateTimes > 0) {
      failCreateTimes--;
      throw StateError('rede caiu');
    }
    return current = TransferSnapshot(id: 't1', amount: amount, progress: TransferProgress.pending);
  }

  @override
  Future<TransferSnapshot> get(String transferId) async => current!;

  @override
  Future<List<NotificationItem>> notifications(String accountId) async => [
    NotificationItem(
      id: 'n1',
      subject: 'Transferência enviada',
      body: 'Sua transferência foi concluída.',
      createdAt: DateTime.utc(2026, 10, 26),
    ),
  ];
}

/// Hub falso: o teste emite os eventos; [failConnection] simula o SignalR indisponível.
final class FakeTransferUpdates implements TransferUpdatesSource {
  FakeTransferUpdates({this.failConnection = false});

  final bool failConnection;
  final _controller = StreamController<TransferUpdate>.broadcast();

  void emit(TransferUpdate update) => _controller.add(update);

  @override
  Stream<TransferUpdate> watch(String accountId) =>
      failConnection ? Stream.error(StateError('hub indisponível')) : _controller.stream;
}

Future<void> pumpApp(
  WidgetTester tester, {
  required FakeAuthRepository auth,
  FakeAccountsRepository? accounts,
  FakeTransfersRepository? transfers,
  FakeTransferUpdates? updates,
}) async {
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        authRepositoryProvider.overrideWithValue(auth),
        accountsRepositoryProvider.overrideWithValue(accounts ?? FakeAccountsRepository()),
        transfersRepositoryProvider.overrideWithValue(transfers ?? FakeTransfersRepository()),
        transferUpdatesSourceProvider.overrideWithValue(updates ?? FakeTransferUpdates()),
      ],
      child: const SemoglyWalletApp(),
    ),
  );
  await tester.pumpAndSettle();
}
