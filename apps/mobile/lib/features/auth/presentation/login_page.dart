import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'session_controller.dart';

class LoginPage extends ConsumerStatefulWidget {
  const LoginPage({super.key});

  @override
  ConsumerState<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends ConsumerState<LoginPage> {
  final _username = TextEditingController();
  bool _submitting = false;
  Object? _error;

  @override
  void dispose() {
    _username.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final username = _username.text.trim();
    if (_submitting) {
      return;
    }

    if (!username.contains('@')) {
      setState(() => _error = const FormatException());
      return;
    }

    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      // Sucesso: o router redireciona para a home ao perceber a nova sessão
      await ref.read(sessionControllerProvider.notifier).signIn(username);
    } catch (error) {
      if (mounted) {
        setState(() => _error = error);
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
                  'Ambiente de desenvolvimento: entre com o e-mail cadastrado na conta.',
                  textAlign: TextAlign.center,
                  style: theme.textTheme.bodySmall,
                ),
                const SizedBox(height: 32),
                TextField(
                  key: const Key('username'),
                  controller: _username,
                  decoration: const InputDecoration(labelText: 'E-mail', border: OutlineInputBorder()),
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.done,
                  autocorrect: false,
                  onSubmitted: (_) => _submit(),
                ),
                const SizedBox(height: 16),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: Text(
                      _messageFor(_error),
                      style: TextStyle(color: theme.colorScheme.error),
                    ),
                  ),
                FilledButton(
                  onPressed: _submitting ? null : _submit,
                  child: Text(_submitting ? 'Entrando…' : 'Entrar'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  static String _messageFor(Object? error) => switch (error) {
    FormatException() => 'Informe um e-mail válido.',
    DioException(type: DioExceptionType.connectionError || DioExceptionType.connectionTimeout) =>
      'Sem conexão com o servidor. Verifique se o AppHost está rodando.',
    _ => 'Não foi possível entrar. Tente novamente.',
  };
}
