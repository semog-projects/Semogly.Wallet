import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/presentation/session_controller.dart';

/// Tela inicial do cliente. Saldo e extrato chegam na #28; transferências na #29.
class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final username = ref.watch(sessionControllerProvider).value?.username ?? '';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Semogly Wallet'),
        actions: [
          IconButton(
            tooltip: 'Sair',
            icon: const Icon(Icons.logout),
            onPressed: () => ref.read(sessionControllerProvider.notifier).signOut(),
          ),
        ],
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text('Olá, $username!', style: Theme.of(context).textTheme.headlineSmall),
        ),
      ),
    );
  }
}
