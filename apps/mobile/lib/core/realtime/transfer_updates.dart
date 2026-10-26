import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:logging/logging.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../api/api_providers.dart';
import '../auth/token_storage.dart';
import '../config/app_config.dart';

enum TransferProgress { pending, completed, failed }

/// Evento TransferUpdated do hub /hubs/transfers do Gateway.
final class TransferUpdate {
  const TransferUpdate({required this.transferId, required this.progress, this.reason});

  factory TransferUpdate.fromJson(Map<String, dynamic> json) => TransferUpdate(
    transferId: json['transferId'] as String,
    progress: switch (json['status']) {
      'Completed' => TransferProgress.completed,
      'Failed' => TransferProgress.failed,
      _ => TransferProgress.pending,
    },
    reason: json['reason'] as String?,
  );

  final String transferId;
  final TransferProgress progress;
  final String? reason;
}

/// Fonte de atualizações de transferência em tempo real para uma conta.
abstract interface class TransferUpdatesSource {
  Stream<TransferUpdate> watch(String accountId);
}

final transferUpdatesSourceProvider = Provider<TransferUpdatesSource>(
  (ref) => SignalRTransferUpdates(ref.watch(tokenStorageProvider)),
);

/// Atualizações de uma conta compartilhadas entre as telas. A tela da conta mantém a conexão aberta
/// enquanto estiver na pilha, então o hub já está assinado quando uma transferência é enviada
/// (a saga leva frações de segundo: conectar só depois perderia os eventos).
final accountUpdatesProvider = StreamProvider.autoDispose.family<TransferUpdate, String>(
  (ref, accountId) => ref.watch(transferUpdatesSourceProvider).watch(accountId),
);

/// Conexão SignalR enquanto houver ouvinte, autenticada pelo accessTokenFactory (no app nativo o token vai
/// no header Authorization, inclusive no WebSocket); a conta é assinada com SubscribeToAccount.
final class SignalRTransferUpdates implements TransferUpdatesSource {
  SignalRTransferUpdates(this._storage);

  final TokenStorage _storage;

  static final _logger = Logger('signalr');

  @override
  Stream<TransferUpdate> watch(String accountId) {
    late final HubConnection hub;
    final controller = StreamController<TransferUpdate>();

    controller
      ..onListen = () async {
        hub = HubConnectionBuilder()
            .withUrl(
              '${AppConfig.gatewayUrl}/hubs/transfers',
              options: HttpConnectionOptions(
                accessTokenFactory: () async => await _storage.read() ?? '',
                logger: _logger,
              ),
            )
            .configureLogging(_logger)
            .withAutomaticReconnect()
            .build();

        hub.on('TransferUpdated', (arguments) {
          final payload = arguments?.firstOrNull;
          if (payload is Map<String, dynamic>) {
            controller.add(TransferUpdate.fromJson(payload));
          }
        });

        try {
          await hub.start();
          await hub.invoke('SubscribeToAccount', args: [accountId]);
        } catch (error, stackTrace) {
          // Quem consome decide o fallback (ex.: consultar o status por polling)
          controller.addError(error, stackTrace);
        }
      }
      ..onCancel = () async {
        await hub.stop();
      };

    return controller.stream;
  }
}
