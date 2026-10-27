import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/auth/oidc_client.dart';
import 'session_controller.dart';

/// Login pelo Keycloak no navegador do sistema: o app nunca vê a senha.
class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  bool _submitting = false;
  bool _failed = false;

  Future<void> _signIn() async {
    if (_submitting) {
      return;
    }

    setState(() {
      _submitting = true;
      _failed = false;
    });

    try {
      // Sucesso: o router redireciona para a home ao perceber a nova sessão
      await ref.read(sessionControllerProvider.notifier).signIn();
    } on SignInCancelledException {
      // Fechou a tela do Keycloak: volta ao estado inicial, sem mensagem de erro
    } catch (_) {
      if (mounted) {
        setState(() => _failed = true);
      }
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Icon(Icons.account_balance_wallet_rounded, size: 56, color: theme.colorScheme.primary),
                const SizedBox(height: 16),
                Text('Semogly Wallet', textAlign: TextAlign.center, style: theme.textTheme.headlineMedium),
                const SizedBox(height: 8),
                Text(
                  'Ambiente de desenvolvimento: usuários do Keycloak (ex.: maria.demo@email.com / Senha@123).',
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodySmall,
                ),
                const SizedBox(height: 32),
                if (_failed)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: Text(
                      'Não foi possível entrar. Verifique a conexão e tente novamente.',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                  ),
                FilledButton.icon(
                  onPressed: _submitting ? null : _signIn,
                  icon: const Icon(Icons.login),
                  label: Text(_submitting ? 'Entrando…' : 'Entrar'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
